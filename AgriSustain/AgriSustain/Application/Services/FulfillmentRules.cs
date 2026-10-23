using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class FulfillmentRules(TimeProvider clock)
{
    public static bool IsConfirmed(string status) => status is "Confirmed" or "Preparing";
    public static bool IsReady(string status) => status is "ReadyToShip" or "Ready";
    public static bool IsShipping(string status) => status is "Shipping" or "InTransit";
    public static bool IsBeforeShipping(string status) =>
        status is "Pending" or "Confirmed" or "Preparing" or "ReadyToShip" or "Ready";

    public decimal VerifiedPayment(SellerOrder seller)
    {
        var payments = seller.Order.PaymentTransactions.Where(x =>
            x.PaymentStatus is "Success" or "Succeeded" or "Paid" or "Completed");
        return payments.Sum(x => decimal.Max(0, x.Amount - x.RefundTransactions
            .Where(r => r.RefundStatus is "Success" or "Succeeded" or "Completed")
            .Sum(r => r.Amount)));
    }

    public decimal RequiredPayment(SellerOrder seller)
    {
        if (seller.Order.IsPreOrder)
        {
            if (seller.Order.SellerOrders.SelectMany(x => x.OrderItems)
                .Any(x => x.PreOrderReservation is null))
            {
                return seller.Order.FinalAmount;
            }

            return decimal.Round(seller.Order.SellerOrders.SelectMany(x => x.OrderItems)
                .Sum(x => x.Quantity * x.UnitPriceSnapshot
                    * x.PreOrderReservation!.DepositPercentageSnapshot / 100), 2);
        }

        return seller.Order.FinalAmount;
    }

    public bool PaymentSatisfied(SellerOrder seller) => RequiredPayment(seller) > 0
        && VerifiedPayment(seller) >= RequiredPayment(seller)
        && seller.Order.PaymentTransactions.Any(x =>
            x.PaymentStatus is "Success" or "Succeeded" or "Paid" or "Completed");

    public bool AllocationsValid(SellerOrder seller)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().AddHours(7).DateTime);
        return seller.OrderItems.Count > 0 && seller.OrderItems.All(item =>
            item.Quantity > 0 && item.OrderItemBatchAllocations.Count > 0
            && item.OrderItemBatchAllocations.All(allocation =>
                allocation.AllocationStatus == "Reserved" && allocation.AllocatedQuantity > 0
                && (!allocation.ExpiresAt.HasValue
                    || allocation.ExpiresAt > clock.GetUtcNow().UtcDateTime)
                && allocation.HarvestBatch.ProductId == item.ProductId
                && allocation.HarvestBatch.IsSaleable(today))
            && item.OrderItemBatchAllocations.Sum(x => x.AllocatedQuantity) == item.Quantity)
            && seller.OrderItems.SelectMany(x => x.OrderItemBatchAllocations)
                .GroupBy(x => x.HarvestBatchId).All(group =>
                    group.Sum(x => x.AllocatedQuantity) <= group.First().HarvestBatch.ReservedQuantity);
    }

    public static string Version(SellerOrder seller)
    {
        string Canonical(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        string value = $"{seller.SellerOrderId}:{seller.Status}:{seller.DeliveredAt:O}:"
            + string.Join("|", seller.OrderItems.SelectMany(x => x.OrderItemBatchAllocations)
                .OrderBy(x => x.OrderItemBatchAllocationId)
                .Select(x => $"{x.OrderItemBatchAllocationId}:{x.AllocationStatus}:"
                    + Canonical(x.AllocatedQuantity)))
            + string.Join("|", seller.Shipments.OrderBy(x => x.ShipmentId)
                .Select(x => $"{x.ShipmentId}:{x.ShipmentStatus}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    public static void Aggregate(Order parent)
    {
        var statuses = parent.SellerOrders.Select(x => x.Status).ToArray();
        if (statuses.All(x => x is "Cancelled" or "Rejected"))
        {
            parent.OrderStatus = "Cancelled";
        }
        else if (statuses.All(x => x is "Delivered" or "Received" or "Completed"
            or "Cancelled" or "Rejected"))
        {
            parent.OrderStatus = "Delivered";
        }
        else if (statuses.Any(x => x is "DeliveryFailed" or "Returned"))
        {
            parent.OrderStatus = "DeliveryFailed";
        }
        else if (statuses.Any(IsShipping))
        {
            parent.OrderStatus = "Shipping";
        }
        else if (statuses.Any(x => IsConfirmed(x) || IsReady(x)))
        {
            parent.OrderStatus = "Processing";
        }
    }
}


