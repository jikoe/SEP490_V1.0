using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgriSustain.Application.Services;

/// <summary>
/// UC-FAR-06: Dispatches in-system notifications and transactional e-mails to RetailCustomers.
/// Implements AF4: temporary e-mail failures do not rollback order transitions.
/// </summary>
public sealed class OrderNotificationService(
    IOrderRepository repository,
    INotificationGateway? notificationGateway = null,
    ILogger<OrderNotificationService>? logger = null)
{
    public async Task NotifyRetailCustomerAsync(
        SellerOrder order,
        string eventCode,
        string key,
        string? reason = null,
        decimal refundableAmount = 0,
        CancellationToken cancellationToken = default)
    {
        long buyerUserId = order.Order.BuyerUserId;
        string orderCode = order.SellerOrderCode;
        string title;
        string inAppMessage;
        string emailSubject;
        string emailBody;

        switch (eventCode)
        {
            case "MSG185": // Confirmed / Preparing
                title = "Đơn hàng đã được xác nhận";
                inAppMessage = $"Đơn hàng {orderCode} đã được nông trại xác nhận và đang được chuẩn bị.";
                emailSubject = $"[AgriSustain] Đơn hàng {orderCode} đã được xác nhận";
                emailBody = $"Kính gửi Quý khách,\n\nĐơn hàng {orderCode} của Quý khách đã được nông trại xác nhận và đang trong quá trình chuẩn bị sản phẩm.\nQuý khách có thể theo dõi tiến độ trên hệ thống AgriSustain.\n\nTrân trọng,\nĐội ngũ AgriSustain";
                break;

            case "MSG65": // Start shipping / In transit
                title = "Đơn hàng đang giao";
                inAppMessage = $"Đơn hàng {orderCode} đã bắt đầu giao mô phỏng. Bạn có thể theo dõi tiến độ tại Chi tiết đơn hàng.";
                emailSubject = $"[AgriSustain] Đơn hàng {orderCode} đang được vận chuyển";
                emailBody = $"Kính gửi Quý khách,\n\nĐơn hàng {orderCode} của Quý khách đã xuất kho và đang trong quá trình giao hàng mô phỏng.\nQuý khách có thể kiểm tra trạng thái đơn tại mục Đơn hàng của tôi.\n\nTrân trọng,\nĐội ngũ AgriSustain";
                break;

            case "MSG186": // Order canceled / rejected
                title = "Đơn hàng đã hủy";
                inAppMessage = AgriSustain.Application.Common.RejectedOrderRefundMessages.Cancellation(orderCode, reason, refundableAmount);
                emailSubject = $"[AgriSustain] Thông báo hủy đơn hàng {orderCode}";
                emailBody = inAppMessage;
                break;

            default:
                title = "Cập nhật đơn hàng";
                inAppMessage = $"Đơn hàng {orderCode} có cập nhật mới.";
                emailSubject = $"[AgriSustain] Cập nhật đơn hàng {orderCode}";
                emailBody = inAppMessage;
                break;
        }

        foreach(var channel in new[]{"InApp","Email"})
        {
            string eventKey=$"{key}:{eventCode}:{channel}";
            if(await repository.NotificationKeyExistsAsync(eventKey,cancellationToken)) continue;
            repository.SaveNotificationsAsync(new Notification { UserId=buyerUserId,Title=channel=="Email"?emailSubject:title,
                Content=channel=="Email"?emailBody:inAppMessage,NotificationType="FarmerOrderUpdate",DeliveryChannel=channel,
                DeliveryStatus=channel=="InApp"?"Sent":"Pending",SentAt=DateTime.UtcNow,RelatedRecordType="SellerOrder",RelatedRecordId=order.SellerOrderId,PayloadJson=eventKey });
        }
    }
    public async Task RetryFailedChannelsAsync(string key, CancellationToken ct = default)
    {
        await repository.RequeueFailedOrderNotificationsAsync(key+":",ct);
    }
    public void RecordDeliveryResult(Notification notification, bool delivered, DateTime now)
    {
        notification.DeliveryStatus = delivered ? "Sent" : "Failed";
        if (!delivered) notification.RetryCount++;
        notification.SentAt = now;
    }
}
