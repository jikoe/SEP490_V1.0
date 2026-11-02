using Xunit;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.TestIT2.Infrastructure;

namespace AgriSustain.TestIT2.Unit;

[Trait("Layer", "Unit")]
public sealed class FarmerFarmingLogValidationTests
{
    private readonly FarmerFarmingLogRules rules = new(new FixedClock(), new OwnedEvidence());

    [Fact]
    public void UT_FAR_02_020_Supported0()
    {
        rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { ActionType = "Gieo trồng" });
    }


    [Fact]
    public void UT_FAR_02_021_Supported1()
    {
        rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { ActionType = "Thu hoạch" });
    }


    [Fact]
    public void UT_FAR_02_022_MissingAction()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { ActionType = " " }), FarmManagementMessages.MSG97);
    }


    [Fact]
    public void UT_FAR_02_023_UnknownAction()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { ActionType = "INVALID" }), FarmManagementMessages.MSG97);
    }


    [Fact]
    public void UT_FAR_02_024_DefaultDate()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { ActionDate = default }), FarmManagementMessages.DateRange);
    }


    [Fact]
    public void UT_FAR_02_025_MissingMaterial()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { ActionType = "Bón phân", Dosage = "1kg" }), FarmManagementMessages.Field("inputMaterial"));
    }


    [Fact]
    public void UT_FAR_02_026_LongDescription()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { DetailDescription = new string('x', 1001) }), FarmManagementMessages.Field("detailDescription"));
    }


    [Fact]
    public void UT_FAR_02_027_LongPerformer()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { PerformedBy = new string('x', 151) }), FarmManagementMessages.Field("performedBy"));
    }


    [Fact]
    public void UT_FAR_02_028_LongDosage()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { ActionType = "Bón phân", InputMaterial = "NPK", Dosage = new string('x', 151) }), FarmManagementMessages.Field("dosage"));
    }


    [Fact]
    public void UT_FAR_02_029_NullProof()
    {
        FarmerTestData.Reject(() => rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { EvidenceUrls = null! }), FarmManagementMessages.MSG120);
    }


    [Fact]
    public void UT_FAR_02_030_MaximumText()
    {
        rules.ValidateActivity(101, FarmerTestData.Season(), FarmerTestData.Log() with { ActionType = "Bón phân", DetailDescription = new string('x', 1000), PerformedBy = new string('p', 150), InputMaterial = new string('m', 255), Dosage = new string('d', 150) });
    }


    [Fact]
    public void UT_FAR_02_031_WrongCorrectionSeason()
    {
        FarmerTestData.Reject(() => rules.ValidateCorrection(101, FarmerTestData.Season(), new() { CropSeasonId = 202 }, FarmerTestData.Log() with { CorrectionReason = "Correct text" }), FarmManagementMessages.MSG120);
    }


    [Fact]
    public void UT_FAR_02_032_LongCorrectionReason()
    {
        FarmerTestData.Reject(() => rules.ValidateCorrection(101, FarmerTestData.Season(), new() { CropSeasonId = 201 }, FarmerTestData.Log() with { CorrectionReason = new string('r', 501) }), FarmManagementMessages.Field("correctionReason"));
    }
}
