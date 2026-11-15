using AgriSustain.Application.DTOs.Offtake;
using Xunit;

namespace AgriSustain.UnitTests.Offtake;

[Trait("UseCase", "UC-WB-07")]
public sealed class OfftakeTrackingTests
{
    [Fact(DisplayName = "[UC-WB-07 #001] List the buyer's contracts with requested filters")]
    public async Task ContractsPassBuyerAndFiltersToRepository()
    {
        var row = new OfftakeTrackingRow { Id = 10, Code = "OFTC-10", Status = "PendingApproval" };
        var store = new OfftakeTestStore { TrackingPage = new([row], 21, 2, 10, [new("PendingApproval", 21)]) };
        var query = new OfftakeTrackingQuery("Farm", "PendingApproval", "Rice", Page: 2);

        var result = await store.Service().TrackAsync(false, query, default);

        Assert.Equal((store.UserId, false, query), store.TrackCall!.Value);
        Assert.True(result.Success);
        Assert.Equal(row, Assert.Single(result.Data!.Items));
        Assert.Equal(21, result.Data.Total);
        Assert.Equal(21, Assert.Single(result.Data.StatusCounts).Count);
    }

    [Fact(DisplayName = "[UC-WB-07 #002] List the buyer's B2B orders with current status")]
    public async Task OrdersPassBuyerAndOrderModeToRepository()
    {
        var row = new OfftakeTrackingRow { Id = 21, Code = "B2B-21", Status = "Shipping" };
        var store = new OfftakeTestStore { TrackingPage = new([row], 1, 1, 10, []) };
        var query = new OfftakeTrackingQuery();

        var result = await store.Service().TrackAsync(true, query, default);

        Assert.Equal((store.UserId, true, query), store.TrackCall!.Value);
        Assert.True(result.Success);
        Assert.Equal(row, Assert.Single(result.Data!.Items));
    }

    [Fact(DisplayName = "[UC-WB-07 #003] Return an empty page when the buyer has no matching records")]
    public async Task EmptyTrackingReturnsSuccessfulEmptyPage()
    {
        var store = new OfftakeTestStore();

        var result = await store.Service().TrackAsync(false, new(), default);

        Assert.True(result.Success);
        Assert.Empty(result.Data!.Items);
        Assert.Equal(0, result.Data.Total);
    }

    [Fact(DisplayName = "[UC-WB-07 #004] Accept page 1000000 and size 100")]
    public async Task TrackingAcceptsMaximumPagination()
    {
        var store = new OfftakeTestStore();
        var query = new OfftakeTrackingQuery(Page: 1000000, PageSize: 100);

        var result = await store.Service().TrackAsync(false, query, default);

        Assert.True(result.Success);
        Assert.Equal((store.UserId, false, query), store.TrackCall!.Value);
    }

    [Fact(DisplayName = "[UC-WB-07 #005] Reject pagination outside the limits before reading")]
    public async Task InvalidPaginationDoesNotQueryRepository()
    {
        var store = new OfftakeTestStore();
        var service = store.Service();

        Assert.Equal(400, (await service.TrackAsync(false, new(Page: 0), default)).StatusCode);
        Assert.Equal(400, (await service.TrackAsync(false, new(Page: 1000001), default)).StatusCode);
        Assert.Equal(400, (await service.TrackAsync(false, new(PageSize: 101), default)).StatusCode);
        Assert.Null(store.TrackCall);
    }

    [Fact(DisplayName = "[UC-WB-07 #006] Reject a reversed date filter before reading")]
    public async Task ReversedDatesDoNotQueryRepository()
    {
        var store = new OfftakeTestStore();
        var query = new OfftakeTrackingQuery(From: new DateOnly(2026, 11, 10), To: new DateOnly(2026, 11, 9));

        var result = await store.Service().TrackAsync(true, query, default);

        Assert.Equal(400, result.StatusCode);
        Assert.Null(store.TrackCall);
    }

    [Fact(DisplayName = "[UC-WB-07 #007] Return contract milestones and invoices through the buyer's read path")]
    public async Task ContractDetailReturnsMilestonesAndInvoices()
    {
        var milestone = new OfftakeDelivery("First delivery", new DateOnly(2026, 11, 20), 10, 4, 3, "Shipping", "Unpaid");
        var invoice = new OfftakeInvoice("INV-10", new DateTime(2026, 11, 9), null, 100000, "Unpaid");
        var store = new OfftakeTestStore
        {
            TrackingDetail = new("OFTC-10", "InExecution", "Farm", 1, null, null, null, null, null,
                [], [milestone], [], [invoice], [])
        };

        var result = await store.Service().DetailAsync(false, 10, default);

        Assert.Equal((store.UserId, false, 10L), store.DetailCall!.Value);
        Assert.True(result.Success);
        Assert.Equal(milestone, Assert.Single(result.Data!.Milestones));
        Assert.Equal(invoice, Assert.Single(result.Data.Invoices));
        Assert.Equal(0, store.Commits);
    }

    [Fact(DisplayName = "[UC-WB-07 #008] Return order shipment information through the buyer's read path")]
    public async Task OrderDetailReturnsShipments()
    {
        var shipment = new OfftakeShipment("SHIP-21", null, new DateTime(2026, 11, 9), null, "Shipping");
        var store = new OfftakeTestStore
        {
            TrackingDetail = new("B2B-21", "Shipping", "Farm", null, null, null, null, null, null,
                [], [], [shipment], [], [])
        };

        var result = await store.Service().DetailAsync(true, 21, default);

        Assert.Equal((store.UserId, true, 21L), store.DetailCall!.Value);
        Assert.True(result.Success);
        Assert.Equal(shipment, Assert.Single(result.Data!.Shipments));
        Assert.Equal(0, store.Commits);
    }

    [Fact(DisplayName = "[UC-WB-07 #009] Return MSG54 when the buyer-scoped detail is unavailable")]
    public async Task UnavailableDetailReturnsNotFound()
    {
        var store = new OfftakeTestStore();

        var result = await store.Service().DetailAsync(false, 99, default);

        Assert.Equal((store.UserId, false, 99L), store.DetailCall!.Value);
        Assert.Equal(404, result.StatusCode);
        Assert.StartsWith("MSG54", result.Message);
        Assert.Null(result.Data);
    }

    [Fact(DisplayName = "[UC-WB-07 #010] Return 404 for an invalid ID without reading")]
    public async Task NonPositiveIdDoesNotQueryRepository()
    {
        var store = new OfftakeTestStore();

        var result = await store.Service().DetailAsync(true, 0, default);

        Assert.Equal(404, result.StatusCode);
        Assert.Null(store.DetailCall);
    }
}
