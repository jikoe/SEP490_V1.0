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
public sealed class FarmerComplaintIntegrationTests
{
    [SqlFact]
    public async Task IT_FAR_10_001_SubmitAndHistory()
    {
        await using var f = await FarmerDatabase.Create();
        var input = (await f.Response()) with
        {
            Explanation = new string('x', 2000)
        };
        var result = await f.Post<FarmerComplaintDetail>($"api/farmer/complaints/{f.TicketId}/responses", input);
        Assert.Equal(FarmerComplaintMessages.Saved, result.Message);
        Assert.Equal(input.Explanation, Assert.Single(result.History).Explanation);
        await using var db = f.Open();
        Assert.Equal("Responded", (await db.DisputeEvidenceRequests.FindAsync(f.RequestId))!.RequestStatus);
        Assert.Equal(2, await db.Notifications.CountAsync());
        Assert.All(await db.Notifications.ToListAsync(), x => Assert.Equal("Sent", x.DeliveryStatus));
        Assert.True((await db.DisputeEvidences.SingleAsync()).Description!.Length <= 500);
        Assert.Equal(1, f.Mail.Attempts);
        Assert.Empty(await db.RefundTransactions.ToListAsync());
        Assert.Equal(20, (await db.HarvestBatches.FindAsync(f.BatchId))!.AvailableQuantity);
    }


    [SqlFact]
    public async Task IT_FAR_10_002_NotificationFailureRetry()
    {
        await using var f = await FarmerDatabase.Create();
        f.Mail.Fail = true;
        var result = await f.Post<FarmerComplaintDetail>($"api/farmer/complaints/{f.TicketId}/responses", await f.Response());
        Assert.Equal(FarmerComplaintMessages.NotificationPending, result.Message);
        f.Mail.Fail = false;
        f.Clock.Now = f.Clock.Now.AddMinutes(5);
        await using (var scope = f.Host.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<FarmerComplaintService>();
            await service.NotifySupportAsync(null, default);
            await service.NotifySupportAsync(null, default);
        }

        await using var db = f.Open();
        Assert.Equal(1, await db.AuditLogs.CountAsync());
        Assert.Equal(2, await db.Notifications.CountAsync());
        Assert.All(await db.Notifications.ToListAsync(), x => Assert.Equal("Sent", x.DeliveryStatus));
        Assert.Equal(2, f.Mail.Attempts);
    }


    [SqlFact]
    public async Task IT_FAR_10_003_DeadlineAndOwnership()
    {
        await using var f = await FarmerDatabase.Create();
        var input = await f.Response();
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync($"api/farmer/complaints/999999/responses", input), HttpStatusCode.Forbidden, FarmerComplaintMessages.Forbidden);
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync($"api/farmer/complaints/{f.TicketId}/responses", input with { EvidenceUrls = [$"/api/files/evidence/{f.OtherBuyerId}/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.pdf"] }), HttpStatusCode.BadRequest, FarmerComplaintMessages.Invalid);
        await f.Edit(async db => (await db.DisputeEvidenceRequests.FindAsync(f.RequestId))!.RequestedAt = f.Clock.Now.AddHours(-48).AddSeconds(-1).UtcDateTime);
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync($"api/farmer/complaints/{f.TicketId}/responses", await f.Response()), HttpStatusCode.Conflict, FarmerComplaintMessages.Expired);
        await using var db = f.Open();
        Assert.Empty(await db.DisputeEvidences.ToListAsync());
        Assert.Empty(await db.Notifications.ToListAsync());
    }


    [SqlFact]
    public async Task IT_FAR_10_004_ConcurrentSubmission()
    {
        await using var f = await FarmerDatabase.Create();
        var input = await f.Response();
        var client = f.Farmer();
        var responses = await Task.WhenAll(client.PostAsJsonAsync($"api/farmer/complaints/{f.TicketId}/responses", input), client.PostAsJsonAsync($"api/farmer/complaints/{f.TicketId}/responses", input with { RequestKey = Guid.NewGuid().ToString("D") }));
        Assert.Single(responses, x => x.IsSuccessStatusCode);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        await using var db = f.Open();
        Assert.Equal(1, await db.AuditLogs.CountAsync());
        Assert.Equal(1, await db.DisputeEvidences.CountAsync());
        Assert.Equal(2, await db.Notifications.CountAsync());
    }


    [SqlFact]
    public async Task IT_FAR_10_005_PrivateEvidence()
    {
        await using var f = await FarmerDatabase.Create();
        var result = await f.Post<FarmerComplaintDetail>($"api/farmer/complaints/{f.TicketId}/responses", await f.Response());
        string url = Assert.Single(Assert.Single(result.History).Files).Url;
        var response = await f.Farmer().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.NotEqual(HttpStatusCode.OK, (await f.As(f.OtherBuyerId, "Farmer").GetAsync(url)).StatusCode);
    }


    [SqlFact]
    public async Task IT_FAR_10_006_RollbackResponse()
    {
        await using var f = await FarmerDatabase.Create();
        var input = await f.Response();
        f.Failure.Armed = true;
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync($"api/farmer/complaints/{f.TicketId}/responses", input), HttpStatusCode.InternalServerError, FarmerComplaintMessages.SaveFailed);
        await using var db = f.Open();
        Assert.Equal("Pending", (await db.DisputeEvidenceRequests.FindAsync(f.RequestId))!.RequestStatus);
        Assert.Empty(await db.DisputeEvidences.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
        Assert.Empty(await db.Notifications.ToListAsync());
        Assert.Equal(0, f.Mail.Attempts);
    }
}
