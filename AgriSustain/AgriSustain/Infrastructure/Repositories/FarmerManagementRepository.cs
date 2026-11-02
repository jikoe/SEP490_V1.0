using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public abstract class FarmerManagementRepository(
    AgriSustainContext database,
    TimeProvider clock) : IFarmerManagementRepository
{
    protected AgriSustainContext Db => database;

    protected TimeProvider Clock => clock;


    private static string Snapshot(object value) => JsonSerializer.Serialize(value switch
    {
        SeasonResponse s => s with { History = [] },
        BatchResponse b => b with { History = [] },
        _ => value
    });

    public Task<FarmerProfile?> GetFarmerAsync(
        long actor,
        bool forUpdate,
        CancellationToken ct) =>
        (forUpdate ? Db.FarmerProfiles.FromSqlInterpolated($"SELECT * FROM dbo.FarmerProfile WITH (UPDLOCK,HOLDLOCK) WHERE UserId={actor}") : Db.FarmerProfiles.Where(x => x.UserId == actor)).Include(x => x.User).SingleOrDefaultAsync(ct);

    public Task<bool> HasRequestAsync(
        string requestKey,
        CancellationToken ct) =>
        Db.AuditLogs.AnyAsync(x => x.TraceId == "farm:" + requestKey, ct);

    public async Task<IReadOnlyList<AuditLog>> HistoryAsync(
        string entity,
        long id,
        CancellationToken ct) =>
        await Db.AuditLogs.AsNoTracking().Include(x => x.PerformedByUser).Where(x => x.EntityType == entity && x.EntityId == id).OrderByDescending(x => x.AuditLogId).ToListAsync(ct);

    public void Audit(
        long actor,
        string entity,
        long id,
        string action,
        FarmCommand cmd,
        object? before,
        object after,
        string? reason = null) =>
        Db.AuditLogs.Add(new AuditLog { PerformedByUserId = actor, EntityType = entity, EntityId = id, Action = action, TraceId = "farm:" + cmd.RequestKey, Reason = reason, OldValue = before is null ? null : Snapshot(before), NewValue = Snapshot(after), OccurredAt = clock.GetUtcNow().UtcDateTime });

    public Task<Product?> ProductAsync(
        long actor,
        long id,
        CancellationToken ct) =>
        Db.Products.Include(x => x.FarmerProfile).SingleOrDefaultAsync(x => x.ProductId == id && x.FarmerProfile.UserId == actor, ct);

    protected IQueryable<CropSeason> Seasons(long actor) =>
        Db.CropSeasons.Where(x => x.FarmerProfile.UserId == actor).Include(x => x.Product).Include(x => x.FarmerProfile).Include(x => x.CultivationLogs).Include(x => x.HarvestBatches).ThenInclude(x => x.InventoryMovements).Include(x => x.PreOrderCampaigns).Include(x => x.ContractLines).ThenInclude(x => x.B2bcontract).Include(x => x.ContractLines).ThenInclude(x => x.ContractMilestones).AsSplitQuery();

    public Task<CropSeason?> SeasonAsync(
        long actor,
        long id,
        CancellationToken ct) =>
        Seasons(actor).SingleOrDefaultAsync(x => x.CropSeasonId == id, ct);

    public async Task<FarmOptions> OptionsAsync(long actor, DateOnly today, CancellationToken ct)
    {
        var farm = await GetFarmerAsync(actor, false, ct);
        var products = await Db.Products.AsNoTracking().Where(x => x.FarmerProfile.UserId == actor).OrderBy(x => x.ProductName).Select(x => new FarmProductOption(x.ProductId, x.ProductName, x.Unit)).ToListAsync(ct);
        var seasons = await Db.CropSeasons.AsNoTracking().Where(x => x.FarmerProfile.UserId == actor).OrderByDescending(x => x.CropSeasonId).Select(x => new FarmSeasonOption(x.CropSeasonId, x.ProductId, x.SeasonName, x.Status, x.PlantingDate, x.YieldUnit, x.CultivatedArea)).ToListAsync(ct);
        return new FarmOptions(farm?.FarmName ?? "", today, products, seasons.Select(x => x with { Status = FarmState.Stage(x.Status) }).ToList(), FarmState.Activities);
    }


    public async Task<IReadOnlyList<FarmCommitment>> CommitmentsAsync(
        long seasonId,
        long? batchId,
        CancellationToken ct)
    {
        var result = new List<FarmCommitment>();
        var now = clock.GetUtcNow().UtcDateTime;
        if (batchId.HasValue)
        {
            var allocations = await Db.OrderItemBatchAllocations.AsNoTracking().Where(x => x.HarvestBatchId == batchId && (x.AllocationStatus == "Reserved" || x.AllocationStatus == "Held" || x.AllocationStatus == "Active") && (!x.ExpiresAt.HasValue || x.ExpiresAt > now) && x.OrderItem.SellerOrder.Status != "Cancelled").Include(x => x.OrderItem).ThenInclude(x => x.SellerOrder).ThenInclude(x => x.Order).Include(x => x.OrderItem).ThenInclude(x => x.ContractLine).ThenInclude(x => x!.ContractMilestones).AsSplitQuery().ToListAsync(ct);
            foreach (var a in allocations)
            {
                var item = a.OrderItem;
                var delivery = item.SellerOrder.Order.ExpectedDeliveryDate;
                if (delivery.HasValue)
                    result.Add(new FarmCommitment("Đơn " + item.SellerOrder.SellerOrderCode, a.AllocatedQuantity, item.UnitSnapshot, DateOnly.FromDateTime(delivery.Value)));
                if (item.ContractLine is { } line)
                    foreach (var m in line.ContractMilestones.Where(x => x.DeliveryQuantity > 0 && x.DeliveredQuantity < x.DeliveryQuantity && x.FulfillmentStatus != "Cancelled"))
                        result.Add(new FarmCommitment("Lịch giao hợp đồng", a.AllocatedQuantity, line.UnitSnapshot, m.DueDate));
            }

            return result;
        }

        var campaigns = await Db.PreOrderCampaigns.AsNoTracking().Where(x => x.CropSeasonId == seasonId).Include(x => x.Product).Include(x => x.PreOrderReservations).ToListAsync(ct);
        foreach (var campaign in campaigns)
        {
            decimal held = campaign.PreOrderReservations.Where(x => x.ReservationStatus is "Reserved" or "Pending" && x.ExpiresAt > now).Sum(x => x.Quantity);
            decimal qty = campaign.CommittedYield + held;
            if (qty <= 0)
                continue;
            var deliveries = await Db.OrderItems.AsNoTracking().Where(x => x.PreOrderCampaignId == campaign.PreOrderCampaignId && x.SellerOrder.Status != "Cancelled").Select(x => x.SellerOrder.Order.ExpectedDeliveryDate).ToListAsync(ct);
            var dates = deliveries.Where(x => x.HasValue).Select(x => DateOnly.FromDateTime(x!.Value)).ToList();
            result.Add(new FarmCommitment("Đặt trước: " + campaign.CampaignName, qty, campaign.Product.Unit, dates.Count == 0 ? null : dates.Min()));
        }

        var lines = await Db.ContractLines.AsNoTracking().Where(x => x.CropSeasonId == seasonId).Include(x => x.B2bcontract).Include(x => x.ContractMilestones).ToListAsync(ct);
        foreach (var line in lines.Where(x => x.B2bcontract.Status is "Active" or "Signed" or "InProgress" or "Fulfilling"))
        {
            decimal delivered = line.ContractMilestones.Where(x => x.DeliveryQuantity.HasValue).Sum(x => x.DeliveredQuantity);
            decimal remaining = Math.Max(0, line.CommittedQuantity - delivered);
            if (remaining == 0)
                continue;
            var milestones = line.ContractMilestones.Where(x => x.DeliveryQuantity > x.DeliveredQuantity && x.FulfillmentStatus != "Cancelled").ToList();
            result.Add(new FarmCommitment("Hợp đồng #" + line.B2bcontractId, remaining, line.UnitSnapshot, milestones.Count == 0 ? null : milestones.Min(x => x.DueDate)));
        }

        return result;
    }


    protected static IQueryable<CropSeason> Filter(IQueryable<CropSeason> q, FarmQuery f)
    {
        if (!string.IsNullOrWhiteSpace(f.Keyword))
        {
            var s = f.Keyword.Trim();
            q = q.Where(x => x.SeasonName.Contains(s) || x.Product.ProductName.Contains(s) || x.CropVariety.Contains(s));
        }

        if (f.Status is { Length: > 0 })
            q = f.Status == "Planned" ? q.Where(x => x.Status == "Planned" || x.Status == "Planning") : q.Where(x => x.Status == f.Status);
        if (f.ProductId.HasValue)
            q = q.Where(x => x.ProductId == f.ProductId);
        if (f.From.HasValue)
            q = q.Where(x => x.PlantingDate >= f.From);
        if (f.To.HasValue)
            q = q.Where(x => x.PlantingDate <= f.To);
        return q;
    }
}

public sealed class FarmerCropSeasonRepository(
    AgriSustainContext db,
    TimeProvider clock) : FarmerManagementRepository(db, clock), IFarmerCropSeasonRepository
{
    public async Task<(IReadOnlyList<CropSeason> Items, int Total)> SearchOwnedAsync(
        long actor,
        FarmQuery query,
        CancellationToken ct)
    {
        var q = Filter(Seasons(actor), query);
        var total = await q.CountAsync(ct);
        return (await q.OrderByDescending(x => x.CropSeasonId).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct), total);
    }


    public Task<CropSeason?> LoadOwnedContextAsync(
        long actor,
        long id,
        CancellationToken ct) =>
        SeasonAsync(actor, id, ct);

    public Task<bool> NameExistsAsync(
        long farm,
        string name,
        long? except,
        CancellationToken ct) =>
        Db.CropSeasons.AnyAsync(x => x.FarmerProfileId == farm && x.CropSeasonId != except && x.SeasonName.ToLower() == name.ToLower(), ct);

    public void Add(CropSeason season) => Db.CropSeasons.Add(season);

    public Task SaveSeasonAsync(IUnitOfWork unit, CancellationToken ct) => unit.FlushAsync(ct);
}

public sealed class FarmerFarmingLogRepository(
    AgriSustainContext db,
    TimeProvider clock) : FarmerManagementRepository(db, clock), IFarmerFarmingLogRepository
{
    private IQueryable<CultivationLog> Logs(long actor) =>
        Db.CultivationLogs.Where(x => x.CropSeason.FarmerProfile.UserId == actor).Include(x => x.RecordedByUser).Include(x => x.CropSeason).ThenInclude(x => x.FarmerProfile).Include(x => x.InverseCorrectsLog).AsSplitQuery();

    public async Task<(IReadOnlyList<CultivationLog> Items, int Total)> SearchOwnedAsync(
        long actor,
        FarmQuery f,
        CancellationToken ct)
    {
        var q = Logs(actor);
        if (f.SeasonId.HasValue)
            q = q.Where(x => x.CropSeasonId == f.SeasonId);
        if (f.Activity is { Length: > 0 })
            q = q.Where(x => x.ActionType == f.Activity);
        if (f.Keyword is { Length: > 0 })
        {
            var s = f.Keyword.Trim();
            q = q.Where(x => x.DetailDescription!.Contains(s) || x.InputMaterial!.Contains(s) || x.PerformedBy!.Contains(s) || x.ActionType.Contains(s) || x.CropSeason.SeasonName.Contains(s));
        }

        if (f.From.HasValue)
        {
            var d = f.From.Value.ToDateTime(TimeOnly.MinValue);
            q = q.Where(x => x.ActionDate >= d);
        }

        if (f.To.HasValue)
        {
            var d = f.To.Value.ToDateTime(TimeOnly.MaxValue);
            q = q.Where(x => x.ActionDate <= d);
        }

        var total = await q.CountAsync(ct);
        return (await q.OrderByDescending(x => x.ActionDate).ThenByDescending(x => x.CultivationLogId).Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToListAsync(ct), total);
    }


    public Task<CultivationLog?> LoadLogAsync(
        long actor,
        long id,
        CancellationToken ct) =>
        Logs(actor).SingleOrDefaultAsync(x => x.CultivationLogId == id, ct);

    public Task<CultivationLog?> LoadOriginalAsync(
        long actor,
        long id,
        CancellationToken ct) =>
        LoadLogAsync(actor, id, ct);

    public void Add(CultivationLog log) => Db.CultivationLogs.Add(log);

    public Task AppendLogAsync(IUnitOfWork unit, CancellationToken ct) => unit.FlushAsync(ct);
}

public sealed class FarmerHarvestBatchRepository(
    AgriSustainContext db,
    TimeProvider clock) : FarmerManagementRepository(db, clock), IFarmerHarvestBatchRepository
{
    private IQueryable<HarvestBatch> Batches(long actor) =>
        Db.HarvestBatches.Where(x => x.Product.FarmerProfile.UserId == actor).Include(x => x.Product).ThenInclude(x => x.FarmerProfile).Include(x => x.CropSeason).ThenInclude(x => x.CultivationLogs).Include(x => x.InventoryMovements).Include(x => x.TraceabilityQrcode).Include(x => x.OrderItemBatchAllocations).ThenInclude(x => x.ShipmentItems).AsSplitQuery();

    public async Task<(IReadOnlyList<HarvestBatch> Items, int Total)> SearchOwnedAsync(
        long actor,
        FarmQuery f,
        CancellationToken ct)
    {
        var q = Batches(actor);
        var today = FarmState.Today(Clock);
        if (f.Keyword is { Length: > 0 })
        {
            var s = f.Keyword.Trim();
            q = q.Where(x => x.BatchCode.Contains(s) || x.Product.ProductName.Contains(s) || x.CropSeason.SeasonName.Contains(s));
        }

        if (f.ProductId.HasValue)
            q = q.Where(x => x.ProductId == f.ProductId);
        if (f.SeasonId.HasValue)
            q = q.Where(x => x.CropSeasonId == f.SeasonId);
        if (f.Status == "Planned")
            q = q.Where(x => !x.StockEnteredAt.HasValue);
        else if (f.Status == "Expired")
            q = q.Where(x => x.StockEnteredAt.HasValue && x.ExpirationDate <= today && x.Status != "Blocked" && x.Status != "Recalled");
        else if (f.Status == "Actual")
            q = q.Where(x => x.StockEnteredAt.HasValue && x.AvailableQuantity > 0 && x.ExpirationDate > today && x.Status != "Blocked" && x.Status != "Recalled");
        else if (f.Status == "Allocated")
            q = q.Where(x => x.StockEnteredAt.HasValue && x.AvailableQuantity == 0 && x.ExpirationDate > today && x.Status != "Blocked" && x.Status != "Recalled");
        else if (f.Status is { Length: > 0 })
            q = q.Where(x => x.Status == f.Status);
        if (f.From.HasValue)
            q = q.Where(x => (x.ActualHarvestDate ?? x.EstimatedHarvestDate) >= f.From);
        if (f.To.HasValue)
            q = q.Where(x => (x.ActualHarvestDate ?? x.EstimatedHarvestDate) <= f.To);
        var total = await q.CountAsync(ct);
        return (await q.OrderByDescending(x => x.HarvestBatchId).Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToListAsync(ct), total);
    }


    public Task<HarvestBatch?> LoadOwnedContextAsync(
        long actor,
        long id,
        CancellationToken ct) =>
        Batches(actor).SingleOrDefaultAsync(x => x.HarvestBatchId == id, ct);

    public Task<bool> CodeExistsAsync(
        string code,
        long? except,
        CancellationToken ct) =>
        Db.HarvestBatches.AnyAsync(x => x.BatchCode == code && x.HarvestBatchId != except, ct);

    public void Add(HarvestBatch batch) => Db.HarvestBatches.Add(batch);

    public void AddMovement(InventoryMovement movement) => Db.InventoryMovements.Add(movement);

    public Task SaveBatchAsync(IUnitOfWork unit, CancellationToken ct) => unit.FlushAsync(ct);

    public Task SaveQrAsync(IUnitOfWork unit, CancellationToken ct) => unit.FlushAsync(ct);
}
