using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Domain.Entities.Ordering;
using AgriSustain.Domain.ValueObjects;
using Xunit;

namespace AgriSustain.IntegrationTests;

public sealed class CheckoutTests
{
    /// <summary>
    /// Builds a valid buyer input using real catalog codes; optional voucher/points exercise
    /// benefit scenarios without relying on external modules.
    /// </summary>
    internal static CheckoutInput Input(string? voucher = null, int points = 0) => new(
        new AddressSnapshot("Khách demo", "0901234567", "Thành phố Hồ Chí Minh",
            "Xã Thạnh An", "123 Đường Mẫu", "12", "268"), VoucherCode: voucher, Points: points);

    /// <summary>
    /// Obtains and deserializes a real application quote through HTTP, failing immediately on a
    /// rejected response instead of fabricating a valid quote.
    /// </summary>
    internal static async Task<CheckoutQuote> QuoteAsync(
        HttpClient client, CheckoutInput? input = null)
    {
        var response = await client.PostAsJsonAsync("/api/checkout/quotes", input ?? Input());
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CheckoutQuote>()
            ?? throw new InvalidOperationException("Missing quote response.");
    }

    /// <summary>
    /// Uses an isolated fixture to verify that the provisional demo key cannot be reused for a
    /// different confirmed intent. This is not evidence of real JWT/payment integration or
    /// durable multi-process SQL behavior.
    /// </summary>
    [Fact]
    public async Task Place_ReusedKeyForDifferentSummary_RejectsSecondOrder()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client("retail-customer");
        var first = await QuoteAsync(client, Input() with
        {
            Notes = "First intent"
        });
        var second = await QuoteAsync(client, Input() with
        {
            Notes = "Different intent"
        });
        Guid key = Guid.NewGuid();
        var created = await client.PostAsJsonAsync("/api/checkout/orders",
            new PlaceOrderRequest(first.Id, key, true));
        var rejected = await client.PostAsJsonAsync("/api/checkout/orders",
            new PlaceOrderRequest(second.Id, key, true));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Contains("REQUEST_KEY_REUSED", await rejected.Content.ReadAsStringAsync());
        Assert.Single((await factory.Store.SnapshotAsync()).Orders);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that successful placement reserves inventory/benefits
    /// and removes only selected cart rows. This is not evidence of real JWT/payment integration
    /// or durable multi-process SQL behavior.
    /// </summary>
    [Fact]
    public async Task Place_ValidSummary_ReservesStockBenefitsAndOnlySelectedItems()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client("retail-customer");
        var quote = await QuoteAsync(client, Input("WELCOME10", 100));
        Assert.Equal(205000, quote.Subtotal);
        Assert.Equal(40000, quote.DeliveryFee);
        Assert.Equal(20500, quote.VoucherDiscount);
        Assert.Equal(10000, quote.PointsDiscount);
        Assert.Equal(214500, quote.Total);
        var response = await client.PostAsJsonAsync("/api/checkout/orders",
            new PlaceOrderRequest(quote.Id, Guid.NewGuid(), true));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var snapshot = await factory.Store.SnapshotAsync();
        var order = Assert.Single(snapshot.Orders.Values);
        Assert.Equal(order.Total, order.Sellers.Sum(seller => seller.FinalAmount));
        Assert.Equal(15, snapshot.Batches[201].AvailableQuantity);
        Assert.Equal(5, snapshot.Batches[201].ReservedQuantity);
        Assert.Equal(900, snapshot.Points[10]);
        Assert.Equal(1, snapshot.Vouchers[1].ReservedCount);
        Assert.False(Assert.Single(snapshot.Carts[10]).Selected);
        Assert.Single(snapshot.Audits);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that the demo retry path reserves and audits once even
    /// when requests overlap. This is not evidence of real JWT/payment integration or durable
    /// multi-process SQL behavior.
    /// </summary>
    [Fact]
    public async Task Place_ConcurrentDuplicateRequests_CreatesOneOrderAndOneReservation()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client("retail-customer");
        var quote = await QuoteAsync(client);
        var request = new PlaceOrderRequest(quote.Id, Guid.NewGuid(), true);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            client.PostAsJsonAsync("/api/checkout/orders", request)));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Equal(7, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        var snapshot = await factory.Store.SnapshotAsync();
        Assert.Single(snapshot.Orders);
        Assert.Equal(5, snapshot.Batches[201].ReservedQuantity);
        Assert.Single(snapshot.Audits);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that only one competing buyer reserves the last shared
    /// stock. This is not evidence of real JWT/payment integration or durable multi-process SQL
    /// behavior.
    /// </summary>
    [Fact]
    public async Task Place_CompetingBuyersForLastStock_OnlyOneBuyerWins()
    {
        using var factory = new ApiFactory(state => state.Batches[201] = new HarvestBatch(5)
        {
            Id = 201,
            ProductId = 101,
            Code = "LAST-STOCK"
        });
        using var first = factory.Client("retail-customer");
        using var second = factory.Client("other-retail-customer");
        var firstQuote = await QuoteAsync(first);
        var secondQuote = await QuoteAsync(second);
        var responses = await Task.WhenAll(
            first.PostAsJsonAsync("/api/checkout/orders", new PlaceOrderRequest(
                firstQuote.Id, Guid.NewGuid(), true)),
            second.PostAsJsonAsync("/api/checkout/orders", new PlaceOrderRequest(
                secondQuote.Id, Guid.NewGuid(), true)));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        var snapshot = await factory.Store.SnapshotAsync();
        Assert.Single(snapshot.Orders);
        Assert.Equal(0, snapshot.Batches[201].AvailableQuantity);
        Assert.Equal(5, snapshot.Batches[201].ReservedQuantity);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that an expired, unconsumed quote cannot create an
    /// order. This is not evidence of real JWT/payment integration or durable multi-process SQL
    /// behavior.
    /// </summary>
    [Fact]
    public async Task Place_ExpiredQuote_RequiresNewConfirmation()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client("retail-customer");
        var quote = await QuoteAsync(client);
        factory.Clock.Advance(TimeSpan.FromMinutes(6));
        var response = await client.PostAsJsonAsync("/api/checkout/orders",
            new PlaceOrderRequest(quote.Id, Guid.NewGuid(), true));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty((await factory.Store.SnapshotAsync()).Orders);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that quote ownership prevents another buyer placing the
    /// order. This is not evidence of real JWT/payment integration or durable multi-process SQL
    /// behavior.
    /// </summary>
    [Fact]
    public async Task Place_OtherBuyersQuote_RejectsWithoutSideEffects()
    {
        using var factory = new ApiFactory();
        using var owner = factory.Client("retail-customer");
        using var other = factory.Client("other-retail-customer");
        var quote = await QuoteAsync(owner);
        var response = await other.PostAsJsonAsync("/api/checkout/orders",
            new PlaceOrderRequest(quote.Id, Guid.NewGuid(), true));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty((await factory.Store.SnapshotAsync()).Orders);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that an invalid voucher is removed with feedback while
    /// a valid undiscounted quote remains available. This is not evidence of real JWT/payment
    /// integration or durable multi-process SQL behavior.
    /// </summary>
    [Fact]
    public async Task Preview_InvalidVoucher_RemovesDiscountWithWarning()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client("retail-customer");
        var quote = await QuoteAsync(client, Input("NOT-A-VOUCHER"));
        Assert.Equal("MSG116", quote.WarningCode);
        Assert.Null(quote.Input.VoucherCode);
        Assert.Equal(0, quote.VoucherDiscount);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that changed prices require a new quote and do not
    /// create a stale-price order. This is not evidence of real JWT/payment integration or
    /// durable multi-process SQL behavior.
    /// </summary>
    [Fact]
    public async Task Place_PriceChangesAfterQuote_RejectsAcceptedOldPrice()
    {
        bool changed = false;
        using var factory = new ApiFactory(decorateSession: session =>
            new ScenarioSession(session, changePrice: changed));
        using var client = factory.Client("retail-customer");
        var quote = await QuoteAsync(client);
        changed = true;
        var response = await client.PostAsJsonAsync("/api/checkout/orders",
            new PlaceOrderRequest(quote.Id, Guid.NewGuid(), true));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("MSG118", await response.Content.ReadAsStringAsync());
        Assert.Empty((await factory.Store.SnapshotAsync()).Orders);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that stock, order, benefits, selected cart and audit
    /// all roll back together. This is not evidence of real JWT/payment integration or durable
    /// multi-process SQL behavior.
    /// </summary>
    [Fact]
    public async Task Place_AuditFailure_RollsBackAllPlacementEffects()
    {
        bool failAudit = false;
        using var factory = new ApiFactory(decorateSession: session =>
            new ScenarioSession(session, failAudit: failAudit));
        using var client = factory.Client("retail-customer");
        var quote = await QuoteAsync(client, Input("WELCOME10", 100));
        failAudit = true;
        var response = await client.PostAsJsonAsync("/api/checkout/orders",
            new PlaceOrderRequest(quote.Id, Guid.NewGuid(), true));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var snapshot = await factory.Store.SnapshotAsync();
        Assert.Empty(snapshot.Orders);
        Assert.Empty(snapshot.Audits);
        Assert.Equal(20, snapshot.Batches[201].AvailableQuantity);
        Assert.Equal(0, snapshot.Batches[201].ReservedQuantity);
        Assert.Equal(1000, snapshot.Points[10]);
        Assert.Equal(0, snapshot.Vouchers[1].ReservedCount);
        Assert.Equal(3, snapshot.Carts[10].Count);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that a seller voucher does not reduce another seller
    /// payable amount. This is not evidence of real JWT/payment integration or durable
    /// multi-process SQL behavior.
    /// </summary>
    [Fact]
    public async Task Place_SellerVoucher_AllocatesDiscountOnlyToEligibleSeller()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client("retail-customer");
        var quote = await QuoteAsync(client, Input("RAUXANH20"));
        var response = await client.PostAsJsonAsync("/api/checkout/orders",
            new PlaceOrderRequest(quote.Id, Guid.NewGuid(), true));
        response.EnsureSuccessStatusCode();
        var order = Assert.Single((await factory.Store.SnapshotAsync()).Orders.Values);
        Assert.Equal(20000, order.Sellers.Single(seller => seller.SellerId == 20).DiscountShare);
        Assert.Equal(0, order.Sellers.Single(seller => seller.SellerId == 21).DiscountShare);
    }
    /// <summary>
    /// Uses an isolated fixture to verify that one product line spans lots in FEFO order,
    /// preserves its tier price and reserves each lot accurately. This is not evidence of real
    /// JWT/payment integration or durable multi-process SQL behavior.
    /// </summary>
    [Fact]
    public async Task Preview_FefoAcrossTwoBatches_ReservesEachAllocation()
    {
        using var factory = new ApiFactory(state =>
        {
            state.Batches[201] = new HarvestBatch(3)
            {
                Id = 201,
                ProductId = 101,
                Code = "FIRST",
                ExpirationDate = new(2026, 11, 1)
            };
            state.Batches[204] = new HarvestBatch(20)
            {
                Id = 204,
                ProductId = 101,
                Code = "SECOND",
                ExpirationDate = new(2026, 11, 2)
            };
        });
        using var client = factory.Client("retail-customer");
        var quote = await QuoteAsync(client);
        var line = quote.Lines.Single(line => line.ProductId == 101);
        Assert.Collection(line.Allocations,
            first => { Assert.Equal(201, first.BatchId); Assert.Equal(3, first.Quantity); },
            second => { Assert.Equal(204, second.BatchId); Assert.Equal(2, second.Quantity); });
        Assert.Equal(125000, line.Subtotal);
        (await client.PostAsJsonAsync("/api/checkout/orders",
            new PlaceOrderRequest(quote.Id, Guid.NewGuid(), true))).EnsureSuccessStatusCode();
        var snapshot = await factory.Store.SnapshotAsync();
        Assert.Equal(0, snapshot.Batches[201].AvailableQuantity);
        Assert.Equal(3, snapshot.Batches[201].ReservedQuantity);
        Assert.Equal(18, snapshot.Batches[204].AvailableQuantity);
        Assert.Equal(2, snapshot.Batches[204].ReservedQuantity);
        Assert.Equal(2, Assert.Single(snapshot.Orders.Values).Lines[0].Allocations.Count);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that two cart lines cannot allocate more of one batch
    /// than its shared available stock. This is not evidence of real JWT/payment integration or
    /// durable multi-process SQL behavior.
    /// </summary>
    [Fact]
    public async Task Preview_DuplicateProductLines_CannotAllocateTheSameStockTwice()
    {
        using var factory = new ApiFactory(state =>
        {
            state.Carts[10] = [new(1, 101, 3, true), new(2, 101, 3, true)];
            state.Batches[201] = new HarvestBatch(5) { Id = 201, ProductId = 101, Code = "FIVE" };
        });
        using var client = factory.Client("retail-customer");
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync("/api/checkout/quotes", Input())).StatusCode);
        Assert.Empty((await factory.Store.SnapshotAsync()).Orders);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that mismatched geographic parents fail and client
    /// names cannot overwrite catalog snapshots. This is not evidence of real JWT/payment
    /// integration or durable multi-process SQL behavior.
    /// </summary>
    [Fact]
    public async Task Preview_AddressCodes_RejectWrongParentAndReplaceClientNames()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client("retail-customer");
        var invalid = Input() with
        {
            Address = Input().Address with
            {
                WardCode = "267"
            }
        };
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/checkout/quotes", invalid)).StatusCode);
        var quote = await QuoteAsync(client, Input() with
        {
            Address = Input().Address with
            {
                Province = "Tampered",
                Ward = "Tampered"
            }
        });
        Assert.Equal("Thành phố Hồ Chí Minh", quote.Input.Address.Province);
        Assert.Equal("Xã Thạnh An", quote.Input.Address.Ward);
        Assert.Equal(64, quote.Input.Address.CatalogVersion.Length);
        var provinces = await client.GetFromJsonAsync<ProvinceOption[]>("/api/checkout/provinces");
        Assert.Equal(34, provinces?.Length);
    }
}
