using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class RefundRepository : IRefundRepository
{
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
            refunds = refunds.Where(refund => refund.RefundCode.Contains(query.Search)
                || (refund.PaymentTransaction.Order != null
                    && refund.PaymentTransaction.Order.OrderCode.Contains(query.Search))
                || (refund.DisputeTicket != null && refund.DisputeTicket.SellerOrder != null
                    && refund.DisputeTicket.SellerOrder.SellerOrderCode.Contains(query.Search)));
        }

        int total = await refunds.CountAsync(cancellationToken);
        var items = await refunds
            .OrderBy(refund => refund.RefundStatus == RefundTransaction.StatusPending ? 0 : 1)
            .ThenBy(refund => refund.RefundTransactionId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(refund => new RefundListItemDto(
                refund.RefundTransactionId,
                refund.RefundCode,
                refund.Amount,
                refund.RefundStatus,
                refund.RefundMethod,
                refund.DisputeTicketId,
                refund.DisputeTicket == null || refund.DisputeTicket.SellerOrder == null
                    ? null : refund.DisputeTicket.SellerOrder.SellerOrderCode,
                refund.PaymentTransaction.Order == null ? null : refund.PaymentTransaction.Order.OrderCode,
                refund.PaymentTransaction.Order == null
                    ? null : refund.PaymentTransaction.Order.BuyerUser.FullName,
                refund.DisputeTicket == null ? null : refund.DisputeTicket.DecisionAt,
                refund.ProcessedAt))
            .ToListAsync(cancellationToken);
        return (total, items);
    }

    public async Task<RefundDetailDto?> GetDetailAsync(
        long refundId,
        CancellationToken cancellationToken)
    {
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

        return await _context.RefundTransactions.AsNoTracking()
            .Where(refund => refund.RefundTransactionId == refundId)
            .Select(refund => new RefundDetailDto(
                refund.RefundTransactionId,
                refund.RefundCode,
                refund.Amount,
                refund.RefundStatus,
                refund.Reason,
                refund.RefundMethod,
                refund.GatewayReference,
                refund.ProcessedAt,
                refund.RefundStatus == RefundTransaction.StatusCompleted
                    ? refund.ProcessedByUser.FullName : null,
                refund.DisputeTicketId,
                refund.DisputeTicket == null ? null : refund.DisputeTicket.DisputeReason,
                refund.DisputeTicket == null ? null : refund.DisputeTicket.FaultParty,
                refund.DisputeTicket == null ? null : refund.DisputeTicket.ResolutionNotes,
                refund.DisputeTicket == null ? null : refund.DisputeTicket.DecisionAt,
                refund.DisputeTicket == null || refund.DisputeTicket.AssignedStaffUser == null
                    ? null : refund.DisputeTicket.AssignedStaffUser.FullName,
                refund.DisputeTicket == null || refund.DisputeTicket.SellerOrder == null
                    ? null : refund.DisputeTicket.SellerOrder.SellerOrderCode,
                refund.DisputeTicket == null || refund.DisputeTicket.SellerOrder == null
                    ? null : refund.DisputeTicket.SellerOrder.Status,
                refund.PaymentTransaction.Order == null ? null : refund.PaymentTransaction.Order.OrderCode,
                refund.PaymentTransaction.Order == null ? null : refund.PaymentTransaction.Order.OrderStatus,
                refund.PaymentTransaction.Order == null
                    ? null : refund.PaymentTransaction.Order.BuyerUser.FullName,
                refund.PaymentTransaction.Order == null
                    ? null : refund.PaymentTransaction.Order.BuyerUser.Email,
                refund.PaymentTransaction.Order == null
                    ? null : refund.PaymentTransaction.Order.BuyerUser.PhoneNumber,
                refund.PaymentTransaction.TransactionCode,
                refund.PaymentTransaction.GatewayProvider,
                refund.PaymentTransaction.PaymentType,
                refund.PaymentTransaction.Amount,
                refund.PaymentTransaction.PaymentStatus,
                refund.PaymentTransaction.PaidAt,
                history))
            .SingleOrDefaultAsync(cancellationToken);
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
