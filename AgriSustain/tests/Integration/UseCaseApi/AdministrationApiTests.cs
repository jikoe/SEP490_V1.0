using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.IntegrationTests.Commerce;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.UseCaseApi;

/// <summary>UC-ADM-03 and UC-ADM-05 through UsersController / CategoriesController on SQL Server.</summary>
[Trait("Layer", "SqlIntegration")]
public sealed class AdministrationApiTests : IAsyncLifetime
{
    private ApiHost _api = null!;

    public async Task InitializeAsync() => _api = await ApiHost.StartAsync();

    public async Task DisposeAsync() => await _api.DisposeAsync();

    // ---------- UC-ADM-03 ----------

    [CommerceSqlFact(DisplayName = "[IT UC-ADM-03 #09] Anonymous request to the user list is challenged")]
    public async Task Users_Anonymous_Unauthorized()
    {
        await ApiAssert.Status(await _api.Anonymous().GetAsync("api/users"), HttpStatusCode.Unauthorized);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-ADM-03 #10] Retail customer cannot open the user list")]
    public async Task Users_RetailCustomer_Forbidden()
    {
        await ApiAssert.Status(await _api.Buyer().GetAsync("api/users"), HttpStatusCode.Forbidden);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-ADM-03 #11] Page size above 100 is rejected")]
    public async Task Users_PageSize101_Validation()
    {
        await ApiAssert.Problem(await _api.Admin().GetAsync("api/users?page=1&pageSize=101"),
            HttpStatusCode.BadRequest, "VALIDATION");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-ADM-03 #12] Suspend without a reason returns a field error and changes nothing")]
    public async Task Users_SuspendBlankReason_FieldError()
    {
        var response = await _api.Admin().PostAsJsonAsync($"api/users/{_api.BuyerId}/status-actions",
            new AccountActionRequest("suspend", "", true));

        var problem = await ApiAssert.Status(response, HttpStatusCode.BadRequest);
        ApiAssert.HasFieldError(problem, "Reason");
        await using var db = _api.Open();
        Assert.Equal("Active", (await db.Users.SingleAsync(user => user.UserId == _api.BuyerId)).Status);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-ADM-03 #13] A stale second suspend is a state conflict")]
    public async Task Users_StaleSecondSuspend_Conflict()
    {
        string url = $"api/users/{_api.BuyerId}/status-actions";
        var first = await _api.Admin().PostAsJsonAsync(url, new AccountActionRequest("suspend", "Vi phạm", true));
        var second = await _api.Admin().PostAsJsonAsync(url, new AccountActionRequest("suspend", "Vi phạm", true));

        await ApiAssert.Status(first, HttpStatusCode.OK);
        await ApiAssert.Problem(second, HttpStatusCode.Conflict, "STATE_CONFLICT");
        await using var db = _api.Open();
        Assert.Equal("Suspended", (await db.Users.SingleAsync(user => user.UserId == _api.BuyerId)).Status);
        Assert.Single(await db.AuditLogs.Where(log => log.EntityType == "User").ToListAsync());
    }

    // ---------- UC-ADM-05 ----------

    [CommerceSqlFact(DisplayName = "[IT UC-ADM-05 #07] Farmer cannot open category management")]
    public async Task Categories_Farmer_Forbidden()
    {
        await ApiAssert.Status(await _api.As(_api.FarmerUserId, "Farmer").GetAsync("api/categories"),
            HttpStatusCode.Forbidden);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-ADM-05 #08] Category name of 151 characters is rejected")]
    public async Task Categories_LongName_FieldError()
    {
        var response = await _api.Admin().PostAsJsonAsync("api/categories",
            new CategoryRequest(new string('a', 151), null, true, false));

        ApiAssert.HasFieldError(await ApiAssert.Status(response, HttpStatusCode.BadRequest), "Name");
        await using var db = _api.Open();
        Assert.Equal(1, await db.Categories.CountAsync());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-ADM-05 #09] Deactivate without confirmation is rejected")]
    public async Task Categories_UnconfirmedDeactivate_Validation()
    {
        var response = await _api.Admin().PostAsJsonAsync($"api/categories/{_api.CategoryId}/deactivate",
            new ConfirmationRequest(false));

        await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "VALIDATION");
        await using var db = _api.Open();
        Assert.True((await db.Categories.SingleAsync()).IsActive);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-ADM-05 #10] Unsupported category operation does not match a route")]
    public async Task Categories_UnknownOperation_NotFound()
    {
        var response = await _api.Admin().PostAsJsonAsync($"api/categories/{_api.CategoryId}/rename",
            new ConfirmationRequest(true));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-ADM-05 #11] Archived category cannot be edited")]
    public async Task Categories_Archived_Conflict()
    {
        await ApiAssert.Status(await _api.Admin().PostAsJsonAsync($"api/categories/{_api.CategoryId}/archive",
            new ConfirmationRequest(true)), HttpStatusCode.Conflict);   // still has an active product
        await using (var db = _api.Open())
        {
            await db.Products.ExecuteUpdateAsync(set => set.SetProperty(product => product.IsPublished, false)
                .SetProperty(product => product.ApprovalStatus, "Rejected"));
        }

        await ApiAssert.Status(await _api.Admin().PostAsJsonAsync($"api/categories/{_api.CategoryId}/archive",
            new ConfirmationRequest(true)), HttpStatusCode.OK);
        var edit = await _api.Admin().PutAsJsonAsync($"api/categories/{_api.CategoryId}",
            new CategoryRequest("Tên mới", null, false, true));

        await ApiAssert.Problem(edit, HttpStatusCode.Conflict, "ARCHIVED");
        await using var check = _api.Open();
        Assert.Equal("Test produce", (await check.Categories.SingleAsync()).CategoryName);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-ADM-05 #12] Product in a deactivated category can no longer be quoted")]
    public async Task Categories_Deactivated_BlocksCheckout()
    {
        await ApiAssert.Status(await _api.Admin().PostAsJsonAsync($"api/categories/{_api.CategoryId}/deactivate",
            new ConfirmationRequest(true)), HttpStatusCode.OK);

        var quote = await _api.Buyer().PostAsJsonAsync("api/checkout/quotes", ApiHost.CheckoutInput());

        await ApiAssert.Problem(quote, HttpStatusCode.Conflict, "MSG88");
        await using var db = _api.Open();
        Assert.Empty(await db.RetailCheckoutQuotes.ToListAsync());
    }
}
