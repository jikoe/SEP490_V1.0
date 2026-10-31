using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgriSustain.Application.Services;

/// <summary>
/// UC-FAR-06: Dispatches in-system notifications and transactional e-mails to RetailCustomers.
/// Implements AF4: temporary e-mail failures do not rollback order transitions.
/// </summary>
public sealed class OrderNotificationService(
    IOrderRepository repository,
    INotificationGateway? notificationGateway = null,
    AgriSustainContext? context = null,
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
                title = refundableAmount > 0 ? "Đơn hàng đã hủy và hoàn tiền" : "Đơn hàng đã hủy";
                string refundNote = refundableAmount > 0
                    ? $"Khoản tiền hoàn {refundableAmount:N0} ₫ đã được xử lý hoàn tất về tài khoản nhận tiền đã xác thực của Quý khách."
                    : "Đơn hàng chưa phát sinh thanh toán nên không có khoản hoàn tiền.";
                inAppMessage = $"Đơn hàng {orderCode} đã bị từ chối bởi nông trại. Lý do: {reason}. {refundNote}";
                emailSubject = $"[AgriSustain] Thông báo hủy đơn hàng {orderCode}";
                emailBody = $"Kính gửi Quý khách,\n\nĐơn hàng {orderCode} của Quý khách đã bị từ chối/hủy bởi nông trại.\nLý do: {reason}\n{refundNote}\n\nNếu cần hỗ trợ thêm, Quý khách vui lòng liên hệ bộ phận CSKH.\n\nTrân trọng,\nĐội ngũ AgriSustain";
                break;

            default:
                title = "Cập nhật đơn hàng";
                inAppMessage = $"Đơn hàng {orderCode} có cập nhật mới.";
                emailSubject = $"[AgriSustain] Cập nhật đơn hàng {orderCode}";
                emailBody = inAppMessage;
                break;
        }

        if (context is null) { repository.Notify(buyerUserId,title,inAppMessage,order.SellerOrderId); return; }
        foreach(var channel in new[]{"InApp","Email"})
        {
            string eventKey=$"{key}:{eventCode}:{channel}";
            if(context.Notifications.Local.Any(n=>n.PayloadJson==eventKey)
                || await context.Notifications.AnyAsync(n=>n.NotificationType=="FarmerOrderUpdate" && n.PayloadJson==eventKey,cancellationToken)) continue;
            context.Notifications.Add(new Notification { UserId=buyerUserId,Title=channel=="Email"?emailSubject:title,
                Content=channel=="Email"?emailBody:inAppMessage,NotificationType="FarmerOrderUpdate",DeliveryChannel=channel,
                DeliveryStatus=channel=="InApp"?"Sent":"Pending",SentAt=DateTime.UtcNow,RelatedRecordType="SellerOrder",RelatedRecordId=order.SellerOrderId,PayloadJson=eventKey });
        }
    }
    public async Task RetryFailedChannelsAsync(string key, CancellationToken ct = default)
    {
        if(context is null) return;
        var prefix=key+":";
        var failed=await context.Notifications.Where(n=>n.NotificationType=="FarmerOrderUpdate" && n.PayloadJson!=null
            && n.PayloadJson.StartsWith(prefix) && n.DeliveryStatus=="Failed").ToListAsync(ct);
        foreach(var row in failed) row.DeliveryStatus="Pending";
        await context.SaveChangesAsync(ct);
    }
}
