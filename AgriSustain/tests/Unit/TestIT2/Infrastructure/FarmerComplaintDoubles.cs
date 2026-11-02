using Xunit;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.TestIT2.Infrastructure;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.Services.Complaints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgriSustain.TestIT2.Infrastructure;

internal sealed class UnitProbe : IUnitOfWork
{
    public int Commits, Rollbacks;

    public bool FailCommit;

    public Task BeginAsync(CancellationToken ct) => Task.CompletedTask;

    public Task FlushAsync(CancellationToken ct) => Task.CompletedTask;

    public Task CommitAsync(CancellationToken ct)
    {
        if (FailCommit)
            throw new IOException("Test save failure");
        Commits++;
        return Task.CompletedTask;
    }


    public Task RollbackAsync(CancellationToken ct)
    {
        Rollbacks++;
        return Task.CompletedTask;
    }
}

internal sealed class ComplaintFilesProbe : IFarmerComplaintFiles
{
    public bool Valid = true;

    public Task<string> SaveAsync(
        long actor,
        IFormFile file,
        CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<bool> IsValidOwnedAsync(
        long actor,
        string url,
        CancellationToken ct) =>
        Task.FromResult(Valid && actor == 101 && url.StartsWith("/owned/101/", StringComparison.Ordinal));

    public string? Resolve(long actor, string url) => null;
}

internal sealed class ComplaintRepositoryProbe : IFarmerComplaintRepository
{
    public FarmerProfile Farmer = new()
    {
        FarmerProfileId = 10,
        UserId = 101,
        VerificationStatus = "Approved",
        User = new()
        {
            UserId = 101,
            Status = "Active",
            Role = "Farmer"
        }
    };

    public DisputeTicket Ticket;

    public readonly List<AuditLog> Audits = [];

    public readonly List<Notification> Notifications = [];

    public bool Repeated, Foreign;

    public bool NoSupport { get; set; }


    public ComplaintRepositoryProbe(FixedClock clock)
    {
        Ticket = new()
        {
            DisputeTicketId = 501,
            TicketStatus = "UnderReview",
            DisputeReason = "Quality",
            SellerOrder = new()
            {
                FarmerProfile = Farmer,
                SellerOrderCode = "ORDER-501",
                Order = new()
                {
                    BuyerUserId = 202,
                    BuyerUser = new()
                    {
                        FullName = "Retail customer"
                    }
                }
            }
        };
        Ticket.DisputeEvidenceRequests.Add(new() { EvidenceRequestId = 601, RequestedPartyUserId = 101, RequestStatus = "Pending", RequestedAt = clock.Now.AddHours(-1).UtcDateTime });
    }


    public Task<FarmerProfile?> GetFarmerAsync(
        long actor,
        CancellationToken ct) =>
        Task.FromResult<FarmerProfile?>(actor == 101 ? Farmer : null);

    public Task<FarmerComplaintPage> SearchOwnedAsync(
        long actor,
        FarmerComplaintQuery query,
        DateTime now,
        CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<DisputeTicket?> LoadOwnedContextAsync(
        long actor,
        long id,
        bool forUpdate,
        CancellationToken ct) =>
        Task.FromResult<DisputeTicket?>(Foreign || id != 501 ? null : Ticket);

    public Task<IReadOnlyList<AuditLog>> GetResponsesAsync(
        long id,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AuditLog>>(Audits);

    public Task<bool> HasRequestAsync(
        string key,
        CancellationToken ct) =>
        Task.FromResult(Repeated || Audits.Any(x => x.TraceId == "farmer-response:" + key));

    public Task<IReadOnlyList<long>> GetSupportRecipientsAsync(
        long? assigned,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<long>>(NoSupport ? [] : [303]);

    public Task<bool> HasFailedNotificationsAsync(
        long id,
        CancellationToken ct) =>
        Task.FromResult(Notifications.Any(x => x.DeliveryStatus == "Failed"));

    public void SaveResponseAsync(AuditLog response, IEnumerable<DisputeEvidence> files)
    {
        response.AuditLogId = Audits.Count + 1;
        Audits.Add(response);
        foreach (var file in files)
            Ticket.DisputeEvidences.Add(file);
    }


    public void QueueSupportNotifications(IEnumerable<Notification> notifications)
    {
        foreach (var notice in notifications)
        {
            notice.NotificationId = Notifications.Count + 1;
            notice.User = new()
            {
                Email = "support@example.test"
            };
            Notifications.Add(notice);
        }
    }


    public Task<IReadOnlyList<long>> PendingNotificationsAsync(
        string? eventKey,
        DateTime now,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<long>>(Notifications.Where(x => x.DeliveryChannel == "Email" && x.DeliveryStatus is "Pending" or "Failed").Select(x => x.NotificationId).ToArray());

    public Task<Notification?> ClaimNotificationAsync(long id, DateTime now, CancellationToken ct)
    {
        var row = Notifications.Single(x => x.NotificationId == id);
        row.DeliveryStatus = "Sending";
        return Task.FromResult<Notification?>(row);
    }


    public Task RecordChannelResultAsync(
        Notification notification,
        bool sent,
        DateTime now,
        CancellationToken ct)
    {
        notification.DeliveryStatus = sent ? "Sent" : "Failed";
        return Task.CompletedTask;
    }
}

internal sealed class ComplaintFixture
{
    public readonly FixedClock Clock = new();

    public readonly UnitProbe Unit = new();

    public readonly MailProbe Mail = new();

    public readonly ComplaintFilesProbe Files = new();

    public readonly ComplaintRepositoryProbe Repo;

    public readonly FarmerComplaintService Service;

    public DisputeEvidenceRequest Request => Repo.Ticket.DisputeEvidenceRequests.Single();


    public ComplaintFixture()
    {
        Repo = new(Clock);
        Mail.MaySend = () => Unit.Commits > 0;
        Service = new(Repo, Unit, Files, Mail, Clock, NullLogger<FarmerComplaintService>.Instance);
    }


    public async Task<FarmerComplaintResponseRequest> Input()
    {
        var detail = await Service.GetComplaintAsync(101, 501, default);
        Assert.True(detail.IsSuccess);
        return new(601, "The original product was packed correctly.", ["/owned/101/proof.pdf"], detail.Value!.Version, Guid.NewGuid().ToString("D"));
    }
}
