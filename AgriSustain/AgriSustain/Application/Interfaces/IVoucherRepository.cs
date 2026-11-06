using AgriSustain.Application.DTOs.Promotions;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

/// <summary>Platform vouchers only (FarmerProfileId is null); farm vouchers are not managed here.</summary>
public interface IVoucherRepository
{
    Task<(int Total, IReadOnlyList<VoucherDto> Items)> SearchAsync(
        VoucherQuery query,
        DateTime now,
        CancellationToken cancellationToken);

    Task<Voucher?> GetPlatformVoucherAsync(
        long voucherId,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        string voucherCode,
        long? exceptVoucherId,
        CancellationToken cancellationToken);

    Task<int> CountRedemptionsAsync(
        long voucherId,
        CancellationToken cancellationToken);

    Task<int> CountReservedAsync(
        long voucherId,
        CancellationToken cancellationToken);

    void Add(Voucher voucher);

    void Remove(Voucher voucher);

    void AddAudit(AuditLog audit);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// BR-PV-12: saves a new voucher and its creation audit atomically. The audit needs the generated
    /// VoucherId, so <paramref name="addAudit"/> runs after the voucher is saved, inside the same
    /// transaction; if any step fails nothing is kept.
    /// </summary>
    async Task AddWithAuditAsync(Voucher voucher, Action addAudit, CancellationToken cancellationToken)
    {
        Add(voucher);
        await SaveChangesAsync(cancellationToken);
        addAudit();
        await SaveChangesAsync(cancellationToken);
    }
}
