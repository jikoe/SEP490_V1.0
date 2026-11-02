using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Checkout;
using AgriSustain.Application.Validators.Checkout;
using AgriSustain.Domain.Entities;
using AgriSustain.UnitTests.Administration;
using AgriSustain.UnitTests.DuIte1;
using Xunit;

namespace AgriSustain.UnitTests.Checkout;

/// <summary>UC-RC-02 Place Retail Order: CheckoutValidator, QuoteAsync and PlaceAsync with in-memory doubles.</summary>
public sealed class CheckoutServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 30, 3, 0, 0, TimeSpan.Zero);
    private const long BuyerId = 2;

    private readonly FakeClock _clock = new(T0);
    private readonly CheckoutStore _store = new();
    private readonly AdministrationServiceTests.AdminStore _accounts = new();
    private readonly Transactions _uow = new();
    private readonly Catalog _catalog = new();
    private readonly Shipping _shipping = new();
    private readonly CheckoutPolicy _policy = new() { PointValueVnd = 1, MaximumPointDiscountPercent = 50 };
    private readonly Product _orange;
    private readonly HarvestBatch _batch;

    public CheckoutServiceTests()
    {
        _accounts.Users[BuyerId] = new User { UserId = BuyerId, Role = "RetailCustomer", Status = "Active" };
        _accounts.Users[3] = new User { UserId = 3, Role = "Farmer", Status = "Active" };
        var today = DateOnly.FromDateTime(T0.UtcDateTime);
        _orange = new Product
        {
            ProductId = 20, ProductName = "Cam sành", Unit = "kg", BaseRetailPrice = 40_000m,
            ApprovalStatus = "Approved", IsPublished = true, CategoryId = 1, FarmerProfileId = 7,
            Category = new Category { CategoryId = 1, CategoryName = "Trái cây", IsActive = true },
            FarmerProfile = new FarmerProfile
            {
                FarmerProfileId = 7, FarmName = "Nông trại A", VerificationStatus = "Approved",
                User = _accounts.Users[3]
            }
        };
        _orange.ProductImages.Add(new ProductImage { ImageUrl = "https://example.test/cam.png", IsPrimary = true });
        _batch = new HarvestBatch
        {
            HarvestBatchId = 30, ProductId = 20, BatchCode = "CAM-01", Status = "Available",
            ActualHarvestDate = today.AddDays(-1), StockEnteredAt = T0.UtcDateTime.AddDays(-1),
            AvailableQuantity = 5m, ExpirationDate = today.AddDays(10)
        };
        _store.Batches.Add(_batch);
        _store.Items.Add(new CartItem { CartItemId = 10, ProductId = 20, Product = _orange, Quantity = 2m, IsSelected = true });
        _store.Points = 100_000;
    }

    private CheckoutService Service(long actorId = BuyerId) => new(_store, _accounts, _uow, new Actor(actorId),
        _clock, _policy, _catalog, _shipping);

    private static CheckoutRequest Input(string phone = "0901234567", string? voucher = null, int points = 0) =>
        new(new CheckoutAddress("Test buyer", phone, "79", "TP HCM", "760", "Quận 1", "26734", "Bến Nghé",
            "123 Test street"), "Standard", "PayOS", voucher, points, null);

    private static PlaceOrderRequest Place(Guid quoteId, string? key = null, bool confirmed = true) =>
        new(quoteId, key ?? Guid.NewGuid().ToString("D"), confirmed);

    private async Task<CheckoutQuoteDto> QuotedAsync(CheckoutRequest? input = null)
    {
        var quote = await Service().QuoteAsync(input ?? Input(), default);
        Assert.True(quote.IsSuccess, quote.Error?.Message);
        return quote.Value!;
    }

    // ---------- CheckoutValidator ----------

    [Theory(DisplayName = "[UC-RC-02 #05-#12] Invalid checkout fields are reported on the right property")]
    [InlineData("phone9", "Address.Phone")]
    [InlineData("prefix04", "Address.Phone")]
    [InlineData("recipient1", "Address.RecipientName")]
    [InlineData("recipient101", "Address.RecipientName")]
    [InlineData("delivery", "DeliveryMethod")]
    [InlineData("payment", "PaymentMethod")]
    [InlineData("points", "Points")]
    [InlineData("notes301", "Notes")]
    public async Task Validator_InvalidField_Reported(string variant, string property)
    {
        var input = Input();
        input = variant switch
        {
            "phone9" => input with { Address = input.Address with { Phone = "091234567" } },
            "prefix04" => input with { Address = input.Address with { Phone = "0412345678" } },
            "recipient1" => input with { Address = input.Address with { RecipientName = "A" } },
            "recipient101" => input with { Address = input.Address with { RecipientName = new string('a', 101) } },
            "delivery" => input with { DeliveryMethod = "Express" },
            "payment" => input with { PaymentMethod = "COD" },
            "points" => input with { Points = -1 },
            _ => input with { Notes = new string('a', 301) }
        };

        var result = await new CheckoutValidator().ValidateAsync(input);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == property);
    }

    // ---------- QuoteAsync ----------

    [Fact(DisplayName = "[UC-RC-02 #24] Valid quote prices lines and delivery and saves the quote")]
    public async Task Quote_Valid_SavedWithoutReservation()
    {
        var quote = await QuotedAsync();

        Assert.Equal((80_000m, 40_000m, 120_000m), (quote.Subtotal, quote.DeliveryFee, quote.Total));
        var saved = Assert.Single(_store.Quotes);
        Assert.Equal((quote.Id, BuyerId), (saved.QuoteId, saved.BuyerUserId));
        Assert.Equal(64, saved.Fingerprint.Length);
        Assert.Equal(T0.UtcDateTime.AddMinutes(5), saved.ExpiresAt);
        Assert.Equal(5m, _batch.AvailableQuantity);
    }

    [Fact(DisplayName = "[UC-RC-02 #25] Non-retail actor cannot quote")]
    public async Task Quote_Farmer_Forbidden()
    {
        var result = await Service(actorId: 3).QuoteAsync(Input(), default);

        Assert.Equal("FORBIDDEN", result.Error?.Code);
        Assert.Empty(_store.Quotes);
    }

    [Fact(DisplayName = "[UC-RC-02 #26] Invalid phone fails validation without saving a quote")]
    public async Task Quote_InvalidPhone_Validation()
    {
        var result = await Service().QuoteAsync(Input(phone: "0401234567"), default);

        Assert.Contains("Address.Phone", result.Error!.Fields!.Keys);
        Assert.Empty(_store.Quotes);
    }

    [Fact(DisplayName = "[UC-RC-02 #27] Address rejected by the catalog")]
    public async Task Quote_UnknownAddress_Invalid()
    {
        _catalog.Rejects = true;

        var result = await Service().QuoteAsync(Input(), default);

        Assert.Equal("ADDRESS_INVALID", result.Error?.Code);
        Assert.Empty(_store.Quotes);
    }

    [Fact(DisplayName = "[UC-RC-02 #28] No selected cart items")]
    public async Task Quote_NoItems_CartEmpty()
    {
        _store.Items.Clear();

        var result = await Service().QuoteAsync(Input(), default);

        Assert.Equal("CART_EMPTY", result.Error?.Code);
    }

    [Fact(DisplayName = "[UC-RC-02 #29] Shipping provider unavailable")]
    public async Task Quote_ShippingDown_Unavailable()
    {
        _shipping.Down = true;

        var result = await Service().QuoteAsync(Input(), default);

        Assert.Equal("SHIPPING_RATE_UNAVAILABLE", result.Error?.Code);
        Assert.Empty(_store.Quotes);
    }

    [Fact(DisplayName = "[UC-RC-02 #30] Expired voucher is dropped with MSG116 warning")]
    public async Task Quote_ExpiredVoucher_Warning()
    {
        _store.Vouchers.Add(Voucher(endDate: T0.UtcDateTime));

        var quote = await QuotedAsync(Input(voucher: "SALE10"));

        Assert.StartsWith("MSG116", quote.Warning);
        Assert.Null(quote.Input.VoucherCode);
        Assert.Equal(0m, quote.VoucherDiscount);
    }

    [Fact(DisplayName = "[UC-RC-02 #31] Points above balance are rejected")]
    public async Task Quote_PointsAboveBalance_Rejected()
    {
        _store.Points = 100;

        var result = await Service().QuoteAsync(Input(points: 500), default);

        Assert.Equal("POINTS_INVALID", result.Error?.Code);
    }

    [Fact(DisplayName = "[UC-RC-02 #32] Points above the discount cap are clamped")]
    public async Task Quote_PointsAboveCap_Clamped()
    {
        var quote = await QuotedAsync(Input(points: 50_000));

        Assert.Equal(40_000, quote.Input.Points);                 // 50% of 80 000 ₫ at 1 ₫ per point
        Assert.Equal(40_000m, quote.PointsDiscount);
        Assert.Contains("40000 điểm", quote.Warning);
    }

    // ---------- PlaceAsync ----------

    [Fact(DisplayName = "[UC-RC-02 #33] Valid placement reserves stock, clears the cart and audits")]
    public async Task Place_Valid_CommitsOrder()
    {
        var quote = await QuotedAsync();

        var result = await Service().PlaceAsync(Place(quote.Id), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var order = Assert.Single(_store.Orders);
        Assert.Equal(("PendingPayment", 120_000m), (order.OrderStatus, order.FinalAmount));
        Assert.Equal((3m, 2m), (_batch.AvailableQuantity, _batch.ReservedQuantity));
        Assert.Equal(10, Assert.Single(_store.RemovedItems).CartItemId);
        Assert.Equal("RetailCheckout", Assert.Single(_accounts.Audits).Action);
        Assert.Equal(1, _uow.Commits);
    }

    [Theory(DisplayName = "[UC-RC-02 #34/#35] Unconfirmed or malformed request key is rejected before the transaction")]
    [InlineData(false, null)]
    [InlineData(true, "abc")]
    public async Task Place_InvalidRequest_Validation(bool confirmed, string? key)
    {
        var quote = await QuotedAsync();

        var result = await Service().PlaceAsync(Place(quote.Id, key, confirmed), default);

        Assert.Equal("VALIDATION", result.Error?.Code);
        Assert.Equal(0, _uow.Begins);
        Assert.Empty(_store.Orders);
    }

    [Fact(DisplayName = "[UC-RC-02 #36] Repeated request key for the same quote returns the existing order")]
    public async Task Place_SameKey_ReturnsExisting()
    {
        var quote = await QuotedAsync();
        string key = Guid.NewGuid().ToString("D");
        _store.Existing = ExistingOrder(key, quote.Id);

        var result = await Service().PlaceAsync(Place(quote.Id, key), default);

        Assert.True(result.Value!.IsDuplicate);
        Assert.Equal(55, result.Value.Id);
        Assert.Empty(_store.Orders);
        Assert.Equal(0m, _batch.ReservedQuantity);
    }

    [Fact(DisplayName = "[UC-RC-02 #37] Request key reused for another quote conflicts")]
    public async Task Place_KeyForOtherQuote_Conflict()
    {
        var quote = await QuotedAsync();
        string key = Guid.NewGuid().ToString("D");
        _store.Existing = ExistingOrder(key, Guid.NewGuid());

        var result = await Service().PlaceAsync(Place(quote.Id, key), default);

        Assert.Equal("IDEMPOTENCY_CONFLICT", result.Error?.Code);
        Assert.Empty(_store.Orders);
    }

    [Fact(DisplayName = "[UC-RC-02 #38] Unknown quote is not found")]
    public async Task Place_UnknownQuote_NotFound()
    {
        var result = await Service().PlaceAsync(Place(Guid.NewGuid()), default);

        Assert.Equal("NOT_FOUND", result.Error?.Code);
    }

    [Fact(DisplayName = "[UC-RC-02 #39] Quote is expired exactly at ExpiresAt")]
    public async Task Place_AtExpiry_Expired()
    {
        var quote = await QuotedAsync();
        _clock.Advance(TimeSpan.FromMinutes(5));

        var result = await Service().PlaceAsync(Place(quote.Id), default);

        Assert.Equal("QUOTE_EXPIRED", result.Error?.Code);
        Assert.Empty(_store.Orders);
    }

    [Fact(DisplayName = "[UC-RC-02 #40] Price changed after quote requires a new confirmation")]
    public async Task Place_PriceChanged_Msg118()
    {
        var quote = await QuotedAsync();
        _orange.BaseRetailPrice = 45_000m;

        var result = await Service().PlaceAsync(Place(quote.Id), default);

        Assert.Equal("MSG118", result.Error?.Code);
        Assert.Empty(_store.Orders);
        Assert.Equal(5m, _batch.AvailableQuantity);
    }

    [Fact(DisplayName = "[UC-RC-02 #41] Voucher revoked after quote")]
    public async Task Place_VoucherRevoked_Msg116()
    {
        var voucher = Voucher(endDate: T0.UtcDateTime.AddDays(1));
        _store.Vouchers.Add(voucher);
        var quote = await QuotedAsync(Input(voucher: "SALE10"));
        Assert.Equal(8_000m, quote.VoucherDiscount);
        voucher.IsActive = false;

        var result = await Service().PlaceAsync(Place(quote.Id), default);

        Assert.Equal("MSG116", result.Error?.Code);
        Assert.Empty(_store.Orders);
    }

    [Fact(DisplayName = "[UC-RC-02 #42] Stock dropped below the ordered quantity")]
    public async Task Place_StockDropped_Msg88()
    {
        var quote = await QuotedAsync();
        _batch.AvailableQuantity = 1m;

        var result = await Service().PlaceAsync(Place(quote.Id), default);

        Assert.Equal("MSG88", result.Error?.Code);
        Assert.Empty(_store.Orders);
    }

    private static Voucher Voucher(DateTime endDate) => new()
    {
        VoucherId = 4, VoucherCode = "SALE10", DiscountType = "Percentage", DiscountValue = 10,
        MinOrderAmount = 0, IsActive = true, StartDate = T0.UtcDateTime.AddDays(-1), EndDate = endDate
    };

    private static Order ExistingOrder(string key, Guid quoteId) => new()
    {
        OrderId = 55, OrderCode = "RC-EXISTING", OrderStatus = "PendingPayment", FinalAmount = 120_000m,
        CheckoutRequestKey = key, RetailQuoteId = quoteId, PaymentMethod = "PayOS",
        ReservationExpiresAt = T0.UtcDateTime.AddMinutes(15)
    };

    private sealed record Actor(long UserId) : IActorContext;

    private sealed class Transactions : IUseCaseUnitOfWork
    {
        public int Begins { get; private set; }
        public int Commits { get; private set; }

        public Task<ITransaction> BeginAsync(CancellationToken cancellationToken)
        {
            Begins++;
            return Task.FromResult<ITransaction>(new Transaction(this));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private sealed class Transaction(Transactions owner) : ITransaction
        {
            public Task CommitAsync(CancellationToken cancellationToken)
            {
                owner.Commits++;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class Catalog : IAddressCatalog
    {
        public bool Rejects { get; set; }

        public Task<IReadOnlyList<ProvinceOption>> GetProvincesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ProvinceOption>>([]);
        public Task<IReadOnlyList<DistrictOption>> GetDistrictsAsync(string provinceCode, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DistrictOption>>([]);
        public Task<IReadOnlyList<WardOption>> GetWardsAsync(string districtCode, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<WardOption>>([]);
        public Task<CheckoutAddress?> NormalizeAsync(CheckoutAddress address, CancellationToken ct) =>
            Task.FromResult(Rejects ? null : address with { CatalogVersion = "test" });
    }

    private sealed class Shipping : IShippingRateProvider
    {
        public bool Down { get; set; }

        public int EstimatedDeliveryDays(string deliveryMethod) => 2;

        public Task<ShippingRateResult?> GetRateAsync(ShippingRateRequest request, CancellationToken ct) =>
            Task.FromResult(Down ? null : new ShippingRateResult(true, 40_000m));
    }

    private sealed class CheckoutStore : ICheckoutRepository
    {
        public List<CartItem> Items { get; } = [];
        public List<HarvestBatch> Batches { get; } = [];
        public List<Voucher> Vouchers { get; } = [];
        public List<RetailCheckoutQuote> Quotes { get; } = [];
        public List<Order> Orders { get; } = [];
        public List<CartItem> RemovedItems { get; } = [];
        public long Points { get; set; }
        public Order? Existing { get; set; }

        public Task<IReadOnlyList<CartItem>> SelectedItemsAsync(long userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CartItem>>(Items.ToList());
        public Task<IReadOnlyList<DeliveryAddress>> AddressesAsync(long userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DeliveryAddress>>([]);
        public Task<IReadOnlyList<HarvestBatch>> BatchesAsync(IReadOnlyList<long> productIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HarvestBatch>>(Batches.Where(b => productIds.Contains(b.ProductId)).ToList());
        public Task<long> PointsAsync(long userId, CancellationToken ct) => Task.FromResult(Points);
        public Task<IReadOnlyList<Voucher>> VouchersAsync(DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Voucher>>(Vouchers);
        public Task<Voucher?> VoucherAsync(string code, CancellationToken ct) =>
            Task.FromResult(Vouchers.SingleOrDefault(v => v.VoucherCode == code));
        public Task<int> ReservedVoucherCountAsync(long voucherId, CancellationToken ct) => Task.FromResult(0);
        public Dictionary<long, int> CustomerUses { get; } = [];
        public Task<IReadOnlyDictionary<long, int>> CustomerVoucherUsesAsync(long userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<long, int>>(CustomerUses);
        public Task<RetailCheckoutQuote?> QuoteAsync(Guid id, long userId, CancellationToken ct) =>
            Task.FromResult(Quotes.SingleOrDefault(q => q.QuoteId == id && q.BuyerUserId == userId));
        public Task<Order?> ExistingOrderAsync(long userId, string requestKey, Guid quoteId, CancellationToken ct) =>
            Task.FromResult(Existing?.CheckoutRequestKey == requestKey ? Existing : null);
        public Task<Order?> GetOrderAsync(long userId, long orderId, CancellationToken ct) =>
            Task.FromResult<Order?>(null);
        public Task<IReadOnlyList<Order>> ExpiredOrdersAsync(DateTimeOffset now, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Order>>([]);
        public Task<bool> IsCategoryArchivedAsync(long id, CancellationToken ct) => Task.FromResult(false);
        public void AddQuote(RetailCheckoutQuote quote) => Quotes.Add(quote);
        public void AddOrder(Order order) => Orders.Add(order);
        public void RemoveCartItems(IEnumerable<CartItem> items) => RemovedItems.AddRange(items);
        public void AddMovement(InventoryMovement movement) { }
        public void AddRewardEntry(RewardPointEntry entry) { }
    }
}
