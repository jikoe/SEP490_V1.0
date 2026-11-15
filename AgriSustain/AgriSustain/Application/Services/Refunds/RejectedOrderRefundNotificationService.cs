using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services.Refunds;

public sealed class RejectedOrderRefundNotificationService(IFinanceRejectedOrderRefundRepository repository)
{
    // Queued in the settlement transaction. JOB-09 retries email delivery independently.
    public void QueueSucceeded(RejectedOrderRefundContext context, DateTime now)
    {
        var r = context.Refund;
        string content = RejectedOrderRefundMessages.Succeeded(r.RefundCode, context.Order.SellerOrderCode,
            r.Amount, r.ReceiverAccountNumber, r.GatewayReference!);
        foreach (string channel in new[] { "InApp", "Email" })
        {
            repository.Notify(new Notification
            {
                UserId = context.Order.Order.BuyerUserId,
                Title = "Kết quả hoàn tiền " + r.RefundCode,
                Content = content,
                NotificationType = "RefundEmail",
                DeliveryChannel = channel,
                DeliveryStatus = channel == "Email" ? "Pending" : "Sent",
                RelatedRecordType = "RefundTransaction",
                RelatedRecordId = r.RefundTransactionId,
                PayloadJson = $"rejected-refund:{r.RefundTransactionId}:succeeded:{channel}",
                SentAt = now
            });
        }
    }
}
