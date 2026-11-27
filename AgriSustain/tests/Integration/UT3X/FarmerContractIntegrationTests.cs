using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Offtake;
using AgriSustain.Application.Services;
using Microsoft.AspNetCore.TestHost;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Infrastructure.Repositories;
using AgriSustain.Infrastructure.UnitOfWork;
using AgriSustain.IntegrationTests.UT1X;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.UT3X;

public sealed class FarmerContractIntegrationTests
{
    [CommerceSqlFact]
    public async Task IT_FAR_08_001_RejectOwnedProposalPersistsReasonAndNotifiesBuyerOnce()
    {
        await using var f = await CommerceDatabase.Create();
        long id = await CreateProposal(f);
        await using var db = f.Open();
        var repo = new FarmerOfftakeContractRepository(db);
        var original = await repo.LoadContextAsync(f.FarmerId, id, false, default);
        Assert.NotNull(original);
        using var farmer = CreateClient(f, f.FarmerId, "Farmer");
        using var buyer = CreateClient(f, f.BuyerId, "RetailCustomer");
        using var denied = await buyer.GetAsync($"api/farmer/offtake-contracts/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var input = new ContractActionRequest
        {
            ExpectedVersion = OfftakeContractRules.Version(original),
            RequestKey = Guid.NewGuid().ToString(),
            Reason = "Nông trại không còn đủ năng lực giao đúng lịch."
        };
        var url = $"api/farmer/offtake-contracts/{id}/reject";
        using var accepted = await farmer.PostAsJsonAsync(url, input);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var repeated = await farmer.PostAsJsonAsync(url, input);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        await using var verify = f.Open();
        Assert.Equal("Rejected", (await verify.B2bcontracts.SingleAsync(x => x.B2bcontractId == id)).Status);
        var audit = await verify.AuditLogs.SingleAsync(x => x.EntityType == "FarmerOfftakeContract"
            && x.EntityId == id && x.Action == "Reject");
        Assert.Equal(input.Reason, audit.Reason);
        var channels = await verify.Notifications.Where(x => x.UserId == f.BuyerId
            && x.RelatedRecordId == id && x.NotificationType == "FarmerOfftakeContract")
            .Select(x => x.DeliveryChannel).OrderBy(x => x).ToListAsync();
        Assert.Equal(new[] { "Email", "InApp" }, channels);
    }

    private static HttpClient CreateClient(CommerceDatabase database, long userId, string role)
    {
        var client = database.Host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", $"{userId}:{role}");
        return client;
    }

    private static async Task<long> CreateProposal(CommerceDatabase f)
    {
        await using var db = f.Open();
        db.WholesaleBuyerProfiles.Add(new WholesaleBuyerProfile
        {
            UserId = f.BuyerId, CompanyName = "Test buyer", TaxCode = "0123456789",
            RepresentativeName = "Buyer", BusinessAddress = "Test address", VerificationStatus = "Approved"
        });
        await db.SaveChangesAsync();
        var farmerId = await db.Products.Where(x => x.ProductId == f.ProductId)
            .Select(x => x.FarmerProfileId).SingleAsync();
        using var buyer = CreateClient(f, f.BuyerId, "WholesaleBuyer");
        // The proposal uses the date fixed by CommerceDatabase.TestClock.
        var proposal = new OfftakeProposalRequest(Guid.NewGuid(), farmerId, f.ProductId,
            20, 25000, new(2026, 11, 1), new(2026, 12, 31), new(2026, 11, 20),
            string.Join("\n\n", new[] { 1, 2, 3, 4, 6, 7, 8 }
                .Select(n => $"Điều {n}: Điều khoản\n  Nội dung {n}")),
            "Thanh toán khi nhận", 100000);
        using var submitted = await buyer.PostAsJsonAsync("api/b2b/contracts", proposal);
        Assert.True(submitted.IsSuccessStatusCode, await submitted.Content.ReadAsStringAsync());
        var receipt = await submitted.Content.ReadFromJsonAsync<Result<OfftakeProposalReceipt>>();
        return Assert.IsType<OfftakeProposalReceipt>(receipt?.Data).Id;
    }
}
