using System.Data;
using AgriSustain.Application.DTOs.Payouts;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class FinanceFarmerPayoutRepository(AgriSustainContext db) : IFinanceFarmerPayoutRepository
{
    public Task<bool> AuthorizedAsync(long actor, CancellationToken ct) => db.Users.AsNoTracking().AnyAsync(x =>
        x.UserId == actor && x.Status == "Active" && (x.Role == "FinanceStaff" || x.Role == "Finance Staff"), ct);

    public async Task<IReadOnlyList<long>> PendingAsync(CancellationToken ct) => await db.FarmerPayouts.AsNoTracking()
        .Where(x => (x.PayoutStatus == "Processing" || x.PayoutStatus == "NeedsReconciliation") && x.PayoutCode.StartsWith("FPO"))
        .OrderBy(x => x.FarmerPayoutId).Select(x => x.FarmerPayoutId).ToListAsync(ct);

    public async Task<object> FarmersAsync(CancellationToken ct) => await db.FarmerProfiles.AsNoTracking()
        .OrderBy(x => x.FarmName).Select(x => new { id = x.FarmerProfileId, name = x.FarmName, x.VerificationStatus }).ToListAsync(ct);

    public async Task<object> SearchOwnedAsync(FarmerPayoutQuery query, CancellationToken ct)
    {
        var rows = db.FarmerPayouts.AsNoTracking().Where(p => db.AuditLogs.Any(a =>
            a.EntityType == "FinanceFarmerPayout" && a.EntityId == p.FarmerPayoutId && a.Action == "Submitted"));
        if (query.FarmerId.HasValue) rows = rows.Where(x => x.FarmerProfileId == query.FarmerId);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            string key = query.Keyword.Trim();
            rows = rows.Where(x => x.PayoutCode.Contains(key) || x.FarmerProfile.FarmName.Contains(key));
        }
        if (!string.IsNullOrWhiteSpace(query.Status))
            rows = query.Status == "Processed" ? rows.Where(x => x.PayoutStatus == "Processed" || x.PayoutStatus == "Paid"
                || x.PayoutStatus == "Succeeded" || x.PayoutStatus == "Completed") : rows.Where(x => x.PayoutStatus == query.Status);
        if (query.From.HasValue) rows = rows.Where(x => (db.AuditLogs.Where(a => a.EntityType == "FinanceFarmerPayout" && a.EntityId == x.FarmerPayoutId && a.Action == "Submitted").Select(a => (DateTime?)a.OccurredAt).Min()) >= query.From.Value.ToDateTime(TimeOnly.MinValue).AddHours(-7));
        if (query.To.HasValue) rows = rows.Where(x => (db.AuditLogs.Where(a => a.EntityType == "FinanceFarmerPayout" && a.EntityId == x.FarmerPayoutId && a.Action == "Submitted").Select(a => (DateTime?)a.OccurredAt).Min()) < query.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7));
        int total = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.FarmerPayoutId).Skip((query.Page - 1) * 10).Take(10)
            .Select(x => new
            {
                id = x.FarmerPayoutId,
                code = x.PayoutCode,
                farmer = x.FarmerProfile.FarmName,
                x.Amount,
                x.DeductedCommission,
                deduction = x.Amount - x.DeductedCommission - x.NetAmount,
                x.NetAmount,
                x.PayoutStatus,
                at = db.AuditLogs.Where(a => a.EntityType == "FinanceFarmerPayout" && a.EntityId == x.FarmerPayoutId && a.Action == "Submitted").Select(a => (DateTime?)a.OccurredAt).Min()
            }).ToListAsync(ct);
        return new { items, total, query.Page, pageSize = 10 };
    }

    public async Task<FarmerPayoutContext?> LoadContextAsync(long farmerId, long? payoutId, bool update, CancellationToken ct)
    {
        if (update)
            await db.FarmerProfiles.FromSqlInterpolated($"SELECT * FROM dbo.FarmerProfile WITH (UPDLOCK,HOLDLOCK) WHERE FarmerProfileId={farmerId}").ToListAsync(ct);
        var farmer = await db.FarmerProfiles.Include(x => x.User).SingleOrDefaultAsync(x => x.FarmerProfileId == farmerId, ct);
        if (farmer == null) return null;
        var lines = await db.InvoiceLines.Where(x =>
                (x.OrderItem != null && x.OrderItem.SellerOrder.FarmerProfileId == farmerId)
                || (x.ShipmentItem != null && x.ShipmentItem.Shipment.SellerOrder.FarmerProfileId == farmerId))
            .Include(x => x.Invoice).ThenInclude(x => x.PaymentInvoiceAllocations).ThenInclude(x => x.PaymentTransaction)
            .Include(x => x.Invoice).ThenInclude(x => x.ContractMilestone)
            .Include(x => x.OrderItem!).ThenInclude(x => x.Product)
            .Include(x => x.OrderItem!).ThenInclude(x => x.Fees)
            .Include(x => x.OrderItem!).ThenInclude(x => x.SellerOrder).ThenInclude(x => x.Order).ThenInclude(x => x.PaymentTransactions)
            .Include(x => x.ShipmentItem!).ThenInclude(x => x.Shipment).ThenInclude(x => x.SellerOrder).ThenInclude(x => x.Order).ThenInclude(x => x.PaymentTransactions)
            .Include(x => x.ShipmentItem!).ThenInclude(x => x.OrderItemBatchAllocation).ThenInclude(x => x.OrderItem).ThenInclude(x => x.Product)
            .Include(x => x.ShipmentItem!).ThenInclude(x => x.OrderItemBatchAllocation).ThenInclude(x => x.OrderItem).ThenInclude(x => x.Fees)
            .AsSplitQuery().ToListAsync(ct);
        var invoiceIds = lines.Select(x => x.InvoiceId).Distinct().ToList();
        var bank = await db.UserBankAccounts.Include(x => x.BankCodeNavigation)
            .SingleOrDefaultAsync(x => x.UserId == farmer.UserId && x.IsDefault, ct);
        var claims = await db.PayoutItems.Where(x => x.FarmerPayout.FarmerProfileId == farmerId)
            .Include(x => x.FarmerPayout).ToListAsync(ct);
        var payout = payoutId.HasValue ? await db.FarmerPayouts.Include(x => x.PayoutItems)
            .SingleOrDefaultAsync(x => x.FarmerPayoutId == payoutId && x.FarmerProfileId == farmerId, ct) : null;
        return new FarmerPayoutContext
        {
            Farmer = farmer,
            Bank = bank,
            Lines = lines,
            Claims = claims,
            Payout = payout,
            Policies = await db.CommissionPolicies.AsNoTracking().ToListAsync(ct),
            InvoiceTotals = await db.InvoiceLines.Where(x => invoiceIds.Contains(x.InvoiceId)).GroupBy(x => x.InvoiceId)
                .Select(g => new { id = g.Key, amount = g.Sum(x => x.LineAmount) }).ToDictionaryAsync(x => x.id, x => x.amount, ct),
            Disputes = await db.DisputeTickets.Where(x => (x.SellerOrder != null && x.SellerOrder.FarmerProfileId == farmerId)
                || (x.B2bcontract != null && x.B2bcontract.FarmerProfileId == farmerId)).ToListAsync(ct),
            Refunds = await db.RefundTransactions.Where(x => (x.DisputeTicket != null && x.DisputeTicket.SellerOrder != null
                    && x.DisputeTicket.SellerOrder.FarmerProfileId == farmerId)
                || (x.PaymentInvoiceAllocation != null && invoiceIds.Contains(x.PaymentInvoiceAllocation.InvoiceId))
                || (x.PaymentTransaction.B2bcontract != null && x.PaymentTransaction.B2bcontract.FarmerProfileId == farmerId)
                || (x.PaymentTransaction.Order != null && x.PaymentTransaction.Order.SellerOrders.Any(s => s.FarmerProfileId == farmerId)))
                .Include(x => x.DisputeTicket).Include(x => x.PaymentInvoiceAllocation).Include(x => x.PaymentTransaction).ToListAsync(ct),
            History = payoutId.HasValue ? await db.AuditLogs.Where(x => x.EntityType == "FinanceFarmerPayout" && x.EntityId == payoutId)
                .OrderBy(x => x.AuditLogId).ToListAsync(ct) : [],
            NotificationFailed = payoutId.HasValue && await db.Notifications.AnyAsync(x => x.NotificationType == "FinanceFarmerPayout"
                && x.RelatedRecordId == payoutId && x.DeliveryStatus == "Failed", ct)
        };
    }

    public Task<FarmerPayout?> FindRequestAsync(string key, CancellationToken ct) => db.FarmerPayouts.Include(x => x.PayoutItems)
        .SingleOrDefaultAsync(x => x.RequestKey == key, ct);
    public Task<long?> FarmerForPayoutAsync(long payoutId, CancellationToken ct) => db.FarmerPayouts
        .Where(x => x.FarmerPayoutId == payoutId).Select(x => (long?)x.FarmerProfileId).SingleOrDefaultAsync(ct);
    public Task<long?> FindOperationAsync(string operation, CancellationToken ct) => db.FarmerPayouts
        .Where(x => x.PayoutCode == operation).Select(x => (long?)x.FarmerPayoutId).SingleOrDefaultAsync(ct);
    public Task<AuditLog?> FindTraceAsync(string trace, CancellationToken ct) => db.AuditLogs
        .SingleOrDefaultAsync(x => x.EntityType == "FinanceFarmerPayout" && x.TraceId == trace, ct);

    public async Task<T> SaveAtomicAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        var outer = db.Database.CurrentTransaction;
        await using var owned = outer == null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var transaction = owned ?? outer!;
        if (outer != null) await transaction.CreateSavepointAsync("FarmerPayoutAction", ct);
        try
        {
            // Serializes payout creation and settlement, including identical events delivered concurrently.
            using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'AgriSustain.FarmerPayout', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; SELECT @r;";
            int result = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
            if (result < 0) throw new DbUpdateConcurrencyException("Payout lock unavailable.");
            // Preview/probe reads may have tracked an older bank or request snapshot.
            // Reload after acquiring the transaction lock before deciding whether funds may move.
            if (db.ChangeTracker.Entries().Any(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
                throw new InvalidOperationException("Payout actions require a clean unit of work.");
            db.ChangeTracker.Clear();
            var value = await operation();
            await db.SaveChangesAsync(ct);
            if (outer == null) await transaction.CommitAsync(ct);
            else await transaction.ReleaseSavepointAsync("FarmerPayoutAction", ct);
            return value;
        }
        catch
        {
            if (outer == null) await transaction.RollbackAsync(CancellationToken.None);
            else await transaction.RollbackToSavepointAsync("FarmerPayoutAction", CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public Task FlushAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public void Add(FarmerPayout payout) => db.FarmerPayouts.Add(payout);
    public void Record(AuditLog entry) => db.AuditLogs.Add(entry);
    public void Queue(IEnumerable<Notification> notices) => db.Notifications.AddRange(notices);
}
