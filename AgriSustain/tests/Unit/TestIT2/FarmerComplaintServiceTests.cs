using Xunit;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.TestIT2.Infrastructure;

namespace AgriSustain.TestIT2.Unit;

[Trait("Layer", "Unit")]
public sealed class FarmerComplaintServiceTests
{
    [Fact]
    public async Task UT_FAR_10_001_AcceptedResponse()
    {
        var f = new ComplaintFixture();
        var result = await f.Service.SubmitResponseAsync(101, 501, await f.Input(), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(FarmerComplaintMessages.Saved, result.Value!.Message);
        Assert.Equal("Responded", f.Request.RequestStatus);
        Assert.Single(f.Repo.Audits);
        Assert.Equal(2, f.Repo.Notifications.Count);
        Assert.All(f.Repo.Notifications, x => Assert.Equal("Sent", x.DeliveryStatus));
        Assert.Equal(1, f.Unit.Commits);
        Assert.Equal(1, f.Mail.Attempts);
        Assert.Equal("UnderReview", f.Repo.Ticket.TicketStatus);
    }


    [Fact]
    public async Task UT_FAR_10_002_Explanation14()
    {
        var f = new ComplaintFixture();
        var input = (await f.Input()) with
        {
            Explanation = "  " + new string('x', 14) + "  "
        };
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.False(result.IsSuccess);
        Assert.Equal(FarmerComplaintMessages.Invalid, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
        Assert.Equal(0, f.Mail.Attempts);
    }


    [Fact]
    public async Task UT_FAR_10_003_Explanation15()
    {
        var f = new ComplaintFixture();
        var input = (await f.Input()) with
        {
            Explanation = "  " + new string('x', 15) + "  "
        };
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, f.Unit.Commits);
    }


    [Fact]
    public async Task UT_FAR_10_004_Explanation2000()
    {
        var f = new ComplaintFixture();
        var input = (await f.Input()) with
        {
            Explanation = "  " + new string('x', 2000) + "  "
        };
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, f.Unit.Commits);
    }


    [Fact]
    public async Task UT_FAR_10_005_Explanation2001()
    {
        var f = new ComplaintFixture();
        var input = (await f.Input()) with
        {
            Explanation = "  " + new string('x', 2001) + "  "
        };
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.False(result.IsSuccess);
        Assert.Equal(FarmerComplaintMessages.Invalid, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
        Assert.Equal(0, f.Mail.Attempts);
    }


    [Fact]
    public async Task UT_FAR_10_006_BlankExplanation()
    {
        var f = new ComplaintFixture();
        var input = (await f.Input()) with
        {
            Explanation = "   "
        };
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.False(result.IsSuccess);
        Assert.Equal(FarmerComplaintMessages.Invalid, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
        Assert.Empty(f.Repo.Notifications);
        Assert.Equal(0, f.Unit.Commits);
    }


    [Fact]
    public async Task UT_FAR_10_007_NoFiles()
    {
        var f = new ComplaintFixture();
        var input = (await f.Input()) with
        {
            EvidenceUrls = []
        };
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.False(result.IsSuccess);
        Assert.Equal(FarmerComplaintMessages.Invalid, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
        Assert.Empty(f.Repo.Notifications);
        Assert.Equal(0, f.Unit.Commits);
    }


    [Fact]
    public async Task UT_FAR_10_008_SixFiles()
    {
        var f = new ComplaintFixture();
        var input = (await f.Input()) with
        {
            EvidenceUrls = Enumerable.Range(0, 6).Select(x => "/owned/101/" + x + ".pdf").ToArray()
        };
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.False(result.IsSuccess);
        Assert.Equal(FarmerComplaintMessages.Invalid, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
        Assert.Empty(f.Repo.Notifications);
        Assert.Equal(0, f.Unit.Commits);
    }


    [Fact]
    public async Task UT_FAR_10_009_DuplicateFiles()
    {
        var f = new ComplaintFixture();
        var input = (await f.Input()) with
        {
            EvidenceUrls = ["/owned/101/a.pdf", "/owned/101/a.pdf"]
        };
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.False(result.IsSuccess);
        Assert.Equal(FarmerComplaintMessages.Invalid, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
        Assert.Empty(f.Repo.Notifications);
        Assert.Equal(0, f.Unit.Commits);
    }


    [Fact]
    public async Task UT_FAR_10_010_ForeignFile()
    {
        var f = new ComplaintFixture();
        var input = (await f.Input()) with
        {
            EvidenceUrls = ["/owned/102/a.pdf"]
        };
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.False(result.IsSuccess);
        Assert.Equal(FarmerComplaintMessages.Invalid, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
        Assert.Empty(f.Repo.Notifications);
        Assert.Equal(0, f.Unit.Commits);
    }


    [Fact]
    public async Task UT_FAR_10_011_StaleVersion()
    {
        var f = new ComplaintFixture();
        var input = (await f.Input()) with
        {
            Version = new string('0', 64)
        };
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.False(result.IsSuccess);
        Assert.Equal(FarmerComplaintMessages.Conflict, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
        Assert.Empty(f.Repo.Notifications);
        Assert.Equal(0, f.Unit.Commits);
    }


    [Fact]
    public async Task UT_FAR_10_012_InvalidRequestKey()
    {
        var f = new ComplaintFixture();
        var input = (await f.Input()) with
        {
            RequestKey = "invalid"
        };
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.False(result.IsSuccess);
        Assert.Equal(FarmerComplaintMessages.Invalid, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
        Assert.Empty(f.Repo.Notifications);
        Assert.Equal(0, f.Unit.Commits);
    }


    [Fact]
    public async Task UT_FAR_10_013_DeadlineExactly48()
    {
        var f = new ComplaintFixture();
        f.Request.RequestedAt = f.Clock.Now.AddHours(-48).UtcDateTime;
        Assert.Equal(f.Clock.Now.UtcDateTime, f.Request.ResponseDeadline);
        Assert.True((await f.Service.SubmitResponseAsync(101, 501, await f.Input(), default)).IsSuccess);
    }


    [Fact]
    public async Task UT_FAR_10_014_AfterDeadline()
    {
        var f = new ComplaintFixture();
        f.Request.RequestedAt = f.Clock.Now.AddHours(-48).AddTicks(-1).UtcDateTime;
        var result = await f.Service.SubmitResponseAsync(101, 501, await f.Input(), default);
        Assert.Equal(FarmerComplaintMessages.Expired, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
    }


    [Fact]
    public async Task UT_FAR_10_015_AlreadyAnswered()
    {
        var f = new ComplaintFixture();
        var input = await f.Input();
        f.Request.MarkAnswered(f.Clock.Now.UtcDateTime);
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.Equal(FarmerComplaintMessages.Closed, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
        Assert.Equal(0, f.Unit.Commits);
    }


    [Fact]
    public async Task UT_FAR_10_016_ClosedTicket()
    {
        var f = new ComplaintFixture();
        var input = await f.Input();
        f.Repo.Ticket.TicketStatus = "Closed";
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.Equal(FarmerComplaintMessages.Closed, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
        Assert.Equal(0, f.Unit.Commits);
    }


    [Fact]
    public async Task UT_FAR_10_017_DuplicateSubmission()
    {
        var f = new ComplaintFixture();
        var input = await f.Input();
        f.Repo.Repeated = true;
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.Equal(FarmerComplaintMessages.Conflict, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
        Assert.Equal(0, f.Unit.Commits);
    }


    [Fact]
    public async Task UT_FAR_10_018_UnapprovedFarm()
    {
        var f = new ComplaintFixture();
        var input = await f.Input();
        f.Repo.Farmer.VerificationStatus = "Pending";
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.Equal(FarmerComplaintMessages.Forbidden, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
        Assert.Equal(0, f.Unit.Commits);
    }


    [Fact]
    public async Task UT_FAR_10_019_NotificationFailureRetry()
    {
        var f = new ComplaintFixture();
        f.Mail.Fail = true;
        var result = await f.Service.SubmitResponseAsync(101, 501, await f.Input(), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(FarmerComplaintMessages.NotificationPending, result.Value!.Message);
        Assert.Single(f.Repo.Audits);
        Assert.Single(f.Repo.Notifications, x => x.DeliveryStatus == "Failed");
        f.Mail.Fail = false;
        await f.Service.NotifySupportAsync(null, default);
        await f.Service.NotifySupportAsync(null, default);
        Assert.Equal(2, f.Mail.Attempts);
        Assert.Equal(2, f.Repo.Notifications.Count);
        Assert.Single(f.Repo.Audits);
        Assert.All(f.Repo.Notifications, x => Assert.Equal("Sent", x.DeliveryStatus));
    }


    [Fact]
    public async Task UT_FAR_10_020_SaveFailure()
    {
        var f = new ComplaintFixture();
        var input = await f.Input();
        f.Unit.FailCommit = true;
        await Assert.ThrowsAsync<IOException>(() => f.Service.SubmitResponseAsync(101, 501, input, default));
        Assert.Equal(1, f.Unit.Rollbacks);
        Assert.Equal(0, f.Mail.Attempts);
    }


    [Fact]
    public async Task UT_FAR_10_021_ForeignComplaint()
    {
        var f = new ComplaintFixture();
        var input = await f.Input();
        f.Repo.Foreign = true;
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.Equal(FarmerComplaintMessages.Forbidden, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
    }
}
