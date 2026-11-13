using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Promotions;

namespace AgriSustain.Application.Interfaces;

/// <summary>UC-PS-01 Manage Promotions and Vouchers for Promotion Staff.</summary>
public interface IVoucherManagementService
{
    /// <summary>
    /// Voucher list: keyword in code or name, discount type and lifecycle status, paged. Vouchers still running come
    /// first, then by start date. Each row shows used, reserved and total redemptions. Read-only; bad paging is clamped.
    /// </summary>
    /// <param name="query">Search, Status, DiscountType, Page and PageSize.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Always success: items, total, page and page size.</returns>
    Task<Result<VoucherListDto>> SearchAsync(
        VoucherQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One platform voucher with its derived status and counts.
    /// </summary>
    /// <param name="voucherId">Platform voucher id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The voucher, or 404 VOUCHER_NOT_FOUND.</returns>
    Task<Result<VoucherDto>> GetAsync(
        long voucherId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Steps: 1) SaveVoucherRequestValidator (BR-PV-01..04); 2) the start must not be in the past (one minute of
    /// tolerance); 3) the upper-cased code must never have been used (MSG-PV-03); 4) the voucher is saved active — it
    /// shows Scheduled or Active by its start time (BR-PV-06) — together with its CreateVoucher audit row in one
    /// transaction (BR-PV-12).
    /// </summary>
    /// <param name="request">Name, code, description, discount, maximum, minimum, usage limits and validity period.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>MSG-PV-08 with the saved voucher, or the validation / conflict error.</returns>
    Task<Result<VoucherActionResponse>> CreateAsync(
        SaveVoucherRequest request,
        CancellationToken cancellationToken = default);

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
    Task<Result<VoucherActionResponse>> UpdateAsync(
        long voucherId,
        SaveVoucherRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivate or reactivate (MSG-PV-09). Reactivation is refused once the voucher has ended or run out of uses
    /// (BR-PV-08, MSG-PV-10). Nothing is written when the flag already has the requested value.
    /// </summary>
    /// <param name="voucherId">Platform voucher id.</param>
    /// <param name="isActive">True to reactivate, false to deactivate.</param>
    /// <param name="version">Version the screen showed; when sent and different, the action is refused (MSG-PV-15).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Confirmation with the voucher, or the error.</returns>
    Task<Result<VoucherActionResponse>> SetActiveAsync(
        long voucherId,
        bool isActive,
        string? version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// BR-PV-09: only a voucher never redeemed or reserved and not currently Active can be deleted; otherwise MSG-PV-11
    /// asks to deactivate it instead. The audit row is written before the removal, so the code stays blocked for reuse.
    /// </summary>
    /// <param name="voucherId">Platform voucher id.</param>
    /// <param name="version">Version the screen showed; when sent and different, the action is refused (MSG-PV-15).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Confirmation without a voucher, or the error.</returns>
    Task<Result<VoucherActionResponse>> DeleteAsync(
        long voucherId,
        string? version,
        CancellationToken cancellationToken = default);
}
