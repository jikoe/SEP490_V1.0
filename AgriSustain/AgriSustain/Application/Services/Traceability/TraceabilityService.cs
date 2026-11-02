using AgriSustain.Application.Common;

namespace AgriSustain.Application.Services.Traceability;

// UC-TRC-01 View Product Traceability (public, read-only).

public sealed record TraceCultivationLogDto(
    DateTime ActionDate,
    string ActionType,
    string? DetailDescription,
    string? InputMaterial,
    string? Dosage,
    string? PerformedBy,
    string? ImageUrl);

public sealed record TraceCertificateDto(
    long FarmCertificateId,
    string CertificateType,
    string CertificateNumber,
    string IssuedBy,
    DateOnly IssueDate,
    DateOnly? ExpiryDate,
    DateTime? ReviewedAt);

public sealed record TraceabilityDto(
    string BatchCode,
    bool IsRevoked,
    string? RecallMessage,
    long ProductId,
    string ProductName,
    string? ProductImageUrl,
    string Unit,
    string? OriginProvince,
    DateOnly? HarvestDate,
    bool HarvestDateIsEstimated,
    DateOnly? ExpirationDate,
    string? QualityNotes,
    string FarmName,
    string? CultivationZone,
    string FarmAddress,
    decimal ReputationScore,
    string SeasonName,
    string CropVariety,
    DateOnly PlantingDate,
    int ScanCount,
    IReadOnlyList<TraceCultivationLogDto> CultivationLogs,
    IReadOnlyList<TraceCertificateDto> Certificates);

public interface ITraceabilityRepository
{
    /// <summary>
    /// Finds the batch by batch code, QR content string or QR target URL. Returns null when no
    /// batch matches. When the matched QR is not revoked its ScanCount is incremented (Step 4).
    /// </summary>
    Task<TraceabilityDto?> FindAndCountScanAsync(string code, DateOnly today, CancellationToken cancellationToken);
}

public interface ITraceabilityService
{
    Task<Result<TraceabilityDto>> LookupAsync(string? code, CancellationToken cancellationToken);
}

public sealed class TraceabilityService(ITraceabilityRepository repository, TimeProvider timeProvider)
    : ITraceabilityService
{
    // AF1
    public const string InvalidCodeMessage = "Mã lô / QR không hợp lệ hoặc không tồn tại.";

    // AF2
    public const string RecallMessage =
        "Lô hàng này đã bị thu hồi. Vui lòng không sử dụng sản phẩm và liên hệ người bán hoặc AgriSustain.";

    public async Task<Result<TraceabilityDto>> LookupAsync(string? code, CancellationToken cancellationToken)
    {
        string normalized = Normalize(code);
        if (normalized.Length is 0 or > 500)
        {
            return Result<TraceabilityDto>.FailureResult(new Error("INVALID_CODE", InvalidCodeMessage, "Validation",
                new Dictionary<string, string[]> { ["code"] = [InvalidCodeMessage] }));
        }

        var found = await repository.FindAndCountScanAsync(normalized,
            DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime), cancellationToken);
        if (found is null)
        {
            return Result<TraceabilityDto>.FailureResult(new Error("TRACE_NOT_FOUND", InvalidCodeMessage, "NotFound"));
        }

        // AF2: a revoked batch shows only the recall warning, never the origin details.
        return Result<TraceabilityDto>.SuccessResult(found.IsRevoked
            ? found with
            {
                RecallMessage = RecallMessage,
                CultivationLogs = [],
                Certificates = [],
                QualityNotes = null
            }
            : found);
    }

    /// <summary>Accepts "BATCH-01", " batch-01 " or a scanned URL ".../trace/BATCH-01".</summary>
    public static string Normalize(string? code)
    {
        string value = code?.Trim() ?? string.Empty;
        int marker = value.LastIndexOf("/trace/", StringComparison.OrdinalIgnoreCase);
        return marker >= 0 ? value[(marker + "/trace/".Length)..].Trim('/', ' ') : value;
    }
}
