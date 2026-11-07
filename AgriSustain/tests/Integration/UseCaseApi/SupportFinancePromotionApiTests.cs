using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.DTOs.Promotions;
using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.IntegrationTests.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgriSustain.IntegrationTests.UseCaseApi;

/// <summary>UC-CS-01, UC-FIN-03 and UC-PS-01 through their controllers on SQL Server.</summary>
[Trait("Layer", "SqlIntegration")]
public sealed class SupportFinancePromotionApiTests : IAsyncLifetime
{
    private ApiHost _api = null!;

    public async Task InitializeAsync() => _api = await ApiHost.StartAsync();

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private string Complaint(long? ticketId = null) => $"api/support/complaints/{ticketId ?? _api.TicketId}";

    private static ResolveComplaintRequest Decision(string resolution, string fault = ComplaintParties.Seller) =>
        new(resolution, fault, null, "Hàng hỏng theo ảnh");

    /// <summary>An approved complaint refund; by default Finance has already started its transfer.</summary>
    private async Task<long> RefundRequestAsync(long? ticketId = null, bool startTransfer = true)
    {
        await ApiAssert.Status(await _api.Support().PostAsJsonAsync($"{Complaint(ticketId)}/resolve",
            Decision(ComplaintResolutions.FullRefund)), HttpStatusCode.OK);
        long refundId;
        await using (var db = _api.Open())
        {
            refundId = (await db.RefundTransactions.OrderByDescending(refund => refund.RefundTransactionId).FirstAsync())
                .RefundTransactionId;
        }

        if (startTransfer)
        {
            await ApiAssert.Status(await _api.Finance().PostAsJsonAsync($"api/finance/refunds/{refundId}/start-transfer",
                new RefundTransferRequest(RefundTransaction.StatusPending)), HttpStatusCode.OK);
        }

        return refundId;
    }

    // ---------- UC-CS-01 ----------

    [CommerceSqlFact(DisplayName = "[IT UC-CS-01 #05] Finance staff cannot open the complaint queue")]
    public async Task Complaints_Finance_Forbidden()
    {
        await ApiAssert.Status(await _api.Finance().GetAsync("api/support/complaints"), HttpStatusCode.Forbidden);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-CS-01 #06] Reject decision is stored, both parties notified, no refund")]
    public async Task Resolve_Reject_Persisted()
    {
        var response = await _api.Support().PostAsJsonAsync($"{Complaint()}/resolve",
            new ResolveComplaintRequest(ComplaintResolutions.RejectClaim, ComplaintParties.Buyer, null, "Không đủ bằng chứng"));

        await ApiAssert.Status(response, HttpStatusCode.OK);
        await using var db = _api.Open();
        Assert.Equal(DisputeTicket.StatusRejected, (await db.DisputeTickets.SingleAsync()).TicketStatus);
        Assert.Empty(await db.RefundTransactions.ToListAsync());
        var notified = await db.Notifications.Select(notice => notice.UserId).ToListAsync();
        Assert.Contains(_api.OtherBuyerId, notified);
        Assert.Contains(_api.FarmerUserId, notified);
        Assert.Contains(await db.AuditLogs.ToListAsync(), log => log.Action == "ResolveComplaint");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-CS-01 #07] Two simultaneous decisions create exactly one refund")]
    public async Task Resolve_Concurrent_OneRefund()
    {
        using var first = _api.NewClient(_api.SupportId, "CustomerSupport");
        using var second = _api.NewClient(_api.SupportId, "CustomerSupport");
        var body = Decision(ComplaintResolutions.FullRefund);

        var responses = await Task.WhenAll(
            first.PostAsJsonAsync($"{Complaint()}/resolve", body),
            second.PostAsJsonAsync($"{Complaint()}/resolve", body));

        var statuses = responses.Select(response => response.StatusCode).OrderBy(status => status).ToArray();
        Assert.True(statuses.SequenceEqual([HttpStatusCode.OK, HttpStatusCode.Conflict]),
            $"Statuses: {string.Join(", ", statuses.Select(status => (int)status))}; bodies: "
                + string.Join(" | ", await Task.WhenAll(responses.Select(response => response.Content.ReadAsStringAsync()))));
        await using var db = _api.Open();
        Assert.Single(await db.RefundTransactions.ToListAsync());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-CS-01 #08] Information request opens a 48-hour request and notifies the farmer")]
    public async Task RequestInfo_Seller_Persisted()
    {
        var response = await _api.Support().PostAsJsonAsync($"{Complaint()}/request-info",
            new RequestComplaintInfoRequest(ComplaintParties.Seller, "Gửi ảnh lô hàng"));

        await ApiAssert.Status(response, HttpStatusCode.OK);
        await using var db = _api.Open();
        var request = await db.DisputeEvidenceRequests.SingleAsync();
        Assert.Equal((_api.FarmerUserId, ApiHost.T0.UtcDateTime.AddHours(48)), (request.RequestedPartyUserId, request.DueAt!.Value));
        Assert.Equal(DisputeTicket.StatusWaitingInfo, (await db.DisputeTickets.SingleAsync()).TicketStatus);
        Assert.Contains(await db.Notifications.ToListAsync(), notice => notice.UserId == _api.FarmerUserId);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-CS-01 #09] A second open request to the same party is refused")]
    public async Task RequestInfo_Twice_Conflict()
    {
        var body = new RequestComplaintInfoRequest(ComplaintParties.Seller, "Gửi ảnh lô hàng");
        await _api.Support().PostAsJsonAsync($"{Complaint()}/request-info", body);

        var again = await _api.Support().PostAsJsonAsync($"{Complaint()}/request-info", body);

        await ApiAssert.Problem(again, HttpStatusCode.Conflict, "INFO_REQUEST_PENDING");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-CS-01 #10] Overdue processor auto-refunds when the seller stays silent for 48 hours")]
    public async Task Overdue_SellerSilent_AutoRefund()
    {
        await _api.Support().PostAsJsonAsync($"{Complaint()}/request-info",
            new RequestComplaintInfoRequest(ComplaintParties.Seller, "Gửi ảnh lô hàng"));
        _api.Clock.Advance(TimeSpan.FromHours(48).Add(TimeSpan.FromMinutes(1)));

        var processor = _api.Service(sp => sp.GetRequiredService<IComplaintEvidenceTimeoutProcessor>(), out var scope);
        int resolved;
        using (scope)
        {
            resolved = await processor.ResolveOverdueAsync(default);
        }

        Assert.Equal(1, resolved);
        await using var db = _api.Open();
        Assert.Equal(DisputeTicket.StatusRefundPending, (await db.DisputeTickets.SingleAsync()).TicketStatus);
        Assert.Equal(180_000m, (await db.RefundTransactions.SingleAsync()).Amount);
        var audit = await db.AuditLogs.SingleAsync(log => log.Action == "AutoResolveComplaint");
        Assert.Null(audit.PerformedByUserId);
        Assert.StartsWith("Evidence Overdue", audit.Reason);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-CS-01 #11] Evidence that does not belong to the ticket is not served")]
    public async Task Evidence_NotOnTicket_NotFound()
    {
        var response = await _api.Support().GetAsync($"{Complaint()}/evidence/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-CS-01 #12] Approved refund snapshots the buyer's default account and queues MSG-RF-07")]
    public async Task Resolve_FullRefund_SnapshotsAccountAndQueuesEmail()
    {
        await ApiAssert.Status(await _api.Support().PostAsJsonAsync($"{Complaint()}/resolve",
            Decision(ComplaintResolutions.FullRefund)), HttpStatusCode.OK);

        await using var db = _api.Open();
        var refund = await db.RefundTransactions.SingleAsync();
        Assert.Equal(("VCB", "0071000222222", "BUYER TWO"),
            (refund.ReceiverBankCode, refund.ReceiverAccountNumber, refund.ReceiverAccountHolder));
        Assert.Equal(ApiHost.T0.UtcDateTime, refund.ReceiverAccountProvidedAt);
        var email = await db.Notifications.SingleAsync(notice => notice.DeliveryChannel == "Email"
            && notice.NotificationType == "RefundEmail");
        Assert.Equal((_api.OtherBuyerId, "RefundEmail", "Pending"), (email.UserId, email.NotificationType, email.DeliveryStatus));
        Assert.Contains("180.000", email.Content.Replace(",", "."));
        // UC-CS-01 Step 9: the decision result is also emailed to the buyer and the farmer.
        var decisionEmails = await db.Notifications.Where(notice => notice.NotificationType == "ComplaintEmail")
            .Select(notice => notice.UserId).ToListAsync();
        Assert.Contains(_api.OtherBuyerId, decisionEmails);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-CS-01 #13] The complaint list shows the open evidence deadline")]
    public async Task Search_ShowsEvidenceDeadline()
    {
        await _api.Support().PostAsJsonAsync($"{Complaint()}/request-info",
            new RequestComplaintInfoRequest(ComplaintParties.Seller, "Gửi ảnh lô hàng"));

        var json = await ApiAssert.Status(await _api.Support().GetAsync("api/support/complaints"), HttpStatusCode.OK);

        var item = json.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(ApiHost.T0.UtcDateTime.AddHours(48), item.GetProperty("evidenceDueAt").GetDateTime().ToUniversalTime());
        Assert.False(item.GetProperty("evidenceComplete").GetBoolean());
        Assert.False(item.GetProperty("hasOverdueEvidence").GetBoolean());
    }

    // ---------- UC-FIN-03 ----------

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-03 #03] Customer support cannot open the refund queue")]
    public async Task Refunds_Support_Forbidden()
    {
        await ApiAssert.Status(await _api.Support().GetAsync("api/finance/refunds"), HttpStatusCode.Forbidden);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-03 #04] A transfer reference already used is refused")]
    public async Task Complete_DuplicateReference_Conflict()
    {
        long firstRefund = await RefundRequestAsync();
        long secondTicket;
        await using (var db = _api.Open())
        {
            var sellerB = await db.SellerOrders.SingleAsync(order => order.SellerOrderCode == "SO-B");
            var ticket = new DisputeTicket
            {
                SubmittedByUserId = _api.OtherBuyerId, SellerOrderId = sellerB.SellerOrderId, DisputeReason = "Damaged",
                TicketStatus = DisputeTicket.StatusOpen, CreatedAt = ApiHost.T0.UtcDateTime
            };
            db.DisputeTickets.Add(ticket);
            await db.SaveChangesAsync();
            secondTicket = ticket.DisputeTicketId;
        }

        long secondRefund = await RefundRequestAsync(secondTicket);
        var transfer = new CompleteRefundRequest(RefundMethods.BankTransfer, "VCB-0001", null);

        await ApiAssert.Status(await _api.Finance().PostAsJsonAsync($"api/finance/refunds/{firstRefund}/complete", transfer),
            HttpStatusCode.OK);
        var duplicate = await _api.Finance().PostAsJsonAsync($"api/finance/refunds/{secondRefund}/complete", transfer);

        await ApiAssert.Problem(duplicate, HttpStatusCode.Conflict, "TRANSFER_REFERENCE_USED");
        await using var check = _api.Open();
        Assert.Equal(RefundTransaction.StatusProcessing,
            (await check.RefundTransactions.SingleAsync(refund => refund.RefundTransactionId == secondRefund)).RefundStatus);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-03 #05] Transfer failure sets Failed, is audited and emails the buyer")]
    public async Task TransferFailed_Audited()
    {
        long refundId = await RefundRequestAsync();

        var response = await _api.Finance().PostAsJsonAsync($"api/finance/refunds/{refundId}/transfer-failed",
            new RefundTransferFailedRequest("Sai số tài khoản"));

        await ApiAssert.Status(response, HttpStatusCode.OK);
        await using var db = _api.Open();
        Assert.Equal(RefundTransaction.StatusFailed, (await db.RefundTransactions.SingleAsync()).RefundStatus);
        var audit = await db.AuditLogs.SingleAsync(log => log.Action == "RefundTransferFailed");
        Assert.Equal("Sai số tài khoản", audit.Reason);
        Assert.True(await db.Notifications.AnyAsync(notice => notice.NotificationType == "RefundEmail"
            && notice.PayloadJson!.Contains(":failed:")));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-03 #06] A completed refund cannot be completed again")]
    public async Task Complete_Twice_Conflict()
    {
        long refundId = await RefundRequestAsync();
        string url = $"api/finance/refunds/{refundId}/complete";
        await _api.Finance().PostAsJsonAsync(url, new CompleteRefundRequest(RefundMethods.BankTransfer, "VCB-0001", null));

        var again = await _api.Finance().PostAsJsonAsync(url, new CompleteRefundRequest(RefundMethods.BankTransfer, "VCB-0002", null));

        await ApiAssert.Problem(again, HttpStatusCode.Conflict, "REFUND_NOT_PENDING");
        await using var db = _api.Open();
        Assert.Single(await db.AuditLogs.Where(log => log.Action == "CompleteRefund").ToListAsync());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-03 #07] Gateway refund completes the request and the seller order")]
    public async Task Complete_GatewayRefund_Completed()
    {
        long refundId = await RefundRequestAsync();

        var response = await _api.Finance().PostAsJsonAsync($"api/finance/refunds/{refundId}/complete",
            new CompleteRefundRequest(RefundMethods.GatewayRefund, "PAYOS-2026/10_01", null));

        await ApiAssert.Status(response, HttpStatusCode.OK);
        await using var db = _api.Open();
        var refund = await db.RefundTransactions.SingleAsync();
        Assert.Equal((RefundTransaction.StatusCompleted, RefundMethods.GatewayRefund), (refund.RefundStatus, refund.RefundMethod));
        Assert.Equal("Refunded", (await db.SellerOrders.SingleAsync(order => order.SellerOrderId == _api.SellerOrderId)).Status);
        var notified = await db.Notifications.Where(notice => notice.NotificationType == "Refund")
            .Select(notice => notice.UserId).ToListAsync();
        Assert.Contains(_api.OtherBuyerId, notified);
        Assert.Contains(_api.FarmerUserId, notified);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-03 #08] Refund search clamps paging")]
    public async Task Search_ClampsPaging()
    {
        var json = await ApiAssert.Status(await _api.Finance().GetAsync("api/finance/refunds?page=0&pageSize=500"),
            HttpStatusCode.OK);

        Assert.Equal((1, 100), (json.GetProperty("page").GetInt32(), json.GetProperty("pageSize").GetInt32()));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-03 #09] Two finance users completing at once: one succeeds")]
    public async Task Complete_Concurrent_OneSucceeds()
    {
        long refundId = await RefundRequestAsync();
        using var first = _api.NewClient(_api.FinanceId, "FinanceStaff");
        using var second = _api.NewClient(_api.FinanceId, "FinanceStaff");
        string url = $"api/finance/refunds/{refundId}/complete";

        var responses = await Task.WhenAll(
            first.PostAsJsonAsync(url, new CompleteRefundRequest(RefundMethods.BankTransfer, "VCB-A", null)),
            second.PostAsJsonAsync(url, new CompleteRefundRequest(RefundMethods.BankTransfer, "VCB-B", null)));

        var statuses = responses.Select(response => response.StatusCode).OrderBy(status => status).ToArray();
        Assert.True(statuses.SequenceEqual([HttpStatusCode.OK, HttpStatusCode.Conflict]),
            $"Statuses: {string.Join(", ", statuses.Select(status => (int)status))}; bodies: "
                + string.Join(" | ", await Task.WhenAll(responses.Select(response => response.Content.ReadAsStringAsync()))));
        await using var db = _api.Open();
        Assert.Single(await db.AuditLogs.Where(log => log.Action == "CompleteRefund").ToListAsync());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-03 #10] Start Transfer needs the customer account and shows it masked")]
    public async Task StartTransfer_NoAccount_Blocked()
    {
        long refundId = await RefundRequestAsync(startTransfer: false);
        var detail = await ApiAssert.Status(await _api.Finance().GetAsync($"api/finance/refunds/{refundId}"), HttpStatusCode.OK);
        Assert.Equal("****2222", detail.GetProperty("receiverAccountMasked").GetString());
        Assert.True(detail.GetProperty("canStartTransfer").GetBoolean());
        await using (var db = _api.Open())
        {
            await db.RefundTransactions.Where(refund => refund.RefundTransactionId == refundId)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(refund => refund.ReceiverBankCode, (string?)null)
                    .SetProperty(refund => refund.ReceiverAccountNumber, (string?)null)
                    .SetProperty(refund => refund.ReceiverAccountHolder, (string?)null)
                    .SetProperty(refund => refund.ReceiverAccountProvidedAt, (DateTime?)null));
        }

        var response = await _api.Finance().PostAsJsonAsync($"api/finance/refunds/{refundId}/start-transfer",
            new RefundTransferRequest(null));

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "REFUND_ACCOUNT_MISSING");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-03 #11] Cancel Transfer and Needs Reconciliation follow BR-RF-03")]
    public async Task CancelAndReconcile_StatusFlow()
    {
        long refundId = await RefundRequestAsync();
        string url = $"api/finance/refunds/{refundId}";

        await ApiAssert.Status(await _api.Finance().PostAsJsonAsync($"{url}/cancel-transfer",
            new RefundTransferRequest(RefundTransaction.StatusProcessing)), HttpStatusCode.OK);
        await ApiAssert.Status(await _api.Finance().PostAsJsonAsync($"{url}/start-transfer",
            new RefundTransferRequest(RefundTransaction.StatusPending)), HttpStatusCode.OK);
        await ApiAssert.Status(await _api.Finance().PostAsJsonAsync($"{url}/needs-reconciliation",
            new RefundReconciliationRequest("Chưa thấy trên sao kê")), HttpStatusCode.OK);
        await ApiAssert.Status(await _api.Finance().PostAsJsonAsync($"{url}/complete",
            new CompleteRefundRequest(RefundMethods.BankTransfer, "VCB-RECON-1", null)), HttpStatusCode.OK);

        var detail = await ApiAssert.Status(await _api.Finance().GetAsync(url), HttpStatusCode.OK);
        Assert.Equal(RefundTransaction.StatusCompleted, detail.GetProperty("refundStatus").GetString());
        var actions = detail.GetProperty("history").EnumerateArray().Select(row => row.GetProperty("action").GetString()).ToArray();
        Assert.Equal(["RequestCreated", "AccountProvided", "StartTransfer", "CancelTransfer", "StartTransfer",
            "NeedsReconciliation", "CompleteRefund"], actions);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-03 #12] Transfer Again restarts a failed refund as a new attempt")]
    public async Task TransferAgain_AfterFailure_Processing()
    {
        long refundId = await RefundRequestAsync();
        string url = $"api/finance/refunds/{refundId}";
        await ApiAssert.Status(await _api.Finance().PostAsJsonAsync($"{url}/transfer-failed",
            new RefundTransferFailedRequest("Sai số tài khoản")), HttpStatusCode.OK);

        // The customer has not checked the account yet: Transfer Again is refused, Finance can remind.
        await ApiAssert.Problem(await _api.Finance().PostAsJsonAsync($"{url}/start-transfer",
            new RefundTransferRequest(RefundTransaction.StatusFailed)), HttpStatusCode.Conflict, "REFUND_ACCOUNT_NOT_RECHECKED");
        await ApiAssert.Status(await _api.Finance().PostAsJsonAsync($"{url}/remind-account", new { }), HttpStatusCode.OK);
        await ApiAssert.Problem(await _api.Finance().PostAsJsonAsync($"{url}/remind-account", new { }),
            HttpStatusCode.Conflict, "REFUND_REMINDER_TOO_SOON");
        // The customer confirms the account again in UC-FIN-08 (this test host does not register that
        // use case, so its saved result is written directly: a newer ReceiverAccountProvidedAt).
        _api.Clock.Advance(TimeSpan.FromMinutes(5));
        await using (var setup = _api.Open())
        {
            var failed = await setup.RefundTransactions.SingleAsync();
            failed.ReceiverAccountProvidedAt = _api.Clock.GetUtcNow().UtcDateTime;
            await setup.SaveChangesAsync();
        }

        await ApiAssert.Status(await _api.Finance().PostAsJsonAsync($"{url}/start-transfer",
            new RefundTransferRequest(RefundTransaction.StatusFailed)), HttpStatusCode.OK);

        await using var db = _api.Open();
        Assert.Equal(RefundTransaction.StatusProcessing, (await db.RefundTransactions.SingleAsync()).RefundStatus);
        var actions = await db.AuditLogs.Where(log => log.EntityType == "RefundTransaction")
            .OrderBy(log => log.AuditLogId).Select(log => log.Action).ToListAsync();
        Assert.Equal(["StartTransfer", "RefundTransferFailed", "RemindRefundAccount", "TransferAgain"], actions);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-03 #13] A request changed by another user is refused (MSG-RF-04)")]
    public async Task StartTransfer_StaleScreen_Conflict()
    {
        long refundId = await RefundRequestAsync(startTransfer: false);

        var response = await _api.Finance().PostAsJsonAsync($"api/finance/refunds/{refundId}/start-transfer",
            new RefundTransferRequest(RefundTransaction.StatusFailed));

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "REFUND_NOT_PENDING");
        await using var db = _api.Open();
        Assert.Equal(RefundTransaction.StatusPending, (await db.RefundTransactions.SingleAsync()).RefundStatus);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-03 #14] Search filters by status and Vietnam creation date")]
    public async Task Search_StatusAndDate_Filtered()
    {
        await RefundRequestAsync();
        string day = ApiHost.T0.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd");
        string nextDay = ApiHost.T0.ToOffset(TimeSpan.FromHours(7)).AddDays(1).ToString("yyyy-MM-dd");

        var match = await ApiAssert.Status(await _api.Finance().GetAsync(
            $"api/finance/refunds?status=Processing&fromDate={day}&toDate={day}"), HttpStatusCode.OK);
        var later = await ApiAssert.Status(await _api.Finance().GetAsync(
            $"api/finance/refunds?fromDate={nextDay}"), HttpStatusCode.OK);
        var reversed = await _api.Finance().GetAsync($"api/finance/refunds?fromDate={nextDay}&toDate={day}");

        var item = match.GetProperty("items").EnumerateArray().Single();
        Assert.True(item.GetProperty("hasReceivingAccount").GetBoolean());
        Assert.Equal("Complaint", item.GetProperty("sourceType").GetString());
        Assert.Equal(0, later.GetProperty("total").GetInt32());
        await ApiAssert.Status(reversed, HttpStatusCode.BadRequest);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-03 #15] Success queues the buyer e-mail with only the last four digits")]
    public async Task Complete_QueuesMaskedEmail()
    {
        long refundId = await RefundRequestAsync();

        await ApiAssert.Status(await _api.Finance().PostAsJsonAsync($"api/finance/refunds/{refundId}/complete",
            new CompleteRefundRequest(RefundMethods.BankTransfer, "VCB-231026-778812", null)), HttpStatusCode.OK);

        await using var db = _api.Open();
        var email = await db.Notifications.SingleAsync(notice => notice.NotificationType == "RefundEmail"
            && notice.PayloadJson!.Contains(":completed:"));
        Assert.Equal((_api.OtherBuyerId, "Email", "Pending"), (email.UserId, email.DeliveryChannel, email.DeliveryStatus));
        Assert.Contains("2222", email.Content);
        Assert.DoesNotContain("0071000222222", email.Content);
        Assert.Contains("VCB-231026-778812", email.Content);
        var history = await db.AuditLogs.Where(log => log.EntityType == "RefundTransaction").Select(log => log.NewValue).ToListAsync();
        Assert.All(history, row => Assert.DoesNotContain("0071000222222", row));
    }

    // ---------- UC-PS-01 ----------

    private static SaveVoucherRequest Voucher(string code = " sale10 ", decimal value = 10, decimal min = 0, int limit = 100,
        int startDays = 0) =>
        new("Mùa vụ xanh", code, null, Domain.Entities.Voucher.TypePercentage, value, 50_000, min, limit, 1,
            ApiHost.T0.AddDays(startDays), ApiHost.T0.AddDays(30));

    private async Task<long> CreateVoucherAsync(SaveVoucherRequest? request = null)
    {
        var json = await ApiAssert.Status(await _api.Promotion().PostAsJsonAsync("api/promotions/vouchers", request ?? Voucher()),
            HttpStatusCode.OK);
        return json.GetProperty("voucherId").GetInt64();
    }

    private async Task<decimal> QuotedDiscountAsync(string code)
    {
        var quote = await _api.QuoteAsync(ApiHost.CheckoutInput(voucher: code));
        return quote.VoucherDiscount;
    }

    [CommerceSqlFact(DisplayName = "[IT UC-PS-01 #01] Create stores an upper-case platform voucher and audits it")]
    public async Task CreateVoucher_Persisted()
    {
        long id = await CreateVoucherAsync();

        await using var db = _api.Open();
        var voucher = await db.Vouchers.SingleAsync(candidate => candidate.VoucherId == id);
        Assert.Equal(("SALE10", (long?)null, 0), (voucher.VoucherCode, voucher.FarmerProfileId, voucher.UsedCount));
        Assert.Contains(await db.AuditLogs.ToListAsync(), log => log.Action == "CreateVoucher" && log.EntityId == id);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-PS-01 #02] Voucher code is unique regardless of case")]
    public async Task CreateVoucher_DuplicateCode_Conflict()
    {
        await CreateVoucherAsync();

        var response = await _api.Promotion().PostAsJsonAsync("api/promotions/vouchers", Voucher(code: "Sale10"));

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "VOUCHER_CODE_EXISTS");
        await using var db = _api.Open();
        Assert.Single(await db.Vouchers.ToListAsync());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-PS-01 #03] Terms of a used voucher are locked")]
    public async Task UpdateVoucher_Used_TermsLocked()
    {
        long id = await CreateVoucherAsync();
        await using (var db = _api.Open())
        {
            await db.Vouchers.Where(voucher => voucher.VoucherId == id).ExecuteUpdateAsync(set => set.SetProperty(voucher => voucher.UsedCount, 1));
        }

        var response = await _api.Promotion().PutAsJsonAsync($"api/promotions/vouchers/{id}", Voucher(value: 15));

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "VOUCHER_TERMS_LOCKED");
        await using var check = _api.Open();
        Assert.Equal(10m, (await check.Vouchers.SingleAsync()).DiscountValue);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-PS-01 #04] Expired voucher cannot be reactivated")]
    public async Task ActivateVoucher_Expired_Rejected()
    {
        long id = await CreateVoucherAsync();
        await using (var db = _api.Open())
        {
            await db.Vouchers.Where(voucher => voucher.VoucherId == id).ExecuteUpdateAsync(set => set
                .SetProperty(voucher => voucher.IsActive, false)
                .SetProperty(voucher => voucher.EndDate, ApiHost.T0.UtcDateTime.AddDays(-1))
                .SetProperty(voucher => voucher.StartDate, ApiHost.T0.UtcDateTime.AddDays(-10)));
        }

        var response = await _api.Promotion().PostAsync($"api/promotions/vouchers/{id}/activate", null);

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "VOUCHER_CANNOT_REACTIVATE");
        await using var check = _api.Open();
        Assert.False((await check.Vouchers.SingleAsync()).IsActive);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-PS-01 #05] Unused scheduled voucher is deleted; used voucher cannot be deleted")]
    public async Task DeleteVoucher_UnusedRemoved_UsedKept()
    {
        long unused = await CreateVoucherAsync(Voucher(code: "UNUSED1", startDays: 1));
        long used = await CreateVoucherAsync(Voucher(code: "USED1", startDays: 1));
        await using (var db = _api.Open())
        {
            await db.Vouchers.Where(voucher => voucher.VoucherId == used).ExecuteUpdateAsync(set => set.SetProperty(voucher => voucher.UsedCount, 1));
        }

        await ApiAssert.Status(await _api.Promotion().DeleteAsync($"api/promotions/vouchers/{unused}"), HttpStatusCode.OK);
        await ApiAssert.Problem(await _api.Promotion().DeleteAsync($"api/promotions/vouchers/{used}"),
            HttpStatusCode.Conflict, "VOUCHER_CANNOT_DELETE");

        await using var check = _api.Open();
        Assert.False(await check.Vouchers.AnyAsync(voucher => voucher.VoucherId == unused));
        Assert.True(await check.Vouchers.AnyAsync(voucher => voucher.VoucherId == used));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-PS-01 #06] Retail customer cannot manage vouchers")]
    public async Task Vouchers_RetailCustomer_Forbidden()
    {
        await ApiAssert.Status(await _api.Buyer().GetAsync("api/promotions/vouchers"), HttpStatusCode.Forbidden);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-PS-01 #07] Farm voucher is not visible to platform promotion staff")]
    public async Task FarmVoucher_NotFound()
    {
        long farmVoucherId;
        await using (var db = _api.Open())
        {
            var voucher = new Voucher
            {
                VoucherCode = "FARM5", VoucherName = "Farm 5", DiscountType = Domain.Entities.Voucher.TypeFixedAmount, DiscountValue = 5_000,
                MinOrderAmount = 0, IsActive = true, FarmerProfileId = _api.FarmBId,
                StartDate = ApiHost.T0.UtcDateTime, EndDate = ApiHost.T0.UtcDateTime.AddDays(30)
            };
            db.Vouchers.Add(voucher);
            await db.SaveChangesAsync();
            farmVoucherId = voucher.VoucherId;
        }

        var response = await _api.Promotion().GetAsync($"api/promotions/vouchers/{farmVoucherId}");

        await ApiAssert.Problem(response, HttpStatusCode.NotFound, "VOUCHER_NOT_FOUND");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-PS-01 #08] Voucher created by promotion staff discounts the checkout quote")]
    public async Task Voucher_AppliedAtCheckout()
    {
        await CreateVoucherAsync();

        Assert.Equal(3_000m, await QuotedDiscountAsync("SALE10"));     // 10% of 2 kg × 15 000 ₫
    }

    [CommerceSqlFact(DisplayName = "[IT UC-PS-01 #09] Deactivated voucher is dropped at checkout with MSG116")]
    public async Task Voucher_Deactivated_DroppedAtCheckout()
    {
        long id = await CreateVoucherAsync();
        await ApiAssert.Status(await _api.Promotion().PostAsync($"api/promotions/vouchers/{id}/deactivate", null), HttpStatusCode.OK);

        var quote = await _api.QuoteAsync(ApiHost.CheckoutInput(voucher: "SALE10"));

        Assert.Equal(0m, quote.VoucherDiscount);
        Assert.StartsWith("MSG116", quote.Warning);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-PS-01 #10] Voucher at its usage limit is dropped at checkout")]
    public async Task Voucher_Depleted_DroppedAtCheckout()
    {
        long id = await CreateVoucherAsync(Voucher(limit: 1));
        await using (var db = _api.Open())
        {
            await db.Vouchers.Where(voucher => voucher.VoucherId == id).ExecuteUpdateAsync(set => set.SetProperty(voucher => voucher.UsedCount, 1));
        }

        var quote = await _api.QuoteAsync(ApiHost.CheckoutInput(voucher: "SALE10"));

        Assert.Equal(0m, quote.VoucherDiscount);
        Assert.StartsWith("MSG116", quote.Warning);
    }
}
