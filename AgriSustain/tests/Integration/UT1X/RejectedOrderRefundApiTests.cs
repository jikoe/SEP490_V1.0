using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgriSustain.Api.Controllers;
using AgriSustain.Api.Middlewares;
using AgriSustain.Api.Security;
using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Application.Interfaces;
using AgriSustain.UnitTests.UT1X;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AgriSustain.IntegrationTests.UT1X;

[Trait("Layer", "ApiIntegration")]
[Trait("UseCase", "UC-FIN-09")]
public sealed class RejectedOrderRefundApiTests
{
    private sealed class RefundApi : IAsyncDisposable
    {
        public readonly RejectedOrderRefundFixture Data = new();
        private WebApplication host = null!;
        public HttpClient Client = null!;

        public static async Task<RefundApi> Create(long? user = 301, string role = "FinanceStaff")
        {
            var f = new RefundApi();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton(f.Data.Service);
            builder.Services.AddScoped<RejectedOrderRefundInputFilter>();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ICurrentActor, CurrentActor>();
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddControllers().AddApplicationPart(typeof(FinanceRejectedOrderRefundController).Assembly);
            f.host = builder.Build();
            f.host.UseMiddleware<FeatureExceptionMiddleware>();
            f.host.UseAuthentication();
            f.host.UseAuthorization();
            f.host.MapControllers();
            await f.host.StartAsync();
            f.Client = f.host.GetTestClient();
            if (user.HasValue) f.Client.DefaultRequestHeaders.Add("X-Test-User", $"{user}:{role}");
            return f;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await host.DisposeAsync();
        }
    }

    [Fact, Trait("CaseId", "IT-FIN-09-001")]
    public async Task AnonymousRequest_Returns401Unauthorized()
    {
        await using var api = await RefundApi.Create(null);
        var res = await api.Client.GetAsync("api/finance/rejected-order-refunds");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory, Trait("CaseId", "IT-FIN-09-002")]
    [InlineData("Farmer")]
    [InlineData("RetailCustomer")]
    public async Task WrongRole_Returns403Forbidden(string role)
    {
        await using var api = await RefundApi.Create(101, role);
        var res = await api.Client.GetAsync("api/finance/rejected-order-refunds");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact, Trait("CaseId", "IT-FIN-09-003")]
    public async Task List_ValidQuery_Returns200WithPagedItems()
    {
        await using var api = await RefundApi.Create();
        var res = await api.Client.GetAsync("api/finance/rejected-order-refunds?status=Pending&page=1");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("RJR202610270001", body);
    }

    [Fact, Trait("CaseId", "IT-FIN-09-004")]
    public async Task List_InvalidDateRangeFromAfterTo_Returns400Validation()
    {
        await using var api = await RefundApi.Create();
        var res = await api.Client.GetAsync("api/finance/rejected-order-refunds?from=2026-10-28&to=2026-10-27");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("VALIDATION", body);
    }

    [Fact, Trait("CaseId", "IT-FIN-09-005")]
    public async Task List_InvalidStatusOrMalformedDate_Returns400()
    {
        await using var api = await RefundApi.Create();
        var badStatus = await api.Client.GetAsync("api/finance/rejected-order-refunds?status=BadStatus");
        Assert.Equal(HttpStatusCode.BadRequest, badStatus.StatusCode);

        var badDate = await api.Client.GetAsync("api/finance/rejected-order-refunds?from=not-a-date");
        Assert.Equal(HttpStatusCode.BadRequest, badDate.StatusCode);
    }

    [Fact, Trait("CaseId", "IT-FIN-09-006")]
    public async Task Detail_ExistingId_Returns200AndMissingIdReturns404()
    {
        await using var api = await RefundApi.Create();
        var ok = await api.Client.GetAsync("api/finance/rejected-order-refunds/801");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Contains("SO-501", await ok.Content.ReadAsStringAsync());

        var missing = await api.Client.GetAsync("api/finance/rejected-order-refunds/9999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact, Trait("CaseId", "IT-FIN-09-007")]
    public async Task Preview_EligibleRequest_Returns200WithQrPlan()
    {
        await using var api = await RefundApi.Create();
        var res = await api.Client.GetAsync("api/finance/rejected-order-refunds/801/qr-preview");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var preview = await res.Content.ReadFromJsonAsync<RejectedOrderRefundPreview>();
        Assert.NotNull(preview);
        Assert.Equal(150000, preview!.Plan.Amount);
        Assert.Equal(64, preview.ExpectedVersion.Length);
    }

    [Fact, Trait("CaseId", "IT-FIN-09-008")]
    public async Task Preview_IneligibleOrGatewayUnavailable_ReturnsUnavailableAndConflict()
    {
        await using var api = await RefundApi.Create();
        api.Data.Gateway.ReadAccess = false;
        var unavailable = await api.Client.GetAsync("api/finance/rejected-order-refunds/801/qr-preview");
        Assert.Equal(HttpStatusCode.BadRequest, unavailable.StatusCode);
        Assert.Contains("UNAVAILABLE", await unavailable.Content.ReadAsStringAsync());

        api.Data.Gateway.ReadAccess = true;
        api.Data.Account.VerificationStatus = "Pending";
        var conflict = await api.Client.GetAsync("api/finance/rejected-order-refunds/801/qr-preview");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact, Trait("CaseId", "IT-FIN-09-009")]
    public async Task OpenQr_ValidRequest_Returns200Processing()
    {
        await using var api = await RefundApi.Create();
        var preview = await (await api.Client.GetAsync("api/finance/rejected-order-refunds/801/qr-preview"))
            .Content.ReadFromJsonAsync<RejectedOrderRefundPreview>();
        var openRes = await api.Client.PostAsJsonAsync("api/finance/rejected-order-refunds/801/open-qr",
            new OpenRejectedOrderRefundRequest(preview!.ExpectedVersion, Guid.NewGuid().ToString("D")));
        Assert.Equal(HttpStatusCode.OK, openRes.StatusCode);
        var result = await openRes.Content.ReadFromJsonAsync<RejectedOrderRefundResult>();
        Assert.Equal("Processing", result!.Status);
    }

    [Fact, Trait("CaseId", "IT-FIN-09-010")]
    public async Task OpenQr_InvalidPayloadOrStaleVersion_Returns400And409()
    {
        await using var api = await RefundApi.Create();
        var bad = await api.Client.PostAsJsonAsync("api/finance/rejected-order-refunds/801/open-qr",
            new OpenRejectedOrderRefundRequest("short", "bad-guid"));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var stale = await api.Client.PostAsJsonAsync("api/finance/rejected-order-refunds/801/open-qr",
            new OpenRejectedOrderRefundRequest(new string('A', 64), Guid.NewGuid().ToString("D")));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact, Trait("CaseId", "IT-FIN-09-011")]
    public async Task Reconcile_FullEndToEndHappyPath_ReturnsSucceededAndQueuesNotifications()
    {
        await using var api = await RefundApi.Create();
        var plan = await api.Data.OpenQrSuccessfullyAsync();
        api.Data.Gateway.Lookup = new([api.Data.MatchingTransfer(plan)], true, null);

        var res = await api.Client.PostAsync("api/finance/rejected-order-refunds/801/reconcile", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var result = await res.Content.ReadFromJsonAsync<RejectedOrderRefundResult>();
        Assert.Equal("Succeeded", result!.Status);
        Assert.Equal(2, api.Data.Repo.Notifications.Count);
        Assert.Equal("Cancelled", api.Data.SellerOrder.Status);
    }

    [Fact, Trait("CaseId", "IT-FIN-09-012")]
    public async Task Reconcile_NoDebitYetOrConflictingDebit_ReturnsProcessingAndNeedsReconciliation()
    {
        await using var api = await RefundApi.Create();
        var plan = await api.Data.OpenQrSuccessfullyAsync();
        api.Data.Gateway.Lookup = new([], true, null);
        var pending = await (await api.Client.PostAsync("api/finance/rejected-order-refunds/801/reconcile", null))
            .Content.ReadFromJsonAsync<RejectedOrderRefundResult>();
        Assert.Equal("Processing", pending!.Status);

        api.Data.Gateway.Lookup = new([api.Data.MatchingTransfer(plan, amount: 120000)], true, null);
        var conflict = await (await api.Client.PostAsync("api/finance/rejected-order-refunds/801/reconcile", null))
            .Content.ReadFromJsonAsync<RejectedOrderRefundResult>();
        Assert.Equal("NeedsReconciliation", conflict!.Status);
    }

    [Fact, Trait("CaseId", "IT-FIN-09-013")]
    public async Task Reconcile_BeforeQrOpened_Returns409Conflict()
    {
        await using var api = await RefundApi.Create();
        var res = await api.Client.PostAsync("api/finance/rejected-order-refunds/801/reconcile", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact, Trait("CaseId", "IT-FIN-09-014")]
    public async Task Reconcile_PersistenceException_Returns500WithoutRawMsgCode()
    {
        await using var api = await RefundApi.Create();
        var plan = await api.Data.OpenQrSuccessfullyAsync();
        api.Data.Gateway.Lookup = new([api.Data.MatchingTransfer(plan)], true, null);
        api.Data.Repo.ThrowOnAtomic = true;

        var res = await api.Client.PostAsync("api/finance/rejected-order-refunds/801/reconcile", null);
        Assert.Equal(HttpStatusCode.InternalServerError, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.DoesNotMatch(@"MSG\d+", body);
    }
}
