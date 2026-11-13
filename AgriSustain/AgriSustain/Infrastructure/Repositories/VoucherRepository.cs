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

    /// <summary>
    /// Receives the EF Core context of the current request.
    /// </summary>
    /// <param name="context">Database context.</param>
    public VoucherRepository(AgriSustainContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Platform vouchers only. The status filter mirrors Voucher.LifecycleStatus so it runs in SQL. Sorted: still running
    /// first, then newest start. Redemption counts are read in the same query.
    /// </summary>
    /// <param name="query">Normalised filters and paging.</param>
    /// <param name="now">Current UTC time.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Total and page.</returns>
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

    /// <summary>
    /// Tracked voucher with FarmerProfileId null.
    /// </summary>
    /// <param name="voucherId">Platform voucher id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Voucher or null.</returns>
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
    /// <param name="voucherCode">Code.</param>
    /// <param name="exceptVoucherId">Voucher to ignore.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>True when taken.</returns>
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

    /// <summary>
    /// Counts every redemption row of the voucher.
    /// </summary>
    /// <param name="voucherId">Platform voucher id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Count.</returns>
    public Task<int> CountRedemptionsAsync(
        long voucherId,
        CancellationToken cancellationToken)
    {
        return _context.VoucherRedemptions.CountAsync(
            redemption => redemption.VoucherId == voucherId,
            cancellationToken);
    }

    /// <summary>
    /// Counts the Reserved redemption rows.
    /// </summary>
    /// <param name="voucherId">Platform voucher id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Count.</returns>
    public Task<int> CountReservedAsync(
        long voucherId,
        CancellationToken cancellationToken)
    {
        return _context.VoucherRedemptions.CountAsync(
            redemption => redemption.VoucherId == voucherId && redemption.Status == ReservedRedemption,
            cancellationToken);
    }

    /// <summary>
    /// Adds the voucher to the context.
    /// </summary>
    /// <param name="voucher">Voucher.</param>
    public void Add(Voucher voucher)
    {
        _context.Vouchers.Add(voucher);
    }

    /// <summary>
    /// Removes the voucher from the context.
    /// </summary>
    /// <param name="voucher">Voucher.</param>
    public void Remove(Voucher voucher)
    {
        _context.Vouchers.Remove(voucher);
    }

    /// <summary>
    /// Adds the audit row to the context.
    /// </summary>
    /// <param name="audit">Row.</param>
    public void AddAudit(AuditLog audit)
    {
        _context.AuditLogs.Add(audit);
    }

    /// <summary>
    /// Runs EF Core SaveChanges.
    /// </summary>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Explicit transaction: insert the voucher, add the audit with the new id, save, commit. On failure it rolls back
    /// and detaches every tracked entry so a later SaveChanges cannot write half of it.
    /// </summary>
    /// <param name="voucher">New voucher.</param>
    /// <param name="addAudit">Adds the audit row.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
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
