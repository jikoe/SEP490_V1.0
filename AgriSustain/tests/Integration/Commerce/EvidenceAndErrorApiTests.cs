using System.Net;
using System.Net.Http.Json;
using AgriSustain.Api.Middlewares;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AgriSustain.IntegrationTests.Commerce;

public sealed partial class CommerceApiTests
{
    [Theory, Trait("UseCase", "UC-FAR-06")]
    [InlineData(1), InlineData(0)]
    public async Task Evidence_WithinDeadlineAndExactlyAtDeadline_IsAcceptedAndNotifiesSupport(int secondsRemaining)
    {
        await using var api = await Api.Create();
        var evidenceRequest = EvidenceRequest(api, secondsRemaining);
        var request = api.Data.Action(explanation: "Detailed explanation with evidence",
            evidence: ["/api/files/evidence/101/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.pdf"], requestId: evidenceRequest.EvidenceRequestId);
        await Read<SellerOrderResponse>(await api.Client.PostAsJsonAsync("api/farmer/orders/501/evidence", request));
        Assert.Equal("Responded", evidenceRequest.RequestStatus);
        Assert.Equal(api.Data.Clock.Now.UtcDateTime, evidenceRequest.RespondedAt);
        Assert.Equal(1, api.Data.Orders.SupportRequests);
        Assert.Equal(101, Assert.Single(api.Data.Orders.Evidence).SubmittedByUserId);
    }
    [Theory, Trait("UseCase", "UC-FAR-06")]
    [InlineData("late"), InlineData("foreign-party"), InlineData("closed"), InlineData("responded")]
    public async Task Evidence_InvalidRequest_IsRejectedWithoutEvidenceOrNotification(string invalid)
    {
        await using var api = await Api.Create();
        var evidenceRequest = EvidenceRequest(api, invalid == "late" ? -1 : 1);
        if (invalid == "foreign-party") evidenceRequest.RequestedPartyUserId = 999;
        if (invalid == "closed") evidenceRequest.RequestStatus = "Closed";
        if (invalid == "responded") evidenceRequest.RespondedAt = api.Data.Clock.Now.AddMinutes(-1).UtcDateTime;
        var request = api.Data.Action(explanation: "Detailed explanation with evidence",
            evidence: ["/api/files/evidence/101/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.pdf"], requestId: evidenceRequest.EvidenceRequestId);
        await Error(await api.Client.PostAsJsonAsync("api/farmer/orders/501/evidence", request), HttpStatusCode.Conflict, "CONFLICT");
        Assert.Empty(api.Data.Orders.Evidence); Assert.Equal(0, api.Data.Orders.SupportRequests); Assert.Equal(0, api.Data.Work.Commits);
    }
    [Theory, Trait("UseCase", "UC-FAR-06")]
    [InlineData("missing"), InlineData("foreign"), InlineData("traversal")]
    public async Task Evidence_InvalidFiles_IsRejectedBeforePersisting(string invalid)
    {
        await using var api = await Api.Create();
        var evidenceRequest = EvidenceRequest(api, 1);
        string[] urls = invalid switch
        {
            "missing" => [],
            "foreign" => ["/api/files/evidence/999/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.pdf"],
            _ => ["/api/files/evidence/101/../private.pdf"]
        };
        var request = api.Data.Action(explanation: "Detailed explanation with evidence", evidence: urls,
            requestId: evidenceRequest.EvidenceRequestId);
        await Error(await api.Client.PostAsJsonAsync("api/farmer/orders/501/evidence", request), HttpStatusCode.BadRequest, "VALIDATION");
        Assert.Empty(api.Data.Orders.Evidence); Assert.Equal(0, api.Data.Work.Commits);
    }
    private static DisputeEvidenceRequest EvidenceRequest(Api api, int secondsRemaining)
    {
        var request = new DisputeEvidenceRequest
        {
            EvidenceRequestId = 801,
            DisputeTicketId = 701,
            RequestedPartyUserId = 101,
            RequestedByUserId = 103,
            RequestedAt = api.Data.Clock.Now.AddHours(-48).UtcDateTime,
            DueAt = api.Data.Clock.Now.AddSeconds(secondsRemaining).UtcDateTime,
            RequestStatus = "Pending"
        };
        var ticket = new DisputeTicket
        {
            DisputeTicketId = 701,
            SellerOrderId = 501,
            SubmittedByUserId = 102,
            DisputeReason = "Quality",
            TicketStatus = "Open"
        };
        ticket.DisputeEvidenceRequests.Add(request);
        api.Data.Seller.DisputeTickets.Add(ticket);
        return request;
    }

    [Theory]
    [InlineData("/api/farmer/products", "GET", false, 500)]
    [InlineData("/api/farmer/products", "POST", false, 500)]
    [InlineData("/api/cart", "GET", false, 500)]
    [InlineData("/api/cart/items", "POST", false, 500)]
    [InlineData("/api/farmer/orders", "GET", false, 500)]
    [InlineData("/api/farmer/orders/501/dispatch", "POST", false, 500)]
    [InlineData("/api/farmer/products/301", "PUT", true, 409)]
    [InlineData("/api/cart/items/701/quantity", "PUT", true, 409)]
    [InlineData("/api/farmer/orders/501/dispatch", "POST", true, 409)]
    public async Task ExceptionPipeline_ReturnsVietnameseProblemWithoutRawExceptionOrMessageId(
        string path, string method, bool conflict, int status)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        await using var app = builder.Build();
        app.UseMiddleware<FeatureExceptionMiddleware>();
        app.Run((HttpContext _) => Task.FromException(conflict ? new DbUpdateConcurrencyException("private test failure") : new IOException("private test failure")));
        await app.StartAsync(); using var client = app.GetTestClient();
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        await Error(response, (HttpStatusCode)status, conflict ? "CONFLICT" : "SERVER_ERROR");
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private test failure", body);
        Assert.DoesNotContain("System.IO", body);
    }
}
