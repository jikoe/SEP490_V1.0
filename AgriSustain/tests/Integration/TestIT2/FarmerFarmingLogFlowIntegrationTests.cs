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
public sealed class FarmerFarmingLogFlowIntegrationTests
{
    [SqlFact]
    public async Task IT_FAR_02_007_FilterEmptyAndImmutableRoutes()
    {
        await using var f = await FarmerDatabase.Create();
        var original = await f.Post<FarmingLogResponse>("api/farmer/farming-logs", f.Log());
        var page = await f.Get<PageResult<FarmingLogResponse>>($"api/farmer/farming-logs?seasonId={f.SeasonId}&from=2026-11-02&to=2026-11-02");
        Assert.Equal(original.CultivationLogId, Assert.Single(page.Items).CultivationLogId);
        Assert.Empty((await f.Get<PageResult<FarmingLogResponse>>("api/farmer/farming-logs?keyword=NO_MATCH")).Items);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await f.Farmer().PutAsJsonAsync($"api/farmer/farming-logs/{original.CultivationLogId}", f.Log())).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await f.Farmer().DeleteAsync($"api/farmer/farming-logs/{original.CultivationLogId}")).StatusCode);
        Assert.Equal(original.DetailDescription, (await f.Get<FarmingLogResponse>($"api/farmer/farming-logs/{original.CultivationLogId}")).DetailDescription);
    }


    [SqlFact]
    public async Task IT_FAR_02_008_CorrectionConflictAndAccess()
    {
        await using var f = await FarmerDatabase.Create();
        var original = await f.Post<FarmingLogResponse>("api/farmer/farming-logs", f.Log());
        var input = f.Log() with
        {
            Version = original.Version,
            CorrectionReason = "Correct details"
        };
        await f.Post<FarmingLogResponse>($"api/farmer/farming-logs/{original.CultivationLogId}/corrections", input);
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync($"api/farmer/farming-logs/{original.CultivationLogId}/corrections", input with { RequestKey = Guid.NewGuid().ToString("D") }), HttpStatusCode.Conflict, FarmManagementMessages.MSG122);
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync("api/farmer/farming-logs/999999/corrections", input with { RequestKey = Guid.NewGuid().ToString("D") }), HttpStatusCode.Forbidden, FarmManagementMessages.MSG121);
        await using var db = f.Open();
        Assert.Equal(2, await db.CultivationLogs.CountAsync());
    }
}
