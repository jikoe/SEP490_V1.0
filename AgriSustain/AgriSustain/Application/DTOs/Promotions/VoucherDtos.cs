namespace AgriSustain.Application.DTOs.Promotions;

/// <summary>Status is a derived lifecycle status; DiscountType is the SRS "Item Type" filter.</summary>
public sealed record VoucherQuery(
    string? Status,
    string? DiscountType,
    string? Search,
    int Page = 1,
    int PageSize = 10);

public sealed record VoucherDto(
    long VoucherId,
    string VoucherCode,
    string DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderAmount,
    int? UsageLimit,
    int UsedCount,
    int ReservedCount,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive,
    string Status,
    bool TermsLocked);

public sealed record VoucherListDto(
    IReadOnlyList<VoucherDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record SaveVoucherRequest(
    string VoucherCode,
    string DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderAmount,
    int? UsageLimit,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    bool IsActive);

public sealed record VoucherActionResponse(
    long VoucherId,
    string Message,
    VoucherDto? Voucher);
