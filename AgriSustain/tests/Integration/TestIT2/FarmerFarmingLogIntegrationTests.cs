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
public sealed class FarmerFarmingLogIntegrationTests
{
    [SqlFact]
    public async Task IT_FAR_02_001_RecordAndRead()
    {
        await using var f = await FarmerDatabase.Create();
        var result = await f.Post<FarmingLogResponse>("api/farmer/farming-logs", f.Log());
        Assert.Equal(FarmManagementMessages.MSG94, result.Message);
        var saved = await f.Get<FarmingLogResponse>($"api/farmer/farming-logs/{result.CultivationLogId}");
        Assert.Equal("Watered the field", saved.DetailDescription);
        Assert.NotEmpty(saved.Recorder);
        var page = await f.Get<PageResult<FarmingLogResponse>>($"api/farmer/farming-logs?seasonId={f.SeasonId}&activity={Uri.EscapeDataString("Tưới nước")}");
        Assert.Single(page.Items);
        await using var db = f.Open();
        Assert.Single(await db.AuditLogs.ToListAsync());
    }


    [SqlFact]
    public async Task IT_FAR_02_002_CorrectionAfterClosure()
    {
        await using var f = await FarmerDatabase.Create();
        var original = await f.Post<FarmingLogResponse>("api/farmer/farming-logs", f.Log());
        await f.Edit(async db => (await db.CropSeasons.FindAsync(f.SeasonId))!.Status = "Completed");
        var correction = await f.Post<FarmingLogResponse>($"api/farmer/farming-logs/{original.CultivationLogId}/corrections", f.Log() with { DetailDescription = "Corrected watering amount", CorrectionReason = "Fix recorded amount", Version = original.Version });
        Assert.Equal(original.CultivationLogId, correction.CorrectsLogId);
        Assert.Equal(FarmManagementMessages.MSG132, correction.Message);
        await using var db = f.Open();
        Assert.Equal("Watered the field", (await db.CultivationLogs.FindAsync(original.CultivationLogId))!.DetailDescription);
        Assert.Equal(2, await db.CultivationLogs.CountAsync());
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync("api/farmer/farming-logs", f.Log()), HttpStatusCode.BadRequest, FarmManagementMessages.MSG133);
    }


    [SqlFact]
    public async Task IT_FAR_02_003_PestProof()
    {
        await using var f = await FarmerDatabase.Create();
        string proof = await f.FarmEvidence();
        var result = await f.Post<FarmingLogResponse>("api/farmer/farming-logs", f.Log() with { ActionType = "Phòng trừ sâu bệnh", InputMaterial = "Bio", Dosage = "10ml", EvidenceUrls = [proof] });
        Assert.Contains(proof, result.EvidenceUrls);
        await using var db = f.Open();
        Assert.NotNull((await db.CultivationLogs.FindAsync(result.CultivationLogId))!.ImageUrl);
    }


    [SqlFact]
    public async Task IT_FAR_02_004_FutureDateAndForeignSeason()
    {
        await using var f = await FarmerDatabase.Create();
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync("api/farmer/farming-logs", f.Log() with { ActionDate = FarmerTestData.Today.AddDays(1) }), HttpStatusCode.BadRequest, FarmManagementMessages.MSG95);
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync("api/farmer/farming-logs", f.Log() with { CropSeasonId = 999999 }), HttpStatusCode.Forbidden, FarmManagementMessages.MSG121);
        await using var db = f.Open();
        Assert.Empty(await db.CultivationLogs.ToListAsync());
    }


    [SqlFact]
    public async Task IT_FAR_02_005_RepeatedRequest()
    {
        await using var f = await FarmerDatabase.Create();
        var input = f.Log();
        await f.Post<FarmingLogResponse>("api/farmer/farming-logs", input);
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync("api/farmer/farming-logs", input), HttpStatusCode.Conflict, FarmManagementMessages.MSG122);
        await using var db = f.Open();
        Assert.Equal(1, await db.CultivationLogs.CountAsync());
        Assert.Equal(1, await db.AuditLogs.CountAsync());
    }


    [SqlFact]
    public async Task IT_FAR_02_006_Rollback()
    {
        await using var f = await FarmerDatabase.Create();
        f.Failure.Armed = true;
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync("api/farmer/farming-logs", f.Log()), HttpStatusCode.InternalServerError, FarmManagementMessages.MSG123);
        await using var db = f.Open();
        Assert.Empty(await db.CultivationLogs.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }
}
