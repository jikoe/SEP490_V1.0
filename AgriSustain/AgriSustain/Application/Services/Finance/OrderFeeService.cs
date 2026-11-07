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
            var inputs = items.Select((item, index) => new FeeItemInput(index,
                categories.TryGetValue(item.ProductId, out var category) ? category : null, item.SubTotal)).ToList();
            decimal sellerTotal = seller.Subtotal + seller.DeliveryFee;
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

    public async Task ReverseCancelledAsync(long sellerOrderId, CancellationToken cancellationToken)
    {
        foreach (var fee in await repository.SellerOrderFeesAsync(sellerOrderId, cancellationToken))
        {
            fee.ReverseAll();
        }
    }

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
