using System.Text.Json;
using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class RefundRepository : IRefundRepository
{
    /// <summary>
    /// RequestKey prefix of a refund created because a Farmer rejected a paid order: "farmer-reject:{sellerOrderId}:…".
    /// </summary>
    private const string FarmerRejectKeyPrefix = "farmer-reject:";

    /// <summary>Refund states that hold part of a payment or seller order (BR-CS-04).</summary>
    private static readonly string[] s_reservingStatuses =
    [
        RefundTransaction.StatusPending, RefundTransaction.StatusProcessing, RefundTransaction.StatusCompleted,
        RefundTransaction.StatusFailed, RefundTransaction.StatusNeedsReconciliation, "Success", "Succeeded"
    ];

    /// <summary>
    /// Date filters are entered in Vietnam time (UTC+7) and converted to UTC for the query.
    /// </summary>
    private static readonly TimeSpan s_vietnamOffset = TimeSpan.FromHours(7);

    private readonly AgriSustainContext _context;

    /// <summary>
    /// Receives the EF Core context of the current request.
    /// </summary>
    /// <param name="context">Database context.</param>
    public RefundRepository(AgriSustainContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Builds the list query: status and method are exact matches; the keyword is contained in the refund code,
    /// payment code, order code or seller-order code, or equals the complaint id; the date range is applied to the
    /// creation time in Vietnam days. No tracking; one COUNT and one page query.
    /// </summary>
    /// <param name="query">Normalized filters and paging.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Total matches and the page.</returns>
    public async Task<(int Total, IReadOnlyList<RefundListItemDto> Items)> SearchAsync(
        RefundQuery query,
        CancellationToken cancellationToken)
    {
        var refunds = _context.RefundTransactions.AsNoTracking();
        if (query.Status is not null)
        {
            refunds = refunds.Where(refund => refund.RefundStatus == query.Status);
        }

        if (query.RefundMethod is not null)
        {
            refunds = refunds.Where(refund => refund.RefundMethod == query.RefundMethod);
        }

        if (query.Search is not null)
        {
            long? ticketId = long.TryParse(query.Search, out long parsed) ? parsed : null;
            refunds = refunds.Where(refund => refund.RefundCode.Contains(query.Search)
                || refund.PaymentTransaction.TransactionCode.Contains(query.Search)
                || (refund.PaymentTransaction.Order != null
                    && refund.PaymentTransaction.Order.OrderCode.Contains(query.Search))
                || (refund.DisputeTicket != null && refund.DisputeTicket.SellerOrder != null
                    && refund.DisputeTicket.SellerOrder.SellerOrderCode.Contains(query.Search))
                || (ticketId != null && refund.DisputeTicketId == ticketId));
        }

        // The creation time is the complaint decision, or the processing time of a Farmer-rejection refund.
        if (query.FromDate is DateOnly fromDate)
        {
            var from = fromDate.ToDateTime(TimeOnly.MinValue) - s_vietnamOffset;
            refunds = refunds.Where(refund =>
                (refund.DisputeTicket != null ? refund.DisputeTicket.DecisionAt : refund.ProcessedAt) >= from);
        }

        if (query.ToDate is DateOnly toDate)
        {
            var before = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue) - s_vietnamOffset;
            refunds = refunds.Where(refund =>
                (refund.DisputeTicket != null ? refund.DisputeTicket.DecisionAt : refund.ProcessedAt) < before);
        }

        int total = await refunds.CountAsync(cancellationToken);
        var items = await refunds
            .OrderBy(refund => refund.RefundStatus == RefundTransaction.StatusProcessing ? 0
                : refund.RefundStatus == RefundTransaction.StatusNeedsReconciliation ? 1
                : refund.RefundStatus == RefundTransaction.StatusPending ? 2
                : refund.RefundStatus == RefundTransaction.StatusFailed ? 3 : 4)
            .ThenBy(refund => refund.RefundTransactionId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(refund => new RefundListItemDto(
                refund.RefundTransactionId,
                refund.RefundCode,
                refund.Amount,
                refund.RefundStatus,
                refund.RefundMethod,
                refund.DisputeTicketId != null ? RefundSources.Complaint : RefundSources.FarmerRejection,
                refund.DisputeTicketId,
                refund.DisputeTicket != null && refund.DisputeTicket.SellerOrder != null
                    ? refund.DisputeTicket.SellerOrder.SellerOrderCode
                    : _context.SellerOrders
                        .Where(sellerOrder => refund.RequestKey.StartsWith(
                            FarmerRejectKeyPrefix + sellerOrder.SellerOrderId.ToString() + ":"))
                        .Select(sellerOrder => sellerOrder.SellerOrderCode)
                        .FirstOrDefault(),
                refund.PaymentTransaction.Order == null ? null : refund.PaymentTransaction.Order.OrderCode,
                refund.PaymentTransaction.Order == null
                    ? null : refund.PaymentTransaction.Order.BuyerUser.FullName,
                refund.ReceiverBankCode != null && refund.ReceiverBankCode != ""
                    && refund.ReceiverAccountNumber != null && refund.ReceiverAccountNumber != "",
                refund.DisputeTicket == null ? refund.ProcessedAt : refund.DisputeTicket.DecisionAt,
                refund.ProcessedAt))
            .ToListAsync(cancellationToken);
        return (total, items);
    }

    /// <summary>
    /// Loads the refund with its payment, complaint or rejected seller order and customer, works out the refund type
    /// (Full / Partial / Cancellation), the remaining balances, the readiness flags (can start transfer, needs customer
    /// account, last reminder) and merges the source events with the history rows.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The detail, or null when the refund does not exist.</returns>
    public async Task<RefundDetailDto?> GetDetailAsync(
        long refundId,
        CancellationToken cancellationToken)
    {
        var refund = await _context.RefundTransactions.AsNoTracking()
            .Include(item => item.PaymentTransaction)
                .ThenInclude(payment => payment.Order)
                    .ThenInclude(order => order!.BuyerUser)
            .Include(item => item.DisputeTicket!)
                .ThenInclude(ticket => ticket.SellerOrder)
            .Include(item => item.DisputeTicket!)
                .ThenInclude(ticket => ticket.AssignedStaffUser)
            .Include(item => item.ProcessedByUser)
            .Include(item => item.ReceiverBankCodeNavigation)
            .AsSplitQuery()
            .SingleOrDefaultAsync(item => item.RefundTransactionId == refundId, cancellationToken);
        if (refund is null)
        {
            return null;
        }

        var history = await _context.AuditLogs.AsNoTracking()
            .Where(log => log.EntityType == "RefundTransaction" && log.EntityId == refundId)
            .OrderBy(log => log.OccurredAt)
            .ThenBy(log => log.AuditLogId)
            .Select(log => new RefundAttemptDto(
                log.Action,
                log.PerformedByUser == null ? null : log.PerformedByUser.FullName,
                log.NewValue,
                log.Reason,
                log.OccurredAt))
            .ToListAsync(cancellationToken);

        var ticket = refund.DisputeTicket;
        history = [.. SourceEvents(refund).Concat(history).OrderBy(entry => entry.OccurredAt)];
        var sellerOrder = ticket?.SellerOrder ?? await FarmerRejectedSellerOrderAsync(refund, cancellationToken);
        decimal? sellerOrderRefundable = null;
        if (sellerOrder is not null)
        {
            // What remains for this refund: the final amount less every other refund holding it.
            string farmerKey = $"{FarmerRejectKeyPrefix}{sellerOrder.SellerOrderId}:";
            decimal heldByOthers = await _context.RefundTransactions.AsNoTracking()
                .Where(other => other.RefundTransactionId != refundId
                    && s_reservingStatuses.Contains(other.RefundStatus)
                    && ((other.DisputeTicket != null && other.DisputeTicket.SellerOrderId == sellerOrder.SellerOrderId)
                        || other.RequestKey.StartsWith(farmerKey)))
                .SumAsync(other => (decimal?)other.Amount, cancellationToken) ?? 0;
            sellerOrderRefundable = Math.Max(0, sellerOrder.FinalAmount - heldByOthers);
        }

        var payment = refund.PaymentTransaction;
        decimal paymentHeldByOthers = await _context.RefundTransactions.AsNoTracking()
            .Where(other => other.PaymentTransactionId == payment.PaymentTransactionId
                && other.RefundTransactionId != refundId
                && s_reservingStatuses.Contains(other.RefundStatus))
            .SumAsync(other => (decimal?)other.Amount, cancellationToken) ?? 0;

        var lastFailureAt = LastAt(history, RefundProcessingActions.TransferFailed);
        bool complaint = refund.DisputeTicketId is not null;
        string refundType = !complaint ? "Cancellation"
            : sellerOrder is not null && refund.Amount >= sellerOrder.FinalAmount ? "Full" : "Partial";
        var buyer = payment.Order?.BuyerUser;
        return new RefundDetailDto(
            refund.RefundTransactionId,
            refund.RefundCode,
            refund.Amount,
            refund.RefundStatus,
            refund.Reason,
            refund.RefundMethod,
            refund.GatewayReference,
            refund.ProcessedAt,
            refund.RefundStatus == RefundTransaction.StatusCompleted ? refund.ProcessedByUser?.FullName : null,
            complaint ? RefundSources.Complaint : RefundSources.FarmerRejection,
            refundType,
            ticket?.DecisionAt ?? refund.ProcessedAt,
            refund.DisputeTicketId,
            ticket?.DisputeReason,
            ticket?.FaultParty,
            ticket?.ResolutionNotes,
            ticket?.DecisionAt,
            ticket?.AssignedStaffUser?.FullName,
            sellerOrder?.SellerOrderCode,
            sellerOrder?.Status,
            sellerOrder?.FinalAmount,
            sellerOrderRefundable,
            payment.Order?.OrderCode,
            payment.Order?.OrderStatus,
            buyer?.FullName,
            buyer?.Email,
            buyer?.PhoneNumber,
            refund.ReceiverBankCode,
            refund.ReceiverBankCodeNavigation?.ShortName ?? refund.ReceiverBankCode,
            refund.ReceiverAccountNumber,
            RefundTransaction.Mask(refund.ReceiverAccountNumber),
            refund.ReceiverAccountHolder,
            refund.ReceiverAccountProvidedAt,
            refund.HasReceivingAccount,
            refund.CanStartTransferAfter(lastFailureAt)
                && ticket?.TicketStatus is null or DisputeTicket.StatusRefundPending,
            payment.TransactionCode,
            payment.GatewayProvider,
            payment.PaymentType,
            payment.Amount,
            Math.Max(0, payment.Amount - paymentHeldByOthers),
            payment.PaymentStatus,
            payment.PaidAt,
            history,
            refund.NeedsAccountFromCustomer(lastFailureAt),
            LastAt(history, RefundProcessingActions.AccountReminder));
    }

    /// <summary>
    /// Latest time of a given action in the history.
    /// </summary>
    /// <param name="history">History rows.</param>
    /// <param name="action">Action name.</param>
    /// <returns>The time, or null when it never happened.</returns>
    private static DateTime? LastAt(IEnumerable<RefundAttemptDto> history, string action) =>
        history.Where(entry => entry.Action == action).Select(entry => (DateTime?)entry.OccurredAt).Max();

    /// <summary>
    /// Time of the latest history row with this action for the refund, or null.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="action">History action name.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Latest OccurredAt, or null.</returns>
    public Task<DateTime?> GetLastActionAtAsync(
        long refundId,
        string action,
        CancellationToken cancellationToken) =>
        _context.AuditLogs.AsNoTracking()
            .Where(log => log.EntityType == "RefundTransaction" && log.EntityId == refundId && log.Action == action)
            .MaxAsync(log => (DateTime?)log.OccurredAt, cancellationToken);

    /// <summary>
    /// BR-RF-04 rows recorded on the complaint rather than the refund: the request created by the
    /// decision and the receiving account snapshotted at that time (actor shown as System).
    /// </summary>
    /// <param name="refund">Refund with its complaint loaded.</param>
    /// <returns>Zero, one or two synthetic history rows.</returns>
    private static IEnumerable<RefundAttemptDto> SourceEvents(RefundTransaction refund)
    {
        var ticket = refund.DisputeTicket;
        if (ticket?.DecisionAt is DateTime decidedAt)
        {
            yield return new RefundAttemptDto("RequestCreated", ticket.AssignedStaffUser?.FullName,
                JsonSerializer.Serialize(new { RefundStatus = RefundTransaction.StatusPending, refund.Amount }),
                ticket.ResolutionNotes, decidedAt);
        }

        if (refund.ReceiverAccountProvidedAt is DateTime providedAt && refund.DisputeTicketId is not null)
        {
            yield return new RefundAttemptDto("AccountProvided", null,
                JsonSerializer.Serialize(new { Account = RefundTransaction.Mask(refund.ReceiverAccountNumber) }),
                null, providedAt);
        }
    }

    /// <summary>
    /// Read-only lookup of the seller order named in a "farmer-reject:" RequestKey, for the detail view.
    /// </summary>
    /// <param name="refund">Refund to inspect.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The seller order, or null for other refund sources.</returns>
    private Task<SellerOrder?> FarmerRejectedSellerOrderAsync(
        RefundTransaction refund,
        CancellationToken cancellationToken)
    {
        if (!refund.RequestKey.StartsWith(FarmerRejectKeyPrefix, StringComparison.Ordinal))
        {
            return Task.FromResult<SellerOrder?>(null);
        }

        string[] parts = refund.RequestKey.Split(':');
        return parts.Length > 1 && long.TryParse(parts[1], out long sellerOrderId)
            ? _context.SellerOrders.AsNoTracking()
                .SingleOrDefaultAsync(sellerOrder => sellerOrder.SellerOrderId == sellerOrderId, cancellationToken)
            : Task.FromResult<SellerOrder?>(null);
    }

    /// <summary>
    /// Loads the tracked graph an action changes: the refund, its payment with all refunds, and for a complaint the
    /// ticket, seller order, farm, sibling complaints, parent order with its seller orders and payments. Split queries
    /// keep the joins small.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Tracked refund, or null.</returns>
    public Task<RefundTransaction?> GetForProcessingAsync(
        long refundId,
        CancellationToken cancellationToken)
    {
        return _context.RefundTransactions
            .Include(refund => refund.PaymentTransaction)
                .ThenInclude(payment => payment.RefundTransactions)
            .Include(refund => refund.DisputeTicket!)
                .ThenInclude(ticket => ticket.SellerOrder!)
                    .ThenInclude(sellerOrder => sellerOrder.FarmerProfile)
            // The seller order's complaints identify which refunds count towards it.
            .Include(refund => refund.DisputeTicket!)
                .ThenInclude(ticket => ticket.SellerOrder!)
                    .ThenInclude(sellerOrder => sellerOrder.DisputeTickets)
            .Include(refund => refund.DisputeTicket!)
                .ThenInclude(ticket => ticket.SellerOrder!)
                    .ThenInclude(sellerOrder => sellerOrder.Order)
                        .ThenInclude(order => order.SellerOrders)
            .Include(refund => refund.DisputeTicket!)
                .ThenInclude(ticket => ticket.SellerOrder!)
                    .ThenInclude(sellerOrder => sellerOrder.Order)
                        .ThenInclude(order => order.PaymentTransactions)
                            .ThenInclude(payment => payment.RefundTransactions)
            .AsSplitQuery()
            .SingleOrDefaultAsync(refund => refund.RefundTransactionId == refundId, cancellationToken);
    }

    /// <summary>
    /// Tracked seller order (with order and farm) of a Farmer-rejection refund; null for complaint refunds.
    /// </summary>
    /// <param name="refund">Refund whose RequestKey names the seller order.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The seller order, or null.</returns>
    public Task<SellerOrder?> GetFarmerRejectedSellerOrderAsync(
        RefundTransaction refund,
        CancellationToken cancellationToken)
    {
        string[] parts = refund.RequestKey.Split(':');
        if (refund.DisputeTicketId is not null
            || !refund.RequestKey.StartsWith(FarmerRejectKeyPrefix, StringComparison.Ordinal)
            || parts.Length < 2 || !long.TryParse(parts[1], out long sellerOrderId))
        {
            return Task.FromResult<SellerOrder?>(null);
        }

        return _context.SellerOrders
            .Include(sellerOrder => sellerOrder.Order)
            .Include(sellerOrder => sellerOrder.FarmerProfile)
            .SingleOrDefaultAsync(sellerOrder => sellerOrder.SellerOrderId == sellerOrderId, cancellationToken);
    }

    /// <summary>
    /// Checks GatewayReference across all refunds.
    /// </summary>
    /// <param name="transferReference">Bank transaction ID.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>True when already used.</returns>
    public Task<bool> TransferReferenceExistsAsync(
        string transferReference,
        CancellationToken cancellationToken)
    {
        return _context.RefundTransactions.AsNoTracking()
            .AnyAsync(refund => refund.GatewayReference == transferReference, cancellationToken);
    }

    /// <summary>
    /// Adds the history row to the context.
    /// </summary>
    /// <param name="audit">New row.</param>
    public void AddAudit(AuditLog audit)
    {
        _context.AuditLogs.Add(audit);
    }

    /// <summary>
    /// Adds the notification row to the context.
    /// </summary>
    /// <param name="notification">New row.</param>
    public void AddNotification(Notification notification)
    {
        _context.Notifications.Add(notification);
    }

    /// <summary>
    /// Runs EF Core SaveChanges: all pending inserts and updates in one transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
