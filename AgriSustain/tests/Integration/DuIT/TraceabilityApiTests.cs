using System.Net;
using AgriSustain.IntegrationTests.Commerce;
using AgriSustain.IntegrationTests.UseCaseApi;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.DuIT;

/// <summary>UC-TRC-01 View Product Traceability (anonymous QR / batch-code lookup) against SQL Server.</summary>
[Trait("Layer", "SqlIntegration"), Trait("UseCase", "UC-TRC-01")]
public sealed class TraceabilityApiTests : IAsyncLifetime
{
    private DuApiHost _api = null!;

    public async Task InitializeAsync() => _api = await DuApiHost.StartAsync();

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private Task<HttpResponseMessage> Lookup(string code) =>
        _api.Anonymous().GetAsync("api/traceability?code=" + Uri.EscapeDataString(code));

    private async Task<int> ScanCountAsync(string batchCode)
    {
        await using var db = _api.Open();
        return await db.TraceabilityQrcodes.Where(q => q.HarvestBatch.BatchCode == batchCode)
            .Select(q => q.ScanCount).SingleAsync();
    }

    [CommerceSqlFact(DisplayName = "[IT UC-TRC-01 #01] A guest sees origin, farm, cultivation log and valid certificate of a batch; the scan is counted")]
    public async Task Lookup_BatchCode_FullTraceAndScanCounted()
    {
        var json = await ApiAssert.Status(await Lookup("BATCH-DL-001"), HttpStatusCode.OK);

        Assert.Equal(("Bơ 034", "Nông trại Bơ Lâm Đồng", false),
            (json.GetProperty("productName").GetString(), json.GetProperty("farmName").GetString(), json.GetProperty("isRevoked").GetBoolean()));
        var log = Assert.Single(json.GetProperty("cultivationLogs").EnumerateArray());
        Assert.Equal("Bón phân", log.GetProperty("actionType").GetString());
        Assert.Contains(json.GetProperty("certificates").EnumerateArray(), c => c.GetProperty("certificateType").GetString() == "VietGAP");
        Assert.Equal(1, await ScanCountAsync("BATCH-DL-001"));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-TRC-01 #02] The full URL scanned from the QR label finds the same batch")]
    public async Task Lookup_QrUrl_SameBatch()
    {
        await ApiAssert.Status(await Lookup("BATCH-DL-001"), HttpStatusCode.OK);

        var json = await ApiAssert.Status(await Lookup("https://agrisustain.vn/trace/BATCH-DL-001"), HttpStatusCode.OK);

        Assert.Equal("BATCH-DL-001", json.GetProperty("batchCode").GetString());
        Assert.Equal(2, await ScanCountAsync("BATCH-DL-001"));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-TRC-01 #03] A revoked batch shows the recall warning, hides the logs and is not counted")]
    public async Task Lookup_Revoked_RecallWarning()
    {
        var json = await ApiAssert.Status(await Lookup("BATCH-DL-002"), HttpStatusCode.OK);

        Assert.True(json.GetProperty("isRevoked").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("recallMessage").GetString()));
        Assert.Empty(json.GetProperty("cultivationLogs").EnumerateArray());
        Assert.Equal(0, await ScanCountAsync("BATCH-DL-002"));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-TRC-01 #04] An unknown code returns 404")]
    public async Task Lookup_Unknown_NotFound()
    {
        await ApiAssert.Problem(await Lookup("NOPE-123"), HttpStatusCode.NotFound, "TRACE_NOT_FOUND");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-TRC-01 #05] An empty code is refused without querying")]
    public async Task Lookup_Empty_InvalidCode()
    {
        await ApiAssert.Problem(await Lookup("   "), HttpStatusCode.BadRequest, "INVALID_CODE");
        Assert.Equal(0, await ScanCountAsync("BATCH-DL-001"));
    }
}
