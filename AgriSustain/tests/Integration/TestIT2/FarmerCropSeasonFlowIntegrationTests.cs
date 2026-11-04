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
public sealed class FarmerCropSeasonFlowIntegrationTests
{
    [SqlFact]
    public async Task IT_FAR_01_008_EditPlan()
    {
        await using var f = await FarmerDatabase.Create();
        var plan = f.Plan();
        var created = await f.Post<SeasonResponse>("api/farmer/crop-seasons", plan);
        var input = plan with
        {
            SeasonName = "Updated winter",
            CultivatedArea = 150,
            EstimatedYield = 120,
            Version = created.Version,
            RequestKey = Guid.NewGuid().ToString("D")
        };
        var response = await f.Farmer().PutAsJsonAsync($"api/farmer/crop-seasons/{created.CropSeasonId}", input);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var detail = await f.Get<SeasonResponse>($"api/farmer/crop-seasons/{created.CropSeasonId}");
        Assert.Equal("Updated winter", detail.SeasonName);
        Assert.Equal(150, detail.CultivatedArea);
        Assert.Equal(120, detail.EstimatedYield);
        Assert.Equal("Planned", detail.Status);
        Assert.Contains(detail.History, h => h.Action == "UpdatePlan");
        Assert.Equal(FarmManagementMessages.MSG90, (await response.Content.ReadFromJsonAsync<SeasonResponse>())!.Message);
    }


    [SqlFact]
    public async Task IT_FAR_01_009_ProtectPaidCommitments()
    {
        await using var f = await FarmerDatabase.Create();
        var input = f.Plan();
        var season = await f.Post<SeasonResponse>("api/farmer/crop-seasons", input);
        await f.Edit(db =>
        {
            db.PreOrderCampaigns.Add(new() { ProductId = f.ProductId, CropSeasonId = season.CropSeasonId, CampaignName = "Committed campaign", StartDate = f.Clock.Now.AddDays(-1).UtcDateTime, EndDate = f.Clock.Now.AddDays(10).UtcDateTime, TargetYield = 100, CommittedYield = 50, DepositPercentage = 20, PreOrderPrice = 30000, Status = "Active" });
            return Task.CompletedTask;
        });
        season = await f.Get<SeasonResponse>($"api/farmer/crop-seasons/{season.CropSeasonId}");
        Assert.Equal(50, Assert.Single(season.Commitments).Quantity);
        await FarmerDatabase.Error(await f.Farmer().PutAsJsonAsync($"api/farmer/crop-seasons/{season.CropSeasonId}", input with { EstimatedYield = 49, Version = season.Version, RequestKey = Guid.NewGuid().ToString("D") }), HttpStatusCode.BadRequest, FarmManagementMessages.MSG130);
        await using var db = f.Open();
        Assert.Equal(100, (await db.CropSeasons.FindAsync(season.CropSeasonId))!.EstimatedYield);
        Assert.Equal(50, (await db.PreOrderCampaigns.SingleAsync()).CommittedYield);
    }


    [SqlFact]
    public async Task IT_FAR_01_010_CropFailureRetainsCommitments()
    {
        await using var f = await FarmerDatabase.Create();
        var season = await f.Post<SeasonResponse>("api/farmer/crop-seasons", f.Plan());
        await f.Edit(db =>
        {
            db.PreOrderCampaigns.Add(new() { ProductId = f.ProductId, CropSeasonId = season.CropSeasonId, CampaignName = "Retained commitment", StartDate = f.Clock.Now.AddDays(-1).UtcDateTime, EndDate = f.Clock.Now.AddDays(10).UtcDateTime, TargetYield = 100, CommittedYield = 5, DepositPercentage = 20, PreOrderPrice = 30000, Status = "Active" });
            return Task.CompletedTask;
        });
        season = await f.Get<SeasonResponse>($"api/farmer/crop-seasons/{season.CropSeasonId}");
        var result = await f.Post<SeasonResponse>($"api/farmer/crop-seasons/{season.CropSeasonId}/cancel", new SeasonActionRequest { Outcome = "Crop Failure", ActualYield = 0, Reason = "Flood destroyed the crop", EvidenceUrls = [await f.FarmEvidence()], Version = season.Version, RequestKey = Guid.NewGuid().ToString("D") });
        Assert.Equal("Crop Failure", result.Status);
        Assert.Equal(FarmManagementMessages.MSG129, result.Message);
        await using var db = f.Open();
        var campaign = await db.PreOrderCampaigns.SingleAsync();
        Assert.Equal("Closed", campaign.Status);
        Assert.Equal(5, campaign.CommittedYield);
        Assert.Empty(await db.RefundTransactions.ToListAsync());
    }


    [SqlFact]
    public async Task IT_FAR_01_011_CancelRequiresReasonAndProof()
    {
        await using var f = await FarmerDatabase.Create();
        var season = await f.Post<SeasonResponse>("api/farmer/crop-seasons", f.Plan());
        var input = new SeasonActionRequest
        {
            Outcome = "Cancelled",
            ActualYield = 0,
            Reason = " ",
            Version = season.Version,
            RequestKey = Guid.NewGuid().ToString("D")
        };
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync($"api/farmer/crop-seasons/{season.CropSeasonId}/cancel", input), HttpStatusCode.BadRequest, FarmManagementMessages.Field("reason"));
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync($"api/farmer/crop-seasons/{season.CropSeasonId}/cancel", input with { Reason = "Stop production", RequestKey = Guid.NewGuid().ToString("D") }), HttpStatusCode.BadRequest, FarmManagementMessages.MSG120);
        Assert.Equal("Planned", (await f.Get<SeasonResponse>($"api/farmer/crop-seasons/{season.CropSeasonId}")).Status);
    }


    [SqlFact]
    public async Task IT_FAR_01_012_FilterAndOptions()
    {
        await using var f = await FarmerDatabase.Create();
        var options = await f.Get<FarmOptions>("api/farmer/crop-seasons/options");
        Assert.Contains(options.Products, p => p.ProductId == f.ProductId);
        var page = await f.Get<PageResult<SeasonResponse>>($"api/farmer/crop-seasons?productId={f.ProductId}&status=Harvesting&from=2026-01-01&to=2026-01-01");
        Assert.Equal(f.SeasonId, Assert.Single(page.Items).CropSeasonId);
        await FarmerDatabase.Error(await f.Farmer().GetAsync("api/farmer/crop-seasons?from=2026-11-02&to=2026-01-01"), HttpStatusCode.BadRequest, FarmManagementMessages.MSG120);
    }
}
