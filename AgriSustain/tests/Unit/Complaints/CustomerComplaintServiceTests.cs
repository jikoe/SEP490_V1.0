using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Complaints;
using AgriSustain.Domain.Entities;
using AgriSustain.UnitTests.DuIte1;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgriSustain.UnitTests.Complaints;

[Trait("UseCase", "UC-RC-07")]
public sealed class CustomerComplaintServiceTests
{
    private const long Buyer = 10;
    private const long TicketId = 42;
    private const long RequestId = 12;
    private const string Url = "/api/files/evidence/10/owned.png";
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);
    private readonly ComplaintResolutionServiceTests.ComplaintStore _notices = new();
    private readonly Store _store = new();
    private readonly CustomerComplaintService _service;

    public CustomerComplaintServiceTests()
    {
        _service = new(_store, _notices, _store, _store, _store, _store, new FakeClock(Now),
            NullLogger<CustomerComplaintService>.Instance);
    }

    [Fact]
    public async Task ValidResponse_SavesHistoryAndEmailFailureDoesNotUndoCommit()
    {
        const string text = "Giải thích theo thông tin đã yêu cầu. ";
        string explanation = string.Concat(Enumerable.Repeat(text, 32)).Trim();
        var input = (await InputAsync()) with { Explanation = explanation };
        var result = await _service.RespondAsync(Buyer, TicketId, input, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(DisputeTicket.StatusUnderReview, _store.Ticket.TicketStatus);
        Assert.Equal("Responded", _store.Request.RequestStatus);
        var audit = Assert.Single(_store.Audits);
        Assert.Equal(CustomerComplaintService.ResponseAction, audit.Action);
        Assert.Equal(Buyer, audit.PerformedByUserId);
        Assert.Equal(1, _store.Commits);
        Assert.Equal(1, _store.EmailAttempts);
        Assert.Collection(_notices.Notifications,
            notice => Assert.Equal(("InApp", "Sent"), (notice.DeliveryChannel, notice.DeliveryStatus)),
            notice => Assert.Equal(("Email", "Pending"), (notice.DeliveryChannel, notice.DeliveryStatus)));
        var saved = await _service.GetAsync(Buyer, TicketId, default);
        var history = Assert.Single(saved.Value!.Requests);
        Assert.False(history.CanRespond);
        Assert.Equal(explanation, history.Explanation);
        Assert.Single(history.Files);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OtherPendingRequest_KeepsTicketWaitingForFarmerOrTimeout(bool overdue)
    {
        _store.Ticket.DisputeEvidenceRequests.Add(new DisputeEvidenceRequest
        {
            EvidenceRequestId = 13,
            RequestedPartyUserId = 20,
            RequestStatus = DisputeTicket.EvidenceRequestPending,
            DueAt = Now.UtcDateTime.AddHours(overdue ? -1 : 1)
        });
        _notices.HasActiveSupport = false;

        var result = await _service.RespondAsync(Buyer, TicketId, await InputAsync(), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _store.Commits);
        Assert.Equal(DisputeTicket.StatusWaitingInfo, _store.Ticket.TicketStatus);
        Assert.Empty(_notices.Notifications);
    }

    [Theory]
    [InlineData("other buyer", "MSG121")]
    [InlineData("farmer request", "MSG121")]
    [InlineData("expired", "MSG166")]
    [InlineData("answered", "MSG184")]
    [InlineData("stale version", "MSG122")]
    [InlineData("short text", "MSG183")]
    [InlineData("too many files", "MSG183")]
    [InlineData("foreign file", "MSG183")]
    public async Task InvalidResponse_DoesNotCommit(string scenario, string code)
    {
        var input = await InputAsync();
        long actor = Buyer;
        switch (scenario)
        {
            case "other buyer": actor = 99; break;
            case "farmer request": _store.Request.RequestedPartyUserId = 20; break;
            case "expired": _store.Request.DueAt = Now.UtcDateTime.AddSeconds(-1); break;
            case "answered": _store.Request.MarkAnswered(Now.UtcDateTime); break;
            case "stale version": input = input with { Version = new string('0', 64) }; break;
            case "short text": input = input with { Explanation = "Quá ngắn" }; break;
            case "too many files":
                input = input with { EvidenceUrls = Enumerable.Range(1, 6).Select(x => $"{x}.png").ToArray() };
                break;
            case "foreign file": input = input with { EvidenceUrls = ["/api/files/evidence/99/foreign.png"] }; break;
        }

        var result = await _service.RespondAsync(actor, TicketId, input, default);

        Assert.Equal(code, result.Error?.Code);
        Assert.Equal(0, _store.Commits);
        Assert.Empty(_store.Audits);
    }

    [Fact]
    public async Task RepeatedRequestKey_RejectsRetryWithoutAddingSecondResponse()
    {
        var input = await InputAsync();
        Assert.True((await _service.RespondAsync(Buyer, TicketId, input, default)).IsSuccess);

        var retry = await _service.RespondAsync(Buyer, TicketId,
            input with { RequestKey = input.RequestKey!.ToUpperInvariant() }, default);

        Assert.Equal("MSG122", retry.Error?.Code);
        Assert.Single(_store.Audits);
        Assert.Equal(1, _store.Commits);
    }

    private async Task<CustomerComplaintResponseRequest> InputAsync()
    {
        var detail = await _service.GetAsync(Buyer, TicketId, default);
        return new(RequestId, "Thông tin bổ sung cho khiếu nại.", [Url],
            Guid.NewGuid().ToString("D"), detail.Value!.Version);
    }

    private sealed class Store : ICustomerComplaintRepository, IFarmerComplaintFiles,
        IComplaintEmailDelivery, IUseCaseUnitOfWork, IPersistenceErrorClassifier, ITransaction
    {
        public DisputeTicket Ticket { get; } = new()
        {
            DisputeTicketId = TicketId,
            TicketStatus = DisputeTicket.StatusWaitingInfo,
            SellerOrder = new SellerOrder { SellerOrderCode = "SO-42" }
        };
        public DisputeEvidenceRequest Request { get; } = new()
        {
            EvidenceRequestId = RequestId,
            RequestedPartyUserId = Buyer,
            RequestedAt = Now.UtcDateTime,
            DueAt = Now.UtcDateTime.AddHours(48),
            RequestStatus = DisputeTicket.EvidenceRequestPending
        };
        public List<AuditLog> Audits { get; } = [];
        public int Commits { get; private set; }
        public int EmailAttempts { get; private set; }

        public Store() => Ticket.DisputeEvidenceRequests.Add(Request);

        public Task<DisputeTicket?> LoadOwnedAsync(long buyerId, long id, bool forUpdate, CancellationToken ct) =>
            Task.FromResult(buyerId == Buyer && id == TicketId ? Ticket : null);
        public Task<IReadOnlyList<AuditLog>> GetResponsesAsync(long buyerId, long id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AuditLog>>(Audits);
        public Task<bool> HasRequestAsync(string traceId, CancellationToken ct) =>
            Task.FromResult(Audits.Any(audit => audit.TraceId == traceId));
        public void AddResponse(AuditLog audit, IEnumerable<DisputeEvidence> evidence)
        {
            Audits.Add(audit);
            foreach (var file in evidence)
            {
                file.DisputeEvidenceId = Ticket.DisputeEvidences.Count + 1;
                Ticket.DisputeEvidences.Add(file);
            }
        }
        public Task<PagedResponse<CustomerComplaintListItem>> SearchAsync(
            long buyerId, CustomerComplaintQuery query, DateTime now, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<string?> GetOwnedEvidenceUrlAsync(long buyerId, long id, long evidenceId, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<string> SaveAsync(long actor, IFormFile file, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<bool> IsValidOwnedAsync(long actor, string url, CancellationToken ct) =>
            Task.FromResult(actor == Buyer && url == Url);
        public string? Resolve(long actor, string url) => throw new NotSupportedException();
        public Task DeliverAsync(long notificationId, CancellationToken ct)
        {
            Assert.Equal(1, Commits);
            EmailAttempts++;
            throw new IOException("Email transport unavailable");
        }
        public Task<ITransaction> BeginAsync(CancellationToken ct) => Task.FromResult<ITransaction>(this);
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken ct)
        {
            Commits++;
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public bool IsConflict(Exception exception) => false;
    }
}
