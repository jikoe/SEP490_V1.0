using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

/// <summary>Coordinates actual stock-in, permitted corrections, issue reporting and QR labels.</summary>
public sealed class FarmerHarvestBatchService(
    IFarmerHarvestBatchRepository repo,
    IUnitOfWork unit,
    FarmerHarvestBatchRules rules,
    IFarmerEvidenceStorage storage,
    FarmerTraceabilityLabelService labels,
    TimeProvider clock,
    IConfiguration config
) : FarmerManagementService(repo, unit)
{
    public Task<FeatureResult<FarmOptions>> OptionsAsync(long actor, CancellationToken ct) =>
        Read(actor, () => repo.OptionsAsync(actor, FarmState.Today(clock), ct), ct);

    public Task<FeatureResult<PageResult<BatchResponse>>> GetBatchesAsync(
        long actor,
        FarmQuery query,
        CancellationToken ct
    ) =>
        Read(
            actor,
            async () =>
            {
                FarmState.Query(query);
                FarmState.Require(
                    query.Status
                        is null
                            or ""
                            or "Actual"
                            or "Allocated"
                            or "Blocked"
                            or "Recalled"
                            or "Expired",
                    FarmManagementMessages.MSG120,
                    "status"
                );
                query = query with { PageSize = 10 };
                var rows = await repo.SearchOwnedAsync(actor, query, ct);
                var items = new List<BatchResponse>();
                foreach (var batch in rows.Items)
                    items.Add(await Map(batch, ct));
                return new PageResult<BatchResponse>(items, rows.Total, query.Page, 10);
            },
            ct
        );

    public Task<FeatureResult<BatchResponse>> GetBatchAsync(
        long actor,
        long id,
        CancellationToken ct
    ) =>
        Read(
            actor,
            async () => await Map(Owned(await repo.LoadOwnedContextAsync(actor, id, ct)), ct),
            ct
        );

    public Task<FeatureResult<BatchResponse>> RecordHarvestAsync(
        long actor,
        HarvestBatchRequest input,
        CancellationToken ct
    ) =>
        Write(
            actor,
            input,
            false,
            async _ =>
            {
                var product = Owned(await repo.ProductAsync(actor, input.ProductId, ct));
                var season = Owned(await repo.SeasonAsync(actor, input.CropSeasonId, ct));
                rules.ValidateHarvest(season, product, input);
                FarmState.Require(
                    !await repo.CodeExistsAsync(input.BatchCode.Trim(), null, ct),
                    FarmManagementMessages.MSG101,
                    "batchCode"
                );
                var batch = new HarvestBatch
                {
                    Product = product,
                    ProductId = product.ProductId,
                    CropSeason = season,
                    CropSeasonId = season.CropSeasonId,
                };
                var now = clock.GetUtcNow().UtcDateTime;
                batch.RecordActualHarvestOnce(
                    input.BatchCode.Trim(),
                    input.ActualHarvestDate!.Value,
                    input.ActualYield,
                    input.ExpirationDate!.Value,
                    input.QualityNotes?.Trim(),
                    now
                );
                rules.ReconcileStock(batch);
                var balance = rules.Balance(batch);
                batch.CalculateAvailable(balance.Adjustment, balance.Held, balance.Shipped);
                repo.Add(batch);
                // Flush obtains the identity; the outer write transaction still owns the complete operation.
                await repo.SaveBatchAsync(Unit, ct);
                repo.AddMovement(
                    new InventoryMovement
                    {
                        HarvestBatchId = batch.HarvestBatchId,
                        RecordedByUserId = actor,
                        MovementType = "StockIn",
                        QuantityChange = batch.HarvestYield,
                        Reason = FarmManagementMessages.StockInReason,
                        OccurredAt = now,
                        RequestKey = "farm-stock-in:" + batch.HarvestBatchId,
                    }
                );
                repo.Audit(
                    actor,
                    "HarvestBatch",
                    batch.HarvestBatchId,
                    "RecordActualHarvest",
                    input,
                    null,
                    input
                );
                await Unit.FlushAsync(ct);
                return (await Map(batch, ct)) with { Message = FarmManagementMessages.MSG135 };
            },
            ct
        );

    public Task<FeatureResult<BatchResponse>> UpdateDetailsAsync(
        long actor,
        long id,
        HarvestBatchRequest input,
        CancellationToken ct
    ) =>
        Write(
            actor,
            input,
            true,
            async _ =>
            {
                var b = Owned(await repo.LoadOwnedContextAsync(actor, id, ct));
                var before = await Map(b, ct);
                Match(input.Version, before.Version);
                FarmState.Text(input.BatchCode, 100, "batchCode");
                FarmState.Text(input.Unit, 50, "unit");
                FarmState.Require(b.StockEnteredAt.HasValue, FarmManagementMessages.MSG131);
                FarmState.Text(input.Reason, 500, "reason");
                FarmState.Text(input.QualityNotes, 500, "qualityNotes", false);
                FarmState.Require(
                    b.BatchCode == input.BatchCode.Trim()
                        && b.ProductId == input.ProductId
                        && b.CropSeasonId == input.CropSeasonId
                        && b.ActualHarvestDate == input.ActualHarvestDate
                        && b.HarvestYield == input.ActualYield
                        && b.Product.Unit.Equals(input.Unit, StringComparison.OrdinalIgnoreCase),
                    FarmManagementMessages.Immutable
                );
                FarmState.Require(
                    input.ExpirationDate.HasValue && input.ExpirationDate > b.ActualHarvestDate,
                    FarmManagementMessages.MSG102,
                    "expirationDate"
                );
                rules.ProtectCommitments(before.Commitments, input.ExpirationDate!.Value);
                b.UpdateDetails(input.ExpirationDate.Value, input.QualityNotes?.Trim());
                repo.Audit(
                    actor,
                    "HarvestBatch",
                    id,
                    "UpdateDetails",
                    input,
                    before,
                    input,
                    input.Reason.Trim()
                );
                await repo.SaveBatchAsync(Unit, ct);
                return (await Map(b, ct)) with { Message = FarmManagementMessages.MSG100 };
            },
            ct
        );

    public Task<FeatureResult<BatchResponse>> RecordIssueAsync(
        long actor,
        long id,
        BatchIssueRequest input,
        CancellationToken ct
    ) =>
        Write(
            actor,
            input,
            true,
            async _ =>
            {
                var b = Owned(await repo.LoadOwnedContextAsync(actor, id, ct));
                var before = await Map(b, ct);
                Match(input.Version, before.Version);
                FarmState.Require(b.StockEnteredAt.HasValue, FarmManagementMessages.MSG131);
                FarmState.Text(input.Reason, 500, "reason");
                FarmState.Proof(
                    storage,
                    actor,
                    input.EvidenceUrls,
                    true,
                    FarmManagementMessages.MSG120
                );
                FarmState.Require(
                    input.IssueType is "quality" or "adjustment",
                    FarmManagementMessages.MSG120,
                    "issueType"
                );
                if (input.IssueType == "quality")
                {
                    FarmState.Amount(
                        input.Quantity,
                        "quantity",
                        message: FarmManagementMessages.MSG103
                    );
                    FarmState.Require(
                        input.Quantity
                            <= b.HarvestYield + before.AdjustmentQuantity - before.ShippedQuantity,
                        FarmManagementMessages.MSG103,
                        "quantity"
                    );
                    b.BlockSuspectGoods();
                }
                else
                {
                    FarmState.Require(
                        input.Quantity != 0
                            && Math.Abs(input.Quantity) <= 999999999999999m
                            && decimal.Round(input.Quantity, 3) == input.Quantity,
                        FarmManagementMessages.MSG103,
                        "quantity"
                    );
                    rules.ReconcileStock(b, input.Quantity);
                    b.ApplyJustifiedAdjustment(input.Quantity, rules.Balance(b));
                    repo.AddMovement(
                        new InventoryMovement
                        {
                            HarvestBatchId = id,
                            RecordedByUserId = actor,
                            MovementType = "Adjustment",
                            QuantityChange = input.Quantity,
                            Reason = input.Reason.Trim(),
                            OccurredAt = clock.GetUtcNow().UtcDateTime,
                            RequestKey = "farm-adjust:" + input.RequestKey,
                        }
                    );
                }

                repo.Audit(
                    actor,
                    "HarvestBatch",
                    id,
                    input.IssueType == "quality" ? "QualityIssue" : "QuantityAdjustment",
                    input,
                    before,
                    input,
                    input.Reason.Trim()
                );
                await repo.SaveBatchAsync(Unit, ct);
                return (await Map(b, ct)) with { Message = FarmManagementMessages.MSG138 };
            },
            ct
        );

    public Task<FeatureResult<FarmLabelResponse>> GenerateQrAsync(
        long actor,
        long id,
        FarmCommand input,
        CancellationToken ct
    ) =>
        Write(
            actor,
            input,
            true,
            async _ =>
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
                    string baseUrl =
                        config["EmailSettings:FrontendBaseUrl"] ?? "http://localhost:5173";
                    string target = string.IsNullOrWhiteSpace(template)
                        ? baseUrl.TrimEnd('/')
                            + "/Guest/trace.html?code="
                            + Uri.EscapeDataString(identity)
                        : template.Replace(
                            "{code}",
                            Uri.EscapeDataString(identity),
                            StringComparison.Ordinal
                        );
                    FarmState.Require(
                        target.Length <= 500
                            && (
                                Uri.TryCreate(target, UriKind.Absolute, out var uri)
                                && uri.Scheme is "http" or "https"
                            ),
                        FarmManagementMessages.MSG123
                    );
                    b.TraceabilityQrcode = TraceabilityQrcode.IssueForBatchOnce(
                        b,
                        identity,
                        target,
                        clock.GetUtcNow().UtcDateTime
                    );
                    repo.Audit(
                        actor,
                        "HarvestBatch",
                        id,
                        "IssueQr",
                        input,
                        null,
                        new { identity, target }
                    );
                    await repo.SaveQrAsync(Unit, ct);
                }

                return labels.RenderLabel(
                    b,
                    created ? FarmManagementMessages.MSG137 : FarmManagementMessages.MSG99
                );
            },
            ct
        );

    public Task<FeatureResult<FarmLabelResponse>> GetLabelAsync(
        long actor,
        long id,
        CancellationToken ct
    ) =>
        Read(
            actor,
            async () =>
            {
                var b = Owned(await repo.LoadOwnedContextAsync(actor, id, ct));
                rules.ValidateQrEligibility(b);
                FarmState.Require(b.TraceabilityQrcode is not null, FarmManagementMessages.MSG136);
                return labels.RenderLabel(b, FarmManagementMessages.MSG99);
            },
            ct
        );

    private async Task<BatchResponse> Map(HarvestBatch batch, CancellationToken ct)
    {
        var history = await repo.HistoryAsync("HarvestBatch", batch.HarvestBatchId, ct);
        var balance = rules.Balance(batch);
        decimal available =
            batch.HarvestYield + balance.Adjustment - balance.Held - balance.Shipped;
        string status =
            batch.Status is "Blocked" or "Suspect" ? "Blocked"
            : batch.Status == "Recalled" ? "Recalled"
            : batch.Status == "Expired" || batch.ExpirationDate <= FarmState.Today(clock)
                ? "Expired"
            : available == 0 ? "Allocated"
            : "Actual";
        string version = FarmState.Stamp(
            new
            {
                batch.HarvestBatchId,
                batch.BatchCode,
                batch.ProductId,
                batch.CropSeasonId,
                batch.ActualHarvestDate,
                batch.ExpirationDate,
                batch.HarvestYield,
                batch.QualityNotes,
                batch.Status,
                batch.StockEnteredAt,
                batch.AvailableQuantity,
                batch.ReservedQuantity,
                Adjustment = balance.Adjustment,
                Held = balance.Held,
                Shipped = balance.Shipped,
                Last = history.FirstOrDefault()?.AuditLogId,
                Qr = batch.TraceabilityQrcode?.QrcontentString,
            }
        );
        return new BatchResponse(
            batch.HarvestBatchId,
            batch.BatchCode,
            batch.ProductId,
            batch.Product.ProductName,
            batch.CropSeasonId,
            batch.CropSeason.SeasonName,
            batch.Product.FarmerProfile.FarmName,
            batch.Product.Unit,
            batch.ActualHarvestDate,
            batch.ExpirationDate,
            batch.HarvestYield,
            balance.Adjustment,
            balance.Held,
            balance.Shipped,
            available,
            status,
            batch.QualityNotes,
            batch.CropSeason.CultivationLogs.Count,
            batch.TraceabilityQrcode?.QrcontentString,
            batch.TraceabilityQrcode?.TargetUrl,
            rules.IsEligible(batch) && batch.CropSeason.CultivationLogs.Count > 0,
            await repo.CommitmentsAsync(batch.CropSeasonId, batch.HarvestBatchId, ct),
            FarmState.History(history),
            version
        );
    }
}
