using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Validators;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class FinanceRepository(AgriSustainContext db) : IFinanceRepository
{
    public Task<bool> CanAccessAsync(long userId, CancellationToken ct) =>
        db.Users.AnyAsync(user => user.UserId == userId && user.Status == "Active"
            && user.IsEmailVerified && (user.Role == "FinanceStaff" || user.Role == "Finance Staff"), ct);

    public async Task<decimal> MetricAsync(string metric, FinancePeriod period, CancellationToken ct) =>
        await Sources(metric, period).SumAsync(row => (decimal?)row.Amount, ct) ?? 0;

    public async Task<PagedResponse<FinanceRecord>> RecordsAsync(
        string metric, FinancePeriod period, int page, int pageSize, CancellationToken ct)
    {
        var rows = Sources(metric, period);
        int count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(row => row.At).ThenByDescending(row => row.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items, count, page, pageSize);
    }

    private IQueryable<FinanceRecord> Sources(string metric, FinancePeriod period)
    {
        var refunds = db.RefundTransactions.Where(refund =>
            FinancePaymentRules.Refunded.Contains(refund.RefundStatus)
            && refund.ProcessedAt != null);
        if (metric == "Gmv")
        {
            var refundedByOrder = refunds.Where(refund => refund.ProcessedAt < period.Until)
                .GroupBy(refund => refund.PaymentTransaction.OrderId)
                .Select(group => new { OrderId = group.Key, Amount = group.Sum(refund => refund.Amount) });
            return (from order in db.Orders.AsNoTracking()
                    where order.OrderDate >= period.From && order.OrderDate < period.Until
                        && order.OrderStatus != "Cancelled"
                    join refund in refundedByOrder on order.OrderId equals refund.OrderId into matches
                    from refund in matches.DefaultIfEmpty()
                    select new { Order = order, Net = order.FinalAmount - ((decimal?)refund.Amount ?? 0) })
                .Select(row => new FinanceRecord
                {
                    Id = row.Order.OrderId,
                    Reference = row.Order.OrderCode,
                    Amount = row.Net > 0 ? row.Net : 0,
                    Status = row.Order.OrderStatus,
                    At = row.Order.OrderDate
                });
        }
        if (metric == "Refunds")
        {
            return refunds.Where(refund => refund.ProcessedAt >= period.From
                    && refund.ProcessedAt < period.Until)
                .Select(refund => new FinanceRecord
                {
                    Id = refund.RefundTransactionId,
                    Reference = refund.RefundCode,
                    RelatedReference = refund.PaymentTransaction.TransactionCode,
                    Amount = refund.Amount,
                    Status = refund.RefundStatus,
                    At = refund.ProcessedAt,
                    PaymentId = refund.PaymentTransactionId
                });
        }
        if (metric == "Subscriptions")
        {
            return db.PaymentTransactions.AsNoTracking().Where(payment =>
                    payment.B2bsubscriptionId != null
                    && FinancePaymentRules.Captured.Contains(payment.PaymentStatus)
                    && payment.PaidAt >= period.From && payment.PaidAt < period.Until)
                .Select(payment => new FinanceRecord
                {
                    Id = payment.PaymentTransactionId,
                    Reference = payment.TransactionCode,
                    RelatedReference = payment.B2bsubscription!.Package.PackageName,
                    Amount = payment.Amount,
                    Status = payment.PaymentStatus,
                    At = payment.PaidAt,
                    PaymentId = payment.PaymentTransactionId
                });
        }
        var payouts = db.FarmerPayouts.AsNoTracking();
        if (metric == "PendingPayouts")
        {
            return payouts.Where(payout => FinancePaymentRules.PendingPayouts.Contains(
                    payout.PayoutStatus) && (payout.EligibleAt ?? payout.ApprovedAt) >= period.From
                    && (payout.EligibleAt ?? payout.ApprovedAt) < period.Until)
                .Select(payout => new FinanceRecord
                {
                    Id = payout.FarmerPayoutId,
                    Reference = payout.PayoutCode,
                    RelatedReference = payout.FarmerProfile.FarmName,
                    Amount = payout.NetAmount,
                    Status = payout.PayoutStatus,
                    At = payout.EligibleAt ?? payout.ApprovedAt
                });
        }
        var completed = payouts.Where(payout => FinancePaymentRules.PaidPayouts.Contains(payout.PayoutStatus)
                && payout.BankTransferRef != null && payout.ProcessedAt >= period.From
                && payout.ProcessedAt < period.Until);
        var commissions = db.PayoutItems.GroupBy(item => item.FarmerPayoutId)
            .Select(group => new { PayoutId = group.Key, Amount = group.Sum(item => item.CommissionAmount) });
        return (from payout in completed
                join commission in commissions on payout.FarmerPayoutId equals commission.PayoutId into matches
                from commission in matches.DefaultIfEmpty()
                select new { Payout = payout, Commission = (decimal?)commission.Amount ?? 0 })
            .Select(row => new FinanceRecord
            {
                Id = row.Payout.FarmerPayoutId,
                Reference = row.Payout.PayoutCode,
                RelatedReference = row.Payout.FarmerProfile.FarmName,
                Amount = metric == "Commission" ? row.Commission : row.Payout.NetAmount,
                Status = row.Payout.PayoutStatus,
                At = row.Payout.ProcessedAt
            });
    }

    public async Task<PagedResponse<FinancePaymentRow>> SearchAsync(
        FinancePaymentQuery query, CancellationToken ct)
    {
        var payments = db.PaymentTransactions.AsNoTracking()
            .Where(payment => payment.OrderId != null || payment.B2bsubscriptionId != null);
        if (query.From is { } from)
        {
            DateTime utc = FinanceQueryValidator.Utc(from);
            payments = payments.Where(payment => payment.RequestedAt >= utc);
        }
        if (query.To is { } to)
        {
            DateTime utc = FinanceQueryValidator.Utc(to.AddDays(1));
            payments = payments.Where(payment => payment.RequestedAt < utc);
        }
        if (query.Category == "Order")
        {
            payments = payments.Where(p => p.OrderId != null);
        }
        if (query.Category == "Subscription")
        {
            payments = payments.Where(p => p.B2bsubscriptionId != null);
        }
        if (!string.IsNullOrEmpty(query.Provider))
        {
            payments = payments.Where(payment => payment.GatewayProvider == query.Provider);
        }
        if (!string.IsNullOrEmpty(query.Status))
        {
            payments = query.Status == "Succeeded"
                ? payments.Where(p => FinancePaymentRules.Captured.Contains(p.PaymentStatus))
                : query.Status == "Pending"
                    ? payments.Where(p => p.PaymentStatus == "Pending"
                        || p.PaymentStatus == "AwaitingPayment" || p.PaymentStatus == "Processing")
                    : payments.Where(p => p.PaymentStatus == query.Status);
        }
        string keyword = (query.Search ?? "").Trim();
        if (keyword.Length > 0)
        {
            payments = payments.Where(payment => payment.TransactionCode.Contains(keyword)
                || (payment.ProviderTransactionReference ?? "").Contains(keyword)
                || payment.Order != null && payment.Order.OrderCode.Contains(keyword)
                || payment.B2bsubscription != null
                    && (payment.B2bsubscription.Package.PackageName.Contains(keyword)
                        || ("SUB #" + payment.B2bsubscriptionId).Contains(keyword)));
        }
        var rows = payments.Select(payment => new
        {
            Payment = payment,
            Latest = db.AuditLogs.Where(audit => audit.EntityType == "PaymentTransaction"
                    && audit.EntityId == payment.PaymentTransactionId
                    && audit.Action == FinancePaymentRules.AuditAction)
                .OrderByDescending(audit => audit.OccurredAt).ThenByDescending(audit => audit.AuditLogId)
                .Select(audit => new { audit.NewValue, audit.Reason }).FirstOrDefault()
        }).Select(row => new FinancePaymentRow
        {
            Id = row.Payment.PaymentTransactionId,
            Code = row.Payment.TransactionCode,
            Reference = row.Payment.ProviderTransactionReference,
            Category = row.Payment.OrderId != null ? "Order" : "Subscription",
            Obligation = row.Payment.Order != null ? row.Payment.Order.OrderCode
                : "SUB #" + row.Payment.B2bsubscriptionId + " · "
                    + row.Payment.B2bsubscription!.Package.PackageName,
            Provider = row.Payment.GatewayProvider,
            Amount = row.Payment.Amount,
            RequestedAt = row.Payment.RequestedAt,
            PaidAt = row.Payment.PaidAt,
            Status = row.Payment.PaymentStatus,
            NeedsReview = row.Latest != null && row.Latest.NewValue != null
                && row.Latest.NewValue.Contains("\"RequiresReview\":true"),
            Note = row.Latest == null ? null : row.Latest.Reason,
            CancelledOrder = row.Payment.Order != null && row.Payment.Order.OrderStatus == "Cancelled"
        });
        if (query.NeedsAttention)
        {
            rows = rows.Where(row => row.NeedsReview || row.Status == "Pending"
                || row.Status == "AwaitingPayment" || row.Status == "Processing");
        }
        int count = await rows.CountAsync(ct);
        return new(await rows.OrderByDescending(row => row.RequestedAt)
            .ThenByDescending(row => row.Id).Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize).ToListAsync(ct), count, query.Page, query.PageSize);
    }

    public Task<PaymentTransaction?> PaymentAsync(long id, CancellationToken ct)
    {
        var payments = db.PaymentTransactions.AsNoTrackingWithIdentityResolution();
        return payments.Where(payment => payment.PaymentTransactionId == id)
            .Include(payment => payment.PaymentGatewayEvents)
            .Include(payment => payment.PaymentInvoiceAllocations)
            .Include(payment => payment.Order).ThenInclude(order => order!.PaymentTransactions)
            .Include(payment => payment.B2bsubscription).ThenInclude(subscription => subscription!.Package)
            .Include(payment => payment.B2bsubscription)
                .ThenInclude(subscription => subscription!.PaymentTransactions)
            .AsSplitQuery().SingleOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Invoice>> InvoicesAsync(
        PaymentTransaction payment, CancellationToken ct)
    {
        var invoices = db.Invoices.AsNoTrackingWithIdentityResolution();
        return await invoices.Where(invoice => payment.OrderId != null
                ? invoice.SellerOrder != null && invoice.SellerOrder.OrderId == payment.OrderId
                : payment.B2bsubscriptionId != null && invoice.B2bsubscriptionId == payment.B2bsubscriptionId)
            .Include(invoice => invoice.PaymentInvoiceAllocations)
                .ThenInclude(allocation => allocation.PaymentTransaction)
            .OrderBy(invoice => invoice.InvoiceId).AsSplitQuery().ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLog>> HistoryAsync(long id, CancellationToken ct) =>
        await db.AuditLogs.AsNoTracking().Where(audit => audit.EntityType == "PaymentTransaction"
                && audit.EntityId == id && audit.Action == FinancePaymentRules.AuditAction)
            .Include(audit => audit.PerformedByUser).OrderByDescending(audit => audit.OccurredAt)
            .ThenByDescending(audit => audit.AuditLogId).ToListAsync(ct);

}
