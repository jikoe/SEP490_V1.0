using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using FluentValidation;

namespace AgriSustain.Application.Services.Complaints;

/// <summary>
/// UC-CS-01 Resolve Complaint (SRS UC-ADM-06). A refund decision creates a manual refund request
/// for Finance (UC-FIN-03) and moves the seller order to RefundPending; a rejection keeps it as it
/// is. Stock is never restored here: returned goods are restocked only after inspection.
/// </summary>
public sealed class ComplaintResolutionService(
    IComplaintRepository repository,
    IActorContext actor,
    IValidator<ResolveComplaintRequest> resolveValidator,
    IValidator<RequestComplaintInfoRequest> requestInfoValidator,
    TimeProvider timeProvider) : IComplaintResolutionService, IComplaintEvidenceTimeoutProcessor
{
    /// <summary>
    /// Seller-order status set when a refund is approved and waits for Finance.
    /// </summary>
    public const string RefundPendingStatus = SellerOrder.StatusRefundPending;
    /// <summary>
    /// BR-CS-02: time a party has to answer an information request. The deadline is stored on each request
    /// (DueAt = request time + this window), so changing the value only affects requests created afterwards.
    /// </summary>
    public static readonly TimeSpan EvidenceResponseWindow = TimeSpan.FromHours(48);
    private const int MaximumPageSize = 100;
    private static readonly string[] s_financeRoles = ["FinanceStaff", "Finance Staff"];

    /// <summary>
    /// Screen (1)–(9). Trims the filters, turns blanks into "no filter", forces Page ≥ 1 and PageSize into 1..100,
    /// then asks the repository for the page and for the reason list. Read-only.
    /// </summary>
    /// <param name="query">Raw filters and paging from the query string.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Always success: items, total, effective page, page size and reasons.</returns>
    public async Task<Result<ComplaintListDto>> SearchAsync(
        ComplaintQuery query,
        CancellationToken cancellationToken)
    {
        var normalized = query with
        {
            Status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim(),
            Reason = string.IsNullOrWhiteSpace(query.Reason) ? null : query.Reason.Trim(),
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            Page = Math.Max(query.Page, 1),
            PageSize = Math.Clamp(query.PageSize, 1, MaximumPageSize)
        };
        var (total, items) = await repository.SearchAsync(normalized, Now(), cancellationToken);
        var reasons = await repository.GetReasonsAsync(cancellationToken);
        return Result<ComplaintListDto>.SuccessResult(new ComplaintListDto(
            items, total, normalized.Page, normalized.PageSize, reasons));
    }

    /// <summary>
    /// Screen (10)–(14). Loads the read model and fills MaxRefundAmount (BR-CS-04):
    /// min(seller order amount − refunds already reserved for it, captured payment − refunds reserved on it), never below 0.
    /// </summary>
    /// <param name="ticketId">Complaint id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The detail with the refundable ceiling, or 404 COMPLAINT_NOT_FOUND.</returns>
    public async Task<Result<ComplaintDetailDto>> GetDetailAsync(
        long ticketId,
        CancellationToken cancellationToken)
    {
        var detail = await repository.GetDetailAsync(ticketId, Now(), cancellationToken);
        if (detail is null)
        {
            return Result<ComplaintDetailDto>.FailureResult(s_notFound);
        }

        // BR-CS-04: at most what is still refundable for this seller order, and never more than
        // what remains on the captured payment.
        decimal sellerOrderRemaining = (detail.SellerOrderAmount ?? 0) - detail.SellerOrderReservedRefundAmount;
        decimal paymentRemaining = detail.PaidAmount - detail.ReservedRefundAmount;
        return Result<ComplaintDetailDto>.SuccessResult(detail with
        {
            MaxRefundAmount = decimal.Max(0, decimal.Min(sellerOrderRemaining, paymentRemaining))
        });
    }

    /// <summary>
    /// Button (19). Steps: 1) validate the request (MSG-CS-03 / MSG-CS-04); 2) load the tracked complaint;
    /// 3) stop if it is missing, already decided or not a retail complaint; 4) apply the decision
    /// (<see cref="ApplyDecisionAsync"/>); 5) save everything in one SaveChanges, so the complaint, refund request,
    /// order status, notifications and audit row are committed together or not at all.
    /// </summary>
    /// <param name="ticketId">Complaint id.</param>
    /// <param name="request">Resolution, fault party, reason and optional items.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>TicketStatus Rejected or RefundPending with the refund code; otherwise the error to show.</returns>
    public async Task<Result<ComplaintActionResponse>> ResolveAsync(
        long ticketId,
        ResolveComplaintRequest request,
        CancellationToken cancellationToken)
    {
        var error = await RequestValidation.CheckAsync(resolveValidator, request, cancellationToken);
        if (error is not null)
        {
            return Result<ComplaintActionResponse>.FailureResult(error);
        }

        var ticket = await repository.GetForDecisionAsync(ticketId, cancellationToken);
        if (DecisionBlocker(ticket) is { } blocked)
        {
            return Result<ComplaintActionResponse>.FailureResult(blocked);
        }

        var applied = await ApplyDecisionAsync(ticket!, request, request.Reason.Trim(), actor.UserId,
            actor.UserId, "ResolveComplaint", Now(), cancellationToken);
        if (!applied.IsSuccess)
        {
            return Result<ComplaintActionResponse>.FailureResult(applied.Error!);
        }

        await repository.SaveChangesAsync(cancellationToken);
        var refund = applied.Value;
        return Result<ComplaintActionResponse>.SuccessResult(new ComplaintActionResponse(
            ticket.DisputeTicketId, ticket.TicketStatus, refund?.RefundCode,
            refund is null
                ? "Đã từ chối khiếu nại và thông báo cho các bên liên quan."
                : "Đã ghi nhận quyết định hoàn tiền và chuyển yêu cầu sang bộ phận Tài chính."));
    }

    /// <summary>
    /// Button (20). Steps: 1) validate party and instructions; 2) load the complaint and apply the same blockers as a
    /// decision; 3) map the party to its user (buyer of the order, or the farm owner); 4) refuse when that user still
    /// has an unanswered, unexpired request (MSG-CS-02); 5) create the request with DueAt = now + 48 h and move the
    /// complaint to WaitingCustomerInfo; 6) notify the party in-app and by email with the deadline in Vietnam time;
    /// 7) write the audit row and save.
    /// </summary>
    /// <param name="ticketId">Complaint id.</param>
    /// <param name="request">Party (Buyer / Seller) and instructions.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Success message with the 48-hour notice, or INFO_REQUEST_PENDING / validation / not-found errors.</returns>
    public async Task<Result<ComplaintActionResponse>> RequestInformationAsync(
        long ticketId,
        RequestComplaintInfoRequest request,
        CancellationToken cancellationToken)
    {
        var error = await RequestValidation.CheckAsync(requestInfoValidator, request, cancellationToken);
        if (error is not null)
        {
            return Result<ComplaintActionResponse>.FailureResult(error);
        }

        var ticket = await repository.GetForDecisionAsync(ticketId, cancellationToken);
        if (DecisionBlocker(ticket) is { } blocked)
        {
            return Result<ComplaintActionResponse>.FailureResult(blocked);
        }

        var sellerOrder = ticket!.SellerOrder!;
        var now = Now();
        long partyUserId = request.Party == ComplaintParties.Buyer
            ? sellerOrder.Order.BuyerUserId
            : sellerOrder.FarmerProfile.UserId;
        if (ticket.HasOpenRequestFor(partyUserId, now))
        {
            return Result<ComplaintActionResponse>.FailureResult(new Error(
                "INFO_REQUEST_PENDING",
                "Bên này đang có yêu cầu bổ sung thông tin chưa phản hồi và chưa quá hạn.",
                "Conflict"));
        }

        string previousStatus = ticket.TicketStatus;
        string instructions = request.Instructions.Trim();
        var evidenceRequest = ticket.RequestInformation(
            partyUserId, instructions, actor.UserId, now, EvidenceResponseWindow);
        AddNotice(partyUserId, "Yêu cầu bổ sung thông tin khiếu nại",
            $"Khiếu nại #{ticket.DisputeTicketId} của đơn {sellerOrder.SellerOrderCode} cần bổ sung "
                // Shown to the buyer or farmer in Vietnam time (UTC+7), like the CS-01 screen.
                + $"thông tin trong 48 giờ (hạn {evidenceRequest.DueAt?.AddHours(7):dd/MM/yyyy HH:mm}): {instructions}",
            ticket.DisputeTicketId);
        AddAudit(ticket, "RequestComplaintInformation", previousStatus,
            new { ticket.TicketStatus, request.Party, evidenceRequest.DueAt }, instructions,
            actor.UserId, now);
        await repository.SaveChangesAsync(cancellationToken);

        return Result<ComplaintActionResponse>.SuccessResult(new ComplaintActionResponse(
            ticket.DisputeTicketId, ticket.TicketStatus, null,
            "Đã gửi yêu cầu bổ sung thông tin. Bên liên quan có 48 giờ để phản hồi."));
    }

    /// <summary>
    /// Pass-through to the repository; the controller turns the URL into a file on disk.
    /// </summary>
    /// <param name="ticketId">Complaint id.</param>
    /// <param name="evidenceId">Evidence id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Stored URL or null.</returns>
    public Task<string?> GetEvidenceFileUrlAsync(
        long ticketId,
        long evidenceId,
        CancellationToken cancellationToken) =>
        repository.GetEvidenceFileUrlAsync(ticketId, evidenceId, cancellationToken);

    /// <summary>
    /// JOB-11 (run by <c>ComplaintEvidenceTimeoutWorker</c> every 5 minutes). For up to 25 complaints whose every open
    /// request is overdue: mark those requests Expired and apply the automatic decision of BR-CS-02
    /// (only buyer silent → reject; only farmer silent → full refund; both silent → reject).
    /// When the refund cannot be created (for example no captured payment) Customer Support is notified to decide
    /// manually; the requests stay Expired so the complaint is not picked up again. Each complaint is saved on its own,
    /// so one failure does not undo the others.
    /// </summary>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Number of complaints decided automatically.</returns>
    public async Task<int> ResolveOverdueAsync(CancellationToken cancellationToken)
    {
        var now = Now();
        var ticketIds = await repository.GetOverdueTicketIdsAsync(
            now, EvidenceResponseWindow, OverdueBatchSize, cancellationToken);
        int resolved = 0;
        foreach (long ticketId in ticketIds)
        {
            var ticket = await repository.GetForDecisionAsync(ticketId, cancellationToken);
            if (ticket is null || DecisionBlocker(ticket) is not null
                || OverdueDecision(ticket, now) is not { } decision)
            {
                continue;
            }

            foreach (var request in decision.OverdueRequests)
            {
                request.RequestStatus = DisputeTicket.EvidenceRequestExpired;
            }

            var applied = await ApplyDecisionAsync(ticket, decision.Request, decision.Request.Reason,
                decision.StaffUserId, null, "AutoResolveComplaint", now, cancellationToken);
            if (applied.IsSuccess)
            {
                resolved++;
            }
            else
            {
                // Nothing can be refunded automatically (e.g. no captured payment): hand back to staff.
                // The expired requests are no longer picked up, so this is reported only once.
                await NotifySupportAsync(ticket,
                    $"Không thể tự động xử lý khiếu nại #{ticket.DisputeTicketId} quá hạn bổ sung thông tin: "
                        + $"{applied.Error!.Message} Vui lòng xử lý thủ công.", cancellationToken);
            }

            await repository.SaveChangesAsync(cancellationToken);
        }

        return resolved;
    }

    /// <summary>
    /// Complaints handled per run of the timeout job.
    /// </summary>
    private const int OverdueBatchSize = 25;
    /// <summary>
    /// Prefix of the reason recorded on an automatic decision.
    /// </summary>
    private const string EvidenceOverdue = "Evidence Overdue";
    private static readonly string[] s_supportRoles = ["CustomerSupport", "Customer Support"];

    private static readonly Error s_notFound = new(
        "COMPLAINT_NOT_FOUND", "Không tìm thấy khiếu nại.", "NotFound");

    /// <summary>
    /// The automatic decision for one complaint.
    /// </summary>
    /// <param name="Request">Decision to apply, built as if staff had entered it.</param>
    /// <param name="StaffUserId">Staff member who sent the latest overdue request; recorded as the assigned staff.</param>
    /// <param name="OverdueRequests">Requests to mark Expired.</param>
    private sealed record OverdueOutcome(
        ResolveComplaintRequest Request,
        long StaffUserId,
        IReadOnlyList<DisputeEvidenceRequest> OverdueRequests);

    /// <summary>
    /// Works out the BR-CS-02 outcome. Returns null (wait) unless at least one request is open and all open requests
    /// are overdue, so a party that still has time is never decided against.
    /// </summary>
    /// <param name="ticket">Complaint with its evidence requests and seller order loaded.</param>
    /// <param name="now">Current UTC time.</param>
    /// <returns>Reject (buyer or both silent) or Full Refund (farmer silent), or null when nothing should be decided yet.</returns>
    private static OverdueOutcome? OverdueDecision(DisputeTicket ticket, DateTime now)
    {
        var open = ticket.DisputeEvidenceRequests
            .Where(request => request.RequestStatus == DisputeTicket.EvidenceRequestPending
                && request.RespondedAt == null)
            .ToList();
        bool IsOverdue(DisputeEvidenceRequest request) =>
            (request.DueAt ?? request.RequestedAt.Add(EvidenceResponseWindow)) < now;
        var overdue = open.Where(IsOverdue).ToList();
        // Wait until every party that was asked has answered or run out of time.
        if (overdue.Count == 0 || overdue.Count != open.Count)
        {
            return null;
        }

        var sellerOrder = ticket.SellerOrder!;
        bool buyerOverdue = overdue.Any(request =>
            request.RequestedPartyUserId == sellerOrder.Order.BuyerUserId);
        bool sellerOverdue = overdue.Any(request =>
            request.RequestedPartyUserId == sellerOrder.FarmerProfile.UserId);
        long staffUserId = overdue.OrderByDescending(request => request.RequestedAt).First().RequestedByUserId;
        var request = (buyerOverdue, sellerOverdue) switch
        {
            (true, true) => new ResolveComplaintRequest(ComplaintResolutions.RejectClaim,
                ComplaintParties.NotDetermined, null,
                $"{EvidenceOverdue}: cả người mua và người bán đều không phản hồi yêu cầu bổ sung thông tin "
                    + "trong 48 giờ; khiếu nại tự động bị từ chối."),
            (true, false) => new ResolveComplaintRequest(ComplaintResolutions.RejectClaim,
                ComplaintParties.Buyer, null,
                $"{EvidenceOverdue}: người mua không phản hồi yêu cầu bổ sung thông tin trong 48 giờ."),
            (false, true) => new ResolveComplaintRequest(ComplaintResolutions.FullRefund,
                ComplaintParties.Seller, null,
                $"{EvidenceOverdue}: người bán không phản hồi yêu cầu bổ sung thông tin trong 48 giờ; "
                    + "hoàn tiền toàn bộ cho người mua."),
            _ => null
        };
        return request is null ? null : new OverdueOutcome(request, staffUserId, overdue);
    }

    /// <summary>
    /// Shared by staff decisions and the timeout job. Reject: record the decision only. Refund: read the buyer's default
    /// bank account, create the refund request (<see cref="CreateRefund"/>), record the decision, move the seller order
    /// to RefundPending, notify Finance and ask the buyer for the receiving account. In both cases the buyer and the
    /// farmer are told the outcome and one audit row is written. Nothing is saved here; the caller saves.
    /// </summary>
    /// <param name="ticket">Tracked complaint.</param>
    /// <param name="request">Decision to apply.</param>
    /// <param name="reason">Trimmed reason text.</param>
    /// <param name="staffUserId">Staff recorded on the complaint and the refund.</param>
    /// <param name="auditUserId">Who performed it; null for the timeout job (System).</param>
    /// <param name="auditAction">ResolveComplaint or AutoResolveComplaint.</param>
    /// <param name="now">Current UTC time.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The created refund, null for a rejection, or the error that stopped the refund.</returns>
    private async Task<Result<RefundTransaction?>> ApplyDecisionAsync(DisputeTicket ticket,
        ResolveComplaintRequest request, string reason, long staffUserId, long? auditUserId,
        string auditAction, DateTime now, CancellationToken cancellationToken)
    {
        var sellerOrder = ticket.SellerOrder!;
        string previousStatus = ticket.TicketStatus;
        RefundTransaction? refund = null;
        if (request.Resolution == ComplaintResolutions.RejectClaim)
        {
            ticket.RecordDecision(
                DisputeTicket.StatusRejected, request.FaultParty, null, reason, staffUserId, now);
        }
        else
        {
            var account = await repository.GetDefaultBankAccountAsync(sellerOrder.Order.BuyerUserId,
                cancellationToken);
            var created = CreateRefund(ticket, sellerOrder, request, reason, staffUserId, account, now);
            if (!created.IsSuccess)
            {
                return Result<RefundTransaction?>.FailureResult(created.Error!);
            }

            refund = created.Value!;
            ticket.RecordDecision(DisputeTicket.StatusRefundPending, request.FaultParty,
                refund.Amount, reason, staffUserId, now);
            MoveToRefundPending(sellerOrder, reason, staffUserId, now);
            await NotifyFinanceAsync(sellerOrder, refund, cancellationToken);
            RequestRefundAccount(ticket, sellerOrder, refund, now);
        }

        NotifyParties(ticket, sellerOrder, DecisionMessage(request.Resolution, refund, reason));
        AddAudit(ticket, auditAction, previousStatus, new
        {
            ticket.TicketStatus, request.Resolution, request.FaultParty,
            RefundAmount = refund?.Amount, refund?.RefundCode, RefundItems = request.Items
        }, reason, auditUserId, now);
        return Result<RefundTransaction?>.SuccessResult(refund);
    }

    /// <summary>
    /// Tells every active Customer Support user that an overdue complaint needs a manual decision.
    /// </summary>
    /// <param name="ticket">Complaint concerned.</param>
    /// <param name="content">Message text.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    private async Task NotifySupportAsync(DisputeTicket ticket, string content,
        CancellationToken cancellationToken)
    {
        var supportUsers = await repository.GetActiveUserIdsByRolesAsync(s_supportRoles, cancellationToken);
        foreach (long userId in supportUsers)
        {
            AddNotice(userId, "Khiếu nại cần xử lý thủ công", content,
                ticket.DisputeTicketId);
        }
    }

    /// <summary>
    /// Common guard for a decision or an information request.
    /// </summary>
    /// <param name="ticket">Loaded complaint, or null.</param>
    /// <returns>404 COMPLAINT_NOT_FOUND, 409 COMPLAINT_ALREADY_DECIDED (status no longer Open / UnderReview / WaitingCustomerInfo),</returns>
    private static Error? DecisionBlocker(DisputeTicket? ticket)
    {
        if (ticket is null)
        {
            return s_notFound;
        }

        if (!ticket.IsAwaitingDecision)
        {
            return new Error("COMPLAINT_ALREADY_DECIDED",
                "Khiếu nại đã được xử lý trước đó. Vui lòng tải lại để xem trạng thái mới nhất.",
                "Conflict");
        }

        return ticket.SellerOrder is null
            ? new Error("COMPLAINT_NOT_SUPPORTED",
                "Khiếu nại không gắn với đơn hàng bán lẻ nên không xử lý được tại màn hình này.",
                "Conflict")
            : null;
    }

    /// <summary>
    /// Builds the Pending refund request for Finance. Picks the captured payment with the most refundable money;
    /// limit = min(seller order final amount − refunds reserved for it, payment refundable amount) (BR-CS-04).
    /// Full Refund uses the whole limit; Partial Refund uses <see cref="PartialRefundAmount"/> and must fit the limit
    /// (MSG-CS-04). RequestKey "complaint:{id}" lets the unique index reject a concurrent duplicate. The buyer's default
    /// bank account is copied as the first receiving account (UC-FIN-08 can change it).
    /// </summary>
    /// <param name="ticket">Complaint being decided.</param>
    /// <param name="sellerOrder">Farm part of the order the complaint is about.</param>
    /// <param name="request">Decision (Full or Partial Refund).</param>
    /// <param name="reason">Decision reason; stored cut to 500 characters.</param>
    /// <param name="staffUserId">Staff recorded on the refund.</param>
    /// <param name="account">Buyer's default bank account, or null.</param>
    /// <param name="now">Current UTC time; also part of the RefundCode.</param>
    /// <returns>The refund already added to the unit of work, or NO_REFUNDABLE_PAYMENT (MSG-CS-06) /</returns>
    private Result<RefundTransaction> CreateRefund(DisputeTicket ticket, SellerOrder sellerOrder,
        ResolveComplaintRequest request, string reason, long staffUserId, UserBankAccount? account,
        DateTime now)
    {
        var order = sellerOrder.Order;
        var payment = order.PaymentTransactions
            .Where(candidate => candidate.IsCaptured)
            .OrderByDescending(candidate => candidate.RefundableAmount)
            .FirstOrDefault();
        // BR-CS-04: the cap is what the buyer paid for this seller order (after its share of
        // vouchers and points, including its delivery fee), less refunds already requested or
        // made for it, and never more than what remains on the captured payment.
        decimal limit = payment is null ? 0 : decimal.Min(
            decimal.Max(0, sellerOrder.FinalAmount - sellerOrder.ReservedRefundAmount),
            payment.RefundableAmount);
        if (payment is null || limit <= 0)
        {
            // MSG-CS-06
            return Result<RefundTransaction>.FailureResult(new Error("NO_REFUNDABLE_PAYMENT",
                "Đơn hàng của người bán này không còn khoản thanh toán thành công nào có thể hoàn. "
                    + "Không thể tạo yêu cầu hoàn tiền.", "Conflict"));
        }

        decimal amount;
        if (request.Resolution == ComplaintResolutions.FullRefund)
        {
            // Full refund returns everything still refundable on the seller order.
            amount = limit;
        }
        else
        {
            var calculated = PartialRefundAmount(sellerOrder, request.Items!);
            if (!calculated.IsSuccess)
            {
                return Result<RefundTransaction>.FailureResult(calculated.Error!);
            }

            amount = calculated.Value;
        }

        if (amount > limit)
        {
            // MSG-CS-04
            string message = $"Số tiền hoàn {amount:N0} ₫ vượt quá số tiền còn có thể hoàn cho đơn "
                + $"của người bán này ({limit:N0} ₫).";
            return Result<RefundTransaction>.FailureResult(new Error("REFUND_AMOUNT_EXCEEDED", message,
                Fields: new Dictionary<string, string[]> { ["items"] = [message] }));
        }

        var refund = new RefundTransaction
        {
            PaymentTransaction = payment,
            DisputeTicket = ticket,
            ProcessedByUserId = staffUserId,
            RefundCode = $"RF-{ticket.DisputeTicketId}-{now:yyyyMMddHHmmss}",
            Amount = amount,
            Reason = reason.Length > 500 ? reason[..500] : reason,
            RefundStatus = RefundTransaction.StatusPending,
            // One refund request per complaint; the unique index rejects a concurrent duplicate.
            RequestKey = $"complaint:{ticket.DisputeTicketId}",
            // UC-FIN-08 lets the buyer confirm or change it; until then the saved default is used.
            // Without one, Finance sees "Awaiting customer account" and cannot start the transfer.
            ReceiverBankCode = account?.BankCode,
            ReceiverAccountNumber = account?.AccountNumber,
            ReceiverAccountHolder = account?.AccountHolder,
            ReceiverAccountProvidedAt = account is null ? null : now
        };
        repository.AddRefund(refund);
        return Result<RefundTransaction>.SuccessResult(refund);
    }

    /// <summary>
    /// BR-CS-03: the refund is the paid value of the selected quantities; each line must belong to
    /// the seller order and no quantity may exceed what was delivered.
    /// </summary>
    /// <param name="sellerOrder">Seller order whose lines may be refunded.</param>
    /// <param name="items">Selected lines and quantities.</param>
    /// <returns>Sum of paid value × quantity ÷ delivered quantity per line, or REFUND_ITEMS_INVALID when a line is foreign,</returns>
    private static Result<decimal> PartialRefundAmount(SellerOrder sellerOrder,
        IReadOnlyList<RefundItemRequest> items)
    {
        decimal amount = 0;
        foreach (var selected in items)
        {
            var item = sellerOrder.OrderItems.SingleOrDefault(line => line.OrderItemId == selected.OrderItemId);
            if (item is null || selected.Quantity > item.Quantity)
            {
                // MSG-CS-04
                string message = item is null
                    ? "Sản phẩm được chọn không thuộc đơn hàng của người bán này."
                    : $"Số lượng hoàn của {item.ProductNameSnapshot} không được vượt quá "
                        + $"{item.Quantity:0.###} {item.UnitSnapshot} đã giao.";
                return Result<decimal>.FailureResult(new Error("REFUND_ITEMS_INVALID", message,
                    Fields: new Dictionary<string, string[]> { ["items"] = [message] }));
            }

            amount += sellerOrder.RefundValueOf(item, selected.Quantity);
        }

        if (amount <= 0)
        {
            const string message = "Số tiền hoàn tính từ các sản phẩm được chọn phải lớn hơn 0.";
            return Result<decimal>.FailureResult(new Error("REFUND_ITEMS_INVALID", message,
                Fields: new Dictionary<string, string[]> { ["items"] = [message] }));
        }

        return Result<decimal>.SuccessResult(amount);
    }

    /// <summary>
    /// Sets the seller order to RefundPending, lets the parent order recompute its own refund status and appends
    /// one status-history row with the reason.
    /// </summary>
    /// <param name="sellerOrder">Seller order to update.</param>
    /// <param name="reason">Decision reason.</param>
    /// <param name="staffUserId">Staff recorded in the history.</param>
    /// <param name="now">Current UTC time.</param>
    private static void MoveToRefundPending(SellerOrder sellerOrder, string reason, long staffUserId,
        DateTime now)
    {
        string previous = sellerOrder.Status;
        sellerOrder.Status = RefundPendingStatus;
        sellerOrder.Order.SyncRefundStatus();
        sellerOrder.OrderStatusHistories.Add(new OrderStatusHistory
        {
            ChangedByUserId = staffUserId,
            OldStatus = previous,
            NewStatus = RefundPendingStatus,
            Reason = Truncate($"Khiếu nại được chấp nhận hoàn tiền: {reason}", 500),
            OccurredAt = now
        });
    }

    /// <summary>
    /// Tells every active Finance Staff user, in-app and by email, that a new refund request waits in UC-FIN-03.
    /// </summary>
    /// <param name="sellerOrder">Seller order being refunded.</param>
    /// <param name="refund">New refund request.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    private async Task NotifyFinanceAsync(SellerOrder sellerOrder, RefundTransaction refund,
        CancellationToken cancellationToken)
    {
        var financeUsers = await repository.GetActiveUserIdsByRolesAsync(s_financeRoles, cancellationToken);
        foreach (long userId in financeUsers)
        {
            AddNotice(userId, "Yêu cầu hoàn tiền mới",
                $"Yêu cầu {refund.RefundCode} hoàn {refund.Amount:N0} ₫ cho đơn "
                    + $"{sellerOrder.SellerOrderCode} đang chờ xử lý.",
                refund.DisputeTicket?.DisputeTicketId ?? 0);
        }
    }

    /// <summary>
    /// BR-RF-07 / MSG-RF-07: the buyer is asked by email to provide or confirm the account.
    /// </summary>
    /// <param name="ticket">Complaint the refund belongs to.</param>
    /// <param name="sellerOrder">Seller order; gives the buyer and order code.</param>
    /// <param name="refund">Refund request with the amount and reason.</param>
    /// <param name="now">Current UTC time.</param>
    private void RequestRefundAccount(DisputeTicket ticket, SellerOrder sellerOrder, RefundTransaction refund,
        DateTime now)
    {
        repository.AddNotification(new Notification
        {
            UserId = sellerOrder.Order.BuyerUserId,
            Title = $"Khoản hoàn tiền cho đơn {sellerOrder.Order.OrderCode} đang chờ chuyển",
            Content = $"AgriSustain sẽ hoàn {refund.Amount:N0} ₫. Lý do: {refund.Reason}. Vui lòng đăng nhập "
                + "để cung cấp hoặc xác nhận tài khoản ngân hàng nhận tiền hoàn.",
            NotificationType = "RefundEmail",
            IsRead = false,
            SentAt = now,
            DeliveryChannel = "Email",
            DeliveryStatus = "Pending",
            RelatedRecordType = "DisputeTicket",
            RelatedRecordId = ticket.DisputeTicketId,
            PayloadJson = $"complaint:{ticket.DisputeTicketId}:refund-requested"
        });
    }

    /// <summary>
    /// Sends the outcome to the buyer and the farmer (once each), in-app and by email.
    /// </summary>
    /// <param name="ticket">Decided complaint.</param>
    /// <param name="sellerOrder">Gives both parties and the order code.</param>
    /// <param name="content">Outcome sentence from <see cref="DecisionMessage"/>.</param>
    private void NotifyParties(DisputeTicket ticket, SellerOrder sellerOrder, string content)
    {
        foreach (long userId in new[] { sellerOrder.Order.BuyerUserId, sellerOrder.FarmerProfile.UserId }
            .Distinct())
        {
            AddNotice(userId, "Kết quả xử lý khiếu nại",
                $"Khiếu nại #{ticket.DisputeTicketId} của đơn {sellerOrder.SellerOrderCode}: {content}",
                ticket.DisputeTicketId);
        }
    }

    /// <summary>
    /// Vietnamese outcome sentence for the notification.
    /// </summary>
    /// <param name="resolution">FullRefund, PartialRefund or RejectClaim.</param>
    /// <param name="refund">Refund request; required for the two refund outcomes.</param>
    /// <param name="reason">Decision reason.</param>
    /// <returns>The sentence, including the refund amount when there is one.</returns>
    private static string DecisionMessage(string resolution, RefundTransaction? refund, string reason) =>
        resolution switch
        {
            ComplaintResolutions.FullRefund =>
                $"chấp nhận hoàn tiền toàn bộ {refund!.Amount:N0} ₫, đang chờ bộ phận Tài chính chuyển khoản. Lý do: {reason}",
            ComplaintResolutions.PartialRefund =>
                $"chấp nhận hoàn tiền một phần {refund!.Amount:N0} ₫, đang chờ bộ phận Tài chính chuyển khoản. Lý do: {reason}",
            _ => $"khiếu nại bị từ chối. Lý do: {reason}"
        };

    /// <summary>Email copy of a complaint notice, sent by the Gmail Notification Service worker.</summary>
    public const string EmailNotificationType = "ComplaintEmail";

    /// <summary>
    /// UC-CS-01 Step 9 / alternative flows: parties, Customer Support and Finance Staff are notified
    /// in-app and by email through the Gmail Notification Service. Queues two Notification rows per recipient.
    /// </summary>
    /// <param name="userId">Recipient.</param>
    /// <param name="title">Notification title.</param>
    /// <param name="content">Notification body.</param>
    /// <param name="ticketId">Related complaint id.</param>
    private void AddNotice(long userId, string title, string content, long ticketId)
    {
        repository.AddNotification(Notice(userId, title, content, ticketId, "InApp"));
        repository.AddNotification(Notice(userId, title, content, ticketId, "Email"));
    }

    /// <summary>
    /// Builds one Notification row. In-app rows are Sent at once; email rows are Pending for the notification
    /// worker and carry a unique PayloadJson key so a retry never sends the same email twice.
    /// </summary>
    /// <param name="userId">Recipient.</param>
    /// <param name="title">Title.</param>
    /// <param name="content">Body.</param>
    /// <param name="ticketId">Related complaint id.</param>
    /// <param name="channel">"InApp" or "Email".</param>
    /// <returns>The row, not yet added to the unit of work.</returns>
    private Notification Notice(long userId, string title, string content, long ticketId, string channel) => new()
    {
        UserId = userId,
        Title = title,
        Content = content,
        NotificationType = channel == "Email" ? EmailNotificationType : "Complaint",
        IsRead = false,
        SentAt = Now(),
        DeliveryChannel = channel,
        DeliveryStatus = channel == "Email" ? "Pending" : "Sent",
        RelatedRecordType = "DisputeTicket",
        RelatedRecordId = ticketId,
        PayloadJson = channel == "Email" ? $"complaint:{ticketId}:{Guid.NewGuid():N}" : null
    };

    /// <summary>
    /// Queues the audit row of a complaint action (BR-CS: every decision and request is traceable).
    /// </summary>
    /// <param name="ticket">Complaint acted on.</param>
    /// <param name="action">ResolveComplaint, AutoResolveComplaint or RequestComplaintInformation.</param>
    /// <param name="previousStatus">Status before the action (OldValue).</param>
    /// <param name="newValue">Object serialized to JSON as NewValue.</param>
    /// <param name="reason">Reason or instructions; cut to 500 characters.</param>
    /// <param name="performedByUserId">Acting user, or null for the system job.</param>
    /// <param name="now">Current UTC time.</param>
    private void AddAudit(DisputeTicket ticket, string action, string previousStatus, object newValue,
        string reason, long? performedByUserId, DateTime now)
    {
        repository.AddAudit(new AuditLog
        {
            PerformedByUserId = performedByUserId,
            EntityType = "DisputeTicket",
            EntityId = ticket.DisputeTicketId,
            Action = action,
            OldValue = previousStatus,
            NewValue = JsonSerializer.Serialize(newValue),
            Reason = Truncate(reason, 500),
            TraceId = actor.TraceId,
            OccurredAt = now
        });
    }

    /// <summary>
    /// Current UTC time from the injected TimeProvider, so tests can control the clock.
    /// </summary>
    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Cuts text to the column length.
    /// </summary>
    /// <param name="value">Text.</param>
    /// <param name="length">Maximum characters.</param>
    /// <returns>The text, shortened when longer than <paramref name="length"/>.</returns>
    private static string Truncate(string value, int length) =>
        value.Length > length ? value[..length] : value;
}
