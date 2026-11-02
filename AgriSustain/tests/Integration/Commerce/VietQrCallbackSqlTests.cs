using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AgriSustain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.Commerce;

// VietQR.vn partner flow: token_generate (Basic) -> transaction-sync (Bearer) against SQL Server.
[Trait("Layer", "SqlIntegration")]
public sealed class VietQrCallbackSqlTests
{
    private const string Username = "vietqr-test";
    private const string Password = "vietqr-secret";
    private static readonly Dictionary<string, string?> Settings = new()
    {
        ["Payment:VietQR:Callback:Username"] = Username,
        ["Payment:VietQR:Callback:Password"] = Password,
        ["Payment:VietQR:Callback:TokenSecret"] = "test-only-token-secret-at-least-32-bytes"
    };

    private static async Task<(CommerceDatabase Db, string OrderCode)> Arrange(decimal finalAmount = 214500)
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
                ReservationExpiresAt = DateTime.UtcNow.AddMinutes(15), PaymentMethod = "VietQR"
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

    private static async Task<string> Token(HttpClient client, string user = Username, string password = Password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/vqr/api/token_generate");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Bearer", json.RootElement.GetProperty("token_type").GetString());
        return json.RootElement.GetProperty("access_token").GetString()!;
    }

    private static async Task<JsonDocument> Sync(HttpClient client, string? token, object body,
        HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/vqr/bank/api/transaction-sync")
        {
            Content = JsonContent.Create(body)
        };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(expected == response.StatusCode, $"{(int)response.StatusCode}: {text}");
        return JsonDocument.Parse(text);
    }

    // Shape and types exactly as VietQR sends them (amount/transactiontime are numbers).
    private static object Callback(string orderCode, decimal amount, string transactionId, string transType = "C") => new
    {
        bankaccount = "0000499456980", amount, transType,
        content = $"QR VQR712a7d45ea {orderCode.Replace("-", "").ToUpperInvariant()[..18]} Ma giao dich Trace756759",
        transactionid = transactionId, transactiontime = 1767844560000L, referencenumber = "FT" + transactionId[..12],
        orderId = "", terminalCode = "", subTerminalCode = "", serviceCode = "", urlLink = "", sign = ""
    };

    [CommerceSqlFact, Trait("UseCase", "UC-RC-03")]
    public async Task SuccessfulTransfer_ConfirmsOrderOnce_EvenWhenVietQrRetries()
    {
        var (f, code) = await Arrange();
        await using var _ = f;
        var token = await Token(f.Client);
        var transactionId = Guid.NewGuid().ToString();

        using var first = await Sync(f.Client, token, Callback(code, 214500, transactionId));
        using var retry = await Sync(f.Client, token, Callback(code, 214500, transactionId));

        Assert.False(first.RootElement.GetProperty("error").GetBoolean());
        var reference = first.RootElement.GetProperty("object").GetProperty("reftransactionid").GetString();
        Assert.Equal(reference, retry.RootElement.GetProperty("object").GetProperty("reftransactionid").GetString());

        await using var db = f.Open();
        var order = await db.Orders.Include(x => x.SellerOrders).Include(x => x.PaymentTransactions)
            .SingleAsync(x => x.OrderCode == code);
        Assert.Equal("Confirmed", order.OrderStatus);
        Assert.All(order.SellerOrders, x => Assert.Equal("Confirmed", x.Status));
        var payment = Assert.Single(order.PaymentTransactions);
        Assert.Equal(("Success", 214500m, "VietQR"), (payment.PaymentStatus, payment.Amount, payment.GatewayProvider));
        Assert.Equal(new DateTime(2026, 1, 8, 3, 56, 0, DateTimeKind.Utc), payment.PaidAt);
        Assert.True(await db.Notifications.AnyAsync(x => x.UserId == f.BuyerId && x.RelatedRecordId == order.OrderId));
    }

    [CommerceSqlFact, Trait("UseCase", "UC-RC-03")]
    public async Task UnderpaidTransfer_IsRecordedForReconciliation_AndOrderStaysPending()
    {
        var (f, code) = await Arrange();
        await using var _ = f;

        using var result = await Sync(f.Client, await Token(f.Client), Callback(code, 100000, Guid.NewGuid().ToString()));

        Assert.False(result.RootElement.GetProperty("error").GetBoolean());
        await using var db = f.Open();
        var order = await db.Orders.Include(x => x.PaymentTransactions).SingleAsync(x => x.OrderCode == code);
        Assert.Equal("PendingPayment", order.OrderStatus);
        Assert.Equal("AmountMismatch", Assert.Single(order.PaymentTransactions).PaymentStatus);
    }

    [CommerceSqlFact, Trait("UseCase", "UC-RC-03")]
    public async Task DebitTransaction_IsAcknowledgedWithoutTouchingTheOrder()
    {
        var (f, code) = await Arrange();
        await using var _ = f;

        using var result = await Sync(f.Client, await Token(f.Client), Callback(code, 214500, Guid.NewGuid().ToString(), "D"));

        Assert.False(result.RootElement.GetProperty("error").GetBoolean());
        await using var db = f.Open();
        var order = await db.Orders.Include(x => x.PaymentTransactions).SingleAsync(x => x.OrderCode == code);
        Assert.Equal("PendingPayment", order.OrderStatus);
        Assert.Empty(order.PaymentTransactions);
    }

    [CommerceSqlFact, Trait("UseCase", "UC-RC-03")]
    public async Task InvalidCredentialsOrToken_AreRejected()
    {
        var (f, code) = await Arrange();
        await using var _ = f;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/vqr/api/token_generate");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:wrong")));
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.SendAsync(request)).StatusCode);

        var forged = (await Token(f.Client)).Split('.')[0] + ".forged";
        using var noToken = await Sync(f.Client, null, Callback(code, 214500, Guid.NewGuid().ToString()), HttpStatusCode.Unauthorized);
        using var badToken = await Sync(f.Client, forged, Callback(code, 214500, Guid.NewGuid().ToString()), HttpStatusCode.Unauthorized);
        Assert.Equal("INVALID_TOKEN", badToken.RootElement.GetProperty("errorReason").GetString());

        await using var db = f.Open();
        Assert.Equal("PendingPayment", (await db.Orders.SingleAsync(x => x.OrderCode == code)).OrderStatus);
    }
}
