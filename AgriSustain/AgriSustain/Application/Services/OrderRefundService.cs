using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Application.Services;

public sealed class OrderRefundService(AgriSustainContext context, FulfillmentRules rules,
    TimeProvider clock, PaymentGatewayProxy gateway)
{
    private static bool IsPaid(PaymentTransaction payment) =>
        payment.PaymentStatus is "Success" or "Succeeded" or "Paid" or "Completed";

    private static bool ConsumesBalance(RefundTransaction refund) =>
        refund.RefundStatus is "Completed" or "Succeeded" or "Success" or "Pending" or "Processing";

    public decimal GetCompletedAmount(SellerOrder order) => order.Order.PaymentTransactions
        .SelectMany(payment => payment.RefundTransactions)
        .Where(refund => refund.RequestKey.StartsWith($"farmer-reject:{order.SellerOrderId}:")
            && refund.RefundStatus is "Completed" or "Succeeded" or "Success")
        .Sum(refund => refund.Amount);

    public decimal GetRefundableAmount(SellerOrder order) => CalculateRefundableAmount(context, rules, order);

    public static decimal CalculateRefundableAmount(AgriSustainContext db, FulfillmentRules rules, SellerOrder order)
    {
        var payments = order.Order.PaymentTransactions.Where(IsPaid).ToList();
        var ids = payments.Select(payment => payment.PaymentTransactionId).ToArray();
        var allocations = db.PaymentInvoiceAllocations.AsNoTracking()
            .Where(allocation => allocation.Invoice.SellerOrderId == order.SellerOrderId
                && ids.Contains(allocation.PaymentTransactionId)).ToList();
        var allocationIds = allocations.Select(allocation => allocation.PaymentInvoiceAllocationId).ToHashSet();
        var refunds = payments.SelectMany(payment => payment.RefundTransactions).ToList();
        decimal paidShare = allocations.Count > 0 ? allocations.Sum(allocation => allocation.Amount)
            : order.Order.SellerOrders.Count <= 1 ? payments.Sum(payment => payment.Amount) : 0;
        decimal committedShare = refunds.Where(refund => ConsumesBalance(refund)
            && (refund.RequestKey.StartsWith($"farmer-reject:{order.SellerOrderId}:")
                || refund.PaymentInvoiceAllocationId is long id && allocationIds.Contains(id)))
            .Sum(refund => refund.Amount);
        decimal paymentBalance = payments.Sum(payment => Math.Max(0, payment.Amount
            - payment.RefundTransactions.Where(ConsumesBalance).Sum(refund => refund.Amount)));
        return Math.Max(0, Math.Min(Math.Min(order.FinalAmount, paidShare) - committedShare, paymentBalance));
    }

    public async Task<RefundTransaction?> RefundSellerShareOnceAsync(SellerOrder order, long actorId,
        string reason, string requestKey, DateTime now, CancellationToken ct)
    {
        decimal remaining = GetRefundableAmount(order);
        if (remaining <= 0)
        {
            // Never misrepresent an ambiguous paid multi-farm purchase as an unpaid cancellation.
            if (order.Order.SellerOrders.Count > 1 && rules.VerifiedPayment(order) > 0
                && !await context.PaymentInvoiceAllocations.AnyAsync(a => a.Invoice.SellerOrderId == order.SellerOrderId, ct))
                throw new InvalidOperationException("Không xác định được khoản thanh toán của đơn nông trại.");
            return null;
        }
        var account = await context.UserBankAccounts.AsNoTracking().SingleOrDefaultAsync(a =>
            a.UserId == order.Order.BuyerUserId && a.IsDefault && a.BankCodeNavigation.IsActive, ct)
            ?? throw new InvalidOperationException("Người mua chưa có tài khoản nhận tiền mặc định hợp lệ.");
        RefundTransaction? last = null;
        now = clock.GetUtcNow().UtcDateTime;
        foreach (var payment in order.Order.PaymentTransactions.Where(IsPaid).OrderBy(p => p.PaymentTransactionId))
        {
            var allocations = await context.PaymentInvoiceAllocations.Include(a => a.RefundTransactions)
                .Where(a => a.PaymentTransactionId == payment.PaymentTransactionId
                    && a.Invoice.SellerOrderId == order.SellerOrderId).OrderBy(a => a.PaymentInvoiceAllocationId).ToListAsync(ct);
            // A legacy single-farm payment has an unambiguous destination even without invoice allocations.
            var shares = allocations.Count > 0 ? allocations.Cast<PaymentInvoiceAllocation?>().ToList()
                : order.Order.SellerOrders.Count <= 1 ? new List<PaymentInvoiceAllocation?> { null } : [];
            foreach (var allocation in shares)
            {
                decimal shareBalance = allocation is null ? remaining : Math.Max(0, allocation.Amount
                    - allocation.RefundTransactions.Where(ConsumesBalance).Sum(r => r.Amount));
                decimal paymentBalance = Math.Max(0, payment.Amount
                    - payment.RefundTransactions.Where(ConsumesBalance).Sum(r => r.Amount));
                decimal amount = Math.Min(remaining, Math.Min(shareBalance, paymentBalance));
                if (amount <= 0) continue;
                string key = $"farmer-reject:{order.SellerOrderId}:{payment.PaymentTransactionId}:{allocation?.PaymentInvoiceAllocationId ?? 0}";
                var result = await gateway.RefundAsync(new(payment.PaymentTransactionId, amount,
                    account.BankCode, account.AccountNumber, account.AccountHolder), key, ct);
                last = new RefundTransaction {
                    PaymentTransactionId = payment.PaymentTransactionId,
                    PaymentInvoiceAllocationId = allocation?.PaymentInvoiceAllocationId,
                    ProcessedByUserId = actorId, RefundMethod = "BankTransfer",
                    RefundCode = $"REF-{order.SellerOrderId}-{payment.PaymentTransactionId}-{allocation?.PaymentInvoiceAllocationId ?? 0}",
                    Amount = result.Amount, Reason = reason[..Math.Min(500, reason.Length)],
                    RefundStatus = "Completed", ProcessedAt = now, GatewayReference = result.Reference, RequestKey = key,
                    ReceiverBankCode = account.BankCode, ReceiverAccountNumber = account.AccountNumber,
                    ReceiverAccountHolder = account.AccountHolder, ReceiverAccountProvidedAt = now
                };
                payment.RefundTransactions.Add(last);
                context.RefundTransactions.Add(last);
                remaining -= amount;
                if (remaining <= 0) return last;
            }
        }
        if (remaining > 0) throw new InvalidOperationException("Khoản hoàn vượt số dư thanh toán có thể hoàn.");
        return last;
    }
}
