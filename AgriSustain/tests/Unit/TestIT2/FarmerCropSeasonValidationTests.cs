using Xunit;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.TestIT2.Infrastructure;

namespace AgriSustain.TestIT2.Unit;

[Trait("Layer", "Unit")]
public sealed class FarmerCropSeasonValidationTests
{
    private readonly FarmerCropSeasonRules rules = new(new FixedClock());

    [Fact]
    public void UT_FAR_01_020_MissingVariety()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Plan() with { CropVariety = " " }, FarmerTestData.Product()), FarmManagementMessages.Field("cropVariety"), "cropVariety");
    }


    [Fact]
    public void UT_FAR_01_021_VarietyTooLong()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Plan() with { CropVariety = new string('v', 151) }, FarmerTestData.Product()), FarmManagementMessages.Field("cropVariety"), "cropVariety");
    }


    [Fact]
    public void UT_FAR_01_022_MissingUnit()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Plan() with { YieldUnit = " " }, FarmerTestData.Product()), FarmManagementMessages.Field("yieldUnit"), "yieldUnit");
    }


    [Fact]
    public void UT_FAR_01_023_NegativeArea()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Plan() with { CultivatedArea = -1 }, FarmerTestData.Product()), FarmManagementMessages.Quantity, "cultivatedArea");
    }


    [Fact]
    public void UT_FAR_01_024_NegativeYield()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Plan() with { EstimatedYield = -1 }, FarmerTestData.Product()), FarmManagementMessages.MSG93, "estimatedYield");
    }


    [Fact]
    public void UT_FAR_01_025_YieldPrecision()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Plan() with { EstimatedYield = .0001m }, FarmerTestData.Product()), FarmManagementMessages.MSG93, "estimatedYield");
    }


    [Fact]
    public void UT_FAR_01_026_MissingPlanting()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Plan() with { PlantingDate = default }, FarmerTestData.Product()), FarmManagementMessages.MSG92, "estimatedHarvestDate");
    }


    [Fact]
    public void UT_FAR_01_027_HarvestEqualsPlanting()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Plan() with { PlantingDate = FarmerTestData.Today.AddDays(2), EstimatedHarvestDate = FarmerTestData.Today.AddDays(2) }, FarmerTestData.Product()), FarmManagementMessages.MSG92, "estimatedHarvestDate");
    }


    [Fact]
    public void UT_FAR_01_028_PositiveMinimum()
    {
        rules.ValidatePlan(null, FarmerTestData.Plan() with { CultivatedArea = .001m, EstimatedYield = .001m, CropVariety = new string('v', 150) }, FarmerTestData.Product());
    }


    [Fact]
    public void UT_FAR_01_029_CannotReopenCompleted()
    {
        FarmerTestData.Reject(() => rules.ValidateStageChange(FarmerTestData.Season("Completed"), "Planting"), FarmManagementMessages.MSG131);
    }


    [Fact]
    public void UT_FAR_01_030_CannotReopenCancelled()
    {
        FarmerTestData.Reject(() => rules.ValidateStageChange(FarmerTestData.Season("Cancelled"), "Planting"), FarmManagementMessages.MSG131);
    }


    [Fact]
    public void UT_FAR_01_031_CannotReopenCropFailure()
    {
        FarmerTestData.Reject(() => rules.ValidateStageChange(FarmerTestData.Season("Crop Failure"), "Planting"), FarmManagementMessages.MSG131);
    }


    [Fact]
    public void UT_FAR_01_032_CompletionBeforePlanting()
    {
        var season = FarmerTestData.Season("Harvesting");
        season.HarvestBatches.Add(FarmerTestData.Batch());
        FarmerTestData.Reject(() => rules.ReconcileHarvest(season, new() { ActualYield = 10, CompletionDate = FarmerTestData.Today.AddDays(-31) }, false), FarmManagementMessages.Completion);
    }


    [Fact]
    public void UT_FAR_01_033_CompletionInFuture()
    {
        var season = FarmerTestData.Season("Harvesting");
        season.HarvestBatches.Add(FarmerTestData.Batch());
        FarmerTestData.Reject(() => rules.ReconcileHarvest(season, new() { ActualYield = 10, CompletionDate = FarmerTestData.Today.AddDays(1) }, false), FarmManagementMessages.Completion);
    }


    [Fact]
    public void UT_FAR_01_034_ChangeAssociatedProduct()
    {
        var season = FarmerTestData.Season("Planned");
        season.CultivationLogs.Add(new());
        FarmerTestData.Reject(() => rules.ValidatePlan(season, FarmerTestData.Plan() with { ProductId = 999 }, FarmerTestData.Product()), FarmManagementMessages.MSG130);
    }
}
