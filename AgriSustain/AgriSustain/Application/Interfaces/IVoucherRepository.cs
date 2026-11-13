using AgriSustain.Application.DTOs.Promotions;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

/// <summary>Platform vouchers only (FarmerProfileId is null); farm vouchers are not managed here.</summary>
public interface IVoucherRepository
{
    /// <summary>
    /// Filtered, paged platform vouchers.
    /// </summary>
    /// <param name="query">Normalised filters.</param>
    /// <param name="now">Current UTC time.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Total and page.</returns>
    Task<(int Total, IReadOnlyList<VoucherDto> Items)> SearchAsync(
        VoucherQuery query,
        DateTime now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Tracked platform voucher (farm vouchers are not returned).
    /// </summary>
    /// <param name="voucherId">Platform voucher id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Voucher or null.</returns>
    Task<Voucher?> GetPlatformVoucherAsync(
        long voucherId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether a code is taken now or was ever used before (BR-PV-01).
    /// </summary>
    /// <param name="voucherCode">Upper-case code.</param>
    /// <param name="exceptVoucherId">Voucher being edited, ignored.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>True when the code cannot be used.</returns>
    Task<bool> CodeExistsAsync(
        string voucherCode,
        long? exceptVoucherId,
        CancellationToken cancellationToken);

    /// <summary>
    /// All redemptions of the voucher, whatever their status.
    /// </summary>
    /// <param name="voucherId">Platform voucher id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Count.</returns>
    Task<int> CountRedemptionsAsync(
        long voucherId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Redemptions held by unpaid orders.
    /// </summary>
    /// <param name="voucherId">Platform voucher id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Count.</returns>
    Task<int> CountReservedAsync(
        long voucherId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stages a new voucher.
    /// </summary>
    /// <param name="voucher">Voucher.</param>
    void Add(Voucher voucher);

    /// <summary>
    /// Stages a deletion.
    /// </summary>
    /// <param name="voucher">Voucher.</param>
    void Remove(Voucher voucher);

    /// <summary>
    /// Stages an audit row.
    /// </summary>
    /// <param name="audit">Row.</param>
    void AddAudit(AuditLog audit);

    /// <summary>
    /// Commits staged and tracked changes.
    /// </summary>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// BR-PV-12: saves a new voucher and its creation audit atomically. The audit needs the generated
    /// VoucherId, so <paramref name="addAudit"/> runs after the voucher is saved, inside the same
    /// transaction; if any step fails nothing is kept.
    /// </summary>
    /// <param name="voucher">New voucher.</param>
    /// <param name="addAudit">Adds the audit row once the id exists.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    async Task AddWithAuditAsync(Voucher voucher, Action addAudit, CancellationToken cancellationToken)
    {
        Add(voucher);
        await SaveChangesAsync(cancellationToken);
        addAudit();
        await SaveChangesAsync(cancellationToken);
    }
}
