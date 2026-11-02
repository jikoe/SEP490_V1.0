using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class FarmerFarmingLogService(
    IFarmerFarmingLogRepository repo,
    IUnitOfWork unit,
    FarmerFarmingLogRules rules,
    IFarmerEvidenceStorage storage,
    TimeProvider clock) : FarmerManagementService(repo, unit)
{
    public Task<FeatureResult<FarmOptions>> OptionsAsync(
        long actor,
        CancellationToken ct) =>
        Read(actor, () => repo.OptionsAsync(actor, FarmState.Today(clock), ct), ct);

    public Task<FeatureResult<PageResult<FarmingLogResponse>>> GetLogsAsync(
        long actor,
        FarmQuery q,
        CancellationToken ct) =>
        Read(actor, async () =>
    {
        FarmState.Query(q);
        var rows = await repo.SearchOwnedAsync(actor, q, ct);
        return new PageResult<FarmingLogResponse>(rows.Items.Select(x => Map(actor, x)).ToList(), rows.Total, q.Page, q.PageSize);
    }, ct);

    public Task<FeatureResult<FarmingLogResponse>> GetLogAsync(
        long actor,
        long id,
        CancellationToken ct) =>
        Read(actor, async () => Map(actor, Owned(await repo.LoadLogAsync(actor, id, ct))), ct);

    public Task<FeatureResult<FarmingLogResponse>> RecordLogAsync(
        long actor,
        FarmingLogRequest input,
        CancellationToken ct) =>
        Save(actor, null, input, ct);

    public Task<FeatureResult<FarmingLogResponse>> CorrectLogAsync(
        long actor,
        long id,
        FarmingLogRequest input,
        CancellationToken ct) =>
        Save(actor, id, input, ct);

    private Task<FeatureResult<FarmingLogResponse>> Save(
        long actor,
        long? originalId,
        FarmingLogRequest input,
        CancellationToken ct) =>
        Write(actor, input, originalId.HasValue, async _ =>
    {
        var s = Owned(await repo.SeasonAsync(actor, input.CropSeasonId, ct));
        CultivationLog? original = null;
        if (originalId.HasValue)
        {
            original = Owned(await repo.LoadOriginalAsync(actor, originalId.Value, ct));
            Match(input.Version, Map(actor, original).Version);
            rules.ValidateCorrection(actor, s, original, input);
        }
        else
            rules.ValidateActivity(actor, s, input);
        var log = new CultivationLog
        {
            CropSeasonId = s.CropSeasonId,
            CropSeason = s,
            RecordedByUserId = actor,
            ActionDate = input.ActionDate.ToDateTime(TimeOnly.MinValue),
            ActionType = input.ActionType.Trim(),
            DetailDescription = input.DetailDescription.Trim(),
            InputMaterial = input.InputMaterial?.Trim(),
            Dosage = input.Dosage?.Trim(),
            PerformedBy = input.PerformedBy.Trim(),
            ImageUrl = await storage.StoreReferencesAsync(actor, input.EvidenceUrls, ct),
            RecordedAt = clock.GetUtcNow().UtcDateTime,
            CorrectionReason = original is null ? null : input.CorrectionReason.Trim()
        };
        log = original is null ? CultivationLog.RecordActivity(log) : original.AppendCorrection(log);
        repo.Add(log);
        await repo.AppendLogAsync(Unit, ct);
        repo.Audit(actor, "CultivationLog", log.CultivationLogId, original is null ? "RecordActivity" : "AppendCorrection", input, original is null ? null : Map(actor, original), input, log.CorrectionReason);
        await Unit.FlushAsync(ct);
        var saved = Owned(await repo.LoadLogAsync(actor, log.CultivationLogId, ct));
        return Map(actor, saved)with
        {
            Message = original is null ? FarmManagementMessages.MSG94 : FarmManagementMessages.MSG132
        };
    }, ct);

    private FarmingLogResponse Map(
        long actor,
        CultivationLog l) =>
        new(l.CultivationLogId, l.CropSeasonId, l.CropSeason.SeasonName, DateOnly.FromDateTime(l.ActionDate), l.ActionType, l.DetailDescription, l.InputMaterial, l.Dosage, l.PerformedBy, l.RecordedByUser?.FullName ?? "", l.RecordedAt, storage.ReadReferences(actor, l.ImageUrl), l.CorrectsLogId, l.CorrectionReason, FarmState.Stamp(new { l.CultivationLogId, l.RecordedAt, l.CorrectsLogId, l.CorrectionReason, Corrections = l.InverseCorrectsLog.Select(x => x.CultivationLogId).Order().ToArray() }));
}
