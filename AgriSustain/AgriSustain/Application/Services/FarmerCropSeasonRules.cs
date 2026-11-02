using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class FarmerCropSeasonRules(TimeProvider clock)
{
    public void ValidatePlan(CropSeason? current, SeasonPlanRequest input, Product product)
    {
        FarmState.Text(input.SeasonName, 200, "seasonName");
        FarmState.Text(input.CropVariety, 150, "cropVariety");
        FarmState.Text(input.YieldUnit, 50, "yieldUnit");
        FarmState.Amount(input.CultivatedArea, "cultivatedArea");
        FarmState.Amount(input.EstimatedYield, "estimatedYield", message: FarmManagementMessages.MSG93);
        FarmState.Require(input.PlantingDate != default && input.EstimatedHarvestDate > input.PlantingDate && (current is not null || input.EstimatedHarvestDate > FarmState.Today(clock)), FarmManagementMessages.MSG92, "estimatedHarvestDate");
        _ = FarmState.ConvertUnit(input.EstimatedYield, input.YieldUnit, product.Unit);
        if (current is null)
            return;
        FarmState.Require(FarmState.Stage(current.Status) == "Planned", FarmManagementMessages.MSG131);
        if (current.CultivationLogs.Count > 0 || current.HarvestBatches.Count > 0 || current.PreOrderCampaigns.Count > 0 || current.ContractLines.Count > 0)
            FarmState.Require(current.ProductId == input.ProductId, FarmManagementMessages.MSG130, "productId");
        if (current.HarvestBatches.Any(x => x.StockEnteredAt.HasValue))
            FarmState.Require(current.YieldUnit.Equals(input.YieldUnit.Trim(), StringComparison.OrdinalIgnoreCase), FarmManagementMessages.Unit, "yieldUnit");
    }


    public void ValidateStageChange(CropSeason season, string nextStage)
    {
        string expected = FarmState.Stage(season.Status) switch
        {
            "Planned" => "Planting",
            "Planting" => "Growing",
            "Growing" => "Harvesting",
            _ => ""
        };
        FarmState.Require(expected != "" && expected == nextStage, FarmManagementMessages.MSG131);
        FarmState.Require(nextStage != "Planting" || season.PlantingDate <= FarmState.Today(clock), FarmManagementMessages.StageDate, "nextStage");
    }


    public decimal RecordedYield(CropSeason season) =>
        season.HarvestBatches.Where(x => x.StockEnteredAt.HasValue && x.ActualHarvestDate.HasValue).Sum(x => FarmState.ConvertUnit(x.HarvestYield, season.Product.Unit, season.YieldUnit));

    public void ReconcileHarvest(CropSeason season, SeasonActionRequest request, bool cancellation)
    {
        FarmState.Require(!FarmState.Closed(season.Status) && (cancellation || season.Status == "Harvesting"), FarmManagementMessages.MSG131);
        FarmState.Amount(request.ActualYield, "actualYield", !cancellation);
        FarmState.Require(request.ActualYield == RecordedYield(season), FarmManagementMessages.Completion, "actualYield");
        if (!cancellation)
            FarmState.Require(request.CompletionDate.HasValue && request.CompletionDate >= season.PlantingDate && request.CompletionDate <= FarmState.Today(clock), FarmManagementMessages.Completion, "completionDate");
    }


    public void ProtectCommitments(
        IReadOnlyList<FarmCommitment> commitments,
        decimal yield,
        string unit,
        DateOnly date)
    {
        var required = commitments.Sum(x => FarmState.ConvertUnit(x.Quantity, x.Unit, unit));
        FarmState.Require(yield >= required && !commitments.Any(x => x.Quantity > 0 && x.DeliveryDate.HasValue && date > x.DeliveryDate), FarmManagementMessages.MSG130);
    }
}
