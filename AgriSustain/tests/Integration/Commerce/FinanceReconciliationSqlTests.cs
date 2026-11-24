using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.Commerce;

[Trait("Layer", "SqlIntegration")]
[Trait("UseCase", "UC-FIN-02")]
public sealed class FinanceReconciliationSqlTests
{
    private static readonly Dictionary<string, string?> Settings = new()
    {
        ["Payment:SePay:ApiToken"] = "test-token",
        ["Payment:SePay:AccountNumber"] = "0001234567",
        ["Payment:SePay:BankShortName"] = "MB",
        ["Payment:SePay:WebhookApiKey"] = "test-webhook"
    };

    [CommerceSqlTheory]
    [InlineData("Succeeded", 100000, "0001234567", false)]
    [InlineData("Mismatch", 110000, "0001234567", false)]
    [InlineData("Mismatch", 90000, "0001234567", false)]
    [InlineData("Pending", 100000, "0001111111", false)]
    [InlineData("LatePayment", 100000, "0001234567", true)]
    public async Task ReconcileOrder_RequiresVerifiedMatchingCredit_AndReplaysOnce(
        string expected, int amount, string account, bool late)
    {
        using var gateway = new Statement();
        await using var fixture = await CommerceDatabase.Create(Settings, gateway);
        var now = fixture.Clock.GetUtcNow().UtcDateTime;
        var staffId = await StaffAsync(fixture);
        var order = new Order
        {
            BuyerUserId = fixture.BuyerId, OrderType = "Retail", OrderCode = "RC-" + Guid.NewGuid().ToString("N"),
            OrderStatus = "PendingPayment", OrderDate = now.AddMinutes(-5), FinalAmount = 100000,
            TotalAmount = 100000, PaymentMethod = "SePay", ShippingAddress = "Local test",
            RecipientNameSnapshot = "Test buyer", RecipientPhoneSnapshot = "0900000000",
            CheckoutRequestKey = Guid.NewGuid().ToString("N"),
            ReservationExpiresAt = late ? now.AddMinutes(-3) : now.AddMinutes(10)
        };
        FinancePaymentRules.AddSePayRequest(order);
        await fixture.Edit(db => { db.Orders.Add(order); return Task.CompletedTask; });
        gateway.Reference = FinancePaymentRules.OrderReference(order.OrderCode);
        gateway.Amount = amount;
        gateway.AccountNumber = account;
        gateway.PaidAt = now.AddMinutes(-1);
        var id = Assert.Single(order.PaymentTransactions).PaymentTransactionId;
        string url = $"api/finance/payments/{id}";

        using var forbidden = await fixture.Buyer().PostAsJsonAsync(url + "/reconcile",
            new ReconcilePaymentRequest(new string('A', 64), Guid.NewGuid().ToString("D")));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(0, gateway.Reads);

        var client = fixture.As(staffId, "FinanceStaff");
        var detail = await client.GetFromJsonAsync<FinancePaymentDetail>(url);
        Assert.True(detail!.CanReconcile);
        using var stale = await client.PostAsJsonAsync(url + "/reconcile",
            new ReconcilePaymentRequest(new string('A', 64), Guid.NewGuid().ToString("D")));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(0, gateway.Reads);
        var request = new ReconcilePaymentRequest(detail.Version, Guid.NewGuid().ToString("D"));
        var result = await CheckAsync(client, url, request);
        Assert.Equal(expected, result.Outcome);
        Assert.Equal(expected == "Succeeded" ? "Success" : "Pending", result.Detail.Payment.Status);
        Assert.Equal(expected == "Succeeded" ? 0 : 100000, result.Detail.Obligation!.Unpaid);
        Assert.NotNull(result.Detail.ReconciledAt);
        Assert.Equal(staffId, await AuditActorAsync(fixture, id));
        Assert.Equal(expected, (await CheckAsync(client, url, request)).Outcome);
        Assert.Equal(1, gateway.Reads);

        await using var check = fixture.Open();
        var saved = await check.Orders.Include(o => o.PaymentTransactions).SingleAsync(o => o.OrderId == order.OrderId);
        Assert.Equal(expected == "Succeeded" ? "Pending" : "PendingPayment", saved.OrderStatus);
        Assert.Single(saved.PaymentTransactions);
    }

    [CommerceSqlFact]
    public async Task PendingSubscription_AppearsInSearch_AndActivatesOnceAcrossManualCheckAndWebhook()
    {
        using var gateway = new Statement();
        await using var fixture = await CommerceDatabase.Create(Settings, gateway);
        var now = fixture.Clock.GetUtcNow().UtcDateTime;
        var staffId = await StaffAsync(fixture);
        var reference = "SUB0123456789";
        var profile = new WholesaleBuyerProfile
        {
            UserId = fixture.OtherBuyerId, CompanyName = "Local test company", TaxCode = "0123456789",
            BusinessAddress = "Local test address", RepresentativeName = "Test buyer", VerificationStatus = "Approved"
        };
        var tier = new B2bsubscriptionPackage { PackageName = "Test tier", Price = 100000, DurationDays = 30, IsActive = true };
        await fixture.Edit(db => { db.AddRange(profile, tier); return Task.CompletedTask; });
        var payment = new PaymentTransaction
        {
            GatewayProvider = "SePay", PaymentPurpose = "SubscriptionPayment", PaymentStatus = "Pending",
            TransactionCode = reference, RequestKey = $"{reference}|{profile.WholesaleBuyerProfileId}|{tier.PackageId}|0",
            PaymentType = "Transfer", Amount = tier.Price, RequestedAt = now.AddMinutes(-5), ExpiresAt = now.AddMinutes(10)
        };
        await fixture.Edit(db => { db.PaymentTransactions.Add(payment); return Task.CompletedTask; });
        gateway.Reference = reference;
        gateway.PaidAt = now.AddMinutes(-1);

        var client = fixture.As(staffId, "FinanceStaff");
        var results = await client.GetFromJsonAsync<PagedResponse<FinancePaymentRow>>(
            "api/finance/payments?category=Subscription&provider=SePay&status=Pending");
        var row = Assert.Single(results!.Items);
        Assert.Equal(reference, row.Reference);
        string path = $"api/finance/payments/{payment.PaymentTransactionId}";
        var detail = await client.GetFromJsonAsync<FinancePaymentDetail>(path);
        Assert.Equal("Succeeded", (await CheckAsync(client, path,
            new ReconcilePaymentRequest(detail!.Version, Guid.NewGuid().ToString("D")))).Outcome);

        using var webhook = new HttpRequestMessage(HttpMethod.Post, "api/payment/sepay-webhook")
        {
            Content = JsonContent.Create(new
            {
                id = 1001, transferAmount = 100000, transferType = "in", transactionDate = gateway.LocalPaidAt,
                referenceCode = "FT1001", accountNumber = gateway.AccountNumber, content = reference, gateway = "MBBank"
            })
        };
        webhook.Headers.TryAddWithoutValidation("Authorization", "Apikey test-webhook");
        using var replay = await client.SendAsync(webhook);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        await using var saved = fixture.Open();
        var subscription = await saved.B2bsubscriptions.SingleAsync();
        Assert.Equal("Active", subscription.SubscriptionStatus);
        Assert.Equal(subscription.B2bsubscriptionId, (await saved.PaymentTransactions.FindAsync(
            payment.PaymentTransactionId))!.B2bsubscriptionId);
        Assert.Single(await saved.Notifications.Where(n => n.NotificationType == "SubscriptionActivated").ToListAsync());
    }

    private static async Task<ReconcilePaymentResponse> CheckAsync(HttpClient client, string url, ReconcilePaymentRequest request)
    {
        using var response = await client.PostAsJsonAsync(url + "/reconcile", request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ReconcilePaymentResponse>())!;
    }

    private static async Task<long> StaffAsync(CommerceDatabase fixture)
    {
        var staff = new User
        {
            Username = "finance-test", Email = "finance@example.test", FullName = "Finance Test",
            PasswordHash = "test-only", Role = "FinanceStaff", Status = "Active", IsEmailVerified = true
        };
        await fixture.Edit(db => { db.Users.Add(staff); return Task.CompletedTask; });
        return staff.UserId;
    }

    private static async Task<long?> AuditActorAsync(CommerceDatabase fixture, long id)
    {
        await using var db = fixture.Open();
        return await db.AuditLogs.Where(a => a.Action == FinancePaymentRules.AuditAction && a.EntityId == id)
            .Select(a => a.PerformedByUserId).SingleAsync();
    }

    private sealed class Statement : HttpMessageHandler
    {
        public int Reads { get; private set; }
        public string Reference { get; set; } = "";
        public string AccountNumber { get; set; } = "0001234567";
        public int Amount { get; set; } = 100000;
        public DateTime PaidAt { get; set; }
        public string LocalPaidAt => PaidAt.AddHours(7).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Reads++;
            Assert.Equal("my.sepay.vn", request.RequestUri!.Host);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-token", request.Headers.Authorization.Parameter);
            Assert.Contains("account_number=0001234567", request.RequestUri.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    status = 200,
                    transactions = new[] { new
                    {
                        id = "1001", account_number = AccountNumber, bank_brand_name = "MBBank",
                        amount_in = Amount, amount_out = "0.00", transaction_date = LocalPaidAt,
                        code = (string?)null, transaction_content = $"MB {Reference} FT1001",
                        reference_number = "FT1001"
                    } }
                })
            });
        }
    }
}
