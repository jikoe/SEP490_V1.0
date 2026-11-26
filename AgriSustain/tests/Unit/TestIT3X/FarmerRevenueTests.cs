using AgriSustain.Api.Middlewares;
using AgriSustain.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Compression;
using System.Text;
using AgriSustain.Application.DTOs.Revenue;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using Xunit;

namespace AgriSustain.UnitTests.TestIT3X;

public sealed class FarmerRevenueTests
{
    private static readonly DateOnly Day = new(2025, 11, 8);
    private static readonly DateTime Midnight = Day.ToDateTime(TimeOnly.MinValue).AddHours(-7);
    private static RevenueFilterRequest Filter() => new() { From = Day, To = Day };

    [Fact]
    public async Task UT_FAR_09_001_SameDayFiltersAndTotalsIgnoreDuplicates()
    {
        var row = Row("sale", Midnight);
        var repository = new RevenueRepository([
            row, row,
            Row("outside", Midnight.AddDays(1)),
            Row("other-season", Midnight) with { SeasonId = 8 },
            Row("other-channel", Midnight) with { Channel = "Wholesale" },
            Row("other-stage", Midnight) with { Stage = "Deposit" },
            Row("other-state", Midnight) with { State = "Held" }
        ]);
        var filter = Filter();
        filter.Season = 7;
        filter.Channel = "Retail";
        filter.PaymentStage = "Goods";
        filter.SettlementState = "Verified";
        var result = await new FarmerRevenueService(repository, new()).GetRevenueAsync(10, filter, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(row, Assert.Single(result.Value!.Items));
        Assert.Equal(1000, result.Value.Totals.Sales);
        Assert.Equal(900, result.Value.Totals.Receipts);
        Assert.Equal(Midnight.AddDays(1), repository.AsOf);
    }

    [Fact]
    public async Task UT_FAR_09_002_PaginationDoesNotChangeSummary()
    {
        var rows = Enumerable.Range(1, 11).Select(i => Row($"sale-{i}", Midnight.AddMinutes(i)));
        var service = new FarmerRevenueService(new RevenueRepository(rows.ToArray()), new());
        var filter = Filter();
        filter.Page = 2;
        var report = (await service.GetRevenueAsync(10, filter, default)).Value!;
        Assert.Equal("sale-1", Assert.Single(report.Items).Id);
        Assert.Equal(11, report.Total);
        Assert.Equal(11000, report.Totals.Sales);
    }

    [Fact]
    public async Task UT_FAR_09_003_EmptyRevenueAndDefaultPeriod()
    {
        var service = new FarmerRevenueService(new RevenueRepository([]), new());
        var report = (await service.GetRevenueAsync(10, new(), default)).Value!;
        Assert.Empty(report.Items);
        Assert.Equal(0, report.Totals.Sales);
        Assert.Equal(29, report.Filters.To!.Value.DayNumber - report.Filters.From!.Value.DayNumber);
        Assert.False(string.IsNullOrWhiteSpace(report.Message));
    }

    [Fact]
    public async Task UT_FAR_09_004_InvalidQueryAndForeignAccessDoNotReadFinancialData()
    {
        var repository = new RevenueRepository([]);
        var service = new FarmerRevenueService(repository, new());
        var filter = Filter();
        filter.From = Day.AddDays(1);
        Assert.Equal("MSG111", (await service.GetRevenueAsync(10, filter, default)).Error?.Code);
        filter.From = Day;
        filter.Channel = "PreOrder";
        Assert.Equal("MSG120", (await service.GetRevenueAsync(10, filter, default)).Error?.Code);
        Assert.Equal("FORBIDDEN",
            (await service.GetTransactionDetailAsync(99, "sale", default)).Error?.Code);
        Assert.Null(repository.AsOf);
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("xlsx")]
    public async Task UT_FAR_09_005_ExportsOnlyTheFilteredRows(string format)
    {
        var service = new FarmerRevenueService(new RevenueRepository([
            Row("sale-in", Midnight), Row("sale-out", Midnight.AddDays(1))
        ]), new());
        var export = (await service.ExportRevenueAsync(10, Filter(), format, default)).Value!;
        Assert.EndsWith("." + format, export.FileName);
        if (format == "csv")
        {
            Check(Encoding.UTF8.GetString(export.Content));
        }
        else
        {
            using var archive = new ZipArchive(new MemoryStream(export.Content), ZipArchiveMode.Read);
            using var reader = new StreamReader(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open());
            Check(await reader.ReadToEndAsync());
        }

        static void Check(string content)
        {
            Assert.Contains("sale-in", content);
            Assert.DoesNotContain("sale-out", content);
            Assert.Contains("VND", content);
        }
    }

    [Fact]
    public async Task UT_FAR_09_006_DetailPreservesHoldAndTransferReferences()
    {
        var item = Row("held", Midnight) with { HoldReason = "Pending dispute", TransferReference = "BANK-REF" };
        var service = new FarmerRevenueService(new RevenueRepository([item]), new());
        var detail = await service.GetTransactionDetailAsync(10, item.Id, default);
        Assert.Equal(item, detail.Value);
        Assert.Equal("FORBIDDEN",
            (await service.GetTransactionDetailAsync(10, "missing", default)).Error?.Code);
        Assert.Equal("MSG120",
            (await service.ExportRevenueAsync(10, Filter(), "pdf", default)).Error?.Code);
    }

    [Theory]
    [InlineData("Retail")]
    [InlineData("Wholesale")]
    [InlineData("Contract")]
    public async Task UT_FAR_09_009_ChannelsStaySeparateInReports(string channel)
    {
        var service = new FarmerRevenueService(new RevenueRepository([
            Row("Retail", Midnight),
            Row("Wholesale", Midnight) with { Channel = "Wholesale", Sales = 2000 },
            Row("Contract", Midnight) with { Channel = "Contract", Sales = 3000 }
        ]), new());
        var filter = Filter();
        filter.Channel = channel;
        var result = await service.GetRevenueAsync(10, filter, default);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(channel, Assert.Single(result.Value!.Items).Id);
        Assert.Equal(result.Value.Items[0].Sales, result.Value.Totals.Sales);
    }

    [Theory]
    [InlineData("summary", "MSG126")]
    [InlineData("export", "MSG125")]
    public async Task FailedRevenueReadUsesTheCorrectErrorMessage(string operation, string code)
    {
        var repo = new RevenueRepository([]) { ReadFailure = new IOException("Fixture read failed") };
        var service = new FarmerRevenueService(repo, new());
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = operation == "export" ? "/api/farmer/revenue/export" : "/api/farmer/revenue";
        await using var response = new MemoryStream();
        context.Response.Body = response;
        var middleware = new FeatureExceptionMiddleware(async _ =>
        {
            RevenueFilterRequest filter = new();
            if (operation == "export")
                await service.ExportRevenueAsync(10, filter, "xlsx", default);
            else
                await service.GetRevenueAsync(10, filter, default);
        }, NullLogger<FeatureExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);
        Assert.NotNull(repo.AsOf);
        response.Position = 0;
        using var problem = await System.Text.Json.JsonDocument.ParseAsync(response);
        Assert.Equal(FarmerBusinessMessages.Text(code), problem.RootElement.GetProperty("title").GetString());
        Assert.Equal("SERVER_ERROR", problem.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("PreOrder", null, null)]
    [InlineData(null, "Invalid stage", null)]
    [InlineData(null, null, "Invalid state")]
    public async Task InvalidBusinessFilterDoesNotQueryLedger(string? channel, string? stage, string? state)
    {
        var repo = new RevenueRepository([Row("existing", Midnight)]);
        var service = new FarmerRevenueService(repo, new());
        var filter = new RevenueFilterRequest { Channel = channel, PaymentStage = stage, SettlementState = state };

        var result = await service.GetRevenueAsync(10, filter, default);

        Assert.Equal("MSG120", result.Error?.Code);
        Assert.Null(repo.AsOf);
    }

    private static FarmerRevenueEvent Row(string id, DateTime at) =>
        new(id, at, id, "Sale", "Retail", 7, "Season", "Goods", "Verified", Sales: 1000, Receipts: 900);

    private sealed class RevenueRepository(IReadOnlyList<FarmerRevenueEvent> rows) : IFarmerRevenueRepository
    {
        public Exception? ReadFailure { get; init; }
        public DateTime? AsOf { get; private set; }

        public Task<bool> CanReadAsync(long actor, CancellationToken ct) => Task.FromResult(actor == 10);

        public Task<IReadOnlyList<FarmerRevenueEvent>> ReadFinancialEventsAsync(
            long actor, DateTime asOf, CancellationToken ct)
        {
            AsOf = asOf;
            return ReadFailure == null ? Task.FromResult(rows) : Task.FromException<IReadOnlyList<FarmerRevenueEvent>>(ReadFailure);
        }

        public Task<object> SeasonsAsync(long actor, CancellationToken ct) =>
            Task.FromResult<object>(Array.Empty<object>());
    }
}

