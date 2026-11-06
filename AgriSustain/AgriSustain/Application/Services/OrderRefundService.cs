using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

/// <summary>
/// UC-FAR-06 Farmer rejection of a paid order. The paid share is not sent anywhere automatically:
/// a Pending refund request goes to Finance Staff, who transfer it manually in UC-FIN-03, the
/// same way as an approved complaint refund (BR-RF-01, BR-RF-03 as revised).
/// </summary>
public sealed class OrderRefundService(IFarmerOrderRepository repository, FulfillmentRules rules,
    TimeProvider clock)
{

    private static bool IsPaid(PaymentTransaction payment) =>
        payment.PaymentStatus is "Success" or "Succeeded" or "Paid" or "Completed";

    private static bool ConsumesBalance(RefundTransaction refund) =>
        refund.ReservesPayment;

    public decimal GetCompletedAmount(SellerOrder order) => order.Order.PaymentTransactions
        .SelectMany(payment => payment.RefundTransactions)
        .Where(refund => refund.RequestKey.StartsWith($"farmer-reject:{order.SellerOrderId}:")
            && refund.RefundStatus is "Completed" or "Succeeded" or "Success")
        .Sum(refund => refund.Amount);

    /// <summary>Farmer-rejection refunds of the order still waiting for Finance to transfer them.</summary>
    public decimal GetPendingAmount(SellerOrder order) => order.Order.PaymentTransactions
        .SelectMany(payment => payment.RefundTransactions)
        .Where(refund => refund.RequestKey.StartsWith($"farmer-reject:{order.SellerOrderId}:")
            && refund.RefundStatus is RefundTransaction.StatusPending or RefundTransaction.StatusProcessing
                or RefundTransaction.StatusFailed or RefundTransaction.StatusNeedsReconciliation)
        .Sum(refund => refund.Amount);

    /// <summary>
    /// Amount of the seller order still refundable. sellerAllocations are the invoice allocations of
    /// this seller order; only those of paid payments count.
    /// </summary>
    public static decimal CalculateRefundableAmount(FulfillmentRules rules, SellerOrder order,
        IEnumerable<PaymentInvoiceAllocation> sellerAllocations)
    {
        var payments = order.Order.PaymentTransactions.Where(IsPaid).ToList();
        var ids = payments.Select(payment => payment.PaymentTransactionId).ToHashSet();
        var allocations = sellerAllocations.Where(allocation => ids.Contains(allocation.PaymentTransactionId)).ToList();
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
        var paidIds = order.Order.PaymentTransactions.Where(IsPaid).Select(p => p.PaymentTransactionId).ToArray();
        var sellerAllocations = await repository.GetSellerAllocationsAsync(order.SellerOrderId, paidIds, ct);
        decimal remaining = CalculateRefundableAmount(rules, order, sellerAllocations);
        if (remaining <= 0)
        {
            // Never misrepresent an ambiguous paid multi-farm purchase as an unpaid cancellation.
            if (order.Order.SellerOrders.Count > 1 && rules.VerifiedPayment(order) > 0 && sellerAllocations.Count == 0)
                throw new InvalidOperationException("Không xác định được khoản thanh toán của đơn nông trại.");
            return null;
        }
        // The saved default account is used until the buyer confirms or changes it (UC-FIN-08); without
        // one the request waits as "Awaiting customer account" and the rejection still goes through.
        var account = await repository.LoadPaymentContextAsync(order, ct);
        RefundTransaction? last = null;
        var created = new List<RefundTransaction>();
        now = clock.GetUtcNow().UtcDateTime;
        foreach (var payment in order.Order.PaymentTransactions.Where(IsPaid).OrderBy(p => p.PaymentTransactionId))
        {
            var allocations = sellerAllocations
                .Where(a => a.PaymentTransactionId == payment.PaymentTransactionId).ToList();
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
                last = new RefundTransaction {
                    PaymentTransactionId = payment.PaymentTransactionId,
                    PaymentInvoiceAllocationId = allocation?.PaymentInvoiceAllocationId,
                    // Replaced by the Finance Staff member who records the transfer.
                    ProcessedByUserId = actorId,
                    RefundCode = $"REF-{order.SellerOrderId}-{payment.PaymentTransactionId}-{allocation?.PaymentInvoiceAllocationId ?? 0}",
                    Amount = amount, Reason = reason[..Math.Min(500, reason.Length)],
                    RefundStatus = RefundTransaction.StatusPending, RequestKey = key,
                    ReceiverBankCode = account?.BankCode, ReceiverAccountNumber = account?.AccountNumber,
                    ReceiverAccountHolder = account?.AccountHolder,
                    ReceiverAccountProvidedAt = account is null ? null : now
                };
                payment.RefundTransactions.Add(last);
                repository.SaveRefundResultAsync(last);
                created.Add(last);
                remaining -= amount;
                if (remaining <= 0) break;
            }
            if (remaining <= 0) break;
        }
        if (remaining > 0) throw new InvalidOperationException("Khoản hoàn vượt số dư thanh toán có thể hoàn.");
        await NotifyFinanceAsync(order, created, now, ct);
        return last;
    }

    /// <summary>Finance Staff learn about the new manual refund requests in-app (UC-FIN-03 queue).</summary>
    private async Task NotifyFinanceAsync(SellerOrder order, IReadOnlyList<RefundTransaction> refunds,
        DateTime now, CancellationToken ct)
    {
        if (refunds.Count == 0)
        {
            return;
        }
        var financeUsers = await repository.GetActiveFinanceUserIdsAsync(ct);
        decimal total = refunds.Sum(refund => refund.Amount);
        foreach (long userId in financeUsers)
        {
            var notice = new Notification
            {
                UserId = userId, Title = "Yêu cầu hoàn tiền mới",
                Content = $"Nông trại từ chối đơn {order.SellerOrderCode}: cần hoàn {total:N0} ₫ "
                    + $"({string.Join(", ", refunds.Select(refund => refund.RefundCode))}).",
                NotificationType = "Refund", IsRead = false, SentAt = now,
                DeliveryChannel = "InApp", DeliveryStatus = "Sent",
                RelatedRecordType = "SellerOrder", RelatedRecordId = order.SellerOrderId
            };
            repository.SaveNotificationsAsync(notice);
        }
    }
}
