using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class OrderTrackingRepository(AgriSustainContext db) : IOrderTrackingRepository
{
    private static readonly string[] s_settledPaymentStatuses =
        ["Success", "Succeeded", "Paid", "Completed"];

    public async Task<PagedResponse<OrderSummaryDto>> GetOrdersByBuyerAsync(
        long buyerId, OrderQuery query, CancellationToken cancellationToken)
    {
        var ordersQuery = db.Orders
            .AsNoTracking()
            .Where(o => o.BuyerUserId == buyerId);

        if (!string.IsNullOrWhiteSpace(query.Status) && query.Status != "All")
        {
            ordersQuery = ordersQuery.Where(o => o.OrderStatus == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            string kw = query.Keyword.Trim();
            ordersQuery = ordersQuery.Where(o =>
                o.OrderCode.Contains(kw)
                || o.SellerOrders.Any(so => so.OrderItems
                    .Any(oi => (oi.ProductNameSnapshot ?? "").Contains(kw))));
        }

        int totalCount = await ordersQuery.CountAsync(cancellationToken);

        var orders = await WithTrackingData(ordersQuery
                .OrderByDescending(o => o.OrderDate)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize))
            .ToListAsync(cancellationToken);

        var dtoList = orders.Select(o => new OrderSummaryDto(
            OrderId: o.OrderId,
            OrderCode: o.OrderCode,
            OrderDate: Utc(o.OrderDate),
            OrderStatus: o.OrderStatus,
            PaymentMethod: o.PaymentMethod,
            DeliveryMethod: o.DeliveryMethod,
            TotalAmount: o.TotalAmount,
            DeliveryFee: o.DeliveryFee,
            DiscountAmount: o.DiscountAmount,
            FinalAmount: o.FinalAmount,
            RecipientName: o.RecipientNameSnapshot,
            RecipientPhone: o.RecipientPhoneSnapshot,
            ShippingAddress: o.ShippingAddress,
            TrackingNumber: TrackingNumber(o),
            SellerOrders: SellerOrders(o))).ToList();

        return new PagedResponse<OrderSummaryDto>(
            dtoList,
            totalCount,
            query.Page,
            query.PageSize);
    }

    public async Task<OrderDetailDto?> GetOrderDetailAsync(
        long buyerId, long orderId, CancellationToken cancellationToken)
    {
        var o = await WithTrackingData(db.Orders.AsNoTracking())
            .Include(o => o.PaymentTransactions)
            .Include(o => o.SellerOrders)
                .ThenInclude(so => so.OrderStatusHistories)
            .FirstOrDefaultAsync(
                o => o.OrderId == orderId && o.BuyerUserId == buyerId,
                cancellationToken);

        if (o == null)
        {
            return null;
        }

        var timeline = new List<OrderStatusTimelineDto>
        {
            new("Created", Utc(o.OrderDate), "Khách hàng tạo đơn")
        };
        timeline.AddRange(o.SellerOrders
            .SelectMany(so => so.OrderStatusHistories.Select(history =>
                new OrderStatusTimelineDto(
                    history.NewStatus,
                    Utc(history.OccurredAt),
                    $"{so.SellerNameSnapshot ?? so.FarmerProfile.FarmName}: "
                        + (string.IsNullOrWhiteSpace(history.Reason)
                            ? history.NewStatus : history.Reason))))
            .OrderBy(entry => entry.Timestamp));

        bool isPaid = o.PaymentTransactions
            .Any(p => s_settledPaymentStatuses.Contains(p.PaymentStatus));

        return new OrderDetailDto(
            OrderId: o.OrderId,
            OrderCode: o.OrderCode,
            OrderDate: Utc(o.OrderDate),
            OrderStatus: o.OrderStatus,
            PaymentMethod: o.PaymentMethod,
            DeliveryMethod: o.DeliveryMethod,
            TotalAmount: o.TotalAmount,
            DeliveryFee: o.DeliveryFee,
            DiscountAmount: o.DiscountAmount,
            FinalAmount: o.FinalAmount,
            RecipientName: o.RecipientNameSnapshot,
            RecipientPhone: o.RecipientPhoneSnapshot,
            ShippingAddress: o.ShippingAddress,
            TrackingNumber: TrackingNumber(o),
            PaymentStatus: isPaid ? "Paid" : "Unpaid",
            ExpectedDeliveryDate: o.ExpectedDeliveryDate.HasValue
                ? Utc(o.ExpectedDeliveryDate.Value) : null,
            SellerOrders: SellerOrders(o),
            Timeline: timeline);
    }

    public async Task<OrderDetailDto?> GetOrderDetailByCodeAsync(
        long buyerId, string orderCode, CancellationToken cancellationToken)
    {
        var o = await db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.OrderCode == orderCode && o.BuyerUserId == buyerId,
                cancellationToken);

        if (o == null)
        {
            return null;
        }

        return await GetOrderDetailAsync(buyerId, o.OrderId, cancellationToken);
    }

    private static IQueryable<Order> WithTrackingData(IQueryable<Order> orders) => orders
        .Include(o => o.SellerOrders)
            .ThenInclude(so => so.FarmerProfile)
        .Include(o => o.SellerOrders)
            .ThenInclude(so => so.Shipments)
        .Include(o => o.SellerOrders)
            .ThenInclude(so => so.OrderItems)
                .ThenInclude(oi => oi.ProductReview)
        .Include(o => o.SellerOrders)
            .ThenInclude(so => so.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.ProductImages)
        .Include(o => o.SellerOrders)
            .ThenInclude(so => so.OrderItems)
                .ThenInclude(oi => oi.OrderItemBatchAllocations)
                    .ThenInclude(a => a.HarvestBatch)
                        .ThenInclude(b => b.CropSeason)
        .Include(o => o.SellerOrders)
            .ThenInclude(so => so.OrderItems)
                .ThenInclude(oi => oi.OrderItemBatchAllocations)
                    .ThenInclude(a => a.HarvestBatch)
                        .ThenInclude(b => b.TraceabilityQrcode)
        .AsSplitQuery();

    private static List<SellerOrderGroupDto> SellerOrders(Order o) => o.SellerOrders
        .OrderBy(so => so.SellerOrderId)
        .Select(so => new SellerOrderGroupDto(
            SellerOrderId: so.SellerOrderId,
            FarmerProfileId: so.FarmerProfileId,
            FarmName: so.SellerNameSnapshot ?? so.FarmerProfile.FarmName,
            FarmLocation: so.FarmerProfile.Address,
            SellerOrderCode: so.SellerOrderCode,
            Status: so.Status,
            FinalAmount: so.FinalAmount,
            Items: so.OrderItems
                .OrderBy(oi => oi.OrderItemId)
                .Select(Item)
                .ToList()))
        .ToList();

    private static OrderItemDetailDto Item(OrderItem oi) => new(
        OrderItemId: oi.OrderItemId,
        ProductId: oi.ProductId,
        ProductName: oi.ProductNameSnapshot ?? oi.Product.ProductName,
        Quantity: oi.Quantity,
        Unit: oi.UnitSnapshot,
        UnitPrice: oi.UnitPriceSnapshot,
        SubTotal: oi.SubTotal,
        ImageUrl: oi.Product.ProductImages
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.DisplayOrder)
            .Select(image => image.ImageUrl)
            .FirstOrDefault(),
        IsReviewed: oi.ProductReview != null,
        Batches: oi.OrderItemBatchAllocations
            .OrderBy(a => a.AllocationSequence)
            .Select(a => new OrderItemBatchDto(
                a.HarvestBatch.BatchCode,
                a.AllocatedQuantity,
                a.HarvestBatch.ActualHarvestDate,
                a.HarvestBatch.CropSeason?.SeasonName,
                a.HarvestBatch.TraceabilityQrcode is { IsRevoked: false } qr
                    ? qr.TargetUrl : null))
            .ToList());

    private static string? TrackingNumber(Order o)
    {
        var numbers = o.SellerOrders.SelectMany(so => so.Shipments)
            .Select(s => s.TrackingNumber)
            .Where(number => !string.IsNullOrWhiteSpace(number))
            .Distinct()
            .ToList();
        return numbers.Count == 0 ? null : string.Join(", ", numbers);
    }

    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
