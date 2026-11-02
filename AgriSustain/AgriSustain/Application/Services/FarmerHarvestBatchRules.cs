using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class FarmerHarvestBatchRules(TimeProvider clock)
{
    public void ValidatePlan(
        HarvestBatch? current,
        CropSeason season,
        Product product,
        HarvestBatchRequest input)
    {
        Identity(season, product, input);
        FarmState.Require(!FarmState.Closed(season.Status), FarmManagementMessages.MSG131);
        FarmState.Require(current is null || !current.StockEnteredAt.HasValue, FarmManagementMessages.MSG131);
        FarmState.Require(input.EstimatedHarvestDate.HasValue && input.EstimatedHarvestDate >= season.PlantingDate, FarmManagementMessages.DateRange, "estimatedHarvestDate");
        FarmState.Amount(input.EstimatedYield, "estimatedYield");
    }


    public void ValidateHarvest(
        HarvestBatch? current,
        CropSeason season,
        Product product,
        HarvestBatchRequest input)
    {
        Identity(season, product, input);
        FarmState.Require(FarmState.ActiveSeason(season.Status), FarmManagementMessages.MSG131);
        FarmState.Require(current is null || (!current.StockEnteredAt.HasValue && !current.ActualHarvestDate.HasValue), FarmManagementMessages.MSG122, code: "CONFLICT");
        FarmState.Require(input.ActualHarvestDate.HasValue && input.ActualHarvestDate >= season.PlantingDate && input.ActualHarvestDate <= FarmState.Today(clock), FarmManagementMessages.DateRange, "actualHarvestDate");
        FarmState.Require(input.ExpirationDate.HasValue && input.ExpirationDate > input.ActualHarvestDate, FarmManagementMessages.MSG102, "expirationDate");
        FarmState.Amount(input.ActualYield, "actualYield", message: FarmManagementMessages.MSG103);
    }


    private static void Identity(CropSeason season, Product product, HarvestBatchRequest input)
    {
        FarmState.Text(input.Unit, 50, "unit");
        FarmState.Text(input.BatchCode, 100, "batchCode");
        FarmState.Require(input.BatchCode.Trim().All(c => c is >= '!' and <= '~'), FarmManagementMessages.MSG120, "batchCode");
        FarmState.Text(input.QualityNotes, 500, "qualityNotes", false);
        FarmState.Require(season.ProductId == product.ProductId && season.FarmerProfileId == product.FarmerProfileId, FarmManagementMessages.ProductSeason, "cropSeasonId");
        FarmState.Require(string.Equals(product.Unit, input.Unit.Trim(), StringComparison.OrdinalIgnoreCase), FarmManagementMessages.MSG103, "unit");
    }


    public (decimal Adjustment, decimal Held, decimal Shipped) Balance(HarvestBatch batch)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        decimal adjustment = batch.InventoryMovements.Where(x => x.MovementType == "Adjustment").Sum(x => x.QuantityChange);
        decimal held = batch.OrderItemBatchAllocations.Count > 0 ? batch.OrderItemBatchAllocations.Where(x => x.AllocationStatus is "Reserved" or "Held" or "Active" && (!x.ExpiresAt.HasValue || x.ExpiresAt > now)).Sum(x => x.AllocatedQuantity) : batch.ReservedQuantity;
        decimal shipped = Math.Max(batch.OrderItemBatchAllocations.SelectMany(x => x.ShipmentItems).Sum(x => x.ShippedQuantity), -batch.InventoryMovements.Where(x => x.MovementType is "Dispatch" or "StockOut").Sum(x => x.QuantityChange));
        return (adjustment, held, shipped);
    }


    public void ReconcileStock(HarvestBatch batch, decimal adjustment = 0)
    {
        var b = Balance(batch);
        FarmState.Require(batch.HarvestYield + b.Adjustment + adjustment - b.Held - b.Shipped >= 0, FarmManagementMessages.MSG103, "quantity");
    }


    public void ProtectCommitments(
        IReadOnlyList<FarmCommitment> commitments,
        DateOnly expiry) =>
        FarmState.Require(!commitments.Any(x => x.Quantity > 0 && x.DeliveryDate.HasValue && expiry <= x.DeliveryDate), FarmManagementMessages.MSG130, "expirationDate");

    public bool IsEligible(HarvestBatch batch) =>
        batch.StockEnteredAt.HasValue && batch.ActualHarvestDate.HasValue && batch.ActualHarvestDate >= batch.CropSeason.PlantingDate && batch.ActualHarvestDate <= FarmState.Today(clock) && batch.ExpirationDate > FarmState.Today(clock) && batch.Status is not ("Blocked" or "Recalled" or "Expired" or "Suspect" or "Planned") && batch.TraceabilityQrcode?.IsRevoked != true;

    public void ValidateQrEligibility(HarvestBatch batch)
    {
        FarmState.Require(batch.StockEnteredAt.HasValue && batch.ActualHarvestDate.HasValue && batch.ActualHarvestDate <= FarmState.Today(clock) && batch.CropSeason.CultivationLogs.Count > 0, FarmManagementMessages.MSG136);
        FarmState.Require(IsEligible(batch), FarmManagementMessages.MSG139);
    }
}
