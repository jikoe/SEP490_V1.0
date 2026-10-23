using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgriSustain.IntegrationTests;

public sealed class AdministrationTests
{
    /// <summary>
    /// Uses an isolated fixture to verify that fixtures cannot be registered outside explicit
    /// Development opt-in. This is not evidence of real JWT/payment integration or durable
    /// multi-process SQL behavior.
    /// </summary>
    [Fact]
    public void AddInfrastructure_ProductionOrMissingOptIn_RejectsDemoRegistration()
    {
        var enabled = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Demo:Enabled"] = "true" }).Build();
        var disabled = new ConfigurationBuilder().Build();
        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddInfrastructure(enabled, false));
        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddInfrastructure(disabled, true));
    }

    /// <summary>
    /// Uses an isolated fixture to verify that authentication and unconfigured staff permissions
    /// fail with the appropriate 401/403 outcome. This is not evidence of real JWT/payment
    /// integration or durable multi-process SQL behavior.
    /// </summary>
    [Theory]
    [InlineData(null, "/api/users", HttpStatusCode.Unauthorized)]
    [InlineData("retail-customer", "/api/users", HttpStatusCode.Forbidden)]
    [InlineData("support", "/api/users", HttpStatusCode.Forbidden)]
    [InlineData("admin", "/api/checkout", HttpStatusCode.Forbidden)]
    public async Task Get_InvalidIdentityOrUnconfiguredRole_ReturnsExpectedStatus(
        string? fixture, string path, HttpStatusCode expected)
    {
        using var factory = new ApiFactory();
        using var client = factory.Client(fixture);
        Assert.Equal(expected, (await client.GetAsync(path)).StatusCode);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that self-action and cancelled confirmation leave
    /// business state/audit unchanged. This is not evidence of real JWT/payment integration or
    /// durable multi-process SQL behavior.
    /// </summary>
    [Fact]
    public async Task Change_SelfActionOrCancelled_DoesNotWriteAudit()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client();
        var self = await client.PostAsJsonAsync("/api/users/1/status-actions",
            new AccountActionRequest("suspend", "Test reason", true));
        var cancelled = await client.PostAsJsonAsync("/api/users/2/status-actions",
            new AccountActionRequest("suspend", "Test reason", false));
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, cancelled.StatusCode);
        Assert.Empty((await factory.Store.SnapshotAsync()).Audits);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that state eligibility still rejects a repeated
    /// suspension without requiring Version. This is not evidence of real JWT/payment integration
    /// or durable multi-process SQL behavior.
    /// </summary>
    [Fact]
    public async Task Change_RepeatedSuspend_RejectsInvalidStateWithoutVersion()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client();
        var request = new AccountActionRequest("suspend", "Test reason", true);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/users/2/status-actions", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync("/api/users/2/status-actions", request)).StatusCode);
        Assert.Single((await factory.Store.SnapshotAsync()).Audits);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that reactivation does not resurrect the revoked
    /// fixture session. This is not evidence of real JWT/payment integration or durable
    /// multi-process SQL behavior.
    /// </summary>
    [Fact]
    public async Task Suspend_RetailCustomer_BlocksOldFixtureAfterReactivation()
    {
        using var factory = new ApiFactory();
        using var admin = factory.Client();
        using var customer = factory.Client("retail-customer");
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/api/checkout")).StatusCode);
        await admin.PostAsJsonAsync("/api/users/10/status-actions",
            new AccountActionRequest("suspend", "Security test", true));
        await admin.PostAsJsonAsync("/api/users/10/status-actions",
            new AccountActionRequest("activate", "Reviewed", true));
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await customer.GetAsync("/api/checkout")).StatusCode);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that the declined duplicate-name rule is not silently
    /// reintroduced. This is not evidence of real JWT/payment integration or durable
    /// multi-process SQL behavior.
    /// </summary>
    [Fact]
    public async Task Create_DuplicateNames_AreAllowedByCurrentDecision()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client();
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/categories",
                new SaveCategoryRequest("Hạt giống", null, null)),
            client.PostAsJsonAsync("/api/categories",
                new SaveCategoryRequest("hạt giống", null, null)));
        Assert.All(responses, response =>
            Assert.Equal(HttpStatusCode.Created, response.StatusCode));
    }

    /// <summary>
    /// Verifies the original active-parent form rule remains enforced without reintroducing
    /// recursive cycle or Version checks. Rejected child writes do not add successful audit rows.
    /// </summary>
    [Fact]
    public async Task SaveOrActivate_HiddenParent_RejectsChildWrite()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client();
        (await client.PostAsJsonAsync("/api/categories/1/deactivate",
            new CategoryActionRequest(true))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/categories",
            new SaveCategoryRequest("Danh mục con", 1, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/categories/3/activate",
                new CategoryActionRequest(true))).StatusCode);
        Assert.Single((await factory.Store.SnapshotAsync()).Audits);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that both published and unpublished product references
    /// block DELETE while IsActive can still be disabled. This is not evidence of real
    /// JWT/payment integration or durable multi-process SQL behavior.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_AnyProductReference_RejectsButAllowsHiding(bool published)
    {
        using var factory = new ApiFactory(state => state.Products[101] = state.Products[101] with
        {
            IsPublished = published,
            IsApproved = published
        });
        using var client = factory.Client();
        var delete = await client.PostAsJsonAsync("/api/categories/3/delete",
            new CategoryActionRequest(true));
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        var hide = await client.PostAsJsonAsync("/api/categories/3/deactivate",
            new CategoryActionRequest(true));
        Assert.Equal(HttpStatusCode.OK, hide.StatusCode);
        var state = await factory.Store.SnapshotAsync();
        Assert.False(state.Categories[3].IsActive);
        Assert.Single(state.Audits);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that child/benchmark/commission references block
    /// deletion without an audit of a successful change. This is not evidence of real JWT/payment
    /// integration or durable multi-process SQL behavior.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task Delete_ChildOrPolicyReference_PreservesCategory(long categoryId)
    {
        using var factory = new ApiFactory(state => state.CategoryPolicyReferences.Add(4));
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(
            $"/api/categories/{categoryId}/delete", new CategoryActionRequest(true))).StatusCode);
        Assert.Empty((await factory.Store.SnapshotAsync()).Audits);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that a reference-free category is removed while its
    /// target ID and old state remain in audit. This is not evidence of real JWT/payment
    /// integration or durable multi-process SQL behavior.
    /// </summary>
    [Fact]
    public async Task Delete_UnreferencedCategory_RemovesAndAuditsTarget()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/categories/4/delete",
            new CategoryActionRequest(true))).StatusCode);
        var state = await factory.Store.SnapshotAsync();
        Assert.False(state.Categories.ContainsKey(4));
        var audit = Assert.Single(state.Audits);
        Assert.Equal(4, audit.TargetId);
        Assert.Equal("null", audit.NewValue);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that an audit failure restores the physically deleted
    /// category. This is not evidence of real JWT/payment integration or durable multi-process
    /// SQL behavior.
    /// </summary>
    [Fact]
    public async Task Delete_AuditFailure_RollsBackPhysicalDelete()
    {
        using var factory = new ApiFactory(decorateSession: session =>
            new ScenarioSession(session, failAudit: true));
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.InternalServerError,
            (await client.PostAsJsonAsync("/api/categories/4/delete",
                new CategoryActionRequest(true))).StatusCode);
        Assert.True((await factory.Store.SnapshotAsync()).Categories.ContainsKey(4));
    }
}
