using System.Net;
using System.Net.Http.Json;
using Xunit;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.TestIT2.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.TestIT2.Integration;

[Trait("Layer", "Integration")]
public sealed class FarmerHarvestBatchFlowIntegrationTests
{
    [SqlFact]
    public async Task IT_FAR_04_008_EditPlannedBatch()
    {
        await using var f = await FarmerDatabase.Create();
        var plan = await f.Post<BatchResponse>("api/farmer/harvest-batches/plans", f.Harvest());
        var response = await f.Farmer().PutAsJsonAsync($"api/farmer/harvest-batches/{plan.HarvestBatchId}/plan", f.Harvest() with { BatchCode = "PLAN-UPDATED", EstimatedYield = 20, Version = plan.Version });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var result = (await response.Content.ReadFromJsonAsync<BatchResponse>())!;
        Assert.Equal(FarmManagementMessages.MSG134, result.Message);
        Assert.Equal(20, result.EstimatedYield);
        Assert.Equal(0, result.AvailableQuantity);
        await using var db = f.Open();
        Assert.Empty(await db.InventoryMovements.ToListAsync());
        Assert.Empty(await db.TraceabilityQrcodes.ToListAsync());
    }


    [SqlFact]
    public async Task IT_FAR_04_009_DuplicateCode()
    {
        await using var f = await FarmerDatabase.Create();
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync("api/farmer/harvest-batches/plans", f.Harvest() with { BatchCode = "TEST-BATCH" }), HttpStatusCode.BadRequest, FarmManagementMessages.MSG101);
        await using var db = f.Open();
        Assert.Equal(1, await db.HarvestBatches.CountAsync());
    }


    [SqlFact]
    public async Task IT_FAR_04_010_UpdateActualDetails()
    {
        await using var f = await FarmerDatabase.Create();
        var detail = await f.Get<BatchResponse>($"api/farmer/harvest-batches/{f.BatchId}");
        var input = f.Harvest() with
        {
            BatchCode = detail.BatchCode,
            ActualHarvestDate = detail.ActualHarvestDate,
            ActualYield = detail.HarvestYield,
            ExpirationDate = detail.ExpirationDate,
            Version = detail.Version,
            Reason = "Correct expiry/quality note"
        };
        var response = await f.Farmer().PutAsJsonAsync($"api/farmer/harvest-batches/{f.BatchId}", input with { ExpirationDate = FarmerTestData.Today.AddDays(10), QualityNotes = "Checked fresh" });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var result = (await response.Content.ReadFromJsonAsync<BatchResponse>())!;
        Assert.Equal(FarmManagementMessages.MSG100, result.Message);
        Assert.Equal("Checked fresh", result.QualityNotes);
        Assert.Equal(25, result.HarvestYield);
        Assert.Equal(detail.BatchCode, result.BatchCode);
        Assert.Contains(result.History, h => h.Action == "UpdateDetails");
    }


    [SqlFact]
    public async Task IT_FAR_04_011_ImmutableIdentity()
    {
        await using var f = await FarmerDatabase.Create();
        var detail = await f.Get<BatchResponse>($"api/farmer/harvest-batches/{f.BatchId}");
        var input = f.Harvest() with
        {
            BatchCode = detail.BatchCode,
            ActualHarvestDate = detail.ActualHarvestDate,
            ActualYield = detail.HarvestYield,
            ExpirationDate = detail.ExpirationDate,
            Version = detail.Version,
            Reason = "Correct expiry/quality note"
        };
        await FarmerDatabase.Error(await f.Farmer().PutAsJsonAsync($"api/farmer/harvest-batches/{f.BatchId}", input with { BatchCode = "CHANGED" }), HttpStatusCode.BadRequest, FarmManagementMessages.Immutable);
        await FarmerDatabase.Error(await f.Farmer().PutAsJsonAsync($"api/farmer/harvest-batches/{f.BatchId}", input with { ActualYield = 26, RequestKey = Guid.NewGuid().ToString("D") }), HttpStatusCode.BadRequest, FarmManagementMessages.Immutable);
        Assert.Equal(25, (await f.Get<BatchResponse>($"api/farmer/harvest-batches/{f.BatchId}")).HarvestYield);
    }


    [SqlFact]
    public async Task IT_FAR_04_012_ExpiryProtectsDelivery()
    {
        await using var f = await FarmerDatabase.Create();
        await f.Edit(async db => (await db.Orders.SingleAsync()).ExpectedDeliveryDate = FarmerTestData.Today.AddDays(3).ToDateTime(TimeOnly.MinValue));
        var detail = await f.Get<BatchResponse>($"api/farmer/harvest-batches/{f.BatchId}");
        var input = f.Harvest() with
        {
            BatchCode = detail.BatchCode,
            ActualHarvestDate = detail.ActualHarvestDate,
            ActualYield = detail.HarvestYield,
            ExpirationDate = detail.ExpirationDate,
            Version = detail.Version,
            Reason = "Correct expiry/quality note"
        };
        Assert.NotEmpty(detail.Commitments);
        await FarmerDatabase.Error(await f.Farmer().PutAsJsonAsync($"api/farmer/harvest-batches/{f.BatchId}", input with { ExpirationDate = FarmerTestData.Today.AddDays(3) }), HttpStatusCode.BadRequest, FarmManagementMessages.MSG130);
        Assert.Equal(detail.ExpirationDate, (await f.Get<BatchResponse>($"api/farmer/harvest-batches/{f.BatchId}")).ExpirationDate);
    }


    [SqlFact]
    public async Task IT_FAR_04_013_ConcurrentInitialHarvest()
    {
        await using var f = await FarmerDatabase.Create();
        var plan = await f.Post<BatchResponse>("api/farmer/harvest-batches/plans", f.Harvest());
        var client = f.Farmer();
        var input = f.Harvest() with
        {
            Version = plan.Version
        };
        string url = $"api/farmer/harvest-batches/{plan.HarvestBatchId}/harvest";
        var responses = await Task.WhenAll(client.PostAsJsonAsync(url, input), client.PostAsJsonAsync(url, input with { RequestKey = Guid.NewGuid().ToString("D") }));
        Assert.Single(responses, x => x.IsSuccessStatusCode);
        await FarmerDatabase.Error(Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict, FarmManagementMessages.MSG122);
        await using var db = f.Open();
        Assert.Equal(1, await db.InventoryMovements.CountAsync(x => x.HarvestBatchId == plan.HarvestBatchId && x.MovementType == "StockIn"));
        Assert.Equal(10, (await db.HarvestBatches.FindAsync(plan.HarvestBatchId))!.HarvestYield);
    }


    [SqlFact]
    public async Task IT_FAR_04_014_FilterAndOwnedAccess()
    {
        await using var f = await FarmerDatabase.Create();
        var page = await f.Get<PageResult<BatchResponse>>($"api/farmer/harvest-batches?productId={f.ProductId}&seasonId={f.SeasonId}&from=2026-10-26&to=2026-10-26");
        Assert.Equal(f.BatchId, Assert.Single(page.Items).HarvestBatchId);
        Assert.Empty((await f.Get<PageResult<BatchResponse>>("api/farmer/harvest-batches?keyword=NO_MATCH")).Items);
        await FarmerDatabase.Error(await f.Farmer().GetAsync("api/farmer/harvest-batches/999999"), HttpStatusCode.Forbidden, FarmManagementMessages.MSG121);
    }
    [SqlFact]
    public async Task IT_FAR_04_017_DirectActualHarvest()
    {
        await using var f = await FarmerDatabase.Create();
        var actual = await f.Post<BatchResponse>("api/farmer/harvest-batches/harvest", f.Harvest());
        Assert.Equal(FarmManagementMessages.MSG135, actual.Message);
        Assert.Equal(10, actual.AvailableQuantity);
        Assert.Null(actual.QrIdentity);
        await using var db = f.Open();
        Assert.Equal(10, (await db.InventoryMovements.SingleAsync()).QuantityChange);
        Assert.Empty(await db.TraceabilityQrcodes.ToListAsync());
    }
    [SqlFact]
    public async Task IT_FAR_04_018_PositiveAdjustmentAndReplay()
    {
        await using var f = await FarmerDatabase.Create();
        var detail = await f.Get<BatchResponse>($"api/farmer/harvest-batches/{f.BatchId}");
        var input = new BatchIssueRequest { IssueType = "adjustment", Quantity = 2, Reason = "Correct verified scale reading", EvidenceUrls = [await f.FarmEvidence()], Version = detail.Version, RequestKey = Guid.NewGuid().ToString("D") };
        var result = await f.Post<BatchResponse>($"api/farmer/harvest-batches/{f.BatchId}/issues", input);
        Assert.Equal(22, result.AvailableQuantity);
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync($"api/farmer/harvest-batches/{f.BatchId}/issues", input), HttpStatusCode.Conflict, FarmManagementMessages.MSG122);
        await using var db = f.Open();
        Assert.Equal(2, (await db.InventoryMovements.SingleAsync()).QuantityChange);
    }
}
