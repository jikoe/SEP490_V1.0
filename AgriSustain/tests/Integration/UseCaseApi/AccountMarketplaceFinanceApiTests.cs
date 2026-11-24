using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Auth;
using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Application.DTOs.Marketplace;
using AgriSustain.Domain.Entities;
using AgriSustain.IntegrationTests.Commerce;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.UseCaseApi;

public sealed class AccountMarketplaceFinanceApiTests
{
    [CommerceSqlFact]
    [Trait("UseCase", "UC-AUTH-06")]
    public async Task ChangePassword_PersistsNewHashAndOnlyNewPasswordSignsIn()
    {
        await using var api = await ApiHost.StartAsync();
        const string newPassword = "FreshPass123";
        const string path = "api/auth/change-password";
        var request = new ChangePasswordRequest(ApiHost.ResetPassword, newPassword, newPassword);
        await using var db = api.Open();
        var original = await db.Users.AsNoTracking().SingleAsync(user => user.UserId == api.ResetUserId);
        await ApiAssert.Status(await api.Anonymous().PostAsJsonAsync(path, request), HttpStatusCode.Unauthorized);

        var client = api.As(api.ResetUserId, "RetailCustomer");
        var problem = await ApiAssert.Problem(await client.PostAsJsonAsync(path,
            request with { CurrentPassword = "IncorrectPass123" }), HttpStatusCode.BadRequest,
            "INVALID_CURRENT_PASSWORD");
        ApiAssert.HasFieldError(problem, "CurrentPassword");
        Assert.Equal(original.PasswordHash, await db.Users.Where(user => user.UserId == api.ResetUserId)
            .Select(user => user.PasswordHash).SingleAsync());

        await ApiAssert.Status(await client.PostAsJsonAsync(path, request), HttpStatusCode.OK);
        var updated = await db.Users.AsNoTracking().SingleAsync(user => user.UserId == api.ResetUserId);
        Assert.NotEqual(original.PasswordHash, updated.PasswordHash);
        Assert.NotEqual(newPassword, updated.PasswordHash);
        Assert.Equal("test-only", await db.Users.Where(user => user.UserId == api.BuyerId)
            .Select(user => user.PasswordHash).SingleAsync());

        await ApiAssert.Status(await api.Anonymous().PostAsJsonAsync("api/auth/login",
            new { identifier = ApiHost.ResetEmail, password = ApiHost.ResetPassword }), HttpStatusCode.Unauthorized);
        using var login = await api.Anonymous().PostAsJsonAsync("api/auth/login",
            new { identifier = ApiHost.ResetEmail, password = newPassword });
        await ApiAssert.Status(login, HttpStatusCode.OK);
        Assert.Equal(api.ResetUserId, (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.User.Id);
    }

    [CommerceSqlFact]
    [Trait("UseCase", "UC-MKT-01")]
    public async Task BrowseMarketplace_AppliesVisibilityFiltersSortingAndPagination()
    {
        await using var api = await ApiHost.StartAsync();
        await using var db = api.Open();
        var hidden = await db.Products.SingleAsync(product => product.ProductName == "Gạo");
        hidden.IsPublished = false;
        await db.SaveChangesAsync();
        var firstId = await db.Products.Where(product => product.ProductName == "Cà chua")
            .Select(product => product.ProductId).SingleAsync();

        string path = $"api/marketplace/products?categoryId={api.CategoryId}&minPrice=10000&sort=price-asc&pageSize=1";
        var first = (await api.Anonymous().GetFromJsonAsync<Result<MarketplacePageResponse>>(
            path + "&page=1"))!.Data;
        var second = (await api.Buyer().GetFromJsonAsync<Result<MarketplacePageResponse>>(
            path + "&page=2"))!.Data;
        Assert.Equal(2, first!.Total);
        Assert.Equal(2, second!.Total);
        Assert.Equal(firstId, Assert.Single(first.Items).Id);
        Assert.Equal(api.SpinachId, Assert.Single(second.Items).Id);
        var empty = await api.Anonymous().GetFromJsonAsync<Result<MarketplacePageResponse>>(
            "api/marketplace/products?keyword=missing-product");
        Assert.Empty(empty!.Data!.Items);
        await ApiAssert.Status(await api.Anonymous().GetAsync(
            "api/marketplace/products?minPrice=20000&maxPrice=10000"), HttpStatusCode.BadRequest);
    }

    [CommerceSqlFact]
    [Trait("UseCase", "UC-MKT-02")]
    public async Task ProductDetail_ReturnsOrderedPublicImagesAndStockThenHidesInactiveCategory()
    {
        await using var api = await ApiHost.StartAsync();
        await using var db = api.Open();
        db.ProductImages.Add(new ProductImage
        {
            ProductId = api.SpinachId, ImageUrl = "https://example.test/second.png", DisplayOrder = 1
        });
        var seededBatch = await db.HarvestBatches.SingleAsync();
        seededBatch.ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(10);
        await db.SaveChangesAsync();
        string path = $"api/marketplace/products/{api.SpinachId}";
        var detail = (await api.Anonymous().GetFromJsonAsync<Result<ProductDetailResponse>>(path))!.Data;

        Assert.Equal(api.SpinachId, detail!.Product.Id);
        Assert.Equal(15_000m, detail.Product.Price);
        Assert.Equal(new[] { "https://example.test/spinach.png", "https://example.test/second.png" },
            detail.Images.Select(image => image.Url));
        var batch = Assert.Single(detail.Batches);
        Assert.Equal(api.BatchId, batch.Id);
        Assert.Equal(20m, batch.AvailableQuantity);

        var category = await db.Categories.SingleAsync();
        category.IsActive = false;
        await db.SaveChangesAsync();
        await ApiAssert.Status(await api.Anonymous().GetAsync(path), HttpStatusCode.NotFound);
        Assert.Equal(20m, await db.HarvestBatches.AsNoTracking().Where(value => value.HarvestBatchId == api.BatchId)
            .Select(value => value.AvailableQuantity).SingleAsync());
    }

    [CommerceSqlFact]
    [Trait("UseCase", "UC-FIN-01")]
    public async Task FinanceDashboard_AggregatesSqlRecordsAndHonorsPeriodAndRole()
    {
        await using var api = await ApiHost.StartAsync();
        const string path = "api/finance/dashboard";
        await ApiAssert.Status(await api.Anonymous().GetAsync(path), HttpStatusCode.Unauthorized);
        await ApiAssert.Status(await api.Buyer().GetAsync(path), HttpStatusCode.Forbidden);
        var client = api.Finance();
        await ApiAssert.Problem(await client.GetAsync(path + "?period=Year"), HttpStatusCode.BadRequest,
            "INVALID_INPUT");

        const string period = "period=Day&date=2026-10-25";
        var dashboard = await client.GetFromJsonAsync<FinanceDashboardResponse>(path + "?" + period);
        Assert.Equal(new DateOnly(2026, 10, 25), dashboard!.Period.Start);
        Assert.Empty(dashboard.Unavailable);
        Assert.Equal(280_000m, dashboard.Metrics["Gmv"]);
        var records = await client.GetFromJsonAsync<PagedResponse<FinanceRecord>>(
            path + "/records?metric=Gmv&" + period);
        var record = Assert.Single(records!.Items);
        Assert.Equal("RC-COMPLAINT", record.Reference);
        Assert.Equal(dashboard.Metrics["Gmv"], record.Amount);

        var empty = await client.GetFromJsonAsync<FinanceDashboardResponse>(path + "?period=Day&date=2026-10-28");
        Assert.Empty(empty!.Unavailable);
        Assert.All(empty.Metrics.Values, amount => Assert.Equal(0m, amount));
        Assert.Equal(0, empty.TransactionsToProcess);
    }
}
