using Xunit;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.TestIT2.Infrastructure;

namespace AgriSustain.TestIT2.Unit;

[Trait("Layer", "Unit")]
public sealed class FarmerHarvestBatchRulesTests
{
    private readonly FarmerHarvestBatchRules rules = new(new FixedClock());

    [Fact]
    public void UT_FAR_04_001_ValidPlannedBatch()
    {
        rules.ValidatePlan(null, FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest());
    }


    [Fact]
    public void UT_FAR_04_002_ValidActualHarvest()
    {
        rules.ValidateHarvest(null, FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest());
    }


    [Fact]
    public void UT_FAR_04_003_ZeroActual()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(null, FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { ActualYield = 0 }), FarmManagementMessages.MSG103, "actualYield");
    }


    [Fact]
    public void UT_FAR_04_004_FractionPrecision()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(null, FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { ActualYield = 1.0001m }), FarmManagementMessages.MSG103, "actualYield");
    }


    [Fact]
    public void UT_FAR_04_005_ExpiryAtHarvest()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(null, FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { ExpirationDate = FarmerTestData.Today }), FarmManagementMessages.MSG102, "expirationDate");
    }


    [Fact]
    public void UT_FAR_04_006_FutureHarvest()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(null, FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { ActualHarvestDate = FarmerTestData.Today.AddDays(1) }), FarmManagementMessages.DateRange, "actualHarvestDate");
    }


    [Fact]
    public void UT_FAR_04_007_UnitMismatch()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(null, FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { Unit = "g" }), FarmManagementMessages.MSG103, "unit");
    }


    [Fact]
    public void UT_FAR_04_008_BatchCodeWhitespace()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(null, FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest() with { BatchCode = "BATCH 01" }), FarmManagementMessages.MSG120, "batchCode");
    }


    [Fact]
    public void UT_FAR_04_009_RepeatedStockIn()
    {
        FarmerTestData.Reject(() => rules.ValidateHarvest(FarmerTestData.Batch(), FarmerTestData.Season(), FarmerTestData.Product(), FarmerTestData.Harvest()), FarmManagementMessages.MSG122);
    }


    [Fact]
    public void UT_FAR_04_010_ExactAvailableBalance()
    {
        var batch = FarmerTestData.Batch();
        batch.ReservedQuantity = 4;
        rules.ReconcileStock(batch, -6);
        FarmerTestData.Reject(() => rules.ReconcileStock(batch, -6.001m), FarmManagementMessages.MSG103);
    }


    [Fact]
    public void UT_FAR_04_011_ExpiredHoldIgnored()
    {
        var batch = FarmerTestData.Batch();
        batch.ReservedQuantity = 9;
        batch.OrderItemBatchAllocations.Add(new() { AllocatedQuantity = 9, AllocationStatus = "Reserved", ExpiresAt = new FixedClock().Now.AddSeconds(-1).UtcDateTime });
        batch.OrderItemBatchAllocations.Add(new() { AllocatedQuantity = 2, AllocationStatus = "Reserved", ExpiresAt = new FixedClock().Now.AddDays(1).UtcDateTime });
        Assert.Equal(2, rules.Balance(batch).Held);
    }


    [Fact]
    public void UT_FAR_04_012_NoDoubleCountDispatch()
    {
        var batch = FarmerTestData.Batch();
        batch.InventoryMovements.Add(new() { MovementType = "Dispatch", QuantityChange = -3 });
        var allocation = new OrderItemBatchAllocation
        {
            AllocationStatus = "Shipped"
        };
        allocation.ShipmentItems.Add(new() { ShippedQuantity = 3 });
        batch.OrderItemBatchAllocations.Add(allocation);
        Assert.Equal(3, rules.Balance(batch).Shipped);
    }


    [Fact]
    public void UT_FAR_04_013_QrNeedsLog()
    {
        FarmerTestData.Reject(() => rules.ValidateQrEligibility(FarmerTestData.Batch()), FarmManagementMessages.MSG136);
    }


    [Fact]
    public void UT_FAR_04_014_ValidQr()
    {
        var batch = FarmerTestData.Batch();
        batch.CropSeason.CultivationLogs.Add(new());
        rules.ValidateQrEligibility(batch);
        Assert.True(rules.IsEligible(batch));
    }


    [Fact]
    public void UT_FAR_04_015_QrBlocked()
    {
        var batch = FarmerTestData.Batch();
        batch.CropSeason.CultivationLogs.Add(new());
        batch.Status = "Blocked";
        FarmerTestData.Reject(() => rules.ValidateQrEligibility(batch), FarmManagementMessages.MSG139);
    }


    [Fact]
    public void UT_FAR_04_016_QrRecalled()
    {
        var batch = FarmerTestData.Batch();
        batch.CropSeason.CultivationLogs.Add(new());
        batch.Status = "Recalled";
        FarmerTestData.Reject(() => rules.ValidateQrEligibility(batch), FarmManagementMessages.MSG139);
    }


    [Fact]
    public void UT_FAR_04_017_QrExpired()
    {
        var batch = FarmerTestData.Batch();
        batch.CropSeason.CultivationLogs.Add(new());
        batch.Status = "Expired";
        FarmerTestData.Reject(() => rules.ValidateQrEligibility(batch), FarmManagementMessages.MSG139);
    }


    [Fact]
    public void UT_FAR_04_018_QrSuspect()
    {
        var batch = FarmerTestData.Batch();
        batch.CropSeason.CultivationLogs.Add(new());
        batch.Status = "Suspect";
        FarmerTestData.Reject(() => rules.ValidateQrEligibility(batch), FarmManagementMessages.MSG139);
    }


    [Fact]
    public void UT_FAR_04_019_ProtectExpiry()
    {
        FarmerTestData.Reject(() => rules.ProtectCommitments([new("Retail", 1, "kg", FarmerTestData.Today.AddDays(5))], FarmerTestData.Today.AddDays(5)), FarmManagementMessages.MSG130);
    }
}
