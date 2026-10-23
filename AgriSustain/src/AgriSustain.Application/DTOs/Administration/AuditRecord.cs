namespace AgriSustain.Application.DTOs.Administration;

public sealed record AuditRecord(
    long ActorId, string EntityType, long TargetId, string Action,
    string OldValue, string NewValue, string? Reason, DateTimeOffset OccurredAt);
