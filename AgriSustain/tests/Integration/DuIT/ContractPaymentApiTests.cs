using System.Net;
using System.Net.Http.Json;
using AgriSustain.IntegrationTests.UT1X;
using AgriSustain.IntegrationTests.UseCaseApi;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.DuIT;

/// <summary>UC-FIN-06 Track B2B Contract Payments (Finance Staff) and SePay auto-matching, against SQL Server.</summary>
[Trait("Layer", "SqlIntegration"), Trait("UseCase", "UC-FIN-06")]
public sealed class ContractPaymentApiTests : IAsyncLifetime
{
    private DuApiHost _api = null!;

    public async Task InitializeAsync() => _api = await DuApiHost.StartAsync();

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private Task<HttpResponseMessage> Confirm(long lineId, decimal amount, string reference = "FT26300111") =>
        _api.Finance().PostAsJsonAsync($"api/finance/contract-payments/lines/{lineId}/confirm",
            new { amount, reference, note = "Đối chiếu sao kê" });

    private async Task<(string Status, decimal Paid)> LineAsync(long lineId)
    {
        await using var db = _api.Open();
        var line = await db.ContractMilestones.AsNoTracking().SingleAsync(m => m.ContractMilestoneId == lineId);
        return (line.PaymentStatus, line.PaidAmount);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-06 #01] The list shows tracked contracts only, with the overdue amount")]
    public async Task List_TrackedContracts()
    {
        var json = await ApiAssert.Status(await _api.Finance().GetAsync("api/finance/contract-payments"), HttpStatusCode.OK);

        Assert.Equal(1, json.GetProperty("total").GetInt32());
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());
        Assert.Equal(("HD-IT-001", "Overdue", 3_000_000m), (item.GetProperty("contractNumber").GetString(),
            item.GetProperty("paymentStatus").GetString(), item.GetProperty("outstanding").GetDecimal()));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-06 #02] The schedule gives each line its CTP transfer reference and allowed actions")]
    public async Task Schedule_LinesAndActions()
    {
        var json = await ApiAssert.Status(await _api.Finance().GetAsync($"api/finance/contract-payments/{_api.ContractId}"),
            HttpStatusCode.OK);

        var lines = json.GetProperty("lines").EnumerateArray().ToDictionary(l => l.GetProperty("lineId").GetInt64());
        var overdue = lines[_api.OverdueLineId];
        Assert.Equal(($"CTP{_api.OverdueLineId:D6}", true, true), (overdue.GetProperty("paymentReference").GetString(),
            overdue.GetProperty("canConfirm").GetBoolean(), overdue.GetProperty("canMarkOverdue").GetBoolean()));
        Assert.False(lines[_api.FutureLineId].GetProperty("canMarkOverdue").GetBoolean());
        Assert.False(json.GetProperty("readOnly").GetBoolean());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-06 #03] Confirming a full transfer pays the line and writes payment, audit and notifications")]
    public async Task Confirm_FullAmount_LinePaid()
    {
        await ApiAssert.Status(await Confirm(_api.OverdueLineId, 1_000_000), HttpStatusCode.OK);

        Assert.Equal(("Paid", 1_000_000m), await LineAsync(_api.OverdueLineId));
        await using var db = _api.Open();
        var payment = await db.PaymentTransactions.SingleAsync(p => p.ContractMilestoneId == _api.OverdueLineId);
        Assert.Equal(("Success", "ContractPayment", "FT26300111", 1_000_000m),
            (payment.PaymentStatus, payment.PaymentPurpose, payment.ProviderTransactionReference, payment.Amount));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityType == "ContractMilestone" && a.EntityId == _api.OverdueLineId
            && a.Action == "ContractPayment:ConfirmReceived" && a.PerformedByUserId == _api.FinanceId));
        var notified = await db.Notifications.Where(n => n.RelatedRecordType == "B2BContract" && n.RelatedRecordId == _api.ContractId)
            .Select(n => n.UserId).ToListAsync();
        Assert.Contains(_api.WholesaleId, notified);
        Assert.Contains(_api.FarmerAId, notified);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-06 #04] A partial transfer leaves the line PartiallyPaid")]
    public async Task Confirm_Partial_PartiallyPaid()
    {
        await ApiAssert.Status(await Confirm(_api.FutureLineId, 300_000), HttpStatusCode.OK);

        Assert.Equal(("PartiallyPaid", 300_000m), await LineAsync(_api.FutureLineId));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-06 #05] An amount above the outstanding balance is refused and nothing is written")]
    public async Task Confirm_TooHigh_Rejected()
    {
        var response = await Confirm(_api.OverdueLineId, 1_500_000);

        await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "AMOUNT_EXCEEDS_DUE");
        Assert.Equal(("Unpaid", 0m), await LineAsync(_api.OverdueLineId));
        await using var db = _api.Open();
        Assert.False(await db.PaymentTransactions.AnyAsync(p => p.ContractMilestoneId == _api.OverdueLineId));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-06 #06] A bank statement reference cannot be recorded twice")]
    public async Task Confirm_ReferenceUsed_Conflict()
    {
        await ApiAssert.Status(await Confirm(_api.FutureLineId, 100_000, "FT-DUP-1"), HttpStatusCode.OK);

        var response = await Confirm(_api.OverdueLineId, 100_000, "FT-DUP-1");

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "REFERENCE_USED");
        Assert.Equal(("Unpaid", 0m), await LineAsync(_api.OverdueLineId));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-06 #07] A terminated contract is read-only")]
    public async Task Confirm_TerminatedContract_ReadOnly()
    {
        var response = await Confirm(_api.TerminatedLineId, 100_000);

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "CONTRACT_READ_ONLY");
        Assert.Equal(("Unpaid", 0m), await LineAsync(_api.TerminatedLineId));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-06 #08] Marking a past-due line overdue saves the status, the note and e-mails the buyer")]
    public async Task MarkOverdue_PastDue_Saved()
    {
        var response = await _api.Finance().PostAsJsonAsync($"api/finance/contract-payments/lines/{_api.OverdueLineId}/overdue",
            new { note = "Đã gọi điện nhắc bên mua" });

        await ApiAssert.Status(response, HttpStatusCode.OK);
        Assert.Equal("Overdue", (await LineAsync(_api.OverdueLineId)).Status);
        await using var db = _api.Open();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == _api.OverdueLineId && a.Action == "ContractPayment:MarkOverdue"
            && a.Reason == "Đã gọi điện nhắc bên mua"));
        Assert.Contains(_api.Mail.Sent, m => m.Recipient == "wholesale@example.test");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-06 #09] A line that is not due yet cannot be marked overdue")]
    public async Task MarkOverdue_NotDue_Conflict()
    {
        var response = await _api.Finance().PostAsJsonAsync($"api/finance/contract-payments/lines/{_api.FutureLineId}/overdue",
            new { note = "Thử" });

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "NOT_DUE_YET");
        Assert.Equal("Unpaid", (await LineAsync(_api.FutureLineId)).Status);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-06 #10] A payment reminder is e-mailed once; a second one within 24 hours returns 429")]
    public async Task Reminder_OncePer24Hours()
    {
        string url = $"api/finance/contract-payments/lines/{_api.FutureLineId}/remind";
        await ApiAssert.Status(await _api.Finance().PostAsync(url, null), HttpStatusCode.OK);
        _api.Clock.Advance(TimeSpan.FromHours(23));

        var response = await _api.Finance().PostAsync(url, null);

        await ApiAssert.Problem(response, (HttpStatusCode)429, "REMINDER_TOO_SOON");
        await using var db = _api.Open();
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.EntityId == _api.FutureLineId && a.Action == "ContractPayment:Reminder"));
        Assert.Single(_api.Mail.Sent, m => m.Recipient == "wholesale@example.test");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-06 #11] A SePay credit carrying the CTP reference is applied to the line automatically")]
    public async Task Webhook_CtpReference_Applied()
    {
        var response = await _api.SePayTransferAsync(9201, 1_000_000, $"CTP{_api.OverdueLineId:D6} thanh toan dot 1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(("Paid", 1_000_000m), await LineAsync(_api.OverdueLineId));
        await using var db = _api.Open();
        var payment = await db.PaymentTransactions.SingleAsync(p => p.ContractMilestoneId == _api.OverdueLineId);
        Assert.Equal(("SePay", "SEPAY-9201"), (payment.GatewayProvider, payment.TransactionCode));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == _api.OverdueLineId && a.Action == "ContractPayment:GatewayApplied"
            && a.PerformedByUserId == null));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-06 #12] A WholesaleBuyer cannot open the Finance contract payment screen")]
    public async Task List_WholesaleBuyer_Forbidden()
    {
        var response = await _api.Wholesale().GetAsync("api/finance/contract-payments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
