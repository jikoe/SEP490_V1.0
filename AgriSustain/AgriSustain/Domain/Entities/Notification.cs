using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class Notification
{
    public long NotificationId { get; set; }

    public long UserId { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string NotificationType { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime SentAt { get; set; }

    public string DeliveryChannel { get; set; } = null!;

    public string DeliveryStatus { get; set; } = null!;

    public int RetryCount { get; set; }

    public string? RelatedRecordType { get; set; }

    public virtual User User { get; set; } = null!;
}
