using Xunit;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.TestIT2.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.Services.Complaints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgriSustain.TestIT2.Integration;

[Trait("Layer", "Integration")]
public sealed class FarmerHarvestBatchIntegrationTests
{
    [SqlFact]
    public async Task IT_FAR_04_001_PlanActualAndQr()
    {
        await using var f = await FarmerDatabase.Create();
        var plan = await f.Post<BatchResponse>("api/farmer/harvest-batches/plans", f.Harvest());
        Assert.Equal("Planned", plan.Status);
        Assert.Equal(0, plan.AvailableQuantity);
        var actual = await f.Post<BatchResponse>($"api/farmer/harvest-batches/{plan.HarvestBatchId}/harvest", f.Harvest() with { Version = plan.Version });
        Assert.Equal(FarmManagementMessages.MSG135, actual.Message);
        Assert.Equal(10, actual.AvailableQuantity);
        await f.Post<FarmingLogResponse>("api/farmer/farming-logs", f.Log());
        actual = await f.Get<BatchResponse>($"api/farmer/harvest-batches/{actual.HarvestBatchId}");
        var first = await f.Post<FarmLabelResponse>($"api/farmer/harvest-batches/{actual.HarvestBatchId}/qr", new FarmCommand { Version = actual.Version, RequestKey = Guid.NewGuid().ToString("D") });
        actual = await f.Get<BatchResponse>($"api/farmer/harvest-batches/{actual.HarvestBatchId}");
        var second = await f.Post<FarmLabelResponse>($"api/farmer/harvest-batches/{actual.HarvestBatchId}/qr", new FarmCommand { Version = actual.Version, RequestKey = Guid.NewGuid().ToString("D") });
        Assert.Equal(first.QrIdentity, second.QrIdentity);
        var label = await f.Get<FarmLabelResponse>($"api/farmer/harvest-batches/{actual.HarvestBatchId}/label");
        Assert.Equal(first.QrIdentity, label.QrIdentity);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, Convert.FromBase64String(label.QrPng.Split(',')[1])[..8]);
        await using var db = f.Open();
        Assert.Equal(1, await db.InventoryMovements.CountAsync(x => x.HarvestBatchId == actual.HarvestBatchId && x.MovementType == "StockIn"));
        Assert.Equal(1, await db.TraceabilityQrcodes.CountAsync());
    }


    [SqlFact]
    public async Task IT_FAR_04_002_DuplicateStockIn()
    {
        await using var f = await FarmerDatabase.Create();
        var detail = await f.Get<BatchResponse>($"api/farmer/harvest-batches/{f.BatchId}");
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync($"api/farmer/harvest-batches/{f.BatchId}/harvest", f.Harvest() with { Version = detail.Version }), HttpStatusCode.Conflict, FarmManagementMessages.MSG122);
        await using var db = f.Open();
        Assert.Equal(25, (await db.HarvestBatches.FindAsync(f.BatchId))!.HarvestYield);
        Assert.Empty(await db.InventoryMovements.ToListAsync());
    }


    [SqlFact]
    public async Task IT_FAR_04_003_QuantityAdjustment()
    {
        await using var f = await FarmerDatabase.Create();
        var detail = await f.Get<BatchResponse>($"api/farmer/harvest-batches/{f.BatchId}");
        var input = new BatchIssueRequest
        {
            IssueType = "adjustment",
            Quantity = -2,
            Reason = "Damaged two kilograms",
            EvidenceUrls = [await f.FarmEvidence()],
            Version = detail.Version,
            RequestKey = Guid.NewGuid().ToString("D")
        };
        var result = await f.Post<BatchResponse>($"api/farmer/harvest-batches/{f.BatchId}/issues", input);
        Assert.Equal(18, result.AvailableQuantity);
        Assert.Equal(FarmManagementMessages.MSG138, result.Message);
        await using var db = f.Open();
        Assert.Equal(-2, (await db.InventoryMovements.SingleAsync()).QuantityChange);
    }


    [SqlFact]
    public async Task IT_FAR_04_004_CannotInvadeReservations()
    {
        await using var f = await FarmerDatabase.Create();
        var detail = await f.Get<BatchResponse>($"api/farmer/harvest-batches/{f.BatchId}");
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync($"api/farmer/harvest-batches/{f.BatchId}/issues", new BatchIssueRequest { IssueType = "adjustment", Quantity = -21, Reason = "Count correction", EvidenceUrls = [await f.FarmEvidence()], Version = detail.Version, RequestKey = Guid.NewGuid().ToString("D") }), HttpStatusCode.BadRequest, FarmManagementMessages.MSG103);
        await using var db = f.Open();
        Assert.Empty(await db.InventoryMovements.ToListAsync());
        Assert.Equal(20, (await db.HarvestBatches.FindAsync(f.BatchId))!.AvailableQuantity);
    }


    [SqlFact]
    public async Task IT_FAR_04_005_QrPrerequisite()
    {
        await using var f = await FarmerDatabase.Create();
        var detail = await f.Get<BatchResponse>($"api/farmer/harvest-batches/{f.BatchId}");
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync($"api/farmer/harvest-batches/{f.BatchId}/qr", new FarmCommand { Version = detail.Version, RequestKey = Guid.NewGuid().ToString("D") }), HttpStatusCode.BadRequest, FarmManagementMessages.MSG136);
        await using var db = f.Open();
        Assert.Empty(await db.TraceabilityQrcodes.ToListAsync());
        Assert.NotNull(await db.HarvestBatches.FindAsync(f.BatchId));
    }


    [SqlFact]
    public async Task IT_FAR_04_006_QualityBlocksQr()
    {
        await using var f = await FarmerDatabase.Create();
        await f.Post<FarmingLogResponse>("api/farmer/farming-logs", f.Log());
        var detail = await f.Get<BatchResponse>($"api/farmer/harvest-batches/{f.BatchId}");
        await f.Post<FarmLabelResponse>($"api/farmer/harvest-batches/{f.BatchId}/qr", new FarmCommand { Version = detail.Version, RequestKey = Guid.NewGuid().ToString("D") });
        detail = await f.Get<BatchResponse>($"api/farmer/harvest-batches/{f.BatchId}");
        var blocked = await f.Post<BatchResponse>($"api/farmer/harvest-batches/{f.BatchId}/issues", new BatchIssueRequest { IssueType = "quality", Quantity = 1, Reason = "Suspected contamination", EvidenceUrls = [await f.FarmEvidence()], Version = detail.Version, RequestKey = Guid.NewGuid().ToString("D") });
        Assert.False(blocked.QrEligible);
        await FarmerDatabase.Error(await f.Farmer().GetAsync($"api/farmer/harvest-batches/{f.BatchId}/label"), HttpStatusCode.BadRequest, FarmManagementMessages.MSG139);
        await using var db = f.Open();
        Assert.True((await db.TraceabilityQrcodes.SingleAsync()).IsRevoked);
        Assert.Empty(await db.RefundTransactions.ToListAsync());
    }


    [SqlFact]
    public async Task IT_FAR_04_007_RollbackStockIn()
    {
        await using var f = await FarmerDatabase.Create();
        f.Failure.Armed = true;
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync("api/farmer/harvest-batches/harvest", f.Harvest()), HttpStatusCode.InternalServerError, FarmManagementMessages.MSG123);
        await using var db = f.Open();
        Assert.False(await db.HarvestBatches.AnyAsync(x => x.BatchCode == "BATCH-01"));
        Assert.Empty(await db.InventoryMovements.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }
}
