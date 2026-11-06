using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Promotions;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Validators.Promotions;
using AgriSustain.Domain.Entities;
using FluentValidation;

namespace AgriSustain.Application.Services.Promotions;

/// <summary>
/// UC-PS-01 Manage Promotions and Vouchers (BR-PV-01..12). Codes are stored upper-case and unique
/// system-wide. A new voucher is saved as Scheduled or Active by its start time. Once a voucher is used,
/// reserved or has started, only its name, description, a later end and a higher total limit can change.
/// A voucher that was used or is Active cannot be deleted; it can only be deactivated.
/// </summary>
public sealed class VoucherManagementService(
    IVoucherRepository repository,
    IActorContext actor,
    IValidator<SaveVoucherRequest> validator,
    TimeProvider timeProvider) : IVoucherManagementService
{
    private const int MaximumPageSize = 100;

    // A start time picked in the current minute is not "in the past" (BR-PV-04).
    private static readonly TimeSpan StartTolerance = TimeSpan.FromMinutes(1);

    private static readonly Dictionary<string, string> s_statusLabels = new()
    {
        [Voucher.StatusScheduled] = "Chờ bắt đầu",
        [Voucher.StatusActive] = "Đang áp dụng",
        [Voucher.StatusExpired] = "Hết hạn",
        [Voucher.StatusExhausted] = "Hết lượt",
        [Voucher.StatusDeactivated] = "Ngừng áp dụng"
    };

    private static readonly Error s_notFound = new(
        "VOUCHER_NOT_FOUND", "Không tìm thấy voucher.", "NotFound");

    // MSG-PV-15
    private static readonly Error s_changed = new(
        "VOUCHER_CHANGED",
        "Voucher đã được người khác cập nhật. Dữ liệu đã được tải lại, vui lòng kiểm tra và thử lại.",
        "Conflict");

    public async Task<Result<VoucherListDto>> SearchAsync(
        VoucherQuery query,
        CancellationToken cancellationToken = default)
    {
        var normalized = query with
        {
            Status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim(),
            DiscountType = string.IsNullOrWhiteSpace(query.DiscountType) ? null : query.DiscountType.Trim(),
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            Page = Math.Max(query.Page, 1),
            PageSize = Math.Clamp(query.PageSize, 1, MaximumPageSize)
        };
        var (total, items) = await repository.SearchAsync(normalized, Now(), cancellationToken);
        return Result<VoucherListDto>.SuccessResult(
            new VoucherListDto(items, total, normalized.Page, normalized.PageSize));
    }

    public async Task<Result<VoucherDto>> GetAsync(
        long voucherId,
        CancellationToken cancellationToken = default)
    {
        var voucher = await repository.GetPlatformVoucherAsync(voucherId, cancellationToken);
        return voucher is null
            ? Result<VoucherDto>.FailureResult(s_notFound)
            : Result<VoucherDto>.SuccessResult(await ToDtoAsync(voucher, cancellationToken));
    }

    public async Task<Result<VoucherActionResponse>> CreateAsync(
        SaveVoucherRequest request,
        CancellationToken cancellationToken = default)
    {
        var error = await RequestValidation.CheckAsync(validator, request, cancellationToken);
        if (error is not null)
        {
            return Result<VoucherActionResponse>.FailureResult(error);
        }

        var now = Now();
        // BR-PV-04: a new voucher cannot start in the past.
        if (request.StartDate.UtcDateTime < now - StartTolerance)
        {
            return Result<VoucherActionResponse>.FailureResult(FieldError(
                "INVALID_PERIOD", "startDate", SaveVoucherRequestValidator.PeriodMessage));
        }

        string code = NormalizeCode(request.VoucherCode);
        if (await repository.CodeExistsAsync(code, null, cancellationToken))
        {
            return Result<VoucherActionResponse>.FailureResult(DuplicateCode());
        }

        // BR-PV-06: saved as Scheduled or Active according to its start time.
        var voucher = new Voucher { UsedCount = 0, IsActive = true };
        Apply(voucher, request, code);
        await repository.AddWithAuditAsync(voucher, () => AddAudit(voucher, "CreateVoucher", null, now),
            cancellationToken);
        return await SavedAsync(voucher, cancellationToken);
    }

    public async Task<Result<VoucherActionResponse>> UpdateAsync(
        long voucherId,
        SaveVoucherRequest request,
        CancellationToken cancellationToken = default)
    {
        var error = await RequestValidation.CheckAsync(validator, request, cancellationToken);
        if (error is not null)
        {
            return Result<VoucherActionResponse>.FailureResult(error);
        }

        var voucher = await repository.GetPlatformVoucherAsync(voucherId, cancellationToken);
        if (voucher is null)
        {
            return Result<VoucherActionResponse>.FailureResult(s_notFound);
        }

        if (request.Version is not null && request.Version != voucher.Version())
        {
            return Result<VoucherActionResponse>.FailureResult(s_changed);
        }

        var now = Now();
        string code = NormalizeCode(request.VoucherCode);
        var start = request.StartDate.UtcDateTime;
        var end = request.EndDate.UtcDateTime;
        int redemptions = await repository.CountRedemptionsAsync(voucherId, cancellationToken);
        if (voucher.TermsLocked(now, redemptions))
        {
            // BR-PV-07: only the name, description, a later end and a higher total limit may change.
            bool sameTerms = voucher.HasSameTerms(code, request.DiscountType, request.DiscountValue,
                request.MaxDiscountAmount, request.MinOrderAmount, request.PerCustomerLimit, start);
            if (!sameTerms || end < voucher.EndDate || request.UsageLimit < (voucher.UsageLimit ?? 0))
            {
                return Result<VoucherActionResponse>.FailureResult(TermsLocked());
            }
        }
        else if (start != voucher.StartDate && start < now - StartTolerance)
        {
            return Result<VoucherActionResponse>.FailureResult(FieldError(
                "INVALID_PERIOD", "startDate", SaveVoucherRequestValidator.PeriodMessage));
        }

        int reserved = await repository.CountReservedAsync(voucherId, cancellationToken);
        if (request.UsageLimit < voucher.UsedCount + reserved)
        {
            return Result<VoucherActionResponse>.FailureResult(FieldError("USAGE_LIMIT_TOO_LOW",
                "usageLimit",
                $"Tổng lượt sử dụng không được nhỏ hơn số lượt đã dùng hoặc đang giữ ({voucher.UsedCount + reserved})."));
        }

        if (code != voucher.VoucherCode
            && await repository.CodeExistsAsync(code, voucherId, cancellationToken))
        {
            return Result<VoucherActionResponse>.FailureResult(DuplicateCode());
        }

        var before = Snapshot(voucher);
        Apply(voucher, request, code);
        AddAudit(voucher, "UpdateVoucher", before, now);
        await repository.SaveChangesAsync(cancellationToken);
        return await SavedAsync(voucher, cancellationToken);
    }

    public async Task<Result<VoucherActionResponse>> SetActiveAsync(
        long voucherId,
        bool isActive,
        string? version,
        CancellationToken cancellationToken = default)
    {
        var voucher = await repository.GetPlatformVoucherAsync(voucherId, cancellationToken);
        if (voucher is null)
        {
            return Result<VoucherActionResponse>.FailureResult(s_notFound);
        }

        if (version is not null && version != voucher.Version())
        {
            return Result<VoucherActionResponse>.FailureResult(s_changed);
        }

        var now = Now();
        if (isActive && !voucher.IsActive && !voucher.CanReactivate(now))
        {
            // MSG-PV-10
            return Result<VoucherActionResponse>.FailureResult(new Error("VOUCHER_CANNOT_REACTIVATE",
                $"Không thể kích hoạt lại voucher {voucher.VoucherCode} vì đã hết hạn hoặc hết lượt dùng.",
                "Conflict"));
        }

        if (voucher.IsActive != isActive)
        {
            var before = Snapshot(voucher);
            voucher.IsActive = isActive;
            AddAudit(voucher, isActive ? "ReactivateVoucher" : "DeactivateVoucher", before, now);
            await repository.SaveChangesAsync(cancellationToken);
        }

        // MSG-PV-09
        return Result<VoucherActionResponse>.SuccessResult(new VoucherActionResponse(
            voucher.VoucherId,
            $"Đã {(isActive ? "kích hoạt lại" : "ngừng áp dụng")} voucher {voucher.VoucherCode}.",
            await ToDtoAsync(voucher, cancellationToken)));
    }

    public async Task<Result<VoucherActionResponse>> DeleteAsync(
        long voucherId,
        string? version,
        CancellationToken cancellationToken = default)
    {
        var voucher = await repository.GetPlatformVoucherAsync(voucherId, cancellationToken);
        if (voucher is null)
        {
            return Result<VoucherActionResponse>.FailureResult(s_notFound);
        }

        if (version is not null && version != voucher.Version())
        {
            return Result<VoucherActionResponse>.FailureResult(s_changed);
        }

        var now = Now();
        int redemptions = await repository.CountRedemptionsAsync(voucherId, cancellationToken);
        // BR-PV-09: never redeemed or reserved and not Active; otherwise deactivate instead.
        if (voucher.UsedCount > 0 || redemptions > 0 || voucher.LifecycleStatus(now) == Voucher.StatusActive)
        {
            // MSG-PV-11
            return Result<VoucherActionResponse>.FailureResult(new Error("VOUCHER_CANNOT_DELETE",
                $"Voucher {voucher.VoucherCode} đã được sử dụng hoặc đang áp dụng nên không thể xoá. "
                    + "Hãy ngừng áp dụng thay vì xoá.",
                "Conflict"));
        }

        AddAudit(voucher, "DeleteVoucher", Snapshot(voucher), now);
        repository.Remove(voucher);
        await repository.SaveChangesAsync(cancellationToken);
        return Result<VoucherActionResponse>.SuccessResult(new VoucherActionResponse(
            voucherId, $"Đã xoá voucher {voucher.VoucherCode}.", null));
    }

    private static void Apply(Voucher voucher, SaveVoucherRequest request, string code)
    {
        voucher.VoucherName = request.VoucherName.Trim();
        voucher.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        voucher.VoucherCode = code;
        voucher.DiscountType = request.DiscountType;
        voucher.DiscountValue = request.DiscountValue;
        voucher.MaxDiscountAmount = request.MaxDiscountAmount;
        voucher.MinOrderAmount = request.MinOrderAmount;
        voucher.UsageLimit = request.UsageLimit;
        voucher.PerCustomerLimit = request.PerCustomerLimit;
        voucher.StartDate = request.StartDate.UtcDateTime;
        voucher.EndDate = request.EndDate.UtcDateTime;
    }

    private async Task<Result<VoucherActionResponse>> SavedAsync(Voucher voucher,
        CancellationToken cancellationToken = default)
    {
        var dto = await ToDtoAsync(voucher, cancellationToken);
        // MSG-PV-08
        return Result<VoucherActionResponse>.SuccessResult(new VoucherActionResponse(
            voucher.VoucherId,
            $"Đã lưu voucher {voucher.VoucherCode}. Trạng thái: {s_statusLabels.GetValueOrDefault(dto.Status, dto.Status)}.",
            dto));
    }

    private async Task<VoucherDto> ToDtoAsync(Voucher voucher, CancellationToken cancellationToken = default)
    {
        int reserved = voucher.VoucherId == 0
            ? 0 : await repository.CountReservedAsync(voucher.VoucherId, cancellationToken);
        int redemptions = voucher.VoucherId == 0
            ? 0 : await repository.CountRedemptionsAsync(voucher.VoucherId, cancellationToken);
        return VoucherDto.From(voucher, reserved, redemptions, Now());
    }

    // BR-PV-12: staff, time, action and the changed values (old → new).
    private void AddAudit(Voucher voucher, string action, string? oldValue, DateTime now)
    {
        repository.AddAudit(new AuditLog
        {
            PerformedByUserId = actor.UserId,
            EntityType = "Voucher",
            EntityId = voucher.VoucherId,
            Action = action,
            OldValue = oldValue,
            NewValue = action == "DeleteVoucher" ? null : Snapshot(voucher),
            TraceId = actor.TraceId,
            OccurredAt = now
        });
    }

    private static string Snapshot(Voucher voucher) => JsonSerializer.Serialize(new
    {
        voucher.VoucherCode, voucher.VoucherName, voucher.Description, voucher.DiscountType,
        voucher.DiscountValue, voucher.MaxDiscountAmount, voucher.MinOrderAmount, voucher.UsageLimit,
        voucher.PerCustomerLimit, voucher.UsedCount, voucher.StartDate, voucher.EndDate, voucher.IsActive
    });

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static Error DuplicateCode()
    {
        const string message = "Mã voucher đã tồn tại.";
        return new Error("VOUCHER_CODE_EXISTS", message, "Conflict",
            new Dictionary<string, string[]> { ["voucherCode"] = [message] });
    }

    // MSG-PV-07
    private static Error TermsLocked() => new("VOUCHER_TERMS_LOCKED",
        "Voucher đã được sử dụng hoặc đã bắt đầu áp dụng. Chỉ được sửa tên, mô tả, gia hạn thời gian kết thúc "
            + "hoặc tăng tổng lượt dùng.",
        "Conflict");

    private static Error FieldError(string code, string field, string message) =>
        new(code, message, Fields: new Dictionary<string, string[]> { [field] = [message] });

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
