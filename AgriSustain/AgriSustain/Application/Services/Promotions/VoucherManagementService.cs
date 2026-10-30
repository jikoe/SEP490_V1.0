using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Promotions;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using FluentValidation;

namespace AgriSustain.Application.Services.Promotions;

/// <summary>
/// UC-PS-01 Manage Promotions and Vouchers (SRS UC-ADM-07, GBR-17). Codes are stored upper-case and
/// are unique system-wide. Once a voucher has been reserved or used, its code and discount rule are
/// frozen so customers keep the terms they received; dates and the usage limit stay editable. A used
/// voucher is archived (deactivated) instead of deleted.
/// </summary>
public sealed class VoucherManagementService(
    IVoucherRepository repository,
    IActorContext actor,
    IValidator<SaveVoucherRequest> validator,
    TimeProvider timeProvider) : IVoucherManagementService
{
    private const int MaximumPageSize = 100;

    private static readonly Error s_notFound = new(
        "VOUCHER_NOT_FOUND", "Không tìm thấy voucher.", "NotFound");

    public async Task<Result<VoucherListDto>> SearchAsync(
        VoucherQuery query,
        CancellationToken cancellationToken)
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
        CancellationToken cancellationToken)
    {
        var voucher = await repository.GetPlatformVoucherAsync(voucherId, cancellationToken);
        return voucher is null
            ? Result<VoucherDto>.FailureResult(s_notFound)
            : Result<VoucherDto>.SuccessResult(await ToDtoAsync(voucher, cancellationToken));
    }

    public async Task<Result<VoucherActionResponse>> CreateAsync(
        SaveVoucherRequest request,
        CancellationToken cancellationToken)
    {
        var error = await RequestValidation.CheckAsync(validator, request, cancellationToken);
        if (error is not null)
        {
            return Result<VoucherActionResponse>.FailureResult(error);
        }

        var now = Now();
        if (request.EndDate.UtcDateTime <= now)
        {
            return Result<VoucherActionResponse>.FailureResult(FieldError(
                "INVALID_PERIOD", "endDate", "Ngày kết thúc phải ở tương lai."));
        }

        string code = NormalizeCode(request.VoucherCode);
        if (await repository.CodeExistsAsync(code, null, cancellationToken))
        {
            return Result<VoucherActionResponse>.FailureResult(DuplicateCode());
        }

        var voucher = new Voucher { UsedCount = 0 };
        Apply(voucher, request, code);
        repository.Add(voucher);
        await repository.SaveChangesAsync(cancellationToken);
        AddAudit(voucher, "CreateVoucher", null, now);
        await repository.SaveChangesAsync(cancellationToken);

        return Result<VoucherActionResponse>.SuccessResult(new VoucherActionResponse(
            voucher.VoucherId, $"Đã tạo voucher {voucher.VoucherCode}.",
            await ToDtoAsync(voucher, cancellationToken)));
    }

    public async Task<Result<VoucherActionResponse>> UpdateAsync(
        long voucherId,
        SaveVoucherRequest request,
        CancellationToken cancellationToken)
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

        string code = NormalizeCode(request.VoucherCode);
        int redemptions = await repository.CountRedemptionsAsync(voucherId, cancellationToken);
        bool termsLocked = voucher.UsedCount > 0 || redemptions > 0;
        if (termsLocked && !voucher.HasSameTerms(code, request.DiscountType, request.DiscountValue,
            request.MaxDiscountAmount, request.MinOrderAmount))
        {
            return Result<VoucherActionResponse>.FailureResult(new Error("VOUCHER_TERMS_LOCKED",
                "Voucher đã được sử dụng nên không thể thay đổi mã, kiểu giảm, giá trị giảm, mức giảm "
                    + "tối đa hoặc giá trị đơn tối thiểu. Bạn vẫn có thể đổi thời hạn và giới hạn lượt dùng.",
                "Conflict"));
        }

        int reserved = await repository.CountReservedAsync(voucherId, cancellationToken);
        if (request.UsageLimit.HasValue && request.UsageLimit < voucher.UsedCount + reserved)
        {
            return Result<VoucherActionResponse>.FailureResult(FieldError("USAGE_LIMIT_TOO_LOW",
                "usageLimit",
                $"Giới hạn lượt dùng không được nhỏ hơn số lượt đã dùng/giữ chỗ ({voucher.UsedCount + reserved})."));
        }

        if (code != voucher.VoucherCode
            && await repository.CodeExistsAsync(code, voucherId, cancellationToken))
        {
            return Result<VoucherActionResponse>.FailureResult(DuplicateCode());
        }

        var before = Snapshot(voucher);
        Apply(voucher, request, code);
        AddAudit(voucher, "UpdateVoucher", before, Now());
        await repository.SaveChangesAsync(cancellationToken);

        return Result<VoucherActionResponse>.SuccessResult(new VoucherActionResponse(
            voucher.VoucherId, $"Đã cập nhật voucher {voucher.VoucherCode}.",
            await ToDtoAsync(voucher, cancellationToken)));
    }

    public async Task<Result<VoucherActionResponse>> SetActiveAsync(
        long voucherId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var voucher = await repository.GetPlatformVoucherAsync(voucherId, cancellationToken);
        if (voucher is null)
        {
            return Result<VoucherActionResponse>.FailureResult(s_notFound);
        }

        var now = Now();
        if (isActive && voucher.EndDate <= now)
        {
            return Result<VoucherActionResponse>.FailureResult(new Error("VOUCHER_EXPIRED",
                "Voucher đã hết hạn. Vui lòng gia hạn ngày kết thúc trước khi kích hoạt.", "Conflict"));
        }

        if (voucher.IsActive != isActive)
        {
            var before = Snapshot(voucher);
            voucher.IsActive = isActive;
            AddAudit(voucher, isActive ? "ActivateVoucher" : "DeactivateVoucher", before, now);
            await repository.SaveChangesAsync(cancellationToken);
        }

        return Result<VoucherActionResponse>.SuccessResult(new VoucherActionResponse(
            voucher.VoucherId,
            isActive ? $"Đã kích hoạt voucher {voucher.VoucherCode}."
                : $"Đã ngừng hoạt động voucher {voucher.VoucherCode}.",
            await ToDtoAsync(voucher, cancellationToken)));
    }

    public async Task<Result<VoucherActionResponse>> DeleteAsync(
        long voucherId,
        CancellationToken cancellationToken)
    {
        var voucher = await repository.GetPlatformVoucherAsync(voucherId, cancellationToken);
        if (voucher is null)
        {
            return Result<VoucherActionResponse>.FailureResult(s_notFound);
        }

        var now = Now();
        var before = Snapshot(voucher);
        int redemptions = await repository.CountRedemptionsAsync(voucherId, cancellationToken);
        if (voucher.UsedCount > 0 || redemptions > 0)
        {
            // Orders keep referring to a used voucher, so it is archived rather than removed.
            voucher.IsActive = false;
            AddAudit(voucher, "ArchiveVoucher", before, now);
            await repository.SaveChangesAsync(cancellationToken);
            return Result<VoucherActionResponse>.SuccessResult(new VoucherActionResponse(
                voucher.VoucherId,
                $"Voucher {voucher.VoucherCode} đã được sử dụng nên được lưu trữ (ngừng hoạt động) thay vì xóa.",
                await ToDtoAsync(voucher, cancellationToken)));
        }

        AddAudit(voucher, "DeleteVoucher", before, now);
        repository.Remove(voucher);
        await repository.SaveChangesAsync(cancellationToken);
        return Result<VoucherActionResponse>.SuccessResult(new VoucherActionResponse(
            voucherId, $"Đã xóa voucher {voucher.VoucherCode}.", null));
    }

    private static void Apply(Voucher voucher, SaveVoucherRequest request, string code)
    {
        voucher.VoucherCode = code;
        voucher.DiscountType = request.DiscountType;
        voucher.DiscountValue = request.DiscountValue;
        voucher.MaxDiscountAmount = request.MaxDiscountAmount;
        voucher.MinOrderAmount = request.MinOrderAmount;
        voucher.UsageLimit = request.UsageLimit;
        voucher.StartDate = request.StartDate.UtcDateTime;
        voucher.EndDate = request.EndDate.UtcDateTime;
        voucher.IsActive = request.IsActive;
    }

    private async Task<VoucherDto> ToDtoAsync(Voucher voucher, CancellationToken cancellationToken)
    {
        int reserved = voucher.VoucherId == 0
            ? 0 : await repository.CountReservedAsync(voucher.VoucherId, cancellationToken);
        int redemptions = voucher.VoucherId == 0
            ? 0 : await repository.CountRedemptionsAsync(voucher.VoucherId, cancellationToken);
        return new VoucherDto(voucher.VoucherId, voucher.VoucherCode, voucher.DiscountType,
            voucher.DiscountValue, voucher.MaxDiscountAmount, voucher.MinOrderAmount, voucher.UsageLimit,
            voucher.UsedCount, reserved, voucher.StartDate, voucher.EndDate, voucher.IsActive,
            voucher.LifecycleStatus(Now()), voucher.UsedCount > 0 || redemptions > 0);
    }

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
        voucher.VoucherCode, voucher.DiscountType, voucher.DiscountValue, voucher.MaxDiscountAmount,
        voucher.MinOrderAmount, voucher.UsageLimit, voucher.UsedCount, voucher.StartDate,
        voucher.EndDate, voucher.IsActive
    });

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static Error DuplicateCode()
    {
        const string message = "Mã voucher đã tồn tại. Vui lòng chọn mã khác.";
        return new Error("VOUCHER_CODE_EXISTS", message, "Conflict",
            new Dictionary<string, string[]> { ["voucherCode"] = [message] });
    }

    private static Error FieldError(string code, string field, string message) =>
        new(code, message, Fields: new Dictionary<string, string[]> { [field] = [message] });

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
