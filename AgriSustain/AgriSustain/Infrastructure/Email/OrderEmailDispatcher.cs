using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AgriSustain.Infrastructure.Email;

public sealed class OrderEmailDispatcher(
    AgriSustainContext db, INotificationGateway gateway, IOptions<EmailSettings> options,
    OrderNotificationService notices, TimeProvider clock, ILogger<OrderEmailDispatcher> logger)
    : IComplaintEmailDelivery
{
    internal const string AccountEmailType =
        AgriSustain.Application.Services.Administration.AdministrationService.AccountEmailType;
    internal const int AccountEmailMaxAttempts = 3;

    public async Task DeliverAsync(long id, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var settings = options.Value;
        // Atomic claim prevents multiple application instances from sending the same queued row.
        int claimed = await db
            .Notifications.Where(n =>
                n.NotificationId == id
                && n.DeliveryChannel == "Email"
                && (
                    (
                        n.DeliveryStatus == "Pending"
                        || n.DeliveryStatus == "Failed" && n.SentAt <= now.AddMinutes(-1)
                    )
                    || n.DeliveryStatus == "Sending" && n.SentAt < now.AddMinutes(-5)
                )
            )
            .ExecuteUpdateAsync(
                set =>
                    set.SetProperty(n => n.DeliveryStatus, "Sending")
                        .SetProperty(n => n.SentAt, now),
                cancellationToken
            );
        if (claimed == 0)
        {
            return;
        }
        var notification = await db
            .Notifications.Include(n => n.User)
            .SingleAsync(n => n.NotificationId == id, cancellationToken);
        try
        {
            var content = notification.Content;
            var eventKey = notification.PayloadJson ?? $"order-notice:{id}";
            if (
                notification.NotificationType == OfftakeService.ProposalNotificationType
                && notification.RelatedRecordId is long contractId
            )
            {
                var link =
                    settings.FrontendBaseUrl.TrimEnd('/')
                    + OfftakeService.ProposalReviewPath(contractId);
                content += "\n\nXem đề xuất: " + link;
                eventKey = $"offtake-proposal-notice:{id}";
            }
            if (
                notification.NotificationType == "RefundEmail"
                && notification.PayloadJson?.StartsWith("rejected-order:") == true
            )
                content +=
                    "\n\nXem yêu cầu: "
                    + settings.FrontendBaseUrl.TrimEnd('/')
                    + "/FinanceStaff/rejected-order-refunds.html";
            if (notification.RelatedRecordType == "CustomerComplaintRequest"
                && notification.RelatedRecordId is long ticketId)
            {
                content += "\n\n" + settings.FrontendBaseUrl.TrimEnd('/')
                    + $"/RetailCustomer/complaints.html?id={ticketId}";
            }
            await gateway.SendEmailAsync(
                notification.User.Email,
                notification.Title,
                content,
                eventKey,
                cancellationToken
            );
            notices.RecordDeliveryResult(notification, true, clock.GetUtcNow().UtcDateTime);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            notices.RecordDeliveryResult(notification, false, clock.GetUtcNow().UtcDateTime);
            if (
                notification.NotificationType == AccountEmailType
                && notification.RetryCount >= AccountEmailMaxAttempts
            )
            {
                // JOB-09: stop after the retry limit and record the final failure.
                notification.DeliveryStatus = "PermanentFailure";
                logger.LogError(
                    "Account email {Id} failed after {Attempts} attempts.",
                    id,
                    notification.RetryCount
                );
            }
            logger.LogWarning("Email {Id} failed ({Type}).", id, ex.GetType().Name);
        }
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }
}
