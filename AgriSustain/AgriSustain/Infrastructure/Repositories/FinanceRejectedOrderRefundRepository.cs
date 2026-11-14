using System.Data;
using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class FinanceRejectedOrderRefundRepository(AgriSustainContext db)
    : IFinanceRejectedOrderRefundRepository
{
    private IQueryable<RefundTransaction> Requests => db.RefundTransactions.Where(x =>
        x.RequestKey.StartsWith("farmer-reject:") && x.DisputeTicketId == null
        && x.ContractTerminationRequestId == null);

    public Task<bool> AuthorizedAsync(long userId, CancellationToken ct) => db.Users.AsNoTracking()
        .AnyAsync(x => x.UserId == userId && x.Status == "Active"
            && (x.Role == "FinanceStaff" || x.Role == "Finance Staff"), ct);

    public async Task<object> SearchAsync(RejectedOrderRefundQuery query, CancellationToken ct)
    {
        // Use the recorded cancellation time; completion must not move a request to another date.
        var source = from refund in Requests.AsNoTracking()
                     from order in db.SellerOrders
                     where refund.RequestKey.StartsWith("farmer-reject:" + order.SellerOrderId + ":")
                        && order.Status == "Cancelled" && order.Order.OrderType == "Retail"
                        && order.Order.BuyerUser.Role == "RetailCustomer"
                        && refund.PaymentTransaction.PaidAt != null
                        && (refund.PaymentTransaction.PaymentStatus == "Paid" || refund.PaymentTransaction.PaymentStatus == "Success"
                            || refund.PaymentTransaction.PaymentStatus == "Succeeded" || refund.PaymentTransaction.PaymentStatus == "Completed"
                            || refund.PaymentTransaction.PaymentStatus == "Refunded")
                     select new
                     {
                         id = refund.RefundTransactionId,
                         code = refund.RefundCode,
                         orderCode = order.SellerOrderCode,
                         customer = order.Order.BuyerUser.FullName,
                         farmer = order.FarmerProfile.FarmName,
                         amount = refund.Amount,
                         status = refund.RefundStatus,
                         submittedAt = db.OrderStatusHistories.Where(h => h.SellerOrderId == order.SellerOrderId
                             && h.NewStatus == "Cancelled").Select(h => (DateTime?)h.OccurredAt).Min()
                     };
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            string key = query.Keyword.Trim();
            source = source.Where(x => x.code.Contains(key) || x.orderCode.Contains(key)
                || x.customer.Contains(key) || x.farmer.Contains(key));
        }
        if (!string.IsNullOrWhiteSpace(query.Status))
            source = query.Status == "Succeeded"
                ? source.Where(x => x.status == "Completed" || x.status == "Succeeded" || x.status == "Success")
                : source.Where(x => x.status == query.Status);
        if (query.From.HasValue)
        {
            var from = query.From.Value.ToDateTime(TimeOnly.MinValue).AddHours(-7);
            source = source.Where(x => x.submittedAt >= from);
        }
        if (query.To.HasValue)
        {
            var to = query.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7);
            source = source.Where(x => x.submittedAt < to);
        }
        int total = await source.CountAsync(ct);
        var items = await source.OrderByDescending(x => x.id).Skip((query.Page - 1) * 10).Take(10).ToListAsync(ct);
        return new { items, total, query.Page, pageSize = 10 };
    }

    public async Task<RejectedOrderRefundContext?> LoadAsync(long id, bool update, CancellationToken ct)
    {
        if (update)
        {
            var paymentId = await Requests.Where(x => x.RefundTransactionId == id)
                .Select(x => (long?)x.PaymentTransactionId).SingleOrDefaultAsync(ct);
            if (!paymentId.HasValue) return null;
            await db.PaymentTransactions.FromSqlInterpolated($"SELECT * FROM dbo.PaymentTransaction WITH (UPDLOCK,HOLDLOCK) WHERE PaymentTransactionId={paymentId.Value}")
                .ToListAsync(ct);
        }
        var refund = await Requests.Include(x => x.PaymentTransaction).ThenInclude(x => x.RefundTransactions)
            .Include(x => x.PaymentInvoiceAllocation).ThenInclude(x => x!.Invoice)
            .SingleOrDefaultAsync(x => x.RefundTransactionId == id, ct);
        if (refund == null) return null;
        string[] source = refund.RequestKey.Split(':');
        if (source.Length != 4 || !long.TryParse(source[1], out long sellerId)) return null;
        var order = await db.SellerOrders.Include(x => x.Order).ThenInclude(x => x.BuyerUser)
            .Include(x => x.Order).ThenInclude(x => x.SellerOrders)
            .Include(x => x.Order).ThenInclude(x => x.PaymentTransactions).ThenInclude(x => x.RefundTransactions)
            .Include(x => x.FarmerProfile).Include(x => x.OrderStatusHistories)
            .SingleOrDefaultAsync(x => x.SellerOrderId == sellerId && x.OrderId == refund.PaymentTransaction.OrderId
                && x.Order.OrderType == "Retail", ct);
        if (order == null) return null;
        var account = await db.UserBankAccounts.Include(x => x.BankCodeNavigation)
            .SingleOrDefaultAsync(x => x.UserId == order.Order.BuyerUserId && x.IsDefault, ct);
        var history = await db.AuditLogs.Where(x => x.EntityType == "RejectedOrderRefund"
            && x.EntityId == id).OrderBy(x => x.OccurredAt).ThenBy(x => x.AuditLogId).ToListAsync(ct);
        return new(refund, order, account, history);
    }

    public async Task<IReadOnlyList<long>> PendingAsync(CancellationToken ct) => await Requests.AsNoTracking()
        .Where(x => x.RefundStatus == "Processing" || x.RefundStatus == "NeedsReconciliation")
        .OrderBy(x => x.RefundTransactionId).Select(x => x.RefundTransactionId).ToListAsync(ct);

    public async Task<bool> BankEventUsedAsync(string eventKey, string reference, CancellationToken ct) =>
        await db.RefundTransactions.AnyAsync(x => x.GatewayReference == reference, ct)
        || await db.FarmerPayouts.AnyAsync(x => x.BankTransferRef == reference, ct)
        || await db.AuditLogs.AnyAsync(x => x.TraceId == eventKey, ct);

    public void Audit(AuditLog row) => db.AuditLogs.Add(row);
    public void Notify(Notification row) => db.Notifications.Add(row);

    public async Task<T> AtomicAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        var outer = db.Database.CurrentTransaction;
        await using var owned = outer == null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var tx = owned ?? outer!;
        if (outer != null) await tx.CreateSavepointAsync("RejectedOrderRefund", ct);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = tx.GetDbTransaction();
            // Same outgoing-event lock as payouts: one bank debit cannot settle two operations.
            command.CommandText = "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'AgriSustain.FarmerPayout', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; SELECT @r;";
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) < 0)
                throw new DbUpdateConcurrencyException();
            if (db.ChangeTracker.HasChanges()) throw new InvalidOperationException("Dirty refund unit of work.");
            db.ChangeTracker.Clear();
            T result = await operation();
            await db.SaveChangesAsync(ct);
            if (owned != null) await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            if (owned != null) await tx.RollbackAsync(ct);
            else await tx.RollbackToSavepointAsync("RejectedOrderRefund", ct);
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
