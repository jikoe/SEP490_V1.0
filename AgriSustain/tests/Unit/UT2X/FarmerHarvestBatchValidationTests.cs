using Xunit;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.UT2X.Infrastructure;

namespace AgriSustain.UT2X.Unit;

[Trait("Layer", "Unit")]
public sealed class FarmerHarvestBatchValidationTests
{
    private readonly FarmerHarvestBatchRules rules = new(new FixedClock());

    [Fact]
    public void UT_FAR_04_020_MissingCode()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { BatchCode = " " }), FarmManagementMessages.Field("batchCode"));
    }


    [Fact]
    public void UT_FAR_04_021_LongCode()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { BatchCode = new string('b', 101) }), FarmManagementMessages.Field("batchCode"));
    }


    [Fact]
    public void UT_FAR_04_022_QualityNotesTooLong()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { QualityNotes = new string('q', 501) }), FarmManagementMessages.Field("qualityNotes"), "qualityNotes");
    }


    [Fact]
    public void UT_FAR_04_023_ExpiryBeforeHarvest()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { ExpirationDate = FarmerTestData.Today.AddDays(-1) }), FarmManagementMessages.MSG102, "expirationDate");
    }


    [Fact]
    public void UT_FAR_04_024_SeasonFarmMismatch()
    {
        var product = FarmerTestData.Product();
        product.FarmerProfileId = 999;
        FarmerTestData.Reject(() => rules.ValidateHarvest(FarmerTestData.Season(), product, FarmerTestData.Harvest()), FarmManagementMessages.ProductSeason, "cropSeasonId");
    }


    [Fact]
    public void UT_FAR_04_025_BeforePlanting()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { ActualHarvestDate = FarmerTestData.Today.AddDays(-31) }), FarmManagementMessages.DateRange);
    }


    [Fact]
    public void UT_FAR_04_026_MissingActualDate()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { ActualHarvestDate = null }), FarmManagementMessages.DateRange);
    }


    [Fact]
    public void UT_FAR_04_027_MissingExpiry()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { ExpirationDate = null }), FarmManagementMessages.MSG102);
    }


    [Fact]
    public void UT_FAR_04_028_NegativeHarvest()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { ActualYield = -1 }), FarmManagementMessages.MSG103);
    }


    [Fact]
    public void UT_FAR_04_029_ProductSeasonMismatch()
    {
        var product = FarmerTestData.Product();
        product.ProductId = 302;
        FarmerTestData.Reject(() => rules.ValidateHarvest(FarmerTestData.Season(), product, FarmerTestData.Harvest()), FarmManagementMessages.ProductSeason);
    }


    [Fact]
    public void UT_FAR_04_030_MinimumHarvestAndEarliestDate()
    {
        rules.ValidateHarvest(FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { BatchCode = new string('b', 100), ActualYield = .001m, ActualHarvestDate = FarmerTestData.Today.AddDays(-30) });
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
        FarmerTestData.Reject(() => rules.ValidateHarvest(FarmerTestData.Season("Planned"), FarmerTestData.Product(), FarmerTestData.Harvest()), FarmManagementMessages.MSG131);
    }

    [Fact]
    public void UT_FAR_04_034_HarvestFromClosed()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(FarmerTestData.Season("Completed"), FarmerTestData.Product(), FarmerTestData.Harvest()), FarmManagementMessages.MSG131);
    }

    [Fact]
    public void UT_FAR_04_035_HarvestFromCancelledSeason()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(FarmerTestData.Season("Cancelled"), FarmerTestData.Product(), FarmerTestData.Harvest()), FarmManagementMessages.MSG131);
    }


    [Fact]
    public void UT_FAR_04_036_HarvestFromCropFailureSeason()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(FarmerTestData.Season("Crop Failure"), FarmerTestData.Product(), FarmerTestData.Harvest()), FarmManagementMessages.MSG131);
    }


    [Fact]
    public void UT_FAR_04_037_MissingUnit()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { Unit = " " }), FarmManagementMessages.Field("unit"), "unit");
    }


    [Fact]
    public void UT_FAR_04_038_QrRequiresActualHarvestDate()
    {
        var batch = FarmerTestData.Batch();
        batch.CropSeason.CultivationLogs.Add(new());
        batch.ActualHarvestDate = null;
        FarmerTestData.Reject(() => rules.ValidateQrEligibility(batch), FarmManagementMessages.MSG136);
    }


    [Fact]
    public void UT_FAR_04_039_QrRejectsPlannedStatus()
    {
        var batch = FarmerTestData.Batch();
        batch.CropSeason.CultivationLogs.Add(new());
        batch.Status = "Planned";
        FarmerTestData.Reject(() => rules.ValidateQrEligibility(batch), FarmManagementMessages.MSG139);
    }


    [Fact]
    public void UT_FAR_04_040_ProtectExpiryAllowsAfterDelivery()
    {
        rules.ProtectCommitments([new("Retail", 1, "kg", FarmerTestData.Today.AddDays(5))], FarmerTestData.Today.AddDays(6));
    }
}
