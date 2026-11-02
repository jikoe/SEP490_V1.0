namespace AgriSustain.Domain.Entities;

public partial class CropSeason
{
    public static CropSeason CreatePlan(CropSeason input)
    {
        input.Status = "Planned";
        return input;
    }


    public void UpdatePlan(CropSeason input)
    {
        if (Status is not ("Planned" or "Planning"))
            throw new InvalidOperationException("Season is not a plan.");
        SeasonName = input.SeasonName;
        ProductId = input.ProductId;
        CropVariety = input.CropVariety;
        CultivatedArea = input.CultivatedArea;
        PlantingDate = input.PlantingDate;
        EstimatedHarvestDate = input.EstimatedHarvestDate;
        EstimatedYield = input.EstimatedYield;
        YieldUnit = input.YieldUnit;
    }


    public void AdvanceStage(string nextStage) => Status = nextStage;

    public void Complete(DateOnly date, decimal actualYield)
    {
        Status = "Completed";
        ActualHarvestDate = date;
        ActualYield = actualYield;
    }


    public void CancelOrFail(string outcome, decimal actualYield)
    {
        Status = outcome;
        ActualYield = actualYield;
    }
}

public partial class CultivationLog
{
    public static CultivationLog RecordActivity(CultivationLog input) => input;

    public CultivationLog AppendCorrection(CultivationLog corrected)
    {
        if (CorrectsLogId.HasValue)
            throw new InvalidOperationException("Select an original activity.");
        corrected.CorrectsLogId = CultivationLogId;
        corrected.CropSeasonId = CropSeasonId;
        return corrected;
    }
}

public partial class HarvestBatch
{
    public void SavePlan(string code, DateOnly estimate, decimal yield, string? notes)
    {
        if (StockEnteredAt.HasValue)
            throw new InvalidOperationException("Batch stock has already been accepted.");
        BatchCode = code;
        EstimatedHarvestDate = estimate;
        HarvestYield = yield;
        QualityNotes = notes;
        Status = "Planned";
        ActualHarvestDate = null;
        ExpirationDate = null;
        AvailableQuantity = 0;
        ReservedQuantity = 0;
    }


    public void RecordActualHarvestOnce(
        string code,
        DateOnly date,
        decimal yield,
        DateOnly expiry,
        string? notes,
        DateTime now)
    {
        if (StockEnteredAt.HasValue || ActualHarvestDate.HasValue)
            throw new InvalidOperationException("Initial stock-in cannot be repeated.");
        BatchCode = code;
        ActualHarvestDate = date;
        HarvestYield = yield;
        ExpirationDate = expiry;
        QualityNotes = notes;
        StockEnteredAt = now;
        Status = "Available";
    }


    public void UpdateDetails(DateOnly expiry, string? notes)
    {
        ExpirationDate = expiry;
        QualityNotes = notes;
    }


    public void CalculateAvailable(decimal adjustment, decimal held, decimal shipped)
    {
        decimal available = HarvestYield + adjustment - held - shipped;
        if (held < 0 || shipped < 0 || available < 0)
            throw new InvalidOperationException("Invalid inventory balance.");
        ReservedQuantity = held;
        AvailableQuantity = available;
    }


    public void BlockSuspectGoods()
    {
        Status = "Blocked";
        if (TraceabilityQrcode is { } qr)
            qr.IsRevoked = true;
    }
}

public partial class TraceabilityQrcode
{
    public static TraceabilityQrcode IssueForBatchOnce(
        HarvestBatch batch,
        string identity,
        string url,
        DateTime now)
    {
        if (batch.TraceabilityQrcode is not null)
            throw new InvalidOperationException("Batch already has a QR identity.");
        return new TraceabilityQrcode
        {
            HarvestBatchId = batch.HarvestBatchId,
            QrcontentString = identity,
            TargetUrl = url,
            GeneratedAt = now
        };
    }


    public string GetExistingIdentity() => QrcontentString;
}
