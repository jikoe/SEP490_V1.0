using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services.Complaints;

internal static class ComplaintNotifications
{
    public static readonly string[] SupportRoles = ["CustomerSupport", "Customer Support"];

    public static IEnumerable<Notification> Create(
        long userId, long ticketId, string title, string content, DateTime now,
        string relatedRecordType = "DisputeTicket")
    {
        yield return new Notification
        {
            UserId = userId,
            Title = title,
            Content = content,
            NotificationType = "Complaint",
            DeliveryChannel = "InApp",
            DeliveryStatus = "Sent",
            SentAt = now,
            RelatedRecordType = relatedRecordType,
            RelatedRecordId = ticketId
        };
        yield return new Notification
        {
            UserId = userId,
            Title = title,
            Content = content,
            NotificationType = ComplaintResolutionService.EmailNotificationType,
            DeliveryChannel = "Email",
            DeliveryStatus = "Pending",
            SentAt = now,
            RelatedRecordType = relatedRecordType,
            RelatedRecordId = ticketId,
            PayloadJson = $"complaint:{ticketId}:{Guid.NewGuid():N}"
        };
    }
}
