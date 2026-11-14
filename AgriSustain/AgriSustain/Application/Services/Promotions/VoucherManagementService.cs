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

    /// <summary>
    /// Voucher list: keyword in code or name, discount type and lifecycle status, paged. Vouchers still running come
    /// first, then by start date. Each row shows used, reserved and total redemptions. Read-only; bad paging is clamped.
    /// </summary>
    /// <param name="query">Search, Status, DiscountType, Page and PageSize.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Always success: items, total, page and page size.</returns>
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

    /// <summary>
    /// One platform voucher with its derived status and counts.
    /// </summary>
    /// <param name="voucherId">Platform voucher id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The voucher, or 404 VOUCHER_NOT_FOUND.</returns>
    public async Task<Result<VoucherDto>> GetAsync(
        long voucherId,
        CancellationToken cancellationToken = default)
    {
        var voucher = await repository.GetPlatformVoucherAsync(voucherId, cancellationToken);
        return voucher is null
            ? Result<VoucherDto>.FailureResult(s_notFound)
            : Result<VoucherDto>.SuccessResult(await ToDtoAsync(voucher, cancellationToken));
    }

    /// <summary>
    /// Steps: 1) SaveVoucherRequestValidator (BR-PV-01..04); 2) the start must not be in the past (one minute of
    /// tolerance); 3) the upper-cased code must never have been used (MSG-PV-03); 4) the voucher is saved active — it
    /// shows Scheduled or Active by its start time (BR-PV-06) — together with its CreateVoucher audit row in one
    /// transaction (BR-PV-12).
    /// </summary>
    /// <param name="request">Name, code, description, discount, maximum, minimum, usage limits and validity period.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>MSG-PV-08 with the saved voucher, or the validation / conflict error.</returns>
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
        await repository.AddWithAuditAsync(voucher, () => AddAudit(voucher, AuditActions.CreateVoucher, null, now),
            cancellationToken);
        return await SavedAsync(voucher, cancellationToken);
    }

    /// <summary>
    /// Steps: 1) validate; 2) load the voucher (404) and compare the version (MSG-PV-15); 3) once the voucher was used,
    /// reserved or has started, its terms are locked (BR-PV-07): code, discount, maximum, minimum, per-customer limit and
    /// start must stay; the end may only move later and the total limit only grow (MSG-PV-07); otherwise a changed start
    /// must not be in the past; 4) the total limit may not drop below used + reserved uses; 5) a changed code must be
    /// new; 6) apply, audit old → new, save.
    /// </summary>
    /// <param name="voucherId">Platform voucher id.</param>
    /// <param name="request">New values, with Version.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>MSG-PV-08 with the voucher, or the error.</returns>
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
        AddAudit(voucher, AuditActions.UpdateVoucher, before, now);
        await repository.SaveChangesAsync(cancellationToken);
        return await SavedAsync(voucher, cancellationToken);
    }

    /// <summary>
    /// Deactivate or reactivate (MSG-PV-09). Reactivation is refused once the voucher has ended or run out of uses
    /// (BR-PV-08, MSG-PV-10). Nothing is written when the flag already has the requested value.
    /// </summary>
    /// <param name="voucherId">Platform voucher id.</param>
    /// <param name="isActive">True to reactivate, false to deactivate.</param>
    /// <param name="version">Version the screen showed; when sent and different, the action is refused (MSG-PV-15).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Confirmation with the voucher, or the error.</returns>
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
            AddAudit(voucher, isActive ? AuditActions.ReactivateVoucher : AuditActions.DeactivateVoucher, before, now);
            await repository.SaveChangesAsync(cancellationToken);
        }

        // MSG-PV-09
        return Result<VoucherActionResponse>.SuccessResult(new VoucherActionResponse(
            voucher.VoucherId,
            $"Đã {(isActive ? "kích hoạt lại" : "ngừng áp dụng")} voucher {voucher.VoucherCode}.",
            await ToDtoAsync(voucher, cancellationToken)));
    }

    /// <summary>
    /// BR-PV-09: only a voucher never redeemed or reserved and not currently Active can be deleted; otherwise MSG-PV-11
    /// asks to deactivate it instead. The audit row is written before the removal, so the code stays blocked for reuse.
    /// </summary>
    /// <param name="voucherId">Platform voucher id.</param>
    /// <param name="version">Version the screen showed; when sent and different, the action is refused (MSG-PV-15).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Confirmation without a voucher, or the error.</returns>
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

        AddAudit(voucher, AuditActions.DeleteVoucher, Snapshot(voucher), now);
        repository.Remove(voucher);
        await repository.SaveChangesAsync(cancellationToken);
        return Result<VoucherActionResponse>.SuccessResult(new VoucherActionResponse(
            voucherId, $"Đã xoá voucher {voucher.VoucherCode}.", null));
    }

    /// <summary>
    /// Copies the form values onto the voucher; dates are stored in UTC and an empty description as null.
    /// </summary>
    /// <param name="voucher">Voucher to fill.</param>
    /// <param name="request">Form values.</param>
    /// <param name="code">Normalised (upper-case) code.</param>
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

    /// <summary>
    /// MSG-PV-08 "Đã lưu voucher … Trạng thái: …" with the refreshed voucher.
    /// </summary>
    /// <param name="voucher">Saved voucher.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Success result.</returns>
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

    /// <summary>
    /// Maps the voucher with its reserved and total redemption counts and the status at the current time.
    /// </summary>
    /// <param name="voucher">Voucher.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The DTO.</returns>
    private async Task<VoucherDto> ToDtoAsync(Voucher voucher, CancellationToken cancellationToken = default)
    {
        int reserved = voucher.VoucherId == 0
            ? 0 : await repository.CountReservedAsync(voucher.VoucherId, cancellationToken);
        int redemptions = voucher.VoucherId == 0
            ? 0 : await repository.CountRedemptionsAsync(voucher.VoucherId, cancellationToken);
        return VoucherDto.From(voucher, reserved, redemptions, Now());
    }

    // BR-PV-12: staff, time, action and the changed values (old → new).
    /// <summary>
    /// BR-PV-12: staff, time, action and the changed values (old → new). A delete keeps only the old value.
    /// </summary>
    /// <param name="voucher">Voucher acted on.</param>
    /// <param name="action">CreateVoucher, UpdateVoucher, ReactivateVoucher, DeactivateVoucher or DeleteVoucher.</param>
    /// <param name="oldValue">JSON snapshot before, or null.</param>
    /// <param name="now">Current UTC time.</param>
    private void AddAudit(Voucher voucher, string action, string? oldValue, DateTime now)
    {
        repository.AddAudit(new AuditLog
        {
            PerformedByUserId = actor.UserId,
            EntityType = "Voucher",
            EntityId = voucher.VoucherId,
            Action = action,
            OldValue = oldValue,
            NewValue = action == AuditActions.DeleteVoucher ? null : Snapshot(voucher),
            TraceId = actor.TraceId,
            OccurredAt = now
        });
    }

    /// <summary>
    /// JSON of every audited voucher field; its "VoucherCode" entry is also what blocks a code from being reused.
    /// </summary>
    /// <param name="voucher">Voucher.</param>
    /// <returns>JSON text.</returns>
    private static string Snapshot(Voucher voucher) => JsonSerializer.Serialize(new
    {
        voucher.VoucherCode, voucher.VoucherName, voucher.Description, voucher.DiscountType,
        voucher.DiscountValue, voucher.MaxDiscountAmount, voucher.MinOrderAmount, voucher.UsageLimit,
        voucher.PerCustomerLimit, voucher.UsedCount, voucher.StartDate, voucher.EndDate, voucher.IsActive
    });

    /// <summary>
    /// Codes are trimmed and stored upper-case (BR-PV-01).
    /// </summary>
    /// <param name="code">Code as typed.</param>
    /// <returns>Normalised code.</returns>
    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    /// <summary>
    /// 409 VOUCHER_CODE_EXISTS on the voucherCode field.
    /// </summary>
    /// <returns>The error.</returns>
    private static Error DuplicateCode()
    {
        const string message = "Mã voucher đã tồn tại.";
        return new Error("VOUCHER_CODE_EXISTS", message, "Conflict",
            new Dictionary<string, string[]> { ["voucherCode"] = [message] });
    }

    // MSG-PV-07
    /// <summary>
    /// 409 VOUCHER_TERMS_LOCKED (MSG-PV-07).
    /// </summary>
    /// <returns>The error.</returns>
    private static Error TermsLocked() => new("VOUCHER_TERMS_LOCKED",
        "Voucher đã được sử dụng hoặc đã bắt đầu áp dụng. Chỉ được sửa tên, mô tả, gia hạn thời gian kết thúc "
            + "hoặc tăng tổng lượt dùng.",
        "Conflict");

    /// <summary>
    /// 400 error attached to one form field.
    /// </summary>
    /// <param name="code">Error code.</param>
    /// <param name="field">Field name.</param>
    /// <param name="message">Message.</param>
    /// <returns>The error.</returns>
    private static Error FieldError(string code, string field, string message) =>
        new(code, message, Fields: new Dictionary<string, string[]> { [field] = [message] });

    /// <summary>
    /// Current UTC time from the injected clock.
    /// </summary>
    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
