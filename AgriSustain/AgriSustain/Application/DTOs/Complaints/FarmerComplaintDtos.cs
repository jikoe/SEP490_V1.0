namespace AgriSustain.Application.DTOs.Complaints;

public sealed record FarmerComplaintQuery(
    string? Keyword = null,
    string? Status = null,
    string? RequestStatus = null,
    int Page = 1);

public sealed record FarmerComplaintResponseRequest(
    long EvidenceRequestId,
    string Explanation,
    string[] EvidenceUrls,
    string Version,
    string RequestKey);

public sealed record FarmerComplaintFile(long Id, string Name, string Url, string? Description);

public sealed record FarmerComplaintRequest(
    long Id, string? Instructions, DateTime RequestedAt, DateTime DueAt,
    DateTime? RespondedAt, string Status, bool CanRespond);

public sealed record FarmerComplaintHistory(
    long RequestId, string Explanation, DateTime SubmittedAt,
    IReadOnlyList<FarmerComplaintFile> Files);

public sealed record FarmerComplaintDetail(
    long Id, string OrderCode, string BuyerName, string Reason, string? Description,
    string Status, DateTime? DeliveredAt, DateTime? ComplaintDeadline,
    string? Resolution, decimal? RefundAmount, string? RefundStatus,
    IReadOnlyList<FarmerComplaintRequest> Requests,
    IReadOnlyList<FarmerComplaintFile> BuyerEvidence,
    IReadOnlyList<FarmerComplaintHistory> History,
    string Version, bool NotificationPending, string? Message = null);

public sealed record FarmerComplaintListItem(
    long Id, string OrderCode, string Reason, string Status,
    string RequestStatus, DateTime? DueAt);

public sealed record FarmerComplaintPage(
    IReadOnlyList<FarmerComplaintListItem> Items, int Total, int Page, int PageSize = 10);
