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
public sealed class FarmerCropSeasonIntegrationTests
{
    [SqlFact]
    public async Task IT_FAR_01_001_CreatePlan()
    {
        await using var f = await FarmerDatabase.Create();
        var result = await f.Post<SeasonResponse>("api/farmer/crop-seasons", f.Plan());
        Assert.Equal("Planned", result.Status);
        Assert.Equal(FarmManagementMessages.MSG89, result.Message);
        await using var db = f.Open();
        Assert.Equal("Planned", (await db.CropSeasons.FindAsync(result.CropSeasonId))!.Status);
        Assert.Single(await db.AuditLogs.Where(x => x.EntityId == result.CropSeasonId && x.EntityType == "CropSeason").ToListAsync());
        Assert.Empty(await db.HarvestBatches.Where(x => x.CropSeasonId == result.CropSeasonId).ToListAsync());
    }


    [SqlFact]
    public async Task IT_FAR_01_002_DuplicatePlan()
    {
        await using var f = await FarmerDatabase.Create();
        var input = f.Plan();
        await f.Post<SeasonResponse>("api/farmer/crop-seasons", input);
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync("api/farmer/crop-seasons", input with { RequestKey = Guid.NewGuid().ToString("D") }), HttpStatusCode.BadRequest, FarmManagementMessages.DuplicateSeason);
        await using var db = f.Open();
        Assert.Equal(1, await db.CropSeasons.CountAsync(x => x.SeasonName == input.SeasonName));
    }


    [SqlFact]
    public async Task IT_FAR_01_003_LifecycleCompletion()
    {
        await using var f = await FarmerDatabase.Create();
        var season = await f.Post<SeasonResponse>("api/farmer/crop-seasons", f.Plan());
        foreach (string next in new[]
        {
            "Planting",
            "Growing",
            "Harvesting"
        }

        )
            season = await f.Post<SeasonResponse>($"api/farmer/crop-seasons/{season.CropSeasonId}/stage", new SeasonActionRequest { NextStage = next, Version = season.Version, RequestKey = Guid.NewGuid().ToString("D") });
        var batch = await f.Post<BatchResponse>("api/farmer/harvest-batches/harvest", f.Harvest() with { CropSeasonId = season.CropSeasonId });
        season = await f.Get<SeasonResponse>($"api/farmer/crop-seasons/{season.CropSeasonId}");
        var done = await f.Post<SeasonResponse>($"api/farmer/crop-seasons/{season.CropSeasonId}/complete", new SeasonActionRequest { ActualYield = 10, CompletionDate = FarmerTestData.Today, Version = season.Version, RequestKey = Guid.NewGuid().ToString("D") });
        Assert.Equal("Completed", done.Status);
        Assert.Equal(FarmManagementMessages.MSG91, done.Message);
        await using var db = f.Open();
        Assert.Equal(1, await db.InventoryMovements.CountAsync(x => x.HarvestBatchId == batch.HarvestBatchId));
    }


    [SqlFact]
    public async Task IT_FAR_01_004_StaleAndRepeated()
    {
        await using var f = await FarmerDatabase.Create();
        var input = f.Plan();
        var season = await f.Post<SeasonResponse>("api/farmer/crop-seasons", input);
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync("api/farmer/crop-seasons", input), HttpStatusCode.Conflict, FarmManagementMessages.MSG122);
        var action = new SeasonActionRequest
        {
            NextStage = "Planting",
            Version = season.Version,
            RequestKey = Guid.NewGuid().ToString("D")
        };
        await f.Post<SeasonResponse>($"api/farmer/crop-seasons/{season.CropSeasonId}/stage", action);
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync($"api/farmer/crop-seasons/{season.CropSeasonId}/stage", action with { NextStage = "Growing", RequestKey = Guid.NewGuid().ToString("D") }), HttpStatusCode.Conflict, FarmManagementMessages.MSG122);
    }


    [SqlFact]
    public async Task IT_FAR_01_005_CancelWithEvidence()
    {
        await using var f = await FarmerDatabase.Create();
        var season = await f.Get<SeasonResponse>($"api/farmer/crop-seasons/{f.SeasonId}");
        var result = await f.Post<SeasonResponse>($"api/farmer/crop-seasons/{f.SeasonId}/cancel", new SeasonActionRequest { Outcome = "Cancelled", ActualYield = 25, Reason = "Storm damaged remaining crop", EvidenceUrls = [await f.FarmEvidence()], Version = season.Version, RequestKey = Guid.NewGuid().ToString("D") });
        Assert.Equal("Cancelled", result.Status);
        Assert.Equal(FarmManagementMessages.MSG129, result.Message);
        await using var db = f.Open();
        Assert.Equal(25, (await db.HarvestBatches.FindAsync(f.BatchId))!.HarvestYield);
        Assert.Contains(await db.AuditLogs.ToListAsync(), x => x.Reason == "Storm damaged remaining crop");
    }


    [SqlFact]
    public async Task IT_FAR_01_006_AccessAndFilter()
    {
        await using var f = await FarmerDatabase.Create();
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.GetAsync("api/farmer/crop-seasons")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Buyer().GetAsync("api/farmer/crop-seasons")).StatusCode);
        await FarmerDatabase.Error(await f.Farmer().GetAsync("api/farmer/crop-seasons/999999"), HttpStatusCode.Forbidden, FarmManagementMessages.MSG121);
        var page = await f.Get<PageResult<SeasonResponse>>("api/farmer/crop-seasons?keyword=NO-MATCH");
        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
    }


    [SqlFact]
    public async Task IT_FAR_01_007_Rollback()
    {
        await using var f = await FarmerDatabase.Create();
        f.Failure.Armed = true;
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync("api/farmer/crop-seasons", f.Plan()), HttpStatusCode.InternalServerError, FarmManagementMessages.MSG123);
        await using var db = f.Open();
        Assert.False(await db.CropSeasons.AnyAsync(x => x.SeasonName == "Winter"));
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }
}
