using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class FarmerCropSeasonService(
    IFarmerCropSeasonRepository repo,
    IUnitOfWork unit,
    FarmerCropSeasonRules rules,
    IFarmerEvidenceStorage storage,
    TimeProvider clock) : FarmerManagementService(repo, unit)
{
    public Task<FeatureResult<FarmOptions>> OptionsAsync(
        long actor,
        CancellationToken ct) =>
        Read(actor, () => repo.OptionsAsync(actor, FarmState.Today(clock), ct), ct);

    public Task<FeatureResult<PageResult<SeasonResponse>>> GetSeasonsAsync(
        long actor,
        FarmQuery q,
        CancellationToken ct) =>
        Read(actor, async () =>
    {
        FarmState.Query(q);
        FarmState.Require(string.IsNullOrEmpty(q.Status) || FarmState.Stages.Contains(q.Status), FarmManagementMessages.MSG120, "status");
        var rows = await repo.SearchOwnedAsync(actor, q, ct);
        var items = new List<SeasonResponse>();
        foreach (var s in rows.Items)
            items.Add(await Map(s, ct));
        return new PageResult<SeasonResponse>(items, rows.Total, q.Page, q.PageSize);
    }, ct);

    public Task<FeatureResult<SeasonResponse>> GetSeasonAsync(
        long actor,
        long id,
        CancellationToken ct) =>
        Read(actor, async () => await Map(Owned(await repo.LoadOwnedContextAsync(actor, id, ct)), ct), ct);

    public Task<FeatureResult<SeasonResponse>> SavePlanAsync(
        long actor,
        long? id,
        SeasonPlanRequest input,
        CancellationToken ct) =>
        Write(actor, input, id.HasValue, async farm =>
    {
        var s = id.HasValue ? Owned(await repo.LoadOwnedContextAsync(actor, id.Value, ct)) : null;
        var before = s is null ? null : await Map(s, ct);
        if (before is not null)
            Match(input.Version, before.Version);
        var product = Owned(await repo.ProductAsync(actor, input.ProductId, ct));
        rules.ValidatePlan(s, input, product);
        FarmState.Require(!await repo.NameExistsAsync(farm.FarmerProfileId, input.SeasonName.Trim(), id, ct), FarmManagementMessages.DuplicateSeason, "seasonName");
        if (s is not null)
            rules.ProtectCommitments(await repo.CommitmentsAsync(s.CropSeasonId, null, ct), input.EstimatedYield, input.YieldUnit, input.EstimatedHarvestDate);
        var candidate = new CropSeason
        {
            FarmerProfileId = farm.FarmerProfileId,
            ProductId = product.ProductId,
            Product = product,
            FarmerProfile = farm,
            SeasonName = input.SeasonName.Trim(),
            CropVariety = input.CropVariety.Trim(),
            CultivatedArea = input.CultivatedArea,
            PlantingDate = input.PlantingDate,
            EstimatedHarvestDate = input.EstimatedHarvestDate,
            EstimatedYield = input.EstimatedYield,
            YieldUnit = input.YieldUnit.Trim()
        };
        if (s is null)
        {
            s = CropSeason.CreatePlan(candidate);
            repo.Add(s);
        }
        else
            s.UpdatePlan(candidate);
        await repo.SaveSeasonAsync(Unit, ct);
        repo.Audit(actor, "CropSeason", s.CropSeasonId, id.HasValue ? "UpdatePlan" : "CreatePlan", input, before, input);
        await Unit.FlushAsync(ct);
        return (await Map(s, ct))with
        {
            Message = id.HasValue ? FarmManagementMessages.MSG90 : FarmManagementMessages.MSG89
        };
    }, ct);

    public Task<FeatureResult<SeasonResponse>> ChangeStageAsync(
        long actor,
        long id,
        SeasonActionRequest input,
        CancellationToken ct) =>
        Write(actor, input, true, async _ =>
    {
        var s = Owned(await repo.LoadOwnedContextAsync(actor, id, ct));
        var before = await Map(s, ct);
        Match(input.Version, before.Version);
        rules.ValidateStageChange(s, input.NextStage);
        s.AdvanceStage(input.NextStage);
        repo.Audit(actor, "CropSeason", id, "ChangeStage", input, before, new { s.Status });
        await repo.SaveSeasonAsync(Unit, ct);
        return (await Map(s, ct))with
        {
            Message = input.NextStage == "Harvesting" ? FarmManagementMessages.MSG128 : FarmManagementMessages.MSG90
        };
    }, ct);

    public Task<FeatureResult<SeasonResponse>> CompleteSeasonAsync(
        long actor,
        long id,
        SeasonActionRequest input,
        CancellationToken ct) =>
        Write(actor, input, true, async _ =>
    {
        var s = Owned(await repo.LoadOwnedContextAsync(actor, id, ct));
        var before = await Map(s, ct);
        Match(input.Version, before.Version);
        rules.ReconcileHarvest(s, input, false);
        rules.ProtectCommitments(before.Commitments, input.ActualYield, s.YieldUnit, input.CompletionDate!.Value);
        s.Complete(input.CompletionDate.Value, input.ActualYield);
        repo.Audit(actor, "CropSeason", id, "Complete", input, before, input);
        await repo.SaveSeasonAsync(Unit, ct);
        return (await Map(s, ct))with
        {
            Message = FarmManagementMessages.MSG91
        };
    }, ct);

    public Task<FeatureResult<SeasonResponse>> CancelSeasonAsync(
        long actor,
        long id,
        SeasonActionRequest input,
        CancellationToken ct) =>
        Write(actor, input, true, async _ =>
    {
        var s = Owned(await repo.LoadOwnedContextAsync(actor, id, ct));
        var before = await Map(s, ct);
        Match(input.Version, before.Version);
        FarmState.Require(input.Outcome is "Cancelled" or "Crop Failure", FarmManagementMessages.MSG120, "outcome");
        FarmState.Text(input.Reason, 500, "reason");
        FarmState.Proof(storage, actor, input.EvidenceUrls, true, FarmManagementMessages.MSG120);
        rules.ReconcileHarvest(s, input, true);
        s.CancelOrFail(input.Outcome, input.ActualYield);
        // Stop accepting new purchases from this cancelled production plan, retain paid obligations.
        foreach (var campaign in s.PreOrderCampaigns.Where(x => x.Status is "Active" or "Open" or "Scheduled"))
            campaign.Status = "Closed";
        repo.Audit(actor, "CropSeason", id, "CancelOrFail", input, before, input, input.Reason.Trim());
        await repo.SaveSeasonAsync(Unit, ct);
        return (await Map(s, ct))with
        {
            Message = FarmManagementMessages.MSG129
        };
    }, ct);

    private async Task<SeasonResponse> Map(CropSeason s, CancellationToken ct)
    {
        var history = await repo.HistoryAsync("CropSeason", s.CropSeasonId, ct);
        var commitments = await repo.CommitmentsAsync(s.CropSeasonId, null, ct);
        var version = FarmState.Stamp(new { s.CropSeasonId, s.SeasonName, s.ProductId, s.CropVariety, s.CultivatedArea, s.PlantingDate, s.EstimatedHarvestDate, s.EstimatedYield, s.YieldUnit, s.ActualYield, s.ActualHarvestDate, s.Status, Harvest = rules.RecordedYield(s), Last = history.FirstOrDefault()?.AuditLogId });
        return new(s.CropSeasonId, s.SeasonName, s.ProductId, s.Product.ProductName, s.CropVariety, s.CultivatedArea, s.PlantingDate, s.EstimatedHarvestDate, s.ActualHarvestDate, s.EstimatedYield, s.ActualYield, s.YieldUnit, FarmState.Stage(s.Status), rules.RecordedYield(s), s.CultivationLogs.Count, commitments, FarmState.History(history), version)
        {
            Activities = s.CultivationLogs.OrderByDescending(x => x.ActionDate).ThenByDescending(x => x.CultivationLogId).Select(x => new SeasonActivity(x.CultivationLogId, x.ActionDate, x.ActionType, x.DetailDescription, x.CorrectsLogId, x.CorrectionReason)).ToList(),
            HarvestBatches = s.HarvestBatches.Where(x => x.StockEnteredAt.HasValue).Select(x => new SeasonHarvest(x.HarvestBatchId, x.BatchCode, x.ActualHarvestDate, x.HarvestYield, s.Product.Unit)).ToList()
        };
    }
}
