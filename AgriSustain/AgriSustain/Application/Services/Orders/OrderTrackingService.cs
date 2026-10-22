using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Application.Services.Orders;

public sealed class OrderTrackingService(
    AgriSustainContext context,
    ICurrentUser currentUser,
    IValidator<OrderQuery> queryValidator) : IOrderTrackingService
{
    public async Task<Result<PagedResponse<OrderSummaryDto>>> GetOrderHistoryAsync(
        OrderQuery query, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(queryValidator, query, cancellationToken);
        if (error != null)
        {
            return Result<PagedResponse<OrderSummaryDto>>.FailureResult(error);
        }

        long buyerId = currentUser.UserId ?? 1;

        var ordersQuery = context.Orders
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
                o.OrderCode.Contains(kw) ||
                o.SellerOrders.Any(so => so.OrderItems.Any(oi => (oi.ProductNameSnapshot ?? "").Contains(kw))));
        }

        int totalCount = await ordersQuery.CountAsync(cancellationToken);

        var orders = await ordersQuery
            .OrderByDescending(o => o.OrderDate)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(o => o.SellerOrders)
                .ThenInclude(so => so.FarmerProfile)
            .Include(o => o.SellerOrders)
                .ThenInclude(so => so.OrderItems)
            .ToListAsync(cancellationToken);

        var dtoList = orders.Select(o => new OrderSummaryDto(
            OrderId: o.OrderId,
            OrderCode: o.OrderCode,
            OrderDate: new DateTimeOffset(DateTime.SpecifyKind(o.OrderDate, DateTimeKind.Utc)),
            OrderStatus: o.OrderStatus,
            PaymentMethod: o.PaymentMethod ?? "VNPay / COD",
            DeliveryMethod: o.DeliveryMethod ?? "Giao hàng tiêu chuẩn",
            TotalAmount: o.TotalAmount,
            DeliveryFee: o.DeliveryFee,
            DiscountAmount: o.DiscountAmount,
            FinalAmount: o.FinalAmount,
            RecipientName: o.RecipientNameSnapshot,
            RecipientPhone: o.RecipientPhoneSnapshot,
            ShippingAddress: o.ShippingAddress,
            TrackingNumber: "VNPOST-" + o.OrderId,
            SellerOrders: o.SellerOrders.Select(so => new SellerOrderGroupDto(
                SellerOrderId: so.SellerOrderId,
                FarmerProfileId: so.FarmerProfileId,
                FarmName: so.FarmerProfile?.FarmName ?? "Hợp tác xã Nông nghiệp",
                FarmLocation: so.FarmerProfile?.Address,
                SellerOrderCode: so.SellerOrderCode,
                Status: so.Status,
                FinalAmount: so.FinalAmount,
                Items: so.OrderItems.Select(oi => new OrderItemDetailDto(
                    OrderItemId: oi.OrderItemId,
                    ProductId: oi.ProductId,
                    ProductName: oi.ProductNameSnapshot ?? "Nông sản sạch",
                    Quantity: oi.Quantity,
                    Unit: oi.UnitSnapshot ?? "Kg",
                    UnitPrice: oi.UnitPriceSnapshot,
                    SubTotal: oi.SubTotal,
                    HarvestBatchCode: "BATCH-" + oi.OrderItemId,
                    HarvestDate: DateOnly.FromDateTime(o.OrderDate.AddDays(-2)),
                    FarmPlot: "Lô A - Vùng canh tác chuẩn VietGAP",
                    IsReviewed: false,
                    ImageEmoji: "🥬")).ToList()
            )).ToList()
        )).ToList();

        return Result<PagedResponse<OrderSummaryDto>>.SuccessResult(
            new PagedResponse<OrderSummaryDto>(dtoList, totalCount, query.Page, query.PageSize));
    }

    public async Task<Result<OrderDetailDto>> GetOrderDetailAsync(
        long orderId, CancellationToken cancellationToken)
    {
        long buyerId = currentUser.UserId ?? 1;

        var o = await context.Orders
            .AsNoTracking()
            .Include(o => o.SellerOrders)
                .ThenInclude(so => so.FarmerProfile)
            .Include(o => o.SellerOrders)
                .ThenInclude(so => so.OrderItems)
            .FirstOrDefaultAsync(o => o.OrderId == orderId && o.BuyerUserId == buyerId, cancellationToken);

        if (o == null)
        {
            return Result<OrderDetailDto>.FailureResult(new Error(
                "ORDER_NOT_FOUND", $"Không tìm thấy đơn hàng #{orderId}.", "NotFound"));
        }

        var timeline = new List<OrderStatusTimelineDto>
        {
            new("PendingPayment", new DateTimeOffset(DateTime.SpecifyKind(o.OrderDate, DateTimeKind.Utc)), "Khách hàng tạo đơn"),
            new(o.OrderStatus, new DateTimeOffset(DateTime.SpecifyKind(o.OrderDate.AddHours(2), DateTimeKind.Utc)), $"Trạng thái đơn: {o.OrderStatus}")
        };

        var detail = new OrderDetailDto(
            OrderId: o.OrderId,
            OrderCode: o.OrderCode,
            OrderDate: new DateTimeOffset(DateTime.SpecifyKind(o.OrderDate, DateTimeKind.Utc)),
            OrderStatus: o.OrderStatus,
            PaymentMethod: o.PaymentMethod ?? "VNPay / COD",
            DeliveryMethod: o.DeliveryMethod ?? "Giao hàng tiêu chuẩn",
            TotalAmount: o.TotalAmount,
            DeliveryFee: o.DeliveryFee,
            DiscountAmount: o.DiscountAmount,
            FinalAmount: o.FinalAmount,
            RecipientName: o.RecipientNameSnapshot,
            RecipientPhone: o.RecipientPhoneSnapshot,
            ShippingAddress: o.ShippingAddress,
            TrackingNumber: "VNPOST-" + o.OrderId,
            PaymentStatus: o.OrderStatus == "Completed" ? "Paid" : "Pending",
            ExpectedDeliveryDate: o.ExpectedDeliveryDate.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(o.ExpectedDeliveryDate.Value, DateTimeKind.Utc)) : null,
            SellerOrders: o.SellerOrders.Select(so => new SellerOrderGroupDto(
                SellerOrderId: so.SellerOrderId,
                FarmerProfileId: so.FarmerProfileId,
                FarmName: so.FarmerProfile?.FarmName ?? "Hợp tác xã Nông nghiệp",
                FarmLocation: so.FarmerProfile?.Address,
                SellerOrderCode: so.SellerOrderCode,
                Status: so.Status,
                FinalAmount: so.FinalAmount,
                Items: so.OrderItems.Select(oi => new OrderItemDetailDto(
                    OrderItemId: oi.OrderItemId,
                    ProductId: oi.ProductId,
                    ProductName: oi.ProductNameSnapshot ?? "Nông sản sạch",
                    Quantity: oi.Quantity,
                    Unit: oi.UnitSnapshot ?? "Kg",
                    UnitPrice: oi.UnitPriceSnapshot,
                    SubTotal: oi.SubTotal,
                    HarvestBatchCode: "BATCH-" + oi.OrderItemId,
                    HarvestDate: DateOnly.FromDateTime(o.OrderDate.AddDays(-2)),
                    FarmPlot: "Lô A - Vùng canh tác chuẩn VietGAP",
                    IsReviewed: false,
                    ImageEmoji: "🥬")).ToList()
            )).ToList(),
            Timeline: timeline);

        return Result<OrderDetailDto>.SuccessResult(detail);
    }

    public async Task<Result<OrderDetailDto>> GetOrderDetailByCodeAsync(
        string orderCode, CancellationToken cancellationToken)
    {
        long buyerId = currentUser.UserId ?? 1;

        var o = await context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderCode == orderCode && o.BuyerUserId == buyerId, cancellationToken);

        if (o == null)
        {
            return Result<OrderDetailDto>.FailureResult(new Error(
                "ORDER_NOT_FOUND", $"Không tìm thấy đơn hàng có mã {orderCode}.", "NotFound"));
        }

        return await GetOrderDetailAsync(o.OrderId, cancellationToken);
    }
}
