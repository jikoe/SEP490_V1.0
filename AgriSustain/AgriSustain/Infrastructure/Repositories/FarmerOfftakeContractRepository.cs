using System.Data;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Offtake;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class FarmerOfftakeContractRepository(AgriSustainContext db) : IFarmerOfftakeContractRepository
{
    public Task<FarmerProfile?> FarmerAsync(long actor, CancellationToken ct) =>
        db.FarmerProfiles.Include(x => x.User).SingleOrDefaultAsync(x => x.UserId == actor, ct);

    public async Task<object> SearchOwnedAsync(long actor, ContractQuery query, CancellationToken ct)
    {
        var source = db.B2bcontracts.AsNoTracking().Where(x => x.FarmerProfile.UserId == actor);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            string term = query.Keyword.Trim();
            source = source.Where(x => x.ContractNumber.Contains(term) || x.WholesaleBuyerProfile.CompanyName.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(query.Status)) source = source.Where(x => x.Status == query.Status);
        if (query.From is { } from) source = source.Where(x => x.CreatedAt >= from.ToDateTime(TimeOnly.MinValue));
        if (query.To is { } to) source = source.Where(x => x.CreatedAt < to.AddDays(1).ToDateTime(TimeOnly.MinValue));
        int total = await source.CountAsync(ct);
        var items = await source.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.B2bcontractId)
            .Skip((query.Page - 1) * 10).Take(10).Select(x => new
            {
                id = x.B2bcontractId,
                code = x.ContractNumber,
                buyer = x.WholesaleBuyerProfile.CompanyName,
                x.Status,
                x.ValidFrom,
                x.ValidUntil,
                x.CurrentVersionNo,
                x.DepositAmount,
                products = x.ContractLines.Where(l => l.ContractVersion.VersionNo == x.CurrentVersionNo)
                    .Select(l => new { l.Product.ProductName, l.UnitSnapshot, l.CommittedQuantity }).ToList(),
                quantity = x.ContractLines.Where(l => l.ContractVersion.VersionNo == x.CurrentVersionNo).Sum(l => l.CommittedQuantity)
            }).ToListAsync(ct);
        return new { items, total, query.Page, pageSize = 10, message = total == 0 ? FarmerBusinessMessages.Text("MSG124") : null };
    }

    public async Task<OfftakeContext?> LoadContextAsync(long actor, long id, bool update, CancellationToken ct)
    {
        // Lock the farm before the contract: capacity checks cover concurrent confirmations for the same farm.
        if (update)
        {
            await db.FarmerProfiles.FromSqlInterpolated($"SELECT * FROM dbo.FarmerProfile WITH (UPDLOCK,HOLDLOCK) WHERE UserId={actor}")
                .ToListAsync(ct);
        }
        var source = update
            ? db.B2bcontracts.FromSqlInterpolated($"SELECT * FROM dbo.B2BContract WITH (UPDLOCK,HOLDLOCK) WHERE B2BContractId={id}")
            : db.B2bcontracts.AsQueryable();
        var contract = await source.Where(x => x.B2bcontractId == id && x.FarmerProfile.UserId == actor)
            .Include(x => x.FarmerProfile).ThenInclude(x => x.User)
            .Include(x => x.WholesaleBuyerProfile).ThenInclude(x => x.User)
            .Include(x => x.ContractVersions).ThenInclude(x => x.ContractSignatures)
            .Include(x => x.ContractVersions).ThenInclude(x => x.ContractLines).ThenInclude(x => x.Product)
            .Include(x => x.ContractVersions).ThenInclude(x => x.ContractMilestones).ThenInclude(x => x.Invoices)
            .Include(x => x.ContractVersions).ThenInclude(x => x.ContractMilestones).ThenInclude(x => x.Shipments).ThenInclude(x => x.ShipmentItems)
                .ThenInclude(x => x.OrderItemBatchAllocation).ThenInclude(x => x.HarvestBatch)
            .Include(x => x.PaymentTransactions).ThenInclude(x => x.ContractDepositAllocations).ThenInclude(x => x.PaymentInvoiceAllocations)
            .Include(x => x.ContractTerminationRequest)
            .Include(x => x.DisputeTickets).ThenInclude(x => x.DisputeEvidences)
            .Include(x => x.SellerOrders).ThenInclude(x => x.OrderItems)
            .Include(x => x.SellerOrders).ThenInclude(x => x.Shipments).ThenInclude(x => x.ShipmentItems)
            .AsSplitQuery().SingleOrDefaultAsync(ct);
        if (contract is null) return null;
        long farmer = contract.FarmerProfileId;
        var seasons = await db.CropSeasons.Where(x => x.FarmerProfileId == farmer).ToListAsync(ct);
        var batches = update
            ? await db.HarvestBatches.FromSqlInterpolated($"SELECT h.* FROM dbo.HarvestBatch h WITH (UPDLOCK,HOLDLOCK) JOIN dbo.Product p ON p.ProductId=h.ProductId WHERE p.FarmerProfileId={farmer}")
                .Include(x => x.TraceabilityQrcode).OrderBy(x => x.HarvestBatchId).ToListAsync(ct)
            : await db.HarvestBatches.Where(x => x.Product.FarmerProfileId == farmer).Include(x => x.TraceabilityQrcode).ToListAsync(ct);
        var others = await db.ContractLines.Where(x => x.B2bcontractId != id && x.B2bcontract.FarmerProfileId == farmer
            && x.ContractVersion.VersionNo == x.B2bcontract.CurrentVersionNo
            && (x.B2bcontract.Status == "Active" || x.B2bcontract.Status == "AwaitingDeposit"
                || x.ContractVersion.ContractSignatures.Any(s => s.PartyRole == "Farmer" && s.ConfirmationStatus == "Confirmed"))
            && x.B2bcontract.Status != "Rejected").Include(x => x.B2bcontract).Include(x => x.ContractMilestones).ToListAsync(ct);
        var payouts = await db.FarmerPayouts.Where(x => x.FarmerProfileId == farmer
            && x.PayoutItems.Any(i => (i.ContractMilestone != null && i.ContractMilestone.B2bcontractId == id)
                || (i.InvoiceLine.Invoice.ContractMilestone != null && i.InvoiceLine.Invoice.ContractMilestone.B2bcontractId == id)))
            .Include(x => x.PayoutItems).ThenInclude(x => x.ContractMilestone)
            .Include(x => x.PayoutItems).ThenInclude(x => x.InvoiceLine).ThenInclude(x => x.Invoice).ToListAsync(ct);
        var refunds = await db.RefundTransactions.Where(x => x.PaymentTransaction.B2bcontractId == id
            || (x.PaymentTransaction.ContractMilestone != null && x.PaymentTransaction.ContractMilestone.B2bcontractId == id)
            || (x.DisputeTicket != null && (x.DisputeTicket.B2bcontractId == id
                || (x.DisputeTicket.SellerOrder != null && x.DisputeTicket.SellerOrder.B2bcontractId == id)))).ToListAsync(ct);
        return new OfftakeContext
        {
            CompletedContractCount = await db.B2bcontracts.CountAsync(x => x.FarmerProfileId == farmer && x.B2bcontractId != id && x.Status == "Completed", ct),
            NotificationFailed = await db.Notifications.AnyAsync(x => x.NotificationType == "FarmerOfftakeContract"
                && x.RelatedRecordId == id && x.DeliveryStatus == "Failed", ct),
            PreOrderCommitments = await db.PreOrderCampaigns.Where(x => x.CropSeason.FarmerProfileId == farmer
                && x.Status != "Cancelled").ToListAsync(ct),
            Contract = contract,
            Seasons = seasons,
            Batches = batches,
            OtherCommitments = others,
            Payouts = payouts,
            Refunds = refunds,
            Disputes = await db.DisputeTickets.Where(x => x.B2bcontractId == id
                || (x.SellerOrder != null && x.SellerOrder.B2bcontractId == id)).Include(x => x.DisputeEvidences).ToListAsync(ct),
            History = await db.AuditLogs.Where(x => x.EntityType == "FarmerOfftakeContract" && x.EntityId == id)
                .OrderBy(x => x.AuditLogId).ToListAsync(ct),
            Reviewers = await db.Users.Where(x => x.Status == "Active" && (x.Role == "CustomerSupport" || x.Role == "FinanceStaff"))
                .ToListAsync(ct)
        };
    }

    public async Task<FeatureResult<ContractActionResult>> SaveAtomicAsync(long actor, long id,
        Func<OfftakeContext, Task<FeatureResult<ContractActionResult>>> action, CancellationToken ct)
    {
        // Participate in an existing unit of work without committing the caller's transaction.
        var outer = db.Database.CurrentTransaction;
        await using var owned = outer == null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var transaction = owned ?? outer!;
        if (outer != null) await transaction.CreateSavepointAsync("FarmerOfftakeAction", ct);
        try
        {
            var context = await LoadContextAsync(actor, id, true, ct);
            if (context is null)
                return FeatureResult<ContractActionResult>.Fail("FORBIDDEN", FarmerBusinessMessages.Text("MSG121"));
            var result = await action(context);
            if (!result.IsSuccess)
            {
                if (outer == null) await transaction.RollbackAsync(ct);
                else await transaction.RollbackToSavepointAsync("FarmerOfftakeAction", ct);
                db.ChangeTracker.Clear();
                return result;
            }
            await db.SaveChangesAsync(ct);
            if (outer == null) await transaction.CommitAsync(ct);
            else await transaction.ReleaseSavepointAsync("FarmerOfftakeAction", ct);
            return result;
        }
        catch
        {
            if (outer == null) await transaction.RollbackAsync(CancellationToken.None);
            else await transaction.RollbackToSavepointAsync("FarmerOfftakeAction", CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public void Record(AuditLog entry) => db.AuditLogs.Add(entry);
    public void Queue(IEnumerable<Notification> notifications) => db.Notifications.AddRange(notifications);
}
