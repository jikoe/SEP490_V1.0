namespace AgriSustain.Application.DTOs.Verification;

public sealed record KycApplicationSearch(
    string? Keyword = null,
    string? Search = null,
    string? Role = null,
    string? Status = null,
    string? VerificationStatus = null,
    int Page = 1,
    int PageSize = 10);

public sealed record KycApplicationSummaryDto(
    long UserId,
    string Username,
    string FullName,
    string? EntityName,
    string? OrganizationName,
    string? RepresentativeName,
    string Email,
    string? PhoneNumber,
    string Role,
    string AccountStatus,
    string VerificationStatus,
    string? IdentificationNumber,
    string? Address,
    int DocumentCount,
    DateTime SubmittedAt,
    DateTime? ReviewedAt,
    string? ReviewedBy);

public sealed record KycDocumentDto(
    long Id,
    long CertificateId,
    string DocumentType,
    string CertificateType,
    string DocumentNumber,
    string CertificateNumber,
    string IssuedBy,
    DateOnly? IssueDate,
    DateOnly? ExpiryDate,
    string? DocumentUrl,
    string VerificationStatus,
    string Status);

public sealed record KycApplicationDetailDto(
    long UserId,
    string Username,
    string FullName,
    string? EntityName,
    string? OrganizationName,
    string? RepresentativeName,
    string Email,
    string? PhoneNumber,
    string Role,
    string AccountStatus,
    bool IsEmailVerified,
    DateTime CreatedAt,
    DateTime SubmittedAt,
    string? Address,
    string? ProvinceCode,
    string? BusinessType,
    string? ProductionScale,
    string? CultivationZone,
    string? GpsCoordinates,
    string IdentificationType,
    string? IdentificationNumber,
    string VerificationStatus,
    DateTime? ApprovedAt,
    DateTime? ReviewedAt,
    string? ReviewedBy,
    string? RejectReason,
    IReadOnlyList<KycDocumentDto> Documents);

public sealed record KycReviewDecisionRequest(
    string? Action = null,
    string? Reason = null);

public sealed record KycReviewResultDto(
    long UserId,
    string VerificationStatus,
    string AccountStatus,
    string Message,
    DateTime ReviewedAt,
    string? ReviewedBy,
    string? Reason);
