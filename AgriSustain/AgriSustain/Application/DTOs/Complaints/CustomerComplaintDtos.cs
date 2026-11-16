namespace AgriSustain.Application.DTOs.Complaints;

public sealed record CustomerComplaintQuery(string? Status, long? OrderId, int Page = 1);

public sealed record CustomerComplaintListItem(
    long Id, string OrderCode, string Reason, string Status, bool NeedsResponse);

public sealed record CustomerComplaintResponseRequest(
    long EvidenceRequestId, string? Explanation, string[]? EvidenceUrls,
    string? RequestKey, string? Version);

public sealed record CustomerComplaintFile(long Id, string Name);

public sealed record CustomerComplaintRequest(
    long Id, string? Instructions, DateTimeOffset RequestedAt, DateTimeOffset DueAt,
    DateTimeOffset? RespondedAt, string Status, bool CanRespond, string? Explanation,
    IReadOnlyList<CustomerComplaintFile> Files);

public sealed record CustomerComplaintDetail(
    long Id, string OrderCode, string Reason, string? Description, string Status,
    string? Resolution, decimal? RefundAmount, string? RefundStatus,
    IReadOnlyList<CustomerComplaintRequest> Requests,
    IReadOnlyList<CustomerComplaintFile> OriginalEvidence, string Version);

public sealed record CustomerComplaintResponse(string Message);
