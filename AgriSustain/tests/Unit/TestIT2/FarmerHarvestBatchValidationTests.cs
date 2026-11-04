using Xunit;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.TestIT2.Infrastructure;

namespace AgriSustain.TestIT2.Unit;

[Trait("Layer", "Unit")]
public sealed class FarmerHarvestBatchValidationTests
{
    private readonly FarmerHarvestBatchRules rules = new(new FixedClock());

    [Fact]
    public void UT_FAR_04_020_MissingCode()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { BatchCode = " " }), FarmManagementMessages.Field("batchCode"));
    }


    [Fact]
    public void UT_FAR_04_021_LongCode()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { BatchCode = new string('b', 101) }), FarmManagementMessages.Field("batchCode"));
    }


    [Fact]
    public void UT_FAR_04_022_ZeroEstimatedYield()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { EstimatedYield = 0 }), FarmManagementMessages.Quantity);
    }


    [Fact]
    public void UT_FAR_04_023_NegativeEstimatedYield()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { EstimatedYield = -1 }), FarmManagementMessages.Quantity);
    }


    [Fact]
    public void UT_FAR_04_024_PlanBeforePlanting()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { EstimatedHarvestDate = FarmerTestData.Today.AddDays(-31) }), FarmManagementMessages.DateRange);
    }


    [Fact]
    public void UT_FAR_04_025_BeforePlanting()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(null, FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { ActualHarvestDate = FarmerTestData.Today.AddDays(-31) }), FarmManagementMessages.DateRange);
    }


    [Fact]
    public void UT_FAR_04_026_MissingActualDate()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(null, FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { ActualHarvestDate = null }), FarmManagementMessages.DateRange);
    }


    [Fact]
    public void UT_FAR_04_027_MissingExpiry()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(null, FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { ExpirationDate = null }), FarmManagementMessages.MSG102);
    }


    [Fact]
    public void UT_FAR_04_028_NegativeHarvest()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(null, FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { ActualYield = -1 }), FarmManagementMessages.MSG103);
    }


    [Fact]
    public void UT_FAR_04_029_ProductSeasonMismatch()
    {
        var product = FarmerTestData.Product();
        product.ProductId = 302;
        FarmerTestData.Reject(() => rules.ValidateHarvest(null, FarmerTestData.Season(), product, FarmerTestData.Harvest()), FarmManagementMessages.ProductSeason);
    }


    [Fact]
    public void UT_FAR_04_030_MinimumHarvestAndEarliestDate()
    {
        rules.ValidateHarvest(null, FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { BatchCode = new string('b', 100), ActualYield = .001m, ActualHarvestDate = FarmerTestData.Today.AddDays(-30) });
    }


    [Fact]
    public void UT_FAR_04_031_ExpiredQrDate()
    {
        var batch = FarmerTestData.Batch();
        batch.CropSeason.CultivationLogs.Add(new());
        batch.ExpirationDate = FarmerTestData.Today;
        FarmerTestData.Reject(() => rules.ValidateQrEligibility(batch), FarmManagementMessages.MSG139);
    }


    [Fact]
    public void UT_FAR_04_032_RevokedQr()
    {
        var batch = FarmerTestData.Batch();
        batch.CropSeason.CultivationLogs.Add(new());
        batch.TraceabilityQrcode = new()
        {
            IsRevoked = true
        };
        FarmerTestData.Reject(() => rules.ValidateQrEligibility(batch), FarmManagementMessages.MSG139);
    }
    [Fact]
    public void UT_FAR_04_033_HarvestFromPlanned()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(null, FarmerTestData.Season("Planned"), FarmerTestData.Product(), FarmerTestData.Harvest()), FarmManagementMessages.MSG131);
    }

    [Fact]
    public void UT_FAR_04_034_HarvestFromClosed()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(null, FarmerTestData.Season("Completed"), FarmerTestData.Product(), FarmerTestData.Harvest()), FarmManagementMessages.MSG131);
    }

    [Fact]
    public void UT_FAR_04_035_PlanForClosedSeason()
    {
        FarmerTestData.Reject(() => rules.ValidatePlan(null, FarmerTestData.Season("Cancelled"), FarmerTestData.Product(), FarmerTestData.Harvest()), FarmManagementMessages.MSG131);
    }
}
