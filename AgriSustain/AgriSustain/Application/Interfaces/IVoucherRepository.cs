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
}
