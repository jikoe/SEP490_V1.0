using Xunit;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.UT2X.Infrastructure;

namespace AgriSustain.UT2X.Unit;

[Trait("Layer", "Unit")]
public sealed class FarmerComplaintEligibilityTests
{
    [Fact]
    public async Task UT_FAR_10_022_InactiveFarmer()
    {
        var f = new ComplaintFixture();
        var input = await f.Input();
        f.Repo.Farmer.User.Status = "Inactive";
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.Equal(FarmerComplaintMessages.Forbidden, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
        Assert.Equal(0, f.Unit.Commits);
        Assert.Equal(0, f.Mail.Attempts);
    }


    [Fact]
    public async Task UT_FAR_10_023_WrongRole()
    {
        var f = new ComplaintFixture();
        var input = await f.Input();
        f.Repo.Farmer.User.Role = "RetailCustomer";
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.Equal(FarmerComplaintMessages.Forbidden, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
        Assert.Equal(0, f.Unit.Commits);
        Assert.Equal(0, f.Mail.Attempts);
    }


    [Fact]
    public async Task UT_FAR_10_024_RequestForAnotherParty()
    {
        var f = new ComplaintFixture();
        var input = await f.Input();
        f.Request.RequestedPartyUserId = 102;
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.Equal(FarmerComplaintMessages.Forbidden, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
        Assert.Equal(0, f.Unit.Commits);
        Assert.Equal(0, f.Mail.Attempts);
    }


    [Fact]
    public async Task UT_FAR_10_025_NoSupportRecipient()
    {
        var f = new ComplaintFixture();
        var input = await f.Input();
        f.Repo.NoSupport = true;
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.Equal(FarmerComplaintMessages.SaveFailed, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
        Assert.Equal(0, f.Unit.Commits);
        Assert.Equal(0, f.Mail.Attempts);
    }


    [Fact]
    public async Task UT_FAR_10_026_FiveEvidenceFiles()
    {
        var f = new ComplaintFixture();
        var input = (await f.Input()) with
        {
            EvidenceUrls = Enumerable.Range(1, 5).Select(n => "/owned/101/" + n + ".pdf").ToArray()
        };
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(5, Assert.Single(result.Value!.History).Files.Count);
    }


    [Fact]
    public async Task UT_FAR_10_027_MissingVersion()
    {
        var f = new ComplaintFixture();
        var result = await f.Service.SubmitResponseAsync(101, 501, (await f.Input()) with { Version = "" }, default);
        Assert.Equal(FarmerComplaintMessages.Invalid, result.Error!.Message);
        Assert.Empty(f.Repo.Audits);
    }


    [Fact]
    public async Task UT_FAR_10_028_OneMp4VideoWithImagesAccepted()
    {
        var f = new ComplaintFixture();
        var input = (await f.Input()) with
        {
            EvidenceUrls = ["/owned/101/clip.mp4", "/owned/101/photo.jpg", "/owned/101/invoice.pdf"]
        };
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(3, Assert.Single(result.Value!.History).Files.Count);
    }


    [Fact]
    public async Task UT_FAR_10_029_TwoMp4VideosRejected()
    {
        var f = new ComplaintFixture();
        var input = (await f.Input()) with
        {
            EvidenceUrls = ["/owned/101/clip1.mp4", "/owned/101/clip2.mp4"]
        };
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.False(result.IsSuccess);
        Assert.Equal(FarmerComplaintMessages.Invalid, result.Error!.Message);
        Assert.Equal(0, f.Unit.Commits);
    }


    [Fact]
    public async Task UT_FAR_10_030_UnsupportedFileExtensionRejected()
    {
        var f = new ComplaintFixture();
        var input = (await f.Input()) with
        {
            EvidenceUrls = ["/owned/101/archive.zip"]
        };
        var result = await f.Service.SubmitResponseAsync(101, 501, input, default);
        Assert.False(result.IsSuccess);
        Assert.Equal(FarmerComplaintMessages.Invalid, result.Error!.Message);
        Assert.Equal(0, f.Unit.Commits);
    }


    [Fact]
    public async Task UT_FAR_10_031_RemainsWaitingWhenAnotherRequestPending()
    {
        var f = new ComplaintFixture();
        f.Repo.Ticket.TicketStatus = "WaitingCustomerInfo";
        f.Repo.Ticket.DisputeEvidenceRequests.Add(new()
        {
            EvidenceRequestId = 602,
            RequestedPartyUserId = 202,
            RequestStatus = "Pending",
            RequestedAt = f.Clock.Now.AddHours(-1).UtcDateTime
        });
        var result = await f.Service.SubmitResponseAsync(101, 501, await f.Input(), default);
        Assert.True(result.IsSuccess);
        Assert.Equal("Responded", f.Repo.Ticket.DisputeEvidenceRequests.Single(x => x.EvidenceRequestId == 601).RequestStatus);
        Assert.Equal("WaitingCustomerInfo", f.Repo.Ticket.TicketStatus);
    }
}
