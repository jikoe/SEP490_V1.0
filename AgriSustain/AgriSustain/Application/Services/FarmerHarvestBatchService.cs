using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class FarmerHarvestBatchService(
    IFarmerHarvestBatchRepository repo,
    IUnitOfWork unit,
    FarmerHarvestBatchRules rules,
    IFarmerEvidenceStorage storage,
    FarmerTraceabilityLabelService labels,
    TimeProvider clock,
    IConfiguration config) : FarmerManagementService(repo, unit)
{
    public Task<FeatureResult<FarmOptions>> OptionsAsync(
        long actor,
        CancellationToken ct) =>
        Read(actor, () => repo.OptionsAsync(actor, FarmState.Today(clock), ct), ct);

    public Task<FeatureResult<PageResult<BatchResponse>>> GetBatchesAsync(
        long actor,
        FarmQuery q,
        CancellationToken ct) =>
        Read(actor, async () =>
    {
        FarmState.Query(q);
        FarmState.Require(q.Status is null or "" or "Planned" or "Actual" or "Allocated" or "Blocked" or "Recalled" or "Expired", FarmManagementMessages.MSG120, "status");
        var rows = await repo.SearchOwnedAsync(actor, q, ct);
        var mapped = new List<BatchResponse>();
        foreach (var b in rows.Items)
            mapped.Add(await Map(b, ct));
        return new PageResult<BatchResponse>(mapped, rows.Total, q.Page, q.PageSize);
    }, ct);

    public Task<FeatureResult<BatchResponse>> GetBatchAsync(
        long actor,
        long id,
        CancellationToken ct) =>
        Read(actor, async () => await Map(Owned(await repo.LoadOwnedContextAsync(actor, id, ct)), ct), ct);

    public Task<FeatureResult<BatchResponse>> SavePlanAsync(
        long actor,
        long? id,
        HarvestBatchRequest input,
        CancellationToken ct) =>
        Save(actor, id, input, false, ct);

    public Task<FeatureResult<BatchResponse>> RecordHarvestAsync(
        long actor,
        long? id,
        HarvestBatchRequest input,
        CancellationToken ct) =>
        Save(actor, id, input, true, ct);

    private Task<FeatureResult<BatchResponse>> Save(
        long actor,
        long? id,
        HarvestBatchRequest input,
        bool actual,
        CancellationToken ct) =>
        Write(actor, input, id.HasValue, async _ =>
    {
        var b = id.HasValue ? Owned(await repo.LoadOwnedContextAsync(actor, id.Value, ct)) : null;
        var before = b is null ? null : await Map(b, ct);
        if (before is not null)
            Match(input.Version, before.Version);
        var product = Owned(await repo.ProductAsync(actor, input.ProductId, ct));
        var season = Owned(await repo.SeasonAsync(actor, input.CropSeasonId, ct));
        if (actual)
            rules.ValidateHarvest(b, season, product, input);
        else
            rules.ValidatePlan(b, season, product, input);
        FarmState.Require(!await repo.CodeExistsAsync(input.BatchCode.Trim(), id, ct), FarmManagementMessages.MSG101, "batchCode");
        if (b is not null)
            FarmState.Require(b.PreOrderCampaignId is null || (b.ProductId == input.ProductId && b.CropSeasonId == input.CropSeasonId), FarmManagementMessages.MSG130);
        b ??= new HarvestBatch
        {
            Product = product,
            ProductId = product.ProductId,
            CropSeason = season,
            CropSeasonId = season.CropSeasonId
        };
        b.ProductId = product.ProductId;
        b.Product = product;
        b.CropSeasonId = season.CropSeasonId;
        b.CropSeason = season;
        if (actual)
        {
            b.RecordActualHarvestOnce(input.BatchCode.Trim(), input.ActualHarvestDate!.Value, input.ActualYield, input.ExpirationDate!.Value, input.QualityNotes?.Trim(), clock.GetUtcNow().UtcDateTime);
            rules.ProtectCommitments(await repo.CommitmentsAsync(season.CropSeasonId, id, ct), input.ExpirationDate.Value);
            rules.ReconcileStock(b);
            var balance = rules.Balance(b);
            b.CalculateAvailable(balance.Adjustment, balance.Held, balance.Shipped);
        }
        else
            b.SavePlan(input.BatchCode.Trim(), input.EstimatedHarvestDate!.Value, input.EstimatedYield, input.QualityNotes?.Trim());
        if (!id.HasValue)
            repo.Add(b);
        await repo.SaveBatchAsync(Unit, ct);
        if (actual)
            repo.AddMovement(new InventoryMovement { HarvestBatchId = b.HarvestBatchId, RecordedByUserId = actor, MovementType = "StockIn", QuantityChange = b.HarvestYield, Reason = "Ghi nhận thu hoạch thực tế", OccurredAt = clock.GetUtcNow().UtcDateTime, RequestKey = "farm-stock-in:" + b.HarvestBatchId });
        repo.Audit(actor, "HarvestBatch", b.HarvestBatchId, actual ? "RecordActualHarvest" : "SavePlan", input, before, input);
        await Unit.FlushAsync(ct);
        return (await Map(b, ct))with
        {
            Message = actual ? FarmManagementMessages.MSG135 : FarmManagementMessages.MSG134
        };
    }, ct);

    public Task<FeatureResult<BatchResponse>> UpdateDetailsAsync(
        long actor,
        long id,
        HarvestBatchRequest input,
        CancellationToken ct) =>
        Write(actor, input, true, async _ =>
    {
        var b = Owned(await repo.LoadOwnedContextAsync(actor, id, ct));
        var before = await Map(b, ct);
        Match(input.Version, before.Version);
        FarmState.Text(input.BatchCode, 100, "batchCode");
        FarmState.Text(input.Unit, 50, "unit");
        FarmState.Require(b.StockEnteredAt.HasValue, FarmManagementMessages.MSG131);
        FarmState.Text(input.Reason, 500, "reason");
        FarmState.Text(input.QualityNotes, 500, "qualityNotes", false);
        FarmState.Require(b.BatchCode == input.BatchCode.Trim() && b.ProductId == input.ProductId && b.CropSeasonId == input.CropSeasonId && b.ActualHarvestDate == input.ActualHarvestDate && b.HarvestYield == input.ActualYield && b.Product.Unit.Equals(input.Unit, StringComparison.OrdinalIgnoreCase), FarmManagementMessages.Immutable);
        FarmState.Require(input.ExpirationDate.HasValue && input.ExpirationDate > b.ActualHarvestDate, FarmManagementMessages.MSG102, "expirationDate");
        rules.ProtectCommitments(before.Commitments, input.ExpirationDate!.Value);
        b.UpdateDetails(input.ExpirationDate.Value, input.QualityNotes?.Trim());
        repo.Audit(actor, "HarvestBatch", id, "UpdateDetails", input, before, input, input.Reason.Trim());
        await repo.SaveBatchAsync(Unit, ct);
        return (await Map(b, ct))with
        {
            Message = FarmManagementMessages.MSG100
        };
    }, ct);

    public Task<FeatureResult<BatchResponse>> RecordIssueAsync(
        long actor,
        long id,
        BatchIssueRequest input,
        CancellationToken ct) =>
        Write(actor, input, true, async _ =>
    {
        var b = Owned(await repo.LoadOwnedContextAsync(actor, id, ct));
        var before = await Map(b, ct);
        Match(input.Version, before.Version);
        FarmState.Require(b.StockEnteredAt.HasValue, FarmManagementMessages.MSG131);
        FarmState.Text(input.Reason, 500, "reason");
        FarmState.Proof(storage, actor, input.EvidenceUrls, true, FarmManagementMessages.MSG120);
        FarmState.Require(input.IssueType is "quality" or "adjustment", FarmManagementMessages.MSG120, "issueType");
        if (input.IssueType == "quality")
        {
            FarmState.Amount(input.Quantity, "quantity", message: FarmManagementMessages.MSG103);
            FarmState.Require(input.Quantity <= b.HarvestYield + before.AdjustmentQuantity - before.ShippedQuantity, FarmManagementMessages.MSG103, "quantity");
            b.BlockSuspectGoods();
        }
        else
        {
            FarmState.Require(input.Quantity != 0 && Math.Abs(input.Quantity) <= 999999999999999m && decimal.Round(input.Quantity, 3) == input.Quantity, FarmManagementMessages.MSG103, "quantity");
            rules.ReconcileStock(b, input.Quantity);
            b.CalculateAvailable(before.AdjustmentQuantity + input.Quantity, before.ReservedQuantity, before.ShippedQuantity);
            repo.AddMovement(new InventoryMovement { HarvestBatchId = id, RecordedByUserId = actor, MovementType = "Adjustment", QuantityChange = input.Quantity, Reason = input.Reason.Trim(), OccurredAt = clock.GetUtcNow().UtcDateTime, RequestKey = "farm-adjust:" + input.RequestKey });
        }

        repo.Audit(actor, "HarvestBatch", id, input.IssueType == "quality" ? "QualityIssue" : "QuantityAdjustment", input, before, input, input.Reason.Trim());
        await repo.SaveBatchAsync(Unit, ct);
        return (await Map(b, ct))with
        {
            Message = FarmManagementMessages.MSG138
        };
    }, ct);

    public Task<FeatureResult<FarmLabelResponse>> GenerateQrAsync(
        long actor,
        long id,
        FarmCommand input,
        CancellationToken ct) =>
        Write(actor, input, true, async _ =>
    {
        var b = Owned(await repo.LoadOwnedContextAsync(actor, id, ct));
        var before = await Map(b, ct);
        Match(input.Version, before.Version);
        rules.ValidateQrEligibility(b);
        bool created = b.TraceabilityQrcode is null;
        if (created)
        {
            string identity = "AGRI-" + Guid.NewGuid().ToString("N");
            // The public lookup page is owned by another feature; use its configured URL when available.
            string? template = config["FarmerManagement:TraceabilityUrlTemplate"];
            string target = string.IsNullOrWhiteSpace(template) ? identity : template.Replace("{code}", Uri.EscapeDataString(identity), StringComparison.Ordinal);
            FarmState.Require(target.Length <= 500 && (target == identity || (Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")), FarmManagementMessages.MSG123);
            b.TraceabilityQrcode = TraceabilityQrcode.IssueForBatchOnce(b, identity, target, clock.GetUtcNow().UtcDateTime);
            repo.Audit(actor, "HarvestBatch", id, "IssueQr", input, null, new { identity, target });
            await repo.SaveQrAsync(Unit, ct);
        }

        return labels.RenderLabel(b, created ? FarmManagementMessages.MSG137 : FarmManagementMessages.MSG99);
    }, ct);

    public Task<FeatureResult<FarmLabelResponse>> GetLabelAsync(
        long actor,
        long id,
        CancellationToken ct) =>
        Read(actor, async () =>
    {
        var b = Owned(await repo.LoadOwnedContextAsync(actor, id, ct));
        rules.ValidateQrEligibility(b);
        FarmState.Require(b.TraceabilityQrcode is not null, FarmManagementMessages.MSG136);
        return labels.RenderLabel(b, FarmManagementMessages.MSG99);
    }, ct);

    private async Task<BatchResponse> Map(HarvestBatch b, CancellationToken ct)
    {
        var history = await repo.HistoryAsync("HarvestBatch", b.HarvestBatchId, ct);
        var balance = rules.Balance(b);
        bool actual = b.StockEnteredAt.HasValue;
        var available = actual ? b.HarvestYield + balance.Adjustment - balance.Held - balance.Shipped : 0;
        string status = !actual ? "Planned" : b.Status is "Blocked" or "Recalled" ? b.Status : b.ExpirationDate <= FarmState.Today(clock) ? "Expired" : available == 0 ? "Allocated" : "Actual";
        var version = FarmState.Stamp(new { b.HarvestBatchId, b.BatchCode, b.ProductId, b.CropSeasonId, b.EstimatedHarvestDate, b.ActualHarvestDate, b.ExpirationDate, b.HarvestYield, b.QualityNotes, b.Status, b.StockEnteredAt, b.AvailableQuantity, b.ReservedQuantity, Adjustment = balance.Adjustment, Held = balance.Held, Shipped = balance.Shipped, Last = history.FirstOrDefault()?.AuditLogId, Qr = b.TraceabilityQrcode?.QrcontentString });
        return new(b.HarvestBatchId, b.BatchCode, b.ProductId, b.Product.ProductName, b.CropSeasonId, b.CropSeason.SeasonName, b.Product.FarmerProfile.FarmName, b.Product.Unit, b.EstimatedHarvestDate, b.ActualHarvestDate, b.ExpirationDate, actual ? 0 : b.HarvestYield, actual ? b.HarvestYield : 0, balance.Adjustment, balance.Held, balance.Shipped, available, status, b.QualityNotes, b.CropSeason.CultivationLogs.Count, b.TraceabilityQrcode?.QrcontentString, b.TraceabilityQrcode?.TargetUrl, rules.IsEligible(b) && b.CropSeason.CultivationLogs.Count > 0, await repo.CommitmentsAsync(b.CropSeasonId, b.HarvestBatchId, ct), FarmState.History(history), version);
    }
}
