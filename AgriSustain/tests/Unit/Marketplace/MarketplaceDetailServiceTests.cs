using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Cart;
using AgriSustain.Application.DTOs.Marketplace;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.UnitTests.DuIte1;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriSustain.UnitTests.Marketplace;

/// <summary>UC-MKT-02 unit tests of services, validation and domain rules.</summary>
[Trait("UseCase", "UC-MKT-02")]
public sealed class MarketplaceDetailServiceTests
{
    private readonly MarketplaceStore _store = new();

    private MarketplaceService Service() => new(_store);

    private static readonly DateTimeOffset T0 = new(2026, 10, 27, 1, 0, 0, TimeSpan.Zero);
    private readonly CartStore _cartStore = new();
    private readonly RecordingUnitOfWork _uow = new();
    private RetailCartService CartService() => new(_cartStore, _uow,
        new CartPricingService(Options.Create(new QuantityOptions()), _cartStore.Clock), _cartStore.Clock);

    // ---------- UC-MKT-02: MarketplaceService.GetDetailAsync ----------

    [Fact(DisplayName = "[UC-MKT-02 #001] Return the product detail and its price")]
    public async Task GetDetailAsync_ExistingDetail_ReturnsProduct()
    {
        _store.Detail = MarketplaceStore.ProductDetail();
        var result = await Service().GetDetailAsync(5, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(38000, result.Value!.Product.Price);
    }

    [Fact(DisplayName = "[UC-MKT-02 #002] Return 404 without detail data")]
    public async Task GetDetailAsync_MissingDetail_ReturnsNotFound()
    {
        var result = await Service().GetDetailAsync(99, default);

        Assert.Equal(404, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact(DisplayName = "[UC-MKT-02 #003] Reject the id before querying storage")]
    public async Task GetDetailAsync_ZeroId_RejectsWithoutQuery()
    {
        var response = await Service().GetDetailAsync(0, default);

        Assert.False(response.IsSuccess);
        Assert.Equal(0, _store.DetailCalls);
    }

    [Fact(DisplayName = "[UC-MKT-02 #004] Return usable detail with empty optional collections")]
    public async Task GetDetailAsync_OptionalData_ReturnsAvailableData()
    {
        _store.Detail = MarketplaceStore.ProductDetail(0);
        var result = await Service().GetDetailAsync(5, default);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Images);
        Assert.Empty(result.Value.Reviews);
        Assert.Equal(0, result.Value.Product.AvailableQuantity);
    }

    // ---------- UC-MKT-02: RetailCartService.AddItemAsync ----------

    [Fact(DisplayName = "[UC-MKT-02 #005] Create one selected line and commit once")]
    public async Task AddItemAsync_AddAvailable_AddsSelectedLine()
    {
        var result = await CartService().AddItemAsync(102, new(301, 401, 2), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, Assert.Single(_cartStore.Cart!.CartItems).Quantity);
        Assert.True(_cartStore.Cart!.CartItems.Single().IsSelected);
        Assert.Equal(1, _uow.Commits);
    }

    [Fact(DisplayName = "[UC-MKT-02 #006] Reject quantity and do not commit")]
    public async Task AddItemAsync_ZeroQuantity_RejectsWithoutCommit()
    {
        var result = await CartService().AddItemAsync(102, new(301, 401, 0), default);

        Assert.Equal("VALIDATION", result.Error!.Code);
        Assert.Equal(0, _uow.Commits);
    }

    [Fact(DisplayName = "[UC-MKT-02 #007] Reject negative quantity")]
    public async Task AddItemAsync_NegativeQuantity_RejectsNegativeQuantity()
    {
        var response = await CartService().AddItemAsync(102, new(301, 401, -1), default);

        Assert.False(response.IsSuccess);
        Assert.Empty(_cartStore.Cart!.CartItems);
    }

    [Fact(DisplayName = "[UC-MKT-02 #008] Reject quantity outside the configured step")]
    public async Task AddItemAsync_InvalidStep_RejectsInvalidStep()
    {
        var response = await CartService().AddItemAsync(102, new(301, 401, 1.5m), default);

        Assert.Equal("VALIDATION", response.Error!.Code);
    }

    [Fact(DisplayName = "[UC-MKT-02 #009] Merge to quantity 5 without another line")]
    public async Task AddItemAsync_MergeLine_MergesExistingLine()
    {
        _cartStore.AddCartLine();
        await CartService().AddItemAsync(102, new(301, 401, 2), default);

        Assert.Equal(5, Assert.Single(_cartStore.Cart!.CartItems).Quantity);
    }

    [Fact(DisplayName = "[UC-MKT-02 #010] Limit added quantity to stock and retain stock balance")]
    public async Task AddItemAsync_CapAtStock_CapsAtStock()
    {
        var response = await CartService().AddItemAsync(102, new(301, 401, 21), default);

        Assert.True(response.IsSuccess);
        Assert.Equal(20, Assert.Single(_cartStore.Cart!.CartItems).Quantity);
        Assert.Equal(20, _cartStore.Batch.AvailableQuantity);
    }

    [Fact(DisplayName = "[UC-MKT-02 #011] Reject unavailable batch without commit")]
    public async Task AddItemAsync_OutOfStock_RejectsUnavailableBatch()
    {
        _cartStore.Batch.AvailableQuantity = 0;
        var response = await CartService().AddItemAsync(102, new(301, 401, 1), default);

        Assert.Equal("CONFLICT", response.Error!.Code);
        Assert.Equal(0, _uow.Commits);
    }

    [Fact(DisplayName = "[UC-MKT-02 #012] Reject product with changed availability")]
    public async Task AddItemAsync_Unpublished_RejectsUnpublishedProduct()
    {
        _cartStore.Product.IsPublished = false;
        var response = await CartService().AddItemAsync(102, new(301, 401, 1), default);

        Assert.False(response.IsSuccess);
        Assert.Empty(_cartStore.Cart!.CartItems);
    }

    [Fact(DisplayName = "[UC-MKT-02 #013] Reject expired goods")]
    public async Task AddItemAsync_Expired_RejectsExpiredBatch()
    {
        _cartStore.Batch.ExpirationDate = DateOnly.FromDateTime(_cartStore.Clock.Now.DateTime).AddDays(-1);
        var response = await CartService().AddItemAsync(102, new(301, 401, 1), default);

        Assert.False(response.IsSuccess);
    }

    [Fact(DisplayName = "[UC-MKT-02 #014] Automatically select the earliest expiring batch")]
    public async Task AddItemAsync_ChooseBatch_ChoosesEarliestExpiry()
    {
        var early = _cartStore.NewBatch(402);
        early.ExpirationDate = _cartStore.Batch.ExpirationDate!.Value.AddDays(-1);
        _cartStore.Offers[402] = early;
        await CartService().AddItemAsync(102, new(301, 0, 1), default);

        Assert.Equal(402, Assert.Single(_cartStore.Cart!.CartItems).HarvestBatchId);
    }

    [Fact(DisplayName = "[UC-MKT-02 #015] Create a cart owned by the requesting buyer")]
    public async Task AddItemAsync_CreateCart_CreatesOwnedCart()
    {
        _cartStore.Cart = null;
        var response = await CartService().AddItemAsync(102, new(301, 401, 1), default);

        Assert.True(response.IsSuccess);
        Assert.Equal(102, _cartStore.Cart!.UserId);
        Assert.Equal(1, _cartStore.Adds);
    }

    [Fact(DisplayName = "[UC-MKT-02 #016] Reject a mismatched product and batch")]
    public async Task AddItemAsync_MismatchedBatch_RejectsMismatchedBatch()
    {
        var response = await CartService().AddItemAsync(102, new(999, 401, 1), default);

        Assert.False(response.IsSuccess);
        Assert.Equal(0, _uow.Commits);
    }

    // ---------- Test doubles ----------

    private sealed class MarketplaceStore : IMarketplaceRepository
    {
        public BrowseMarketplaceRequest? LastQuery { get; private set; }
        public int BrowseCalls { get; private set; }
        public int DetailCalls { get; private set; }
        public MarketplacePageResponse Page { get; set; } = new([], 0, 1, 12);
        public ProductDetailResponse? Detail { get; set; }
        public Task<MarketplacePageResponse> BrowseAsync(BrowseMarketplaceRequest query, CancellationToken ct)
        {
            BrowseCalls++;
            LastQuery = query;
            return Task.FromResult(Page);
        }
        public Task<MarketplaceFiltersResponse> GetFiltersAsync(CancellationToken ct,
            long? categoryId = null, long? supplierId = null) =>
            Task.FromResult(new MarketplaceFiltersResponse([new(1, "Rice", 2)], [], [], ["An Giang"], ["VietGAP"]));
        public Task<ProductDetailResponse?> GetDetailAsync(long productId, CancellationToken ct)
        {
            DetailCalls++;
            return Task.FromResult(Detail);
        }
        public static Product PublicProduct() => new()
        {
            ProductId = 5,
            ProductName = "Rice",
            Unit = "kg",
            BaseRetailPrice = 38000,
            IsPublished = true,
            ApprovalStatus = "Approved",
            Category = new Category { IsActive = true },
            FarmerProfile = new FarmerProfile { VerificationStatus = "Approved", User = new User { Status = "Active" } }
        };
        public static ProductDetailResponse ProductDetail(decimal stock = 10) => new(
            new ProductSummaryResponse
            {
                Id = 5,
                Name = "Rice",
                Unit = "kg",
                Price = 38000,
                AvailableQuantity = stock,
                Supplier = "Unit Farm",
                Category = "Rice"
            },
            "Rice description", "Unit address", true, null, null, [], [], [], [], []);
    }

    private sealed class CartStore : IRetailCartRepository
    {
        public FakeClock Clock { get; } = new(T0);
        public Product Product { get; }
        public HarvestBatch Batch { get; }
        public Cart? Cart { get; set; } = new() { CartId = 601, UserId = 102 };
        public Dictionary<long, HarvestBatch> Offers { get; } = [];
        public int Adds { get; private set; }

        public CartStore()
        {
            Product = new Product
            {
                ProductId = 301,
                ProductName = "Tomato",
                Unit = "kg",
                BaseRetailPrice = 30000,
                WholesaleMinQuantity = 10,
                WholesalePrice = 25000,
                IsPublished = true,
                ApprovalStatus = "Approved",
                Category = new Category { IsActive = true },
                FarmerProfile = new FarmerProfile
                {
                    FarmerProfileId = 1,
                    UserId = 101,
                    FarmName = "Test Farm",
                    VerificationStatus = "Approved",
                    User = new User { UserId = 101, Status = "Active" }
                }
            };
            Batch = NewBatch(401);
            Batch.ReservedQuantity = 5;
            Offers[401] = Batch;
        }

        public HarvestBatch NewBatch(long id) => new()
        {
            HarvestBatchId = id,
            ProductId = Product.ProductId,
            Product = Product,
            BatchCode = $"BATCH-{id}",
            Status = "Available",
            AvailableQuantity = 20,
            StockEnteredAt = Clock.Now.UtcDateTime,
            ActualHarvestDate = DateOnly.FromDateTime(Clock.Now.DateTime).AddDays(-1),
            ExpirationDate = DateOnly.FromDateTime(Clock.Now.DateTime).AddDays(10)
        };

        public void AddCartLine() => Cart!.CartItems.Add(new CartItem
        {
            CartItemId = 701,
            CartId = Cart.CartId,
            ProductId = Product.ProductId,
            Product = Product,
            HarvestBatchId = Batch.HarvestBatchId,
            HarvestBatch = Batch,
            Quantity = 3,
            IsSelected = true
        });

        public Task<Cart?> GetOwnedAsync(long userId, CancellationToken ct) =>
            Task.FromResult(Cart?.UserId == userId ? Cart : null);
        public Task<HarvestBatch?> GetOfferAsync(long productId, long batchId, CancellationToken ct) =>
            Task.FromResult(Offers.TryGetValue(batchId, out var batch) && batch.ProductId == productId ? batch : null);
        public Task<IReadOnlyList<HarvestBatch>> GetOffersAsync(long productId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HarvestBatch>>(Offers.Values.Where(b => b.ProductId == productId).ToArray());
        public void Add(Cart cart) { Adds++; cart.CartId = 601; Cart = cart; }
        public void Remove(CartItem item) => Cart!.CartItems.Remove(item);
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int Commits { get; private set; }
        public Task BeginAsync(CancellationToken ct) => Task.CompletedTask;
        public Task FlushAsync(CancellationToken ct) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken ct) { Commits++; return Task.CompletedTask; }
        public Task RollbackAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
