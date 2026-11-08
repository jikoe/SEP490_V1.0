using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgriSustain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.Commerce;

// SePay balance-change webhook (Authorization: Apikey) against SQL Server.
[Trait("Layer", "SqlIntegration")]
public sealed class SePayWebhookSqlTests
{
    private const string ApiKey = "sepay-test-key";
    private static readonly Dictionary<string, string?> Settings = new() { ["Payment:SePay:WebhookApiKey"] = ApiKey };

    private static async Task<(CommerceDatabase Db, string OrderCode)> Arrange(decimal finalAmount = 175700)
    {
        var f = await CommerceDatabase.Create(Settings);
        var code = $"RC-{Guid.NewGuid():N}";
        await f.Edit(async db =>
        {
            var farmId = await db.SellerOrders.Select(x => x.FarmerProfileId).FirstAsync();
            var order = new Order
            {
                BuyerUserId = f.BuyerId, OrderCode = code, OrderType = "Retail", OrderStatus = "PendingPayment",
                OrderDate = DateTime.UtcNow, ShippingAddress = "Test address", RecipientNameSnapshot = "Buyer",
                RecipientPhoneSnapshot = "0912345678", TotalAmount = finalAmount, DiscountAmount = 0,
                DeliveryFee = 0, FinalAmount = finalAmount, CheckoutRequestKey = Guid.NewGuid().ToString("D"),
                ReservationExpiresAt = DateTime.UtcNow.AddMinutes(15), PaymentMethod = "SePay"
            };
            order.SellerOrders.Add(new SellerOrder
            {
                FarmerProfileId = farmId, SellerOrderCode = code + "-1", Subtotal = finalAmount,
                DiscountShare = 0, DeliveryFee = 0, FinalAmount = finalAmount, Status = "PendingPayment"
            });
            db.Orders.Add(order);
        });
        return (f, code);
    }

    // Shape exactly as SePay sends it; MB wraps the transfer content with its own prefix/suffix.
    private static object Webhook(string orderCode, decimal amount, long id, string transferType = "in") => new
    {
        id, gateway = "MBBank", transactionDate = "2026-11-02 13:45:10", accountNumber = "0000499456980",
        subAccount = "", code = (string?)null,
        content = $"{orderCode.Replace("-", "").ToUpperInvariant()[..18]} FT26306123456789",
        transferType, description = "BankAPINotify NGUYEN VAN A chuyen tien", transferAmount = amount,
        accumulated = 1_000_000, referenceCode = $"FT{id}"
    };

    private static async Task<HttpResponseMessage> Send(HttpClient client, object body, string? key = ApiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payment/sepay-webhook")
        {
            Content = JsonContent.Create(body)
        };
        if (key is not null) request.Headers.TryAddWithoutValidation("Authorization", $"Apikey {key}");
        return await client.SendAsync(request);
    }

    private static async Task AssertAcknowledged(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
    }

    [CommerceSqlFact, Trait("UseCase", "UC-RC-03")]
    public async Task IncomingTransfer_ConfirmsOrderOnce_EvenWhenSePayRetries()
    {
        var (f, code) = await Arrange();
        await using var _ = f;

        await AssertAcknowledged(await Send(f.Client, Webhook(code, 175700, 92704)));
        await AssertAcknowledged(await Send(f.Client, Webhook(code, 175700, 92704)));

        await using var db = f.Open();
        var order = await db.Orders.Include(x => x.SellerOrders).Include(x => x.PaymentTransactions)
            .SingleAsync(x => x.OrderCode == code);
        Assert.Equal("Confirmed", order.OrderStatus);
        Assert.All(order.SellerOrders, x => Assert.Equal("Confirmed", x.Status));
        var payment = Assert.Single(order.PaymentTransactions);
        Assert.Equal(("SEPAY-92704", "Success", 175700m, "SePay"),
            (payment.TransactionCode, payment.PaymentStatus, payment.Amount, payment.GatewayProvider));
        Assert.Equal(new DateTime(2026, 11, 2, 6, 45, 10), payment.PaidAt);
    }

    [CommerceSqlFact, Trait("UseCase", "UC-PS-01")]
    public async Task PaidOrder_AppliesHeldVoucherUse_AndCountsIt()
    {
        var (f, code) = await Arrange();
        await using var _ = f;
        string voucherCode = $"PAY{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await f.Edit(async db =>
        {
            var order = await db.Orders.SingleAsync(x => x.OrderCode == code);
            var voucher = new Voucher
            {
                VoucherCode = voucherCode, VoucherName = "Paid use", DiscountType = Voucher.TypeFixedAmount,
                DiscountValue = 10_000, MinOrderAmount = 0, UsageLimit = 1, PerCustomerLimit = 1, UsedCount = 0,
                IsActive = true, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(1)
            };
            voucher.VoucherRedemptions.Add(new VoucherRedemption
            {
                OrderId = order.OrderId, DiscountSnapshot = 10_000, Status = VoucherRedemptionStatus.Reserved,
                ReservedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            });
            db.Vouchers.Add(voucher);
        });

        await AssertAcknowledged(await Send(f.Client, Webhook(code, 175700, 92710)));
        await AssertAcknowledged(await Send(f.Client, Webhook(code, 175700, 92710)));   // SePay retry

        await using var db = f.Open();
        var saved = await db.Vouchers.Include(x => x.VoucherRedemptions).SingleAsync(x => x.VoucherCode == voucherCode);
        var use = Assert.Single(saved.VoucherRedemptions);
        Assert.Equal(VoucherRedemptionStatus.Applied, use.Status);
        Assert.NotNull(use.AppliedAt);
        Assert.Equal(1, saved.UsedCount);                                   // counted once despite the retry
        Assert.Equal(Voucher.StatusExhausted, saved.LifecycleStatus(DateTime.UtcNow));
    }

    [CommerceSqlFact(DisplayName = "[IT-PS01-15] A voucher deactivated while the order awaits payment keeps its discount and is counted (BR-PV-08)"),
        Trait("UseCase", "UC-PS-01")]
    public async Task PaidOrder_VoucherDeactivatedMeanwhile_StillApplied()
    {
        var (f, code) = await Arrange();
        await using var _ = f;
        string voucherCode = $"OFF{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await f.Edit(async db =>
        {
            var order = await db.Orders.SingleAsync(x => x.OrderCode == code);
            order.DiscountAmount = 10_000;
            var voucher = new Voucher
            {
                VoucherCode = voucherCode, VoucherName = "Deactivated meanwhile", DiscountType = Voucher.TypeFixedAmount,
                DiscountValue = 10_000, MinOrderAmount = 0, UsageLimit = 10, PerCustomerLimit = 1, UsedCount = 0,
                IsActive = true, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(1)
            };
            voucher.VoucherRedemptions.Add(new VoucherRedemption
            {
                OrderId = order.OrderId, DiscountSnapshot = 10_000, Status = VoucherRedemptionStatus.Reserved,
                ReservedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            });
            db.Vouchers.Add(voucher);
        });
        // Promotion Staff deactivates the voucher after the order was placed (UC-PS-01).
        await f.Edit(async db => (await db.Vouchers.SingleAsync(x => x.VoucherCode == voucherCode)).IsActive = false);

        await AssertAcknowledged(await Send(f.Client, Webhook(code, 175700, 92790)));

        await using var db = f.Open();
        var order = await db.Orders.SingleAsync(x => x.OrderCode == code);
        Assert.Equal(("Confirmed", 10_000m, 175700m), (order.OrderStatus, order.DiscountAmount, order.FinalAmount));
        var saved = await db.Vouchers.Include(x => x.VoucherRedemptions).SingleAsync(x => x.VoucherCode == voucherCode);
        Assert.Equal(VoucherRedemptionStatus.Applied, Assert.Single(saved.VoucherRedemptions).Status);
        Assert.Equal(1, saved.UsedCount);
        Assert.False(saved.IsActive);
    }

    [CommerceSqlFact, Trait("UseCase", "UC-RC-03")]
    public async Task UnderpaidTransfer_IsRecordedForReconciliation_AndOrderStaysPending()
    {
        var (f, code) = await Arrange();
        await using var _ = f;

        await AssertAcknowledged(await Send(f.Client, Webhook(code, 100000, 92705)));

        await using var db = f.Open();
        var order = await db.Orders.Include(x => x.PaymentTransactions).SingleAsync(x => x.OrderCode == code);
        Assert.Equal("PendingPayment", order.OrderStatus);
        Assert.Equal("AmountMismatch", Assert.Single(order.PaymentTransactions).PaymentStatus);
    }

    [CommerceSqlFact, Trait("UseCase", "UC-RC-03")]
    public async Task OutgoingTransfer_IsAcknowledgedWithoutTouchingTheOrder()
    {
        var (f, code) = await Arrange();
        await using var _ = f;

        await AssertAcknowledged(await Send(f.Client, Webhook(code, 175700, 92706, "out")));

        await using var db = f.Open();
        var order = await db.Orders.Include(x => x.PaymentTransactions).SingleAsync(x => x.OrderCode == code);
        Assert.Equal("PendingPayment", order.OrderStatus);
        Assert.Empty(order.PaymentTransactions);
    }

    [CommerceSqlTheory, Trait("UseCase", "UC-RC-03")]
    [InlineData(null), InlineData("wrong-key")]
    public async Task MissingOrWrongApiKey_IsRejected(string? key)
    {
        var (f, code) = await Arrange();
        await using var _ = f;

        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(f.Client, Webhook(code, 175700, 92707), key)).StatusCode);

        await using var db = f.Open();
        Assert.Equal("PendingPayment", (await db.Orders.SingleAsync(x => x.OrderCode == code)).OrderStatus);
    }
}
