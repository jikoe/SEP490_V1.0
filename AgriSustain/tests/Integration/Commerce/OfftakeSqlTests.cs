using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Offtake;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.BackgroundWorkers;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Infrastructure.Repositories;
using AgriSustain.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgriSustain.IntegrationTests.Commerce;

public sealed class OfftakeSqlTests
{
    [CommerceSqlFact]
    public async Task ProposalAndTrackingUseTheSameOwnedContractAndCurrentVersion()
    {
        await using var fixture = await CommerceDatabase.Create();
        await using var db = fixture.Open();
        var profile = new WholesaleBuyerProfile
        {
            UserId = fixture.BuyerId, CompanyName = "Buyer Co", TaxCode = "0123456789",
            RepresentativeName = "Buyer", BusinessAddress = "Test address",
            VerificationStatus = "Approved"
        };
        db.WholesaleBuyerProfiles.Add(profile);
        await db.SaveChangesAsync();
        var farmerId = await db.Products.Where(x => x.ProductId == fixture.ProductId)
            .Select(x => x.FarmerProfileId).SingleAsync();
        var repository = new OfftakeRepository(db);
        var service = new OfftakeService(repository, new SqlUnitOfWork(db),
            new BuyerActor(fixture.BuyerId), new PersistenceErrorClassifier(), fixture.Clock,
            new AccountNotificationSettings(null));
        var proposal = new OfftakeProposalRequest(Guid.NewGuid(), farmerId, fixture.ProductId,
            20, 25000, new(2026, 11, 1), new(2026, 12, 31), new(2026, 11, 20),
            "Giao tại kho", "Thanh toán khi nhận", 100000);

        var submitted = await service.ProposeAsync(proposal, default);
        Assert.True(submitted.Success, submitted.Message);
        var id = Assert.IsType<OfftakeProposalReceipt>(submitted.Data).Id;
        var query = new OfftakeTrackingQuery("Test farm", "PendingApproval", "Tomato",
            new(2026, 10, 27), new(2026, 10, 27));
        var list = await repository.TrackAsync(fixture.BuyerId, false, query, default);
        Assert.Equal(id, Assert.Single(list.Items).Id);
        Assert.Empty((await repository.TrackAsync(fixture.OtherBuyerId, false, query, default)).Items);

        var detail = Assert.IsType<OfftakeTrackingDetail>(
            await repository.DetailAsync(fixture.BuyerId, false, id, default));
        Assert.Equal("Confirmed", Assert.Single(detail.Confirmations).Status);
        var notices = await db.Notifications.Where(x => x.RelatedRecordType == "B2BContract"
            && x.RelatedRecordId == id).OrderBy(x => x.DeliveryChannel).ToListAsync();
        Assert.Collection(notices,
            email =>
            {
                Assert.Equal(fixture.FarmerId, email.UserId);
                Assert.Equal("Email", email.DeliveryChannel);
                Assert.Equal("Pending", email.DeliveryStatus);
                Assert.Contains(detail.Code, email.Content);
            },
            inApp =>
            {
                Assert.Equal(fixture.FarmerId, inApp.UserId);
                Assert.Equal("InApp", inApp.DeliveryChannel);
                Assert.Equal("Sent", inApp.DeliveryStatus);
            });
        Assert.Equal(proposal.DeliveryDate, Assert.Single(detail.Milestones).DueDate);
        Assert.Null(await repository.DetailAsync(fixture.OtherBuyerId, false, id, default));
        var farmerUrl = $"api/farmer/offtake-proposals/{id}";
        var farmerDetail = await fixture.Farmer().GetFromJsonAsync<Result<OfftakeTrackingDetail>>(
            farmerUrl);
        Assert.Equal("Buyer Co", farmerDetail?.Data?.Partner);
        Assert.Null(await repository.FarmerDetailAsync(fixture.OtherBuyerId, id, default));
        await VerifyEmailRetryAsync(fixture, notices[0].NotificationId, id);
        Assert.Empty((await repository.TrackAsync(fixture.BuyerId, true,
            new OfftakeTrackingQuery(), default)).Items);

        var order = await db.Orders.SingleAsync(x => x.BuyerUserId == fixture.BuyerId);
        order.OrderType = "PreOrder";
        await db.SaveChangesAsync();
        var orders = await repository.TrackAsync(fixture.BuyerId, true,
            new OfftakeTrackingQuery("TEST-ORDER", Product: "Tomato"), default);
        Assert.Equal(order.OrderId, Assert.Single(orders.Items).Id);
        Assert.Null(await repository.DetailAsync(fixture.OtherBuyerId, true, order.OrderId, default));

        var contract = await db.B2bcontracts.SingleAsync(x => x.B2bcontractId == id);
        var revised = new ContractVersion
        {
            B2bcontractId = id, VersionNo = 2, VersionStatus = "Pending",
            TermsAndConditions = "Giao tại kho của nhà cung cấp",
            ProposedByUserId = fixture.FarmerId
        };
        revised.ContractSignatures.Add(new ContractSignature
        {
            PartyRole = "Farmer", SignedByUserId = fixture.FarmerId,
            ConfirmationMethod = "InApp", ConfirmationStatus = "Confirmed",
            ConfirmedAt = fixture.Clock.GetUtcNow().UtcDateTime
        });
        db.ContractVersions.Add(revised);
        contract.CurrentVersionNo = revised.VersionNo;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Assert.True((await service.ConfirmRevisionAsync(id, 2, default)).Success);
        var confirmed = Assert.IsType<OfftakeTrackingDetail>(
            await repository.DetailAsync(fixture.BuyerId, false, id, default));
        Assert.Equal("AwaitingDeposit", confirmed.Status);
        Assert.Equal(2, confirmed.Confirmations.Count(x => x.Status == "Confirmed"));
        var buyer = fixture.As(fixture.BuyerId, "WholesaleBuyer");
        using var blocked = await buyer.PostAsync($"api/b2b/contracts/{id}/cancellation", null);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        var draftProposal = proposal with { RequestId = Guid.NewGuid() };
        using var saveResponse = await buyer.PostAsJsonAsync("api/b2b/contracts/drafts", draftProposal);
        Assert.Equal(HttpStatusCode.Created, saveResponse.StatusCode);
        var saved = await saveResponse.Content.ReadFromJsonAsync<Result<OfftakeProposalReceipt>>();
        var draftId = Assert.IsType<OfftakeProposalReceipt>(saved?.Data).Id;
        var draftUrl = $"api/b2b/contracts/{draftId}/draft";
        var draft = Assert.IsType<OfftakeDraft>(
            (await buyer.GetFromJsonAsync<Result<OfftakeDraft>>(draftUrl))?.Data);
        var draftDetail = Assert.IsType<OfftakeTrackingDetail>(
            await repository.DetailAsync(fixture.BuyerId, false, draftId, default));
        Assert.Equal("Draft", draftDetail.Status);
        Assert.Empty(draftDetail.Confirmations);
        Assert.False(await db.Notifications.AnyAsync(x => x.RelatedRecordType == "B2BContract"
            && x.RelatedRecordId == draftId));
        using var unpublished = await fixture.Farmer().GetAsync(
            $"api/farmer/offtake-proposals/{draftId}");
        Assert.Equal(HttpStatusCode.NotFound, unpublished.StatusCode);
        fixture.As(fixture.BuyerId, "WholesaleBuyer");
        var updated = draftProposal with { Quantity = 25 };
        using var updateResponse = await buyer.PutAsJsonAsync($"{draftUrl}/{draft.Version}", updated);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(409, (await service.UpdateDraftAsync(
            draftId, draft.Version, draftProposal, true, default)).StatusCode);
        var latest = Assert.IsType<OfftakeDraft>((await service.DraftAsync(draftId, default)).Data);
        Assert.Equal(25, latest.Proposal.Quantity);
        using var submitResponse = await buyer.PutAsJsonAsync(
            $"{draftUrl}/{latest.Version}?submit=true", updated);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        using var submitRetry = await buyer.PutAsJsonAsync(
            $"{draftUrl}/{latest.Version}?submit=true", updated);
        Assert.Equal(HttpStatusCode.OK, submitRetry.StatusCode);
        Assert.Equal(2, await db.Notifications.CountAsync(x => x.RelatedRecordType == "B2BContract"
            && x.RelatedRecordId == draftId && x.UserId == fixture.FarmerId));
        var sent = Assert.IsType<OfftakeTrackingDetail>(
            await repository.DetailAsync(fixture.BuyerId, false, draftId, default));
        Assert.Equal("PendingApproval", sent.Status);
        Assert.Equal("Confirmed", Assert.Single(sent.Confirmations).Status);
        using var cancelled = await buyer.PostAsync($"api/b2b/contracts/{draftId}/cancellation", null);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("Cancelled", (await repository.DetailAsync(
            fixture.BuyerId, false, draftId, default))?.Status);
    }

    private sealed record BuyerActor(long UserId) : IActorContext;

    private static async Task VerifyEmailRetryAsync(CommerceDatabase fixture, long noticeId,
        long contractId)
    {
        await RunWorkerUntilAsync("Failed");
        var first = Assert.Single(fixture.NotificationGateway.Attempts);
        Assert.Equal("farmer@example.test", first.Recipient);
        Assert.Contains($"https://agrisustain.example.test/Farmer/offtake-proposal.html?id={contractId}",
            first.Body);

        await using var db = fixture.Open();
        var email = await db.Notifications.SingleAsync(x => x.NotificationId == noticeId);
        Assert.Equal(1, email.RetryCount);
        email.SentAt = fixture.Clock.GetUtcNow().UtcDateTime.AddMinutes(-1);
        await db.SaveChangesAsync();

        await RunWorkerUntilAsync("Sent");
        Assert.Equal(new[] { first, first }, fixture.NotificationGateway.Attempts.ToArray());
        Assert.Equal(2, await db.Notifications.CountAsync(x =>
            x.RelatedRecordType == "B2BContract" && x.RelatedRecordId == contractId));

        async Task RunWorkerUntilAsync(string status)
        {
            var scopes = fixture.Host.Services.GetRequiredService<IServiceScopeFactory>();
            using var worker = new OrderNotificationWorker(scopes,
                NullLogger<OrderNotificationWorker>.Instance);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await worker.StartAsync(timeout.Token);
            try
            {
                await using var probe = fixture.Open();
                while (!await probe.Notifications.AnyAsync(x =>
                    x.NotificationId == noticeId && x.DeliveryStatus == status, timeout.Token))
                {
                    await Task.Delay(30, timeout.Token);
                }
            }
            finally
            {
                await worker.StopAsync(CancellationToken.None);
            }
        }
    }
}
