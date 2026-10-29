using AgriSustain.Application.DTOs.Promotions;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class VoucherRepository : IVoucherRepository
{
    private const string ReservedRedemption = "Reserved";

    private readonly AgriSustainContext _context;

    public VoucherRepository(AgriSustainContext context)
    {
        _context = context;
    }

    public async Task<(int Total, IReadOnlyList<VoucherDto> Items)> SearchAsync(
        VoucherQuery query,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var vouchers = _context.Vouchers.AsNoTracking().Where(voucher => voucher.FarmerProfileId == null);
        if (query.DiscountType is not null)
        {
            vouchers = vouchers.Where(voucher => voucher.DiscountType == query.DiscountType);
        }

        if (query.Search is not null)
        {
            vouchers = vouchers.Where(voucher => voucher.VoucherCode.Contains(query.Search));
        }

        // Mirrors Voucher.LifecycleStatus so the filter runs in SQL.
        vouchers = query.Status switch
        {
            Voucher.StatusInactive => vouchers.Where(voucher => !voucher.IsActive),
            Voucher.StatusExpired => vouchers.Where(voucher => voucher.IsActive && voucher.EndDate <= now),
            Voucher.StatusDepleted => vouchers.Where(voucher => voucher.IsActive && voucher.EndDate > now
                && voucher.UsageLimit != null && voucher.UsedCount >= voucher.UsageLimit),
            Voucher.StatusScheduled => vouchers.Where(voucher => voucher.IsActive && voucher.EndDate > now
                && (voucher.UsageLimit == null || voucher.UsedCount < voucher.UsageLimit)
                && voucher.StartDate > now),
            Voucher.StatusActive => vouchers.Where(voucher => voucher.IsActive && voucher.EndDate > now
                && (voucher.UsageLimit == null || voucher.UsedCount < voucher.UsageLimit)
                && voucher.StartDate <= now),
            _ => vouchers
        };

        int total = await vouchers.CountAsync(cancellationToken);
        var rows = await vouchers
            .OrderByDescending(voucher => voucher.EndDate > now)
            .ThenByDescending(voucher => voucher.StartDate)
            .ThenByDescending(voucher => voucher.VoucherId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(voucher => new
            {
                Voucher = voucher,
                Reserved = voucher.VoucherRedemptions.Count(redemption =>
                    redemption.Status == ReservedRedemption),
                Redemptions = voucher.VoucherRedemptions.Count()
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new VoucherDto(row.Voucher.VoucherId, row.Voucher.VoucherCode,
                row.Voucher.DiscountType, row.Voucher.DiscountValue, row.Voucher.MaxDiscountAmount,
                row.Voucher.MinOrderAmount, row.Voucher.UsageLimit, row.Voucher.UsedCount, row.Reserved,
                row.Voucher.StartDate, row.Voucher.EndDate, row.Voucher.IsActive,
                row.Voucher.LifecycleStatus(now), row.Voucher.UsedCount > 0 || row.Redemptions > 0))
            .ToList();
        return (total, items);
    }

    public Task<Voucher?> GetPlatformVoucherAsync(
        long voucherId,
        CancellationToken cancellationToken)
    {
        return _context.Vouchers.SingleOrDefaultAsync(
            voucher => voucher.VoucherId == voucherId && voucher.FarmerProfileId == null,
            cancellationToken);
    }

    public Task<bool> CodeExistsAsync(
        string voucherCode,
        long? exceptVoucherId,
        CancellationToken cancellationToken)
    {
        return _context.Vouchers.AsNoTracking().AnyAsync(
            voucher => voucher.VoucherCode == voucherCode && voucher.VoucherId != exceptVoucherId,
            cancellationToken);
    }

    public Task<int> CountRedemptionsAsync(
        long voucherId,
        CancellationToken cancellationToken)
    {
        return _context.VoucherRedemptions.CountAsync(
            redemption => redemption.VoucherId == voucherId,
            cancellationToken);
    }

    public Task<int> CountReservedAsync(
        long voucherId,
        CancellationToken cancellationToken)
    {
        return _context.VoucherRedemptions.CountAsync(
            redemption => redemption.VoucherId == voucherId && redemption.Status == ReservedRedemption,
            cancellationToken);
    }

    public void Add(Voucher voucher)
    {
        _context.Vouchers.Add(voucher);
    }

    public void Remove(Voucher voucher)
    {
        _context.Vouchers.Remove(voucher);
    }

    public void AddAudit(AuditLog audit)
    {
        _context.AuditLogs.Add(audit);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
