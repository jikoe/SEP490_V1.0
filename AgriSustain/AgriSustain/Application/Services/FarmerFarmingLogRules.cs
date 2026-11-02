using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class FarmerFarmingLogRules(TimeProvider clock, IFarmerEvidenceStorage storage)
{
    public void ValidateActivity(
        long actor,
        CropSeason season,
        FarmingLogRequest input,
        bool correcting = false,
        string? originalType = null)
    {
        FarmState.Require(correcting || FarmState.ActiveSeason(season.Status), FarmManagementMessages.MSG133);
        FarmState.Require(!string.IsNullOrWhiteSpace(input.ActionType) && !string.IsNullOrWhiteSpace(input.DetailDescription), FarmManagementMessages.MSG97, "detailDescription");
        FarmState.Text(input.ActionType, 100, "actionType");
        FarmState.Text(input.DetailDescription, 1000, "detailDescription");
        FarmState.Require(FarmState.Activities.Contains(input.ActionType) || (correcting && input.ActionType == originalType), FarmManagementMessages.MSG97, "actionType");
        FarmState.Text(input.PerformedBy, 150, "performedBy");
        FarmState.Text(input.InputMaterial, 255, "inputMaterial", false);
        FarmState.Text(input.Dosage, 150, "dosage", false);
        FarmState.Require(input.ActionDate <= FarmState.Today(clock), FarmManagementMessages.MSG95, "actionDate");
        FarmState.Require(input.ActionDate != default && input.ActionDate >= season.PlantingDate, FarmManagementMessages.DateRange, "actionDate");
        bool materials = input.ActionType is "Bón phân" or "Phòng trừ sâu bệnh";
        if (materials)
        {
            FarmState.Text(input.InputMaterial, 255, "inputMaterial");
            FarmState.Text(input.Dosage, 150, "dosage");
        }

        ValidateEvidence(actor, input.EvidenceUrls, input.ActionType == "Phòng trừ sâu bệnh");
    }


    public void ValidateCorrection(
        long actor,
        CropSeason season,
        CultivationLog original,
        FarmingLogRequest input)
    {
        FarmState.Require(!original.CorrectsLogId.HasValue && original.CropSeasonId == input.CropSeasonId, FarmManagementMessages.MSG120, "cropSeasonId");
        FarmState.Text(input.CorrectionReason, 500, "correctionReason");
        ValidateActivity(actor, season, input, true, original.ActionType);
    }


    public void ValidateEvidence(
        long actor,
        string[] refs,
        bool required) =>
        FarmState.Proof(storage, actor, refs, required, FarmManagementMessages.MSG120);
}
