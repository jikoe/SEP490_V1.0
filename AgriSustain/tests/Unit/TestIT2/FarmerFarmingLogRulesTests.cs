using Xunit;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.TestIT2.Infrastructure;

namespace AgriSustain.TestIT2.Unit;

[Trait("Layer", "Unit")]
public sealed class FarmerFarmingLogRulesTests
{
    private readonly FarmerFarmingLogRules rules = new(new FixedClock(), new OwnedEvidence());

    [Fact]
    public void UT_FAR_02_001_Watering()
    {
        rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { ActionType = "Tưới nước", InputMaterial = null, Dosage = null, EvidenceUrls = [] });
    }


    [Fact]
    public void UT_FAR_02_002_Fertilizing()
    {
        rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { ActionType = "Bón phân", InputMaterial = "NPK", Dosage = "1kg", EvidenceUrls = [] });
    }


    [Fact]
    public void UT_FAR_02_003_PestTreatment()
    {
        rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { ActionType = "Phòng trừ sâu bệnh", InputMaterial = "Biological", Dosage = "10ml", EvidenceUrls = [OwnedEvidence.Reference] });
    }


    [Fact]
    public void UT_FAR_02_004_RejectPlanned()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season("Planned"), FarmerTestData.Log()), FarmManagementMessages.MSG133);
    }


    [Fact]
    public void UT_FAR_02_005_RejectCompleted()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season("Completed"), FarmerTestData.Log()), FarmManagementMessages.MSG133);
    }


    [Fact]
    public void UT_FAR_02_006_RejectCancelled()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season("Cancelled"), FarmerTestData.Log()), FarmManagementMessages.MSG133);
    }


    [Fact]
    public void UT_FAR_02_007_RejectCropFailure()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season("Crop Failure"), FarmerTestData.Log()), FarmManagementMessages.MSG133);
    }


    [Fact]
    public void UT_FAR_02_008_FutureDate()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { ActionDate = FarmerTestData.Today.AddDays(1) }), FarmManagementMessages.MSG95, "actionDate");
    }


    [Fact]
    public void UT_FAR_02_009_BeforePlanting()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { ActionDate = FarmerTestData.Today.AddDays(-31) }), FarmManagementMessages.DateRange, "actionDate");
    }


    [Fact]
    public void UT_FAR_02_010_BlankDescription()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { DetailDescription = "  " }), FarmManagementMessages.MSG97, "detailDescription");
    }


    [Fact]
    public void UT_FAR_02_011_BlankPerformer()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { PerformedBy = " " }), FarmManagementMessages.Field("performedBy"), "performedBy");
    }


    [Fact]
    public void UT_FAR_02_012_MissingDosage()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { ActionType = "Bón phân", InputMaterial = "NPK" }), FarmManagementMessages.Field("dosage"), "dosage");
    }


    [Fact]
    public void UT_FAR_02_013_MissingPestProof()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { ActionType = "Phòng trừ sâu bệnh", InputMaterial = "Bio", Dosage = "1ml" }), FarmManagementMessages.MSG120, "evidenceUrls");
    }


    [Fact]
    public void UT_FAR_02_014_ForeignProof()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { EvidenceUrls = ["/owned/102/proof.png"] }), FarmManagementMessages.MSG120, "evidenceUrls");
    }


    [Fact]
    public void UT_FAR_02_015_DuplicateProof()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { EvidenceUrls = [OwnedEvidence.Reference, OwnedEvidence.Reference] }), FarmManagementMessages.MSG120, "evidenceUrls");
    }


    [Fact]
    public void UT_FAR_02_016_PlantingBoundary()
    {
        rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { ActionDate = FarmerTestData.Today.AddDays(-30) });
    }


    [Fact]
    public void UT_FAR_02_017_CorrectionClosedSeason()
    {
        var original = new CultivationLog
        {
            CultivationLogId = 1,
            CropSeasonId = 201,
            ActionType = "Tưới nước",
            DetailDescription = "Original"
        };
        rules.ValidateCorrection(101, FarmerTestData.Season("Completed"), original, FarmerTestData.Log() with { CorrectionReason = "Correct performer" });
        Assert.Equal("Original", original.DetailDescription);
    }


    [Fact]
    public void UT_FAR_02_018_CorrectionOfCorrection()
    {
        FarmerTestData.Reject(() => rules.ValidateCorrection(101, FarmerTestData.Season(), new() { CropSeasonId = 201, CorrectsLogId = 1 }, FarmerTestData.Log() with { CorrectionReason = "Fix" }), FarmManagementMessages.MSG120);
    }


    [Fact]
    public void UT_FAR_02_019_CorrectionNeedsReason()
    {
        FarmerTestData.Reject(() => rules.ValidateCorrection(101, FarmerTestData.Season(), new() { CropSeasonId = 201 }, FarmerTestData.Log() with { CorrectionReason = " " }), FarmManagementMessages.Field("correctionReason"));
    }
}
