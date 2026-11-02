using System.Text.Json;
using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class RefundRepository : IRefundRepository
{
    private const string FarmerRejectKeyPrefix = "farmer-reject:";

    /// <summary>Refund states that hold part of a payment or seller order (BR-CS-04).</summary>
    private static readonly string[] s_reservingStatuses =
    [
        RefundTransaction.StatusPending, RefundTransaction.StatusProcessing, RefundTransaction.StatusCompleted,
        RefundTransaction.StatusFailed, RefundTransaction.StatusNeedsReconciliation, "Success", "Succeeded"
    ];

    private static readonly TimeSpan s_vietnamOffset = TimeSpan.FromHours(7);

    private readonly AgriSustainContext _context;

    public RefundRepository(AgriSustainContext context)
    {
        _context = context;
    }

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
            refund.CanStartTransfer && ticket?.TicketStatus is null or DisputeTicket.StatusRefundPending,
            payment.TransactionCode,
            payment.GatewayProvider,
            payment.PaymentType,
            payment.Amount,
            Math.Max(0, payment.Amount - paymentHeldByOthers),
            payment.PaymentStatus,
            payment.PaidAt,
            history);
    }

    /// <summary>
    /// BR-RF-04 rows recorded on the complaint rather than the refund: the request created by the
    /// decision and the receiving account snapshotted at that time (actor shown as System).
    /// </summary>
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

    public Task<bool> TransferReferenceExistsAsync(
        string transferReference,
        CancellationToken cancellationToken)
    {
        return _context.RefundTransactions.AsNoTracking()
            .AnyAsync(refund => refund.GatewayReference == transferReference, cancellationToken);
    }

    public void AddAudit(AuditLog audit)
    {
        _context.AuditLogs.Add(audit);
    }

    public void AddNotification(Notification notification)
    {
        _context.Notifications.Add(notification);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
