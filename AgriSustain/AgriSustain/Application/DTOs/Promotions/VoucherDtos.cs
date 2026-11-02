using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.DTOs.Promotions;

/// <summary>Status is a derived lifecycle status (BR-PV-06); Search matches the code or the name.</summary>
public sealed record VoucherQuery(
    string? Status,
    string? DiscountType,
    string? Search,
    int Page = 1,
    int PageSize = 10);

/// <summary>
/// TermsLocked follows BR-PV-07; Version is sent back on a save or status change so a stale screen is
/// rejected (MSG-PV-15).
/// </summary>
public sealed record VoucherDto(
    long VoucherId,
    string VoucherCode,
    string VoucherName,
    string? Description,
    string DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderAmount,
    int? UsageLimit,
    int PerCustomerLimit,
    int UsedCount,
    int ReservedCount,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive,
    string Status,
    bool TermsLocked,
    bool CanDelete,
    bool CanReactivate,
    string Version)
{
    public static VoucherDto From(Voucher voucher, int reserved, int redemptions, DateTime now)
    {
        string status = voucher.LifecycleStatus(now);
        return new VoucherDto(voucher.VoucherId, voucher.VoucherCode, voucher.VoucherName,
            voucher.Description, voucher.DiscountType, voucher.DiscountValue, voucher.MaxDiscountAmount,
            voucher.MinOrderAmount, voucher.UsageLimit, voucher.PerCustomerLimit, voucher.UsedCount, reserved,
            voucher.StartDate, voucher.EndDate, voucher.IsActive, status,
            voucher.TermsLocked(now, redemptions),
            // BR-PV-09: never redeemed or reserved, and not currently Active.
            voucher.UsedCount == 0 && redemptions == 0 && status != Voucher.StatusActive,
            !voucher.IsActive && voucher.CanReactivate(now),
            voucher.Version());
    }
}

public sealed record VoucherListDto(
    IReadOnlyList<VoucherDto> Items,
    int Total,
    int Page,
    int PageSize);

/// <summary>Version is the token of the voucher being edited; it is ignored when creating.</summary>
public sealed record SaveVoucherRequest(
    string VoucherName,
    string VoucherCode,
    string? Description,
    string DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderAmount,
    int UsageLimit,
    int PerCustomerLimit,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string? Version = null);

public sealed record VoucherActionResponse(
    long VoucherId,
    string Message,
    VoucherDto? Voucher);
