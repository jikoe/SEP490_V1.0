using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.DTOs.Orders;
using Xunit;

namespace AgriSustain.IntegrationTests;

/// <summary>Exercises additional partitions, boundaries and state rules with isolated HTTP fixtures.</summary>
public sealed class DetailedThreeUcTests
{
    /// <summary>Rejects unsellable products, hidden ancestors and malformed cart quantities before saving a quote.</summary>
    [Theory]
    [InlineData("unpublished")]
    [InlineData("unapproved")]
    [InlineData("seller-ineligible")]
    [InlineData("seller-suspended")]
    [InlineData("category-hidden")]
    [InlineData("ancestor-hidden")]
    [InlineData("no-selection")]
    [InlineData("zero-quantity")]
    [InlineData("excess-precision")]
    [InlineData("insufficient-stock")]
    public async Task Quote_IneligibleOfferOrCart_RejectsWithoutReservation(string reason)
    {
        using var factory = new ApiFactory(state =>
        {
            switch (reason)
            {
                case "unpublished": state.Products[101] = state.Products[101] with { IsPublished = false }; break;
                case "unapproved": state.Products[101] = state.Products[101] with { IsApproved = false }; break;
                case "seller-ineligible": state.Products[101] = state.Products[101] with { IsSellerEligible = false }; break;
                case "seller-suspended": state.Accounts[20].Suspend("Test seller eligibility"); break;
                case "category-hidden": state.Categories[3].SetActive(false); break;
                case "ancestor-hidden": state.Categories[1].SetActive(false); break;
                case "no-selection": state.Carts[10] = [new(1, 101, 5, false)]; break;
                case "zero-quantity": state.Carts[10] = [new(1, 101, 0, true)]; break;
                case "excess-precision": state.Carts[10] = [new(1, 101, 5.0001m, true)]; break;
                case "insufficient-stock": state.Carts[10] = [new(1, 101, 20.001m, true)]; break;
            }
        });
        using var client = factory.Client("retail-customer");
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync("/api/checkout/quotes", CheckoutTests.Input())).StatusCode);
        var state = await factory.Store.SnapshotAsync();
        Assert.Empty(state.Quotes);
        Assert.Empty(state.Orders);
        Assert.Empty(state.Audits);
        Assert.Equal(0, state.Batches[201].ReservedQuantity);
    }

    /// <summary>Requires the cart preview and accepted quote to select the same independently expected price.</summary>
    [Theory]
    [InlineData(25000, 5, 4.999, 30000)]
    [InlineData(25000, 5, 5, 25000)]
    [InlineData(25000, 5, 5.001, 25000)]
    [InlineData(35000, 5, 5, 30000)]
    [InlineData(0, 5, 5, 30000)]
    [InlineData(-100, 5, 5, 30000)]
    [InlineData(25000, 0, 5, 30000)]
    [InlineData(25000, 1, 5, 30000)]
    [InlineData(30000, 5, 5, 30000)]
    public async Task ContextAndQuote_TierBoundaries_UseConsistentPrices(
        decimal tier, decimal minimum, decimal quantity, decimal expected)
    {
        using var factory = new ApiFactory(state =>
        {
            state.Products[101] = state.Products[101] with
            {
                WholesalePrice = tier, WholesaleMinQuantity = minimum
            };
            state.Carts[10] = [new(1, 101, quantity, true)];
        });
        using var client = factory.Client("retail-customer");
        var context = await client.GetFromJsonAsync<CommerceContext>("/api/checkout");
        var quote = await CheckoutTests.QuoteAsync(client);
        Assert.Equal(expected, Assert.Single(context!.SelectedItems!).UnitPrice);
        Assert.Equal(expected, Assert.Single(quote.Lines).UnitPrice);
        Assert.Empty((await factory.Store.SnapshotAsync()).Orders);
    }

    /// <summary>Rejects invalid pagination/status inputs before returning any account data.</summary>
    [Theory]
    [InlineData("page=0")]
    [InlineData("page=100001")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("status=Locked")]
    public async Task UserSearch_InvalidBoundaries_Returns400(string query)
    {
        using var factory = new ApiFactory();
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/users?" + query)).StatusCode);
        Assert.Empty((await factory.Store.SnapshotAsync()).Audits);
    }

    /// <summary>Checks exact role/status filtering and an empty page without treating it as a server error.</summary>
    [Fact]
    public async Task UserSearch_CombinedFilterAndEmptyPage_ReturnsOnlyMatchingRows()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client();
        var result = await client.GetFromJsonAsync<PagedResponse<UserResponse>>(
            "/api/users?role=RetailCustomer&status=Suspended");
        Assert.Single(result!.Items);
        Assert.Equal("Suspended", result.Items[0].Status);
        var empty = await client.GetFromJsonAsync<PagedResponse<UserResponse>>("/api/users?page=100000");
        Assert.Empty(empty!.Items);
        Assert.Equal(10, empty.Total);
    }

    /// <summary>An audit exception must roll back the account transition and local session revocation together.</summary>
    [Fact]
    public async Task AccountChange_AuditFailure_RestoresStatusAndSession()
    {
        using var factory = new ApiFactory(decorateSession: session => new ScenarioSession(session, failAudit: true));
        using var admin = factory.Client();
        using var buyer = factory.Client("retail-customer");
        Assert.Equal(HttpStatusCode.InternalServerError, (await admin.PostAsJsonAsync(
            "/api/users/10/status-actions", new AccountActionRequest("suspend", "Audit fault", true))).StatusCode);
        var state = await factory.Store.SnapshotAsync();
        Assert.Equal("Active", state.Accounts[10].Status);
        Assert.Equal(0, state.Accounts[10].SessionVersion);
        Assert.Empty(state.Audits);
        Assert.Equal(HttpStatusCode.OK, (await buyer.GetAsync("/api/checkout")).StatusCode);
    }

    /// <summary>Unlock removes temporary lock counters but never activates a suspended business account.</summary>
    [Fact]
    public async Task AccountUnlock_TemporarilyLocked_ClearsMetadataAndAudits()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/users/12/status-actions",
            new AccountActionRequest("unlock", null, true))).StatusCode);
        var state = await factory.Store.SnapshotAsync();
        Assert.Null(state.Accounts[12].LockedUntil);
        Assert.Equal(0, state.Accounts[12].FailedLoginCount);
        Assert.Equal("Active", state.Accounts[12].Status);
        Assert.Equal("unlock", Assert.Single(state.Audits).Action);
    }

    /// <summary>Rejects invalid category filter combinations without executing a write.</summary>
    [Theory]
    [InlineData("parentId=0")]
    [InlineData("id=0")]
    [InlineData("rootOnly=true&parentId=1")]
    [InlineData("pageSize=101")]
    public async Task CategorySearch_InvalidFilters_Returns400(string query)
    {
        using var factory = new ApiFactory();
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/categories?" + query)).StatusCode);
        Assert.Empty((await factory.Store.SnapshotAsync()).Audits);
    }

    /// <summary>Invalid/unconfirmed category writes preserve data and produce no success audit.</summary>
    [Theory]
    [InlineData("A", 0, null)]
    [InlineData("Valid category", -1, null)]
    [InlineData("Valid category", 0, 999999L)]
    public async Task CategoryCreate_InvalidFieldsOrParent_DoesNotPersist(string name, int priority, long? parent)
    {
        using var factory = new ApiFactory();
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/categories",
            new SaveCategoryRequest(name, parent, null, priority))).StatusCode);
        var state = await factory.Store.SnapshotAsync();
        Assert.Equal(4, state.Categories.Count);
        Assert.Empty(state.Audits);
    }

    /// <summary>Tests rejected checkout values independently while preserving cart, stock and benefits.</summary>
    [Theory]
    [InlineData("phone")]
    [InlineData("recipient")]
    [InlineData("detail")]
    [InlineData("delivery")]
    [InlineData("payment")]
    [InlineData("points")]
    [InlineData("notes")]
    public async Task CheckoutQuote_InvalidInput_HasNoPlacementSideEffects(string field)
    {
        using var factory = new ApiFactory();
        using var client = factory.Client("retail-customer");
        var input = CheckoutTests.Input();
        input = field switch
        {
            "phone" => input with { Address = input.Address with { Phone = "0401234567" } },
            "recipient" => input with { Address = input.Address with { RecipientName = "A" } },
            "detail" => input with { Address = input.Address with { Detail = "" } },
            "delivery" => input with { DeliveryMethod = "Drone" },
            "payment" => input with { PaymentMethod = "Cash" },
            "points" => input with { Points = -1 },
            _ => input with { Notes = new string('x', 301) }
        };
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/checkout/quotes", input)).StatusCode);
        var state = await factory.Store.SnapshotAsync();
        Assert.Empty(state.Quotes);
        Assert.Empty(state.Orders);
        Assert.Equal(3, state.Carts[10].Count);
        Assert.Equal(0, state.Batches[201].ReservedQuantity);
        Assert.Equal(1000, state.Points[10]);
    }

    /// <summary>Invalid or exhausted vouchers yield explicit feedback and zero discount without reserving quota.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("not-started")]
    [InlineData("quota")]
    [InlineData("inactive")]
    public async Task CheckoutVoucher_Ineligible_ReturnsZeroDiscountAndFeedback(string reason)
    {
        using var factory = new ApiFactory(state => state.Vouchers[1] = reason switch
        {
            "expired" => state.Vouchers[1] with { EndsAt = DateTimeOffset.Parse("2026-10-18T03:00:00Z") },
            "not-started" => state.Vouchers[1] with { StartsAt = DateTimeOffset.Parse("2026-10-19T03:00:00Z") },
            "quota" => state.Vouchers[1] with { UsageLimit = 1, ReservedCount = 1 },
            "inactive" => state.Vouchers[1] with { IsActive = false },
            _ => state.Vouchers[1]
        });
        using var client = factory.Client("retail-customer");
        var quote = await CheckoutTests.QuoteAsync(client, CheckoutTests.Input(reason == "missing" ? "MISSING" : "WELCOME10"));
        Assert.Equal(0, quote.VoucherDiscount);
        Assert.Null(quote.Input.VoucherCode);
        Assert.Equal(245000, quote.Total);
        Assert.Empty((await factory.Store.SnapshotAsync()).Orders);
    }

    /// <summary>A cancelled placement must leave the quote reusable and all placement side effects absent.</summary>
    [Fact]
    public async Task Place_Unconfirmed_PreservesQuoteAndAllState()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client("retail-customer");
        var quote = await CheckoutTests.QuoteAsync(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/checkout/orders",
            new PlaceOrderRequest(quote.Id, Guid.NewGuid(), false))).StatusCode);
        var state = await factory.Store.SnapshotAsync();
        Assert.Single(state.Quotes);
        Assert.Empty(state.Orders);
        Assert.Empty(state.Audits);
        Assert.Equal(0, state.Batches[201].ReservedQuantity);
        Assert.Equal(3, state.Carts[10].Count);
    }
}
