using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Domain.Entities;
using Xunit;

namespace AgriSustain.UnitTests.UT1X;

[Trait("UseCase", "UC-FIN-09")]
public sealed class RejectedOrderRefundTests
{
    [Fact, Trait("CaseId", "UT-FIN-09-001")]
    public async Task Search_AuthorizedValidFilters_ReturnsPagedResults()
    {
        var f = new RejectedOrderRefundFixture();
        var result = await f.Service.SearchAsync(301, new RejectedOrderRefundQuery
        {
            Keyword = "RJR",
            Status = "Pending",
            From = new DateOnly(2026, 10, 1),
            To = new DateOnly(2026, 10, 31),
            Page = 1
        }, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, f.Repo.SearchCalls);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-002")]
    public async Task Search_UnauthorizedActor_ReturnsForbidden()
    {
        var f = new RejectedOrderRefundFixture();
        f.Repo.IsAuthorized = false;
        var result = await f.Service.SearchAsync(999, new RejectedOrderRefundQuery(), default);
        Assert.False(result.IsSuccess);
        Assert.Equal("FORBIDDEN", result.Error!.Code);
        Assert.Equal(0, f.Repo.SearchCalls);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-003")]
    public async Task Search_FromAfterTo_ReturnsValidationOnToField()
    {
        var f = new RejectedOrderRefundFixture();
        var result = await f.Service.SearchAsync(301, new RejectedOrderRefundQuery
        {
            From = new DateOnly(2026, 10, 28),
            To = new DateOnly(2026, 10, 27)
        }, default);
        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION", result.Error!.Code);
        Assert.True(result.Error.Fields!.ContainsKey("to"));
        Assert.Equal(0, f.Repo.SearchCalls);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-004")]
    public async Task Search_ToDateMaxValue_ReturnsValidation()
    {
        var f = new RejectedOrderRefundFixture();
        var result = await f.Service.SearchAsync(301, new RejectedOrderRefundQuery { To = DateOnly.MaxValue }, default);
        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION", result.Error!.Code);
        Assert.True(result.Error.Fields!.ContainsKey("to"));
    }

    [Theory, Trait("CaseId", "UT-FIN-09-005")]
    [InlineData(0, "Pending", 10)]
    [InlineData(100001, "Pending", 10)]
    [InlineData(1, "UnknownStatus", 10)]
    [InlineData(1, "Pending", 256)]
    public async Task Search_InvalidPageStatusOrKeywordBoundary_ReturnsValidation(int page, string status, int keywordLen)
    {
        var f = new RejectedOrderRefundFixture();
        var result = await f.Service.SearchAsync(301, new RejectedOrderRefundQuery
        {
            Page = page,
            Status = status,
            Keyword = new string('a', keywordLen)
        }, default);
        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION", result.Error!.Code);
        Assert.Equal(0, f.Repo.SearchCalls);
    }

    [Theory, Trait("CaseId", "UT-FIN-09-006")]
    [InlineData(1, "Pending", 255)]
    [InlineData(100000, "Processing", 0)]
    [InlineData(1, "NeedsReconciliation", 10)]
    [InlineData(1, "Succeeded", 10)]
    public async Task Search_ValidBoundaryParameters_AreAccepted(int page, string status, int keywordLen)
    {
        var f = new RejectedOrderRefundFixture();
        var result = await f.Service.SearchAsync(301, new RejectedOrderRefundQuery
        {
            Page = page,
            Status = status,
            Keyword = keywordLen == 0 ? null : new string('k', keywordLen)
        }, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, f.Repo.SearchCalls);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-007")]
    public async Task Detail_ValidRequest_ReturnsFullContextAndVersion()
    {
        var f = new RejectedOrderRefundFixture();
        var result = await f.Service.DetailAsync(301, 801, default);
        Assert.True(result.IsSuccess);
        var json = JsonSerializer.Serialize(result.Value);
        Assert.Contains("RJR202610270001", json);
        Assert.Contains("SO-501", json);
        Assert.Contains("\"canOpenQr\":true", json);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-008")]
    public async Task Detail_UnauthorizedOrMissing_ReturnsExpectedError()
    {
        var f = new RejectedOrderRefundFixture();
        Assert.Equal("NOT_FOUND", (await f.Service.DetailAsync(301, 9999, default)).Error!.Code);
        f.Repo.IsAuthorized = false;
        Assert.Equal("FORBIDDEN", (await f.Service.DetailAsync(999, 801, default)).Error!.Code);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-009")]
    public async Task Preview_EligiblePendingRequest_ReturnsPlanAndConfirmationMessage()
    {
        var f = new RejectedOrderRefundFixture();
        var result = await f.Service.PreviewAsync(301, 801, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(64, result.Value!.ExpectedVersion.Length);
        Assert.Equal(150000, result.Value.Plan.Amount);
        Assert.Equal("NGUYEN VAN A", result.Value.RecipientName);
        Assert.Equal("Pending", f.Refund.RefundStatus);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-010")]
    public async Task Preview_GatewayUnavailableOrReadAccessDenied_ReturnsUnavailable()
    {
        var f = new RejectedOrderRefundFixture();
        f.Gateway.ReadAccess = false;
        var denied = await f.Service.PreviewAsync(301, 801, default);
        Assert.False(denied.IsSuccess);
        Assert.Equal("UNAVAILABLE", denied.Error!.Code);

        f.Gateway.ReadAccess = true;
        f.Gateway.CanCreateQr = false;
        var noQr = await f.Service.PreviewAsync(301, 801, default);
        Assert.False(noQr.IsSuccess);
        Assert.Equal("UNAVAILABLE", noQr.Error!.Code);
    }

    [Theory, Trait("CaseId", "UT-FIN-09-011")]
    [InlineData("Processing")]
    [InlineData("Completed")]
    [InlineData("NeedsReconciliation")]
    public async Task Preview_NonPendingRefundStatus_ReturnsConflict(string status)
    {
        var f = new RejectedOrderRefundFixture();
        f.Refund.RefundStatus = status;
        var result = await f.Service.PreviewAsync(301, 801, default);
        Assert.False(result.IsSuccess);
        Assert.Equal("CONFLICT", result.Error!.Code);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-012")]
    public async Task Preview_OrderNotCancelledOrNonRetailBuyerOrUnpaid_ReturnsConflict()
    {
        var f1 = new RejectedOrderRefundFixture();
        f1.SellerOrder.Status = "Pending";
        Assert.Equal("CONFLICT", (await f1.Service.PreviewAsync(301, 801, default)).Error!.Code);

        var f2 = new RejectedOrderRefundFixture();
        f2.Order.BuyerUser.Role = "WholesaleBuyer";
        Assert.Equal("CONFLICT", (await f2.Service.PreviewAsync(301, 801, default)).Error!.Code);

        var f3 = new RejectedOrderRefundFixture();
        f3.Payment.PaidAt = null;
        Assert.Equal("CONFLICT", (await f3.Service.PreviewAsync(301, 801, default)).Error!.Code);
    }

    [Theory, Trait("CaseId", "UT-FIN-09-013")]
    [InlineData(0)]
    [InlineData(-1000)]
    [InlineData(1000.5)]
    [InlineData(150001)]
    public async Task Preview_InvalidOrExcessRefundAmount_ReturnsConflict(decimal amount)
    {
        var f = new RejectedOrderRefundFixture();
        f.Refund.Amount = amount;
        var result = await f.Service.PreviewAsync(301, 801, default);
        Assert.False(result.IsSuccess);
        Assert.Equal("CONFLICT", result.Error!.Code);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-014")]
    public async Task Preview_ReceivingAccountInvalidUnverifiedInactiveOrMismatched_ReturnsConflict()
    {
        var f1 = new RejectedOrderRefundFixture();
        f1.Repo.Context = f1.Repo.Context! with { Account = null };
        Assert.Equal("CONFLICT", (await f1.Service.PreviewAsync(301, 801, default)).Error!.Code);

        var f2 = new RejectedOrderRefundFixture();
        f2.Account.VerificationStatus = "Pending";
        Assert.Equal("CONFLICT", (await f2.Service.PreviewAsync(301, 801, default)).Error!.Code);

        var f3 = new RejectedOrderRefundFixture();
        f3.Account.BankCodeNavigation.IsActive = false;
        Assert.Equal("CONFLICT", (await f3.Service.PreviewAsync(301, 801, default)).Error!.Code);

        var f4 = new RejectedOrderRefundFixture();
        f4.Account.AccountNumber = "9999999999";
        Assert.Equal("CONFLICT", (await f4.Service.PreviewAsync(301, 801, default)).Error!.Code);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-015")]
    public async Task Preview_PayoutHoldBoundary_ExactNowAllowedOneSecondFutureBlocked()
    {
        var f = new RejectedOrderRefundFixture();
        f.Account.PayoutHoldUntil = f.Clock.Now.UtcDateTime;
        Assert.True((await f.Service.PreviewAsync(301, 801, default)).IsSuccess);

        f.Account.PayoutHoldUntil = f.Clock.Now.AddSeconds(1).UtcDateTime;
        Assert.Equal("CONFLICT", (await f.Service.PreviewAsync(301, 801, default)).Error!.Code);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-016")]
    public async Task Preview_MultiFarmOrderWithoutAllocation_ReturnsConflict()
    {
        var f = new RejectedOrderRefundFixture();
        f.Order.SellerOrders.Add(new SellerOrder { SellerOrderId = 502, OrderId = 10, Status = "Confirmed" });
        var result = await f.Service.PreviewAsync(301, 801, default);
        Assert.False(result.IsSuccess);
        Assert.Equal("CONFLICT", result.Error!.Code);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-017")]
    public async Task OpenQr_ValidConfirmation_TransitionsToProcessingAndRecordsAudit()
    {
        var f = new RejectedOrderRefundFixture();
        var reqKey = Guid.NewGuid().ToString("D");
        var result = await f.Service.OpenQrAsync(301, 801, new OpenRejectedOrderRefundRequest(f.CurrentVersion, reqKey), default);
        Assert.True(result.IsSuccess);
        Assert.Equal("Processing", result.Value!.Status);
        Assert.Equal("Processing", f.Refund.RefundStatus);
        Assert.Equal(301, f.Refund.ProcessedByUserId);
        Assert.Single(f.Repo.Audits, a => a.Action == "QrOpened" && a.TraceId == reqKey);
        Assert.Empty(f.Repo.Notifications);
    }

    [Theory, Trait("CaseId", "UT-FIN-09-018")]
    [InlineData("not-a-guid", 64)]
    [InlineData("11111111-1111-1111-1111-111111111111", 63)]
    [InlineData("11111111-1111-1111-1111-111111111111", 65)]
    public async Task OpenQr_InvalidRequestKeyOrVersionLength_ReturnsValidation(string key, int versionLen)
    {
        var f = new RejectedOrderRefundFixture();
        var result = await f.Service.OpenQrAsync(301, 801, new OpenRejectedOrderRefundRequest(new string('A', versionLen), key), default);
        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION", result.Error!.Code);
        Assert.Equal("Pending", f.Refund.RefundStatus);
        Assert.Equal(0, f.Repo.AtomicCalls);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-019")]
    public async Task OpenQr_StaleVersion_ReturnsConflictWithoutChangingState()
    {
        var f = new RejectedOrderRefundFixture();
        var result = await f.Service.OpenQrAsync(301, 801,
            new OpenRejectedOrderRefundRequest(new string('F', 64), Guid.NewGuid().ToString("D")), default);
        Assert.False(result.IsSuccess);
        Assert.Equal("CONFLICT", result.Error!.Code);
        Assert.Equal("Pending", f.Refund.RefundStatus);
        Assert.Empty(f.Repo.Audits);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-020")]
    public async Task OpenQr_SecondCallAfterProcessing_IsRejected()
    {
        var f = new RejectedOrderRefundFixture();
        await f.OpenQrSuccessfullyAsync();
        var second = await f.Service.OpenQrAsync(301, 801,
            new OpenRejectedOrderRefundRequest(f.CurrentVersion, Guid.NewGuid().ToString("D")), default);
        Assert.False(second.IsSuccess);
        Assert.Equal("CONFLICT", second.Error!.Code);
        Assert.Single(f.Repo.Audits, a => a.Action == "QrOpened");
    }

    [Fact, Trait("CaseId", "UT-FIN-09-021")]
    public async Task Reconcile_ExactMatchingDebit_CompletesRefundReversesFeesAndNotifiesCustomer()
    {
        var f = new RejectedOrderRefundFixture();
        var plan = await f.OpenQrSuccessfullyAsync();
        f.Gateway.Lookup = new([f.MatchingTransfer(plan)], true, null);

        var result = await f.Service.ReconcileAsync(301, 801, default);
        Assert.True(result.IsSuccess);
        Assert.Equal("Succeeded", result.Value!.Status);
        Assert.Equal(RefundTransaction.StatusCompleted, f.Refund.RefundStatus);
        Assert.Equal("FT26300123456", f.Refund.GatewayReference);
        Assert.Equal("Cancelled", f.SellerOrder.Status);
        Assert.Equal("Refunded", f.Payment.PaymentStatus);
        Assert.Contains(501, f.Fees.ReversedCancelledSellerOrderIds);
        Assert.Single(f.Repo.Audits, a => a.Action == "SePayVerified");
        Assert.Equal(2, f.Repo.Notifications.Count);
        Assert.Single(f.Repo.Notifications, n => n.DeliveryChannel == "InApp" && n.DeliveryStatus == "Sent");
        Assert.Single(f.Repo.Notifications, n => n.DeliveryChannel == "Email" && n.DeliveryStatus == "Pending");
    }

    [Fact, Trait("CaseId", "UT-FIN-09-022")]
    public async Task Reconcile_AlreadySucceeded_IsIdempotentWithoutDuplicateEffects()
    {
        var f = new RejectedOrderRefundFixture();
        var plan = await f.OpenQrSuccessfullyAsync();
        f.Gateway.Lookup = new([f.MatchingTransfer(plan)], true, null);
        Assert.True((await f.Service.ReconcileAsync(301, 801, default)).IsSuccess);

        var again = await f.Service.ReconcileAsync(301, 801, default);
        Assert.True(again.IsSuccess);
        Assert.Equal("Succeeded", again.Value!.Status);
        Assert.Single(f.Repo.Audits, a => a.Action == "SePayVerified");
        Assert.Equal(2, f.Repo.Notifications.Count);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-023")]
    public async Task Reconcile_BeforeQrOpened_ReturnsConflict()
    {
        var f = new RejectedOrderRefundFixture();
        var result = await f.Service.ReconcileAsync(301, 801, default);
        Assert.False(result.IsSuccess);
        Assert.Equal("CONFLICT", result.Error!.Code);
        Assert.Equal("Pending", f.Refund.RefundStatus);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-024")]
    public async Task Reconcile_GatewayLookupUnavailableOrNoDebitYet_KeepsProcessing()
    {
        var f = new RejectedOrderRefundFixture();
        await f.OpenQrSuccessfullyAsync();

        f.Gateway.Lookup = new([], false, "SePay timeout");
        var unavailable = await f.Service.ReconcileAsync(301, 801, default);
        Assert.True(unavailable.IsSuccess);
        Assert.Equal("Processing", unavailable.Value!.Status);
        Assert.Equal(RejectedOrderRefundMessages.Pending, unavailable.Value.Message);

        f.Gateway.Lookup = new([], true, null);
        var noDebit = await f.Service.ReconcileAsync(301, 801, default);
        Assert.True(noDebit.IsSuccess);
        Assert.Equal("Processing", noDebit.Value!.Status);
        Assert.Empty(f.Repo.Notifications);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-025")]
    public async Task Reconcile_ConflictingAmountOrWrongBankOrMultipleDebits_MarksNeedsReconciliation()
    {
        var f1 = new RejectedOrderRefundFixture();
        var plan1 = await f1.OpenQrSuccessfullyAsync();
        f1.Gateway.Lookup = new([f1.MatchingTransfer(plan1, amount: 149000)], true, null);
        var wrongAmt = await f1.Service.ReconcileAsync(301, 801, default);
        Assert.True(wrongAmt.IsSuccess);
        Assert.Equal("NeedsReconciliation", wrongAmt.Value!.Status);

        var f2 = new RejectedOrderRefundFixture();
        var plan2 = await f2.OpenQrSuccessfullyAsync();
        f2.Gateway.Lookup = new([f2.MatchingTransfer(plan2, eventId: "E1"), f2.MatchingTransfer(plan2, eventId: "E2")], true, null);
        var multi = await f2.Service.ReconcileAsync(301, 801, default);
        Assert.True(multi.IsSuccess);
        Assert.Equal("NeedsReconciliation", multi.Value!.Status);
        Assert.Empty(f2.Repo.Notifications);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-026")]
    public async Task Reconcile_ReusedBankEvent_MarksNeedsReconciliation()
    {
        var f = new RejectedOrderRefundFixture();
        var plan = await f.OpenQrSuccessfullyAsync();
        f.Repo.EventAlreadyUsed = true;
        f.Gateway.Lookup = new([f.MatchingTransfer(plan)], true, null);

        var result = await f.Service.ReconcileAsync(301, 801, default);
        Assert.True(result.IsSuccess);
        Assert.Equal("NeedsReconciliation", result.Value!.Status);
        Assert.Empty(f.Repo.Notifications);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-027")]
    public async Task Reconcile_TimestampBoundaries_WithinWindowSucceedsOutsideMarksNeedsReconciliation()
    {
        var fValid = new RejectedOrderRefundFixture();
        var p1 = await fValid.OpenQrSuccessfullyAsync();
        fValid.Gateway.Lookup = new([fValid.MatchingTransfer(p1, at: p1.OpenedAt.AddSeconds(-5))], true, null);
        Assert.Equal("Succeeded", (await fValid.Service.ReconcileAsync(301, 801, default)).Value!.Status);

        var fTooEarly = new RejectedOrderRefundFixture();
        var p2 = await fTooEarly.OpenQrSuccessfullyAsync();
        fTooEarly.Gateway.Lookup = new([fTooEarly.MatchingTransfer(p2, at: p2.OpenedAt.AddSeconds(-6))], true, null);
        Assert.Equal("NeedsReconciliation", (await fTooEarly.Service.ReconcileAsync(301, 801, default)).Value!.Status);

        var fTooLate = new RejectedOrderRefundFixture();
        var p3 = await fTooLate.OpenQrSuccessfullyAsync();
        fTooLate.Gateway.Lookup = new([fTooLate.MatchingTransfer(p3, at: fTooLate.Clock.Now.UtcDateTime.AddMinutes(2).AddSeconds(1))], true, null);
        Assert.Equal("NeedsReconciliation", (await fTooLate.Service.ReconcileAsync(301, 801, default)).Value!.Status);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-028")]
    public async Task Reconcile_RecoveryFromNeedsReconciliation_CompletesWhenExactMatchAppears()
    {
        var f = new RejectedOrderRefundFixture();
        var plan = await f.OpenQrSuccessfullyAsync();
        f.Gateway.Lookup = new([f.MatchingTransfer(plan, bankRef: "")], true, null);
        Assert.Equal("NeedsReconciliation", (await f.Service.ReconcileAsync(301, 801, default)).Value!.Status);

        f.Gateway.Lookup = new([f.MatchingTransfer(plan, bankRef: "FT26300999999")], true, null);
        var recovered = await f.Service.ReconcileAsync(301, 801, default);
        Assert.True(recovered.IsSuccess);
        Assert.Equal("Succeeded", recovered.Value!.Status);
        Assert.Equal("FT26300999999", f.Refund.GatewayReference);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-029")]
    public async Task Reconcile_MutatedRefundAfterQrOpened_MarksNeedsReconciliation()
    {
        var f = new RejectedOrderRefundFixture();
        var plan = await f.OpenQrSuccessfullyAsync();
        f.Refund.Amount = 140000;
        f.Gateway.Lookup = new([f.MatchingTransfer(plan)], true, null);

        var result = await f.Service.ReconcileAsync(301, 801, default);
        Assert.True(result.IsSuccess);
        Assert.Equal("NeedsReconciliation", result.Value!.Status);
        Assert.Empty(f.Repo.Notifications);
    }

    [Fact, Trait("CaseId", "UT-FIN-09-030")]
    public async Task Reconcile_UnauthorizedOrPersistenceFailure_BehavesSafely()
    {
        var f = new RejectedOrderRefundFixture();
        var plan = await f.OpenQrSuccessfullyAsync();
        f.Repo.IsAuthorized = false;
        Assert.Equal("FORBIDDEN", (await f.Service.ReconcileAsync(999, 801, default)).Error!.Code);

        f.Repo.IsAuthorized = true;
        f.Repo.ThrowOnAtomic = true;
        f.Gateway.Lookup = new([f.MatchingTransfer(plan)], true, null);
        await Assert.ThrowsAsync<IOException>(() => f.Service.ReconcileAsync(301, 801, default));
        Assert.Equal("Processing", f.Refund.RefundStatus);
    }
}
