using Xunit;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.TestIT2.Infrastructure;

namespace AgriSustain.TestIT2.Unit;

[Trait("Layer", "Unit")]
public sealed class FarmerCropSeasonRulesTests
{
    private readonly FarmerCropSeasonRules rules = new(new FixedClock());

    [Fact]
    public void UT_FAR_01_001_ValidPlan()
    {
        rules.ValidatePlan(null, FarmerTestData.Plan(), FarmerTestData.Product());
    }


    [Fact]
    public void UT_FAR_01_002_BlankName()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Plan() with { SeasonName = "   " }, FarmerTestData.Product()), FarmManagementMessages.Field("seasonName"), "seasonName");
    }


    [Fact]
    public void UT_FAR_01_003_NameTooLong()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Plan() with { SeasonName = new string('a', 201) }, FarmerTestData.Product()), FarmManagementMessages.Field("seasonName"), "seasonName");
    }


    [Fact]
    public void UT_FAR_01_004_ZeroArea()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Plan() with { CultivatedArea = 0 }, FarmerTestData.Product()), FarmManagementMessages.Quantity, "cultivatedArea");
    }


    [Fact]
    public void UT_FAR_01_005_AreaPrecision()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Plan() with { CultivatedArea = 1.0001m }, FarmerTestData.Product()), FarmManagementMessages.Quantity, "cultivatedArea");
    }


    [Fact]
    public void UT_FAR_01_006_ZeroYield()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Plan() with { EstimatedYield = 0 }, FarmerTestData.Product()), FarmManagementMessages.MSG93, "estimatedYield");
    }


    [Fact]
    public void UT_FAR_01_007_InvalidDate()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Plan() with { EstimatedHarvestDate = FarmerTestData.Today }, FarmerTestData.Product()), FarmManagementMessages.MSG92, "estimatedHarvestDate");
    }


    [Fact]
    public void UT_FAR_01_008_IncompatibleUnit()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Plan() with { YieldUnit = "lít" }, FarmerTestData.Product()), FarmManagementMessages.Unit, "unit");
    }


    [Fact]
    public void UT_FAR_01_009_MaximumName()
    {
        rules.ValidatePlan(null, FarmerTestData.Plan() with { SeasonName = new string('a', 200) }, FarmerTestData.Product());
    }


    [Fact]
    public void UT_FAR_01_010_StageSequence()
    {
        var season = FarmerTestData.Season("Planned");
        foreach (string next in new[]
        {
            "Planting",
            "Growing",
            "Harvesting"
        }

        )
        {
            rules.ValidateStageChange(season, next);
            season.AdvanceStage(next);
            Assert.Equal(next, season.Status);
        }
    }


    [Fact]
    public void UT_FAR_01_011_SkipStage()
    {
        FarmerTestData.Reject(() => rules.ValidateStageChange(FarmerTestData.Season("Planned"), "Harvesting"), FarmManagementMessages.MSG131);
    }


    [Fact]
    public void UT_FAR_01_012_FuturePlanting()
    {
        var season = FarmerTestData.Season("Planned");
        season.PlantingDate = FarmerTestData.Today.AddDays(1);
        FarmerTestData.Reject(() => rules.ValidateStageChange(season, "Planting"), FarmManagementMessages.StageDate);
    }


    [Fact]
    public void UT_FAR_01_013_NoEditActivePlan()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(FarmerTestData.Season(), FarmerTestData.Plan(), FarmerTestData.Product()), FarmManagementMessages.MSG131);
    }


    [Fact]
    public void UT_FAR_01_014_CompleteReconcilesYield()
    {
        var season = FarmerTestData.Season("Harvesting");
        season.HarvestBatches.Add(FarmerTestData.Batch());
        Assert.Equal(10, rules.RecordedYield(season));
        rules.ReconcileHarvest(season, new() { ActualYield = 10, CompletionDate = FarmerTestData.Today }, false);
    }


    [Fact]
    public void UT_FAR_01_015_MismatchYield()
    {
        FarmerTestData.Reject(() => rules.ReconcileHarvest(FarmerTestData.Season("Harvesting"), new() { ActualYield = 1, CompletionDate = FarmerTestData.Today }, false), FarmManagementMessages.Completion);
    }


    [Fact]
    public void UT_FAR_01_016_CancelZeroYield()
    {
        rules.ReconcileHarvest(FarmerTestData.Season(), new() { ActualYield = 0 }, true);
        FarmerTestData.Reject(() => rules.ReconcileHarvest(FarmerTestData.Season("Harvesting"), new() { ActualYield = 0 }, false), FarmManagementMessages.Quantity);
    }


    [Fact]
    public void UT_FAR_01_017_InsufficientCommitmentYield()
    {
        FarmerTestData.Reject(() => rules.ProtectCommitments([new("PreOrder", 10, "kg", FarmerTestData.Today.AddDays(10))], 9, "kg", FarmerTestData.Today.AddDays(5)), FarmManagementMessages.MSG130);
    }


    [Fact]
    public void UT_FAR_01_018_LateCommitment()
    {
        FarmerTestData.Reject(() => rules.ProtectCommitments([new("PreOrder", 10, "kg", FarmerTestData.Today.AddDays(10))], 10, "kg", FarmerTestData.Today.AddDays(11)), FarmManagementMessages.MSG130);
    }


    [Fact]
    public void UT_FAR_01_019_ConvertCommitments()
    {
        rules.ProtectCommitments([new("Contract", 1000, "g", FarmerTestData.Today)], 1, "kg", FarmerTestData.Today);
    }
}
