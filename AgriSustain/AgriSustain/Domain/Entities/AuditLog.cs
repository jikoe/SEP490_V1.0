using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class AuditLog
{
    public long AuditLogId { get; set; }

    public long? PerformedByUserId { get; set; }

    public string EntityType { get; set; } = null!;

    public string Action { get; set; } = null!;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? Reason { get; set; }

    public long? EntityId { get; set; }

    public string? TraceId { get; set; }

    public DateTime OccurredAt { get; set; }

    public virtual User? PerformedByUser { get; set; }
}
