using System.Net;
using System.Text.Json;
using AgriSustain.Domain.Entities;
using AgriSustain.IntegrationTests.Commerce;
using AgriSustain.IntegrationTests.UseCaseApi;
using Xunit;

namespace AgriSustain.IntegrationTests.DuIT;

/// <summary>UC-WB-01 Search Suppliers (WholesaleBuyer) against SQL Server.</summary>
[Trait("Layer", "SqlIntegration"), Trait("UseCase", "UC-WB-01")]
public sealed class SupplierDirectoryApiTests : IAsyncLifetime
{
    private DuApiHost _api = null!;

    public async Task InitializeAsync() => _api = await DuApiHost.StartAsync();

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private async Task<JsonElement> Search(string query = "")
    {
        var response = await _api.Wholesale().GetAsync("api/b2b/suppliers" + query);
        return await ApiAssert.Status(response, HttpStatusCode.OK);
    }

    private static List<string?> FarmNames(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("farmName").GetString()).ToList();

    [CommerceSqlFact(DisplayName = "[IT UC-WB-01 #01] Only Approved farms are listed, best reputation first, contacts locked without a tier")]
    public async Task Search_Default_ApprovedFarmsContactLocked()
    {
        var page = await Search();

        Assert.Equal(2, page.GetProperty("total").GetInt32());
        Assert.Equal(new[] { "Nông trại Bơ Lâm Đồng", "Trang trại Rau Sạch" }, FarmNames(page));
        Assert.True(page.GetProperty("contactLocked").GetBoolean());
        var first = page.GetProperty("items")[0];
        Assert.Equal(JsonValueKind.Null, first.GetProperty("phone").ValueKind);
        Assert.Contains(first.GetProperty("certificates").EnumerateArray(), c => c.GetString() == "VietGAP");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-WB-01 #02] Keyword, certificate and production-scale filters run in SQL")]
    public async Task Search_Filters_Applied()
    {
        Assert.Equal(new[] { "Trang trại Rau Sạch" }, FarmNames(await Search("?keyword=Rau")));
        Assert.Equal(new[] { "Nông trại Bơ Lâm Đồng" }, FarmNames(await Search("?certificateType=VietGAP")));
        Assert.Equal(new[] { "Trang trại Rau Sạch" }, FarmNames(await Search("?productionScale=" + Uri.EscapeDataString("Nhỏ"))));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-WB-01 #03] Sorting by newest puts the most recently approved farm first")]
    public async Task Search_SortNewest_Ordered()
    {
        var page = await Search("?sort=newest");

        Assert.Equal(new[] { "Trang trại Rau Sạch", "Nông trại Bơ Lâm Đồng" }, FarmNames(page));
        Assert.Equal("Newest", page.GetProperty("sort").GetString());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-WB-01 #04] An active tier with direct contact unlocks the farm phone and e-mail")]
    public async Task Search_WithDirectContactTier_ContactShown()
    {
        await using (var seed = _api.Open())
        {
            var start = DuApiHost.T0.UtcDateTime.AddDays(-1);
            seed.B2bsubscriptions.Add(new B2bsubscription
            {
                WholesaleBuyerProfileId = _api.WholesaleProfileId, PackageId = _api.SilverTierId, StartDate = start,
                EndDate = start.AddDays(30), SubscriptionStatus = "Active", PriceSnapshot = 500_000,
                UnlocksFarmerDirectContactSnapshot = true, CanDownloadContractTemplateSnapshot = true, DurationDaysSnapshot = 30
            });
            await seed.SaveChangesAsync();
        }

        var page = await Search();

        Assert.False(page.GetProperty("contactLocked").GetBoolean());
        var first = page.GetProperty("items")[0];
        Assert.Equal(("0901234567", "farmer-a@example.test"), (first.GetProperty("phone").GetString(), first.GetProperty("email").GetString()));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-WB-01 #05] A buyer whose business profile is not approved cannot search")]
    public async Task Search_BuyerNotApproved_Forbidden()
    {
        var response = await _api.WholesaleNew().GetAsync("api/b2b/suppliers");

        await ApiAssert.Problem(response, HttpStatusCode.Forbidden, "BUYER_NOT_APPROVED");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-WB-01 #06] A keyword longer than 100 characters is refused")]
    public async Task Search_KeywordTooLong_Rejected()
    {
        var response = await _api.Wholesale().GetAsync("api/b2b/suppliers?keyword=" + new string('a', 101));

        await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "KEYWORD_TOO_LONG");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-WB-01 #07] The filter panel lists the certificate types and production scales in use")]
    public async Task Filters_FromSql()
    {
        var json = await ApiAssert.Status(await _api.Wholesale().GetAsync("api/b2b/suppliers/filters"), HttpStatusCode.OK);

        Assert.Contains(json.GetProperty("certificateTypes").EnumerateArray(), c => c.GetString() == "VietGAP");
        Assert.Contains(json.GetProperty("productionScales").EnumerateArray(), c => c.GetString() == "Lớn");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-WB-01 #08] A RetailCustomer cannot open the supplier directory")]
    public async Task Search_RetailCustomer_Forbidden()
    {
        var response = await _api.Customer().GetAsync("api/b2b/suppliers");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }
}
