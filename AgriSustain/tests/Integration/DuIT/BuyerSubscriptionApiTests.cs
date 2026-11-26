using System.Net;
using System.Net.Http.Json;
using AgriSustain.Domain.Entities;
using AgriSustain.IntegrationTests.Commerce;
using AgriSustain.IntegrationTests.UseCaseApi;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.DuIT;

/// <summary>UC-WB-08 Manage Buyer Subscription: tiers, SePay VietQR payment and the SePay webhook, against SQL Server.</summary>
[Trait("Layer", "SqlIntegration"), Trait("UseCase", "UC-WB-08")]
public sealed class BuyerSubscriptionApiTests : IAsyncLifetime
{
    private DuApiHost _api = null!;

    public async Task InitializeAsync() => _api = await DuApiHost.StartAsync();

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private Task<HttpResponseMessage> Purchase(long packageId, bool autoRenew = false, bool confirmDowngrade = false) =>
        _api.Wholesale().PostAsJsonAsync("api/b2b/subscription/purchase", new { packageId, autoRenew, confirmDowngrade });

    /// <summary>Buys the Silver tier and returns the "SUB..." transfer reference shown under the VietQR code.</summary>
    private async Task<string> StartSilverPaymentAsync()
    {
        var json = await ApiAssert.Status(await Purchase(_api.SilverTierId), HttpStatusCode.OK);
        Assert.Equal("PaymentRequired", json.GetProperty("status").GetString());
        return json.GetProperty("payment").GetProperty("reference").GetString()!;
    }

    private async Task SeedActiveSilverAsync(int daysLeft)
    {
        await using var seed = _api.Open();
        var end = DuApiHost.T0.UtcDateTime.AddDays(daysLeft);
        seed.B2bsubscriptions.Add(new B2bsubscription
        {
            WholesaleBuyerProfileId = _api.WholesaleProfileId, PackageId = _api.SilverTierId, StartDate = end.AddDays(-30),
            EndDate = end, SubscriptionStatus = "Active", PriceSnapshot = 500_000, UnlocksFarmerDirectContactSnapshot = true,
            CanDownloadContractTemplateSnapshot = true, DurationDaysSnapshot = 30
        });
        await seed.SaveChangesAsync();
    }

    [CommerceSqlFact(DisplayName = "[IT UC-WB-08 #01] The membership screen lists the active tiers from SQL")]
    public async Task Overview_ListsTiers()
    {
        var json = await ApiAssert.Status(await _api.Wholesale().GetAsync("api/b2b/subscription"), HttpStatusCode.OK);

        var names = json.GetProperty("tiers").EnumerateArray().Select(t => t.GetProperty("packageName").GetString()).ToHashSet();
        Assert.True(names.SetEquals(new[] { "Cơ bản", "Bạc", "Vàng" }), string.Join(", ", names));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, json.GetProperty("current").ValueKind);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-WB-08 #02] The free tier is activated at once without a payment")]
    public async Task Purchase_FreeTier_ActivatedWithoutPayment()
    {
        var json = await ApiAssert.Status(await Purchase(_api.FreeTierId), HttpStatusCode.OK);

        Assert.Equal("Activated", json.GetProperty("status").GetString());
        await using var db = _api.Open();
        var subscription = await db.B2bsubscriptions.SingleAsync(s => s.WholesaleBuyerProfileId == _api.WholesaleProfileId);
        Assert.Equal((_api.FreeTierId, "Active", DuApiHost.T0.UtcDateTime.AddDays(30)),
            (subscription.PackageId, subscription.SubscriptionStatus, subscription.EndDate));
        Assert.False(await db.PaymentTransactions.AnyAsync(p => p.PaymentPurpose == "SubscriptionPayment"));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-WB-08 #03] A paid tier creates one pending SePay payment with a 15-minute window and no subscription yet")]
    public async Task Purchase_PaidTier_PendingPayment()
    {
        string reference = await StartSilverPaymentAsync();

        Assert.Matches("^SUB[0-9A-F]{10}$", reference);
        await using var db = _api.Open();
        var payment = await db.PaymentTransactions.SingleAsync(p => p.PaymentPurpose == "SubscriptionPayment");
        Assert.Equal((reference, "Pending", 500_000m, DuApiHost.T0.UtcDateTime.AddMinutes(15)),
            (payment.TransactionCode, payment.PaymentStatus, payment.Amount, payment.ExpiresAt));
        Assert.False(await db.B2bsubscriptions.AnyAsync());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-WB-08 #04] The SePay webhook with the exact amount activates the tier and links the payment")]
    public async Task Webhook_ExactAmount_Activates()
    {
        string reference = await StartSilverPaymentAsync();

        var response = await _api.SePayTransferAsync(9101, 500_000, $"{reference} thanh toan goi Bac");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = _api.Open();
        var subscription = await db.B2bsubscriptions.SingleAsync(s => s.WholesaleBuyerProfileId == _api.WholesaleProfileId);
        Assert.Equal((_api.SilverTierId, "Active", true), (subscription.PackageId, subscription.SubscriptionStatus, subscription.UnlocksFarmerDirectContactSnapshot));
        var payment = await db.PaymentTransactions.SingleAsync(p => p.PaymentPurpose == "SubscriptionPayment");
        Assert.Equal(("Success", "SEPAY-9101", (long?)subscription.B2bsubscriptionId),
            (payment.PaymentStatus, payment.TransactionCode, payment.B2bsubscriptionId));
        var status = await ApiAssert.Status(await _api.Wholesale().GetAsync($"api/b2b/subscription/payments/{reference}"), HttpStatusCode.OK);
        Assert.Equal("Success", status.GetProperty("status").GetString());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-WB-08 #05] A transfer with the wrong amount does not activate the tier")]
    public async Task Webhook_WrongAmount_NotActivated()
    {
        string reference = await StartSilverPaymentAsync();

        Assert.Equal(HttpStatusCode.OK, (await _api.SePayTransferAsync(9102, 400_000, reference)).StatusCode);

        await using var db = _api.Open();
        Assert.Equal("AmountMismatch", (await db.PaymentTransactions.SingleAsync(p => p.PaymentPurpose == "SubscriptionPayment")).PaymentStatus);
        Assert.False(await db.B2bsubscriptions.AnyAsync());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-WB-08 #06] A transfer made after the payment window does not activate the tier")]
    public async Task Webhook_AfterWindow_NotActivated()
    {
        string reference = await StartSilverPaymentAsync();
        _api.Clock.Advance(TimeSpan.FromMinutes(20));

        var response = await _api.SePayTransferAsync(9103, 500_000, reference, transactionDate: "2026-10-27 08:20:00");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = _api.Open();
        Assert.Equal("Unmatched", (await db.PaymentTransactions.SingleAsync(p => p.PaymentPurpose == "SubscriptionPayment")).PaymentStatus);
        Assert.False(await db.B2bsubscriptions.AnyAsync());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-WB-08 #07] SePay retrying the same credit is ignored (one subscription, one payment)")]
    public async Task Webhook_Retry_Idempotent()
    {
        string reference = await StartSilverPaymentAsync();
        await _api.SePayTransferAsync(9104, 500_000, reference);

        Assert.Equal(HttpStatusCode.OK, (await _api.SePayTransferAsync(9104, 500_000, reference)).StatusCode);

        await using var db = _api.Open();
        Assert.Equal(1, await db.B2bsubscriptions.CountAsync());
        Assert.Equal(1, await db.PaymentTransactions.CountAsync(p => p.PaymentPurpose == "SubscriptionPayment"));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-WB-08 #08] Buying the current tier while more than 7 days remain returns 409")]
    public async Task Purchase_SameTier_Conflict()
    {
        await SeedActiveSilverAsync(daysLeft: 25);

        var response = await Purchase(_api.SilverTierId);

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "SAME_TIER");
        await using var db = _api.Open();
        Assert.False(await db.PaymentTransactions.AnyAsync(p => p.PaymentPurpose == "SubscriptionPayment"));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-WB-08 #09] Turning on auto-renew is saved on the active subscription")]
    public async Task AutoRenew_On_Saved()
    {
        await SeedActiveSilverAsync(daysLeft: 25);

        var response = await _api.Wholesale().PutAsJsonAsync("api/b2b/subscription/auto-renew", new { autoRenew = true });

        await ApiAssert.Status(response, HttpStatusCode.OK);
        await using var db = _api.Open();
        Assert.True((await db.B2bsubscriptions.SingleAsync()).AutoRenew);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-WB-08 #10] A buyer whose business profile is not approved cannot buy a tier")]
    public async Task Purchase_NotApproved_Forbidden()
    {
        var response = await _api.WholesaleNew().PostAsJsonAsync("api/b2b/subscription/purchase",
            new { packageId = _api.SilverTierId, autoRenew = false, confirmDowngrade = false });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var db = _api.Open();
        Assert.False(await db.PaymentTransactions.AnyAsync(p => p.PaymentPurpose == "SubscriptionPayment"));
    }
}
