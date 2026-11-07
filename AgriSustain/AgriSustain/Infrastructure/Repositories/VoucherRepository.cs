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
            vouchers = vouchers.Where(voucher => voucher.VoucherCode.Contains(query.Search)
                || voucher.VoucherName.Contains(query.Search));
        }

        // Mirrors Voucher.LifecycleStatus so the filter runs in SQL.
        vouchers = query.Status switch
        {
            Voucher.StatusDeactivated => vouchers.Where(voucher => !voucher.IsActive),
            Voucher.StatusExpired => vouchers.Where(voucher => voucher.IsActive && voucher.EndDate <= now),
            Voucher.StatusExhausted => vouchers.Where(voucher => voucher.IsActive && voucher.EndDate > now
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

        var items = rows.Select(row => VoucherDto.From(row.Voucher, row.Reserved, row.Redemptions, now))
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

    /// <summary>
    /// BR-PV-01: a code is never reused, so it counts as taken while another voucher has it and also
    /// when it appears in any voucher's audit history (a deleted voucher, or a code changed before use).
    /// </summary>
    public async Task<bool> CodeExistsAsync(
        string voucherCode,
        long? exceptVoucherId,
        CancellationToken cancellationToken)
    {
        if (await _context.Vouchers.AsNoTracking().AnyAsync(
            voucher => voucher.VoucherCode == voucherCode && voucher.VoucherId != exceptVoucherId,
            cancellationToken))
        {
            return true;
        }

        // Audit snapshots are JSON written by VoucherManagementService ("VoucherCode":"CODE").
        string marker = $"\"VoucherCode\":\"{voucherCode}\"";
        return await _context.AuditLogs.AsNoTracking().AnyAsync(
            log => log.EntityType == "Voucher" && log.EntityId != exceptVoucherId
                && ((log.OldValue != null && log.OldValue.Contains(marker))
                    || (log.NewValue != null && log.NewValue.Contains(marker))),
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

    public async Task AddWithAuditAsync(Voucher voucher, Action addAudit, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.Vouchers.Add(voucher);
            await _context.SaveChangesAsync(cancellationToken);
            addAudit();
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            // Nothing was kept, so the tracked rows must not be saved by a later SaveChanges.
            foreach (var entry in _context.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }
            throw;
        }
    }
}
