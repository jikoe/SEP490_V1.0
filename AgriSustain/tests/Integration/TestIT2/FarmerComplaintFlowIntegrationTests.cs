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
public sealed class FarmerComplaintFlowIntegrationTests
{
    [SqlFact]
    public async Task IT_FAR_10_007_FilterComplaintAndRequestStatus()
    {
        await using var f = await FarmerDatabase.Create();
        var page = await f.Get<FarmerComplaintPage>("api/farmer/complaints?status=UnderReview&requestStatus=Pending&keyword=TEST-SELLER");
        Assert.Equal(f.TicketId, Assert.Single(page.Items).Id);
        Assert.Empty((await f.Get<FarmerComplaintPage>("api/farmer/complaints?keyword=NO_MATCH")).Items);
        await FarmerDatabase.Error(await f.Farmer().GetAsync("api/farmer/complaints?status=INVALID"), HttpStatusCode.BadRequest, FarmerComplaintMessages.Invalid);
    }


    [SqlFact]
    public async Task IT_FAR_10_008_RepeatedAndAnsweredRequest()
    {
        await using var f = await FarmerDatabase.Create();
        var input = await f.Response();
        await f.Post<FarmerComplaintDetail>($"api/farmer/complaints/{f.TicketId}/responses", input);
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync($"api/farmer/complaints/{f.TicketId}/responses", input), HttpStatusCode.Conflict, FarmerComplaintMessages.Conflict);
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync($"api/farmer/complaints/{f.TicketId}/responses", input with { RequestKey = Guid.NewGuid().ToString("D") }), HttpStatusCode.Conflict, FarmerComplaintMessages.Closed);
        await using var db = f.Open();
        Assert.Equal(1, await db.AuditLogs.CountAsync());
        Assert.Equal(2, await db.Notifications.CountAsync());
    }


    [SqlFact]
    public async Task IT_FAR_10_009_NewEvidenceRequestIndependentDeadline()
    {
        await using var f = await FarmerDatabase.Create();
        var first = await f.Post<FarmerComplaintDetail>($"api/farmer/complaints/{f.TicketId}/responses", await f.Response());
        var firstHistory = Assert.Single(first.History);
        long next = 0;
        await f.Edit(async db =>
        {
            var original = await db.DisputeEvidenceRequests.FindAsync(f.RequestId);
            var request = new DisputeEvidenceRequest
            {
                DisputeTicketId = f.TicketId,
                RequestedPartyUserId = f.FarmerId,
                RequestedByUserId = original!.RequestedByUserId,
                RequestedAt = f.Clock.Now.UtcDateTime,
                RequestStatus = "Pending",
                Instructions = "Supply additional proof"
            };
            db.Add(request);
            await db.SaveChangesAsync();
            next = request.EvidenceRequestId;
        });
        var detail = await f.Get<FarmerComplaintDetail>($"api/farmer/complaints/{f.TicketId}");
        var pending = Assert.Single(detail.Requests, r => r.Id == next);
        Assert.True(pending.CanRespond);
        Assert.Equal(f.Clock.Now.AddHours(48).UtcDateTime, pending.DueAt);
        var input = new FarmerComplaintResponseRequest(next, "Additional evidence and explanation.", [f.Evidence], detail.Version, Guid.NewGuid().ToString("D"));
        var result = await f.Post<FarmerComplaintDetail>($"api/farmer/complaints/{f.TicketId}/responses", input);
        Assert.Equal(2, result.History.Count);
        Assert.Equal(firstHistory.Explanation, result.History.Single(h => h.RequestId == f.RequestId).Explanation);
        Assert.Equal(firstHistory.SubmittedAt, result.History.Single(h => h.RequestId == f.RequestId).SubmittedAt);
    }


    [SqlFact]
    public async Task IT_FAR_10_010_ReadDecisionWithoutFinancialMutation()
    {
        await using var f = await FarmerDatabase.Create();
        await f.Edit(async db =>
        {
            var ticket = await db.DisputeTickets.FindAsync(f.TicketId);
            ticket!.TicketStatus = "RefundPending";
            ticket.ResolutionNotes = "Support approved refund";
            ticket.RefundAmount = 100000;
        });
        var detail = await f.Get<FarmerComplaintDetail>($"api/farmer/complaints/{f.TicketId}");
        Assert.Equal("Support approved refund", detail.Resolution);
        Assert.Equal(100000, detail.RefundAmount);
        Assert.All(detail.Requests, r => Assert.False(r.CanRespond));
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync($"api/farmer/complaints/{f.TicketId}/responses", await f.Response()), HttpStatusCode.Conflict, FarmerComplaintMessages.Closed);
        await using var db = f.Open();
        Assert.Empty(await db.RefundTransactions.ToListAsync());
        Assert.Equal(20, (await db.HarvestBatches.FindAsync(f.BatchId))!.AvailableQuantity);
    }


    [SqlFact]
    public async Task IT_FAR_10_011_PendingFarmReadsOnly()
    {
        await using var f = await FarmerDatabase.Create();
        await f.Edit(async db => (await db.FarmerProfiles.SingleAsync())!.VerificationStatus = "Pending");
        var detail = await f.Get<FarmerComplaintDetail>($"api/farmer/complaints/{f.TicketId}");
        Assert.All(detail.Requests, r => Assert.False(r.CanRespond));
        await FarmerDatabase.Error(await f.Farmer().PostAsJsonAsync($"api/farmer/complaints/{f.TicketId}/responses", await f.Response()), HttpStatusCode.Forbidden, FarmerComplaintMessages.Forbidden);
    }
}
