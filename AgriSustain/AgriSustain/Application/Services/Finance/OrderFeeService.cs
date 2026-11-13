using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services.Finance;

/// <summary>
/// BR-FEE-05 / BR-FEE-08: records each order item's fee lines from the policies active when the order
/// is placed, and reverses them when the seller order is cancelled or refunded. Only Farmers pay these
/// fees (BR-FEE-09), so the buyer's payable amount never changes.
/// </summary>
public sealed class OrderFeeService(IFeeRepository repository) : IOrderFeeService
{
    /// <summary>
    /// BR-FEE-05. Loads the policies active on the placement date (Vietnam day) for the order type; with none, nothing
    /// is recorded. For each seller order: the fee base of an item is its subtotal minus its share of that seller's own
    /// discount (a farm voucher — BR-FEE-06; platform vouchers and points do not reduce it); <see cref="FeeCalculator"/>
    /// works out the lines; every line above 0 is stored as an OrderItemFee snapshot (code, group, refund mode, rate,
    /// base, amount). The caller saves, so the fee lines commit with the order.
    /// </summary>
    /// <param name="order">Order being placed.</param>
    /// <param name="nowUtc">Placement time (UTC).</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    public async Task ApplyAsync(Order order, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var date = DateOnly.FromDateTime(nowUtc.AddHours(7));
        var policies = await repository.ActivePoliciesAsync(order.OrderType, date, cancellationToken);
        if (policies.Count == 0)
        {
            return;
        }
        var productIds = order.SellerOrders.SelectMany(seller => seller.OrderItems)
            .Select(item => item.ProductId).Distinct().ToList();
        var categories = await repository.ProductCategoriesAsync(productIds, cancellationToken);
        foreach (var seller in order.SellerOrders)
        {
            var items = seller.OrderItems.ToList();
            // BR-FEE-06 / BR-PV-11: the base is each item's subtotal after the seller's own discount (a farm
            // voucher, recorded on this seller order); platform vouchers and reward points do not reduce it.
            decimal sellerDiscount = Math.Min(seller.Subtotal, order.VoucherRedemptions
                .Where(use => ReferenceEquals(use.SellerOrder, seller)
                    || use.SellerOrderId is long id && id != 0 && id == seller.SellerOrderId)
                .Sum(use => use.DiscountSnapshot));
            var discounts = sellerDiscount > 0
                ? FeeCalculator.Allocate(sellerDiscount, items.Select(item => item.SubTotal).ToList())
                : items.Select(_ => 0m).ToList();
            var inputs = items.Select((item, index) => new FeeItemInput(index,
                categories.TryGetValue(item.ProductId, out var category) ? category : null,
                Math.Max(0, item.SubTotal - discounts[index]))).ToList();
            decimal sellerTotal = seller.Subtotal - sellerDiscount + seller.DeliveryFee;
            foreach (var line in FeeCalculator.Calculate(inputs, sellerTotal, policies))
            {
                if (line.Amount <= 0)
                {
                    continue;
                }
                var policy = line.Policy;
                items[line.ItemIndex].Fees.Add(new OrderItemFee
                {
                    FeeTypeId = policy.FeeTypeId, FeePolicyId = policy.FeePolicyId,
                    FeeCode = policy.FeeType.FeeCode, FeeGroup = policy.FeeType.FeeGroup,
                    RefundMode = policy.FeeType.RefundMode, CategoryId = inputs[line.ItemIndex].CategoryId,
                    RateSnapshot = policy.Method == FeePolicy.MethodPercentage ? policy.Rate : null,
                    BaseAmount = line.BaseAmount, Amount = line.Amount
                });
            }
        }
    }

    /// <summary>
    /// Marks every fee line of a cancelled seller order fully reversed. The caller saves.
    /// </summary>
    /// <param name="sellerOrderId">Cancelled seller order.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    public async Task ReverseCancelledAsync(long sellerOrderId, CancellationToken cancellationToken)
    {
        foreach (var fee in await repository.SellerOrderFeesAsync(sellerOrderId, cancellationToken))
        {
            fee.ReverseAll();
        }
    }

    /// <summary>
    /// share = min(1, refunded total ÷ seller order amount); each fee line is reversed according to its refund mode
    /// (Full: all, Proportional: by share, NonRefundable: nothing). Does nothing when either amount is not positive.
    /// </summary>
    /// <param name="sellerOrderId">Refunded seller order.</param>
    /// <param name="refundedTotal">Completed refunds of the seller order.</param>
    /// <param name="sellerOrderAmount">Final amount of the seller order.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    public async Task ReverseRefundAsync(long sellerOrderId, decimal refundedTotal, decimal sellerOrderAmount,
        CancellationToken cancellationToken)
    {
        if (sellerOrderAmount <= 0 || refundedTotal <= 0)
        {
            return;
        }
        decimal share = Math.Min(1, refundedTotal / sellerOrderAmount);
        foreach (var fee in await repository.SellerOrderFeesAsync(sellerOrderId, cancellationToken))
        {
            fee.ReverseForRefund(share);
        }
    }
}
