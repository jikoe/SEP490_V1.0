using System.Text.Json;
using AgriSustain.Application.DTOs.Cart;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriSustain.UnitTests;

public sealed class CartServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddItemAsync_NoBatch_AllocatesEarliestExpiryAndExistingCartCapacity()
    {
        var early = Batch(2, 4, 2);
        var late = Batch(1, 5, 5);
        var repository = new TestRepository(late, early);
        repository.Cart = new Cart { UserId = 7 };
        repository.Cart.CartItems.Add(Item(early, 3));
        var unitOfWork = new TestUnitOfWork();

        var result = await Service(repository, unitOfWork).AddItemAsync(7,
            new CartItemRequest(1, 0, 3), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, repository.Cart.CartItems.Single(x => x.HarvestBatchId == 2).Quantity);
        Assert.Equal(2, repository.Cart.CartItems.Single(x => x.HarvestBatchId == 1).Quantity);
        Assert.Equal(7, repository.RequestedUserId);
        Assert.Equal(1, unitOfWork.Begins);
        Assert.Equal(1, unitOfWork.Commits);
        Assert.Equal(4, early.AvailableQuantity);
        Assert.Equal(5, late.AvailableQuantity);
    }

    [Fact]
    public async Task AddItemAsync_InsufficientStock_PreservesCartAndReportsRemainingAmount()
    {
        var batch = Batch(1, 5, 2);
        var repository = new TestRepository(batch, Batch(2, 1, 3))
        {
            Cart = new Cart { UserId = 7 }
        };
        repository.Cart.CartItems.Add(Item(batch, 3));
        var unitOfWork = new TestUnitOfWork();

        var result = await Service(repository, unitOfWork).AddItemAsync(7,
            new CartItemRequest(1, 0, 4), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("CONFLICT", result.Error?.Code);
        Assert.Contains("3 kg", result.Error?.Message);
        Assert.Equal(3, Assert.Single(repository.Cart.CartItems).Quantity);
        Assert.Equal(0, unitOfWork.Commits);
    }

    [Fact]
    public async Task AddItemAsync_OmittedBatchId_DeserializesAsAutomaticSelection()
    {
        var request = JsonSerializer.Deserialize<CartItemRequest>(
            """{"productId":1,"quantity":1}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(request);
        Assert.Equal(0, request.HarvestBatchId);
        var repository = new TestRepository(Batch(1, 5, 2));

        var result = await Service(repository, new TestUnitOfWork())
            .AddItemAsync(7, request, default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AddItemAsync_FractionalStep_RoundsEachBatchCapacityBeforeAllocating()
    {
        var repository = new TestRepository(Batch(1, 0.74m, 1), Batch(2, 1, 2));
        var unitOfWork = new TestUnitOfWork();

        var result = await Service(repository, unitOfWork, 0.25m).AddItemAsync(7,
            new CartItemRequest(1, 0, 0.75m), default);

        Assert.True(result.IsSuccess);
        Assert.NotNull(repository.Cart);
        Assert.Equal(0.5m, repository.Cart.CartItems.Single(x => x.HarvestBatchId == 1).Quantity);
        Assert.Equal(0.25m, repository.Cart.CartItems.Single(x => x.HarvestBatchId == 2).Quantity);
        Assert.Equal(1, unitOfWork.Commits);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.5)]
    [InlineData(1000000000)]
    public async Task AddItemAsync_InvalidQuantity_DoesNotCreateOrCommitCart(decimal quantity)
    {
        var repository = new TestRepository(Batch(1, 5, 2));
        var unitOfWork = new TestUnitOfWork();

        var result = await Service(repository, unitOfWork).AddItemAsync(7,
            new CartItemRequest(1, 0, quantity), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION", result.Error?.Code);
        Assert.Null(repository.Cart);
        Assert.Equal(0, unitOfWork.Commits);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("not-stocked")]
    [InlineData("future-harvest")]
    [InlineData("recalled")]
    [InlineData("unpublished")]
    [InlineData("inactive-category")]
    [InlineData("inactive-seller")]
    public async Task AddItemAsync_UnavailableOffer_DoesNotCreateOrCommitCart(string state)
    {
        var batch = Batch(1, 5, 2);
        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        switch (state)
        {
            case "expired": batch.ExpirationDate = today; break;
            case "not-stocked": batch.StockEnteredAt = null; break;
            case "future-harvest": batch.ActualHarvestDate = today.AddDays(1); break;
            case "recalled": batch.Status = "Recalled"; break;
            case "unpublished": batch.Product.IsPublished = false; break;
            case "inactive-category": batch.Product.Category.IsActive = false; break;
            case "inactive-seller": batch.Product.FarmerProfile.User.Status = "Suspended"; break;
        }

        var repository = new TestRepository(batch);
        var unitOfWork = new TestUnitOfWork();
        var result = await Service(repository, unitOfWork).AddItemAsync(7,
            new CartItemRequest(1, 0, 1), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("CONFLICT", result.Error?.Code);
        Assert.Null(repository.Cart);
        Assert.Equal(0, unitOfWork.Commits);
    }

    [Fact]
    public async Task AddItemAsync_ExpiryTiesAndNoExpiry_UsesStableBatchOrder()
    {
        var repository = new TestRepository(Batch(1, 10, null), Batch(3, 2, 2), Batch(2, 2, 2));
        var unitOfWork = new TestUnitOfWork();

        var result = await Service(repository, unitOfWork).AddItemAsync(7,
            new CartItemRequest(1, 0, 3), default);

        Assert.True(result.IsSuccess);
        Assert.NotNull(repository.Cart);
        Assert.Equal(2, repository.Cart.CartItems.Single(x => x.HarvestBatchId == 2).Quantity);
        Assert.Equal(1, repository.Cart.CartItems.Single(x => x.HarvestBatchId == 3).Quantity);
        Assert.DoesNotContain(repository.Cart.CartItems, x => x.HarvestBatchId == 1);
    }

    [Fact]
    public async Task AddItemAsync_AllocationWouldExceedLineLimit_PreservesAllExistingLines()
    {
        var repository = new TestRepository(Batch(1, 1, 1), Batch(2, 1, 2))
        {
            Cart = FullCart(99)
        };
        var unitOfWork = new TestUnitOfWork();

        var result = await Service(repository, unitOfWork).AddItemAsync(7,
            new CartItemRequest(1, 0, 2), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION", result.Error?.Code);
        Assert.Equal(99, repository.Cart.CartItems.Count);
        Assert.All(repository.Cart.CartItems, x => Assert.Equal(1, x.Quantity));
        Assert.Equal(0, unitOfWork.Commits);
    }

    [Fact]
    public async Task AddItemAsync_FullCartExistingBatch_CanIncreaseWithoutNewLine()
    {
        var batch = Batch(1, 5, 1);
        var repository = new TestRepository(batch) { Cart = FullCart(99) };
        repository.Cart.CartItems.Add(Item(batch, 1));
        var unitOfWork = new TestUnitOfWork();

        var result = await Service(repository, unitOfWork).AddItemAsync(7,
            new CartItemRequest(1, 0, 2), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, repository.Cart.CartItems.Count);
        Assert.Equal(3, repository.Cart.CartItems.Single(x => x.HarvestBatchId == 1).Quantity);
        Assert.Equal(1, unitOfWork.Commits);
    }

    [Fact]
    public async Task AddItemAsync_DifferentOwner_DoesNotReuseOtherUsersCart()
    {
        var batch = Batch(1, 5, 2);
        var previousCart = new Cart { UserId = 8 };
        previousCart.CartItems.Add(Item(batch, 4));
        var repository = new TestRepository(batch) { Cart = previousCart };
        var unitOfWork = new TestUnitOfWork();

        var result = await Service(repository, unitOfWork).AddItemAsync(7,
            new CartItemRequest(1, 0, 2), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, Assert.Single(previousCart.CartItems).Quantity);
        Assert.NotNull(repository.Cart);
        Assert.Equal(7, repository.Cart.UserId);
        Assert.Equal(2, Assert.Single(repository.Cart.CartItems).Quantity);
    }

    [Fact]
    public async Task AddItemAsync_ExplicitBatch_PreservesExistingStockAdjustmentBehavior()
    {
        var batch = Batch(1, 5, 2);
        var repository = new TestRepository(batch) { Cart = new Cart { UserId = 7 } };
        repository.Cart.CartItems.Add(Item(batch, 3));
        var unitOfWork = new TestUnitOfWork();

        var result = await Service(repository, unitOfWork).AddItemAsync(7,
            new CartItemRequest(1, 1, 4), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, Assert.Single(repository.Cart.CartItems).Quantity);
        Assert.Contains(result.Value?.Messages ?? [], x => x.Contains("MSG86"));
        Assert.Equal(1, unitOfWork.Commits);
    }

    [Fact]
    public async Task AddItemAsync_NegativeBatchId_RejectsBeforeTransaction()
    {
        var repository = new TestRepository(Batch(1, 5, 2));
        var unitOfWork = new TestUnitOfWork();

        var result = await Service(repository, unitOfWork).AddItemAsync(7,
            new CartItemRequest(1, -1, 1), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION", result.Error?.Code);
        Assert.Null(repository.Cart);
        Assert.Equal(0, unitOfWork.Begins);
        Assert.Equal(0, unitOfWork.Commits);
    }

    [Fact]
    public async Task AddItemAsync_SplitWholesaleQuantity_PricesBySelectedProductTotal()
    {
        var early = Batch(1, 3, 1);
        var late = Batch(2, 7, 2);
        foreach (var batch in new[] { early, late })
        {
            batch.Product.WholesaleMinQuantity = 10;
            batch.Product.WholesalePrice = 8000;
        }

        var repository = new TestRepository(early, late);
        var result = await Service(repository, new TestUnitOfWork()).AddItemAsync(7,
            new CartItemRequest(1, 0, 10), default);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(80000, result.Value.SelectedSubtotal);
        Assert.All(result.Value.Items, item => Assert.Equal(8000, item.UnitPrice));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetAsync_UnselectedOrUnavailableBatch_DoesNotCountTowardWholesaleThreshold(
        bool unavailable)
    {
        var early = Batch(1, 3, 1);
        var late = Batch(2, 7, 2);
        foreach (var batch in new[] { early, late })
        {
            batch.Product.WholesaleMinQuantity = 10;
            batch.Product.WholesalePrice = 8000;
        }

        var excluded = Item(early, 3);
        excluded.IsSelected = unavailable;
        if (unavailable)
        {
            early.Status = "Recalled";
        }

        var repository = new TestRepository { Cart = new Cart { UserId = 7 } };
        repository.Cart.CartItems.Add(excluded);
        repository.Cart.CartItems.Add(Item(late, 7));

        var result = await Service(repository, new TestUnitOfWork()).GetAsync(7, default);

        Assert.Equal(70000, result.SelectedSubtotal);
        Assert.Equal(10000, result.Items.Single(x => x.HarvestBatchId == 2).UnitPrice);
        Assert.False(result.Items.Single(x => x.HarvestBatchId == 1).IsSelected);
    }

    [Fact]
    public async Task UpdateQuantityAsync_CrossesCombinedWholesaleThreshold_ReportsPriceChange()
    {
        var early = Batch(1, 10, 1);
        var late = Batch(2, 10, 2);
        foreach (var batch in new[] { early, late })
        {
            batch.Product.WholesaleMinQuantity = 10;
            batch.Product.WholesalePrice = 8000;
        }

        var repository = new TestRepository(early, late) { Cart = new Cart { UserId = 7 } };
        repository.Cart.CartItems.Add(Item(early, 3));
        repository.Cart.CartItems.Add(Item(late, 6));
        var service = Service(repository, new TestUnitOfWork());

        var wholesale = await service.UpdateQuantityAsync(7, 1, 4, default);

        Assert.True(wholesale.IsSuccess);
        Assert.NotNull(wholesale.Value);
        Assert.Equal(80000, wholesale.Value.SelectedSubtotal);
        Assert.Contains(wholesale.Value.Messages, message => message.Contains("MSG82"));

        var retail = await service.UpdateQuantityAsync(7, 1, 3, default);

        Assert.True(retail.IsSuccess);
        Assert.NotNull(retail.Value);
        Assert.Equal(90000, retail.Value.SelectedSubtotal);
        Assert.Contains(retail.Value.Messages, message => message.Contains("MSG83"));
    }

    private static Cart FullCart(int count)
    {
        var cart = new Cart { UserId = 7 };
        for (int index = 0; index < count; index++)
        {
            cart.CartItems.Add(Item(Batch(index + 100, 5, 2), 1));
        }

        return cart;
    }

    private static CartService Service(TestRepository repository, TestUnitOfWork unitOfWork,
        decimal step = 1)
    {
        var clock = new TestClock();
        var options = new QuantityOptions();
        options.Steps["kg"] = step;
        return new CartService(repository, unitOfWork,
            new CartPricingService(Options.Create(options), clock), clock);
    }

    private static HarvestBatch Batch(long id, decimal available, int? expiryDays)
    {
        var product = new Product
        {
            ProductId = 1, ProductName = "Rau", Unit = "kg", BaseRetailPrice = 10000,
            ApprovalStatus = "Approved", IsPublished = true,
            Category = new Category { IsActive = true },
            FarmerProfile = new FarmerProfile
            {
                FarmName = "Nông trại", VerificationStatus = "Approved",
                User = new User { Status = "Active" }
            }
        };
        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        return new HarvestBatch
        {
            HarvestBatchId = id, ProductId = 1, Product = product,
            BatchCode = $"B{id}", Status = "Available", AvailableQuantity = available,
            ActualHarvestDate = today.AddDays(-1), StockEnteredAt = Now.UtcDateTime,
            ExpirationDate = expiryDays.HasValue ? today.AddDays(expiryDays.Value) : null
        };
    }

    private static CartItem Item(HarvestBatch batch, decimal quantity) => new()
    {
        CartItemId = batch.HarvestBatchId, ProductId = batch.ProductId, Product = batch.Product,
        HarvestBatchId = batch.HarvestBatchId, HarvestBatch = batch,
        Quantity = quantity, IsSelected = true, AddedAt = Now.UtcDateTime
    };

    private sealed class TestClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class TestRepository(params HarvestBatch[] offers) : ICartRepository
    {
        public Cart? Cart { get; set; }
        public long RequestedUserId { get; private set; }

        public Task<Cart?> GetOwnedAsync(long userId, CancellationToken cancellationToken)
        {
            RequestedUserId = userId;
            return Task.FromResult(Cart?.UserId == userId ? Cart : null);
        }

        public Task<HarvestBatch?> GetOfferAsync(long productId, long batchId,
            CancellationToken cancellationToken) => Task.FromResult(offers.SingleOrDefault(
                x => x.ProductId == productId && x.HarvestBatchId == batchId));

        public Task<IReadOnlyList<HarvestBatch>> GetOffersAsync(long productId,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<HarvestBatch>>(
                offers.Where(x => x.ProductId == productId).ToList());

        public void Add(Cart cart) => Cart = cart;
        public void Remove(CartItem item) => Cart?.CartItems.Remove(item);
    }

    private sealed class TestUnitOfWork : IUnitOfWork
    {
        public int Begins { get; private set; }
        public int Commits { get; private set; }

        public Task BeginAsync(CancellationToken cancellationToken)
        {
            Begins++;
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            Commits++;
            return Task.CompletedTask;
        }

        public Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
