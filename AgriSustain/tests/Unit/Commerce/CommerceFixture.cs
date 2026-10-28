using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Products;
using AgriSustain.Application.DTOs.Cart;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Application.Validators;
using AgriSustain.Domain.Entities;
using Microsoft.Extensions.Options;

namespace AgriSustain.UnitTests.Commerce;

internal sealed class CommerceClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 27, 1, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class RecordingUnitOfWork : IUnitOfWork
{
    public int Begins, Flushes, Commits, Rollbacks, Attempts;
    public bool FailCommit;
    public Task BeginAsync(CancellationToken ct) { Begins++; return Task.CompletedTask; }
    public Task FlushAsync(CancellationToken ct) { Flushes++; return Task.CompletedTask; }
    public Task CommitAsync(CancellationToken ct)
    {
        Attempts++;
        if (FailCommit) throw new IOException("Injected persistence failure");
        Commits++;
        return Task.CompletedTask;
    }
    public Task RollbackAsync(CancellationToken ct) { Rollbacks++; return Task.CompletedTask; }
}

internal sealed class OwnedFiles : IFileStorageService
{
    public bool AllowImages = true, AllowEvidence = true;
    public int ImageSaves;
    public bool OwnsImage(long userId, string url) => AllowImages && url.StartsWith($"/media/{userId}/");
    public bool OwnsEvidence(long userId, string url) => AllowEvidence && url.StartsWith($"/api/files/evidence/{userId}/");
    public Task<string> SaveImageAsync(Stream content, string extension, CancellationToken ct)
    { ImageSaves++; return Task.FromResult("/media/101/uploaded" + extension); }
}

internal sealed class ProductDouble : IProductRepository
{
    public required FarmerProfile Farmer;
    public Product? Product;
    public bool CategoryActive = true, UnitLocked = false;
    public int Adds, Audits, RemovedImages;
    public const string Version = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    public Task<FarmerProfile?> GetFarmerAsync(long userId, CancellationToken ct) =>
        Task.FromResult<FarmerProfile?>(Farmer.UserId == userId ? Farmer : null);
    public Task<Product?> GetOwnedAsync(long userId, long id, CancellationToken ct) =>
        Task.FromResult(Product is { } p && p.ProductId == id && p.FarmerProfile.UserId == userId ? p : null);
    public Task<bool> IsCategoryActiveAsync(long id, CancellationToken ct) => Task.FromResult(CategoryActive && id == 201);
    public Task<bool> IsUnitLockedAsync(long id, CancellationToken ct) => Task.FromResult(UnitLocked);
    public void Add(Product product) { Adds++; product.ProductId = 302; Product = product; }
    public void RemoveImages(Product product) { RemovedImages++; product.ProductImages.Clear(); }
    public void Audit(long userId, string action, long? id, string? before, string after) { Audits++; }
    public Task<ProductOptions> GetOptionsAsync(long userId, CancellationToken ct) =>
        Task.FromResult(new ProductOptions(true, Farmer.VerificationStatus, []));
    public Task<PageResult<ProductResponse>> SearchAsync(long userId, ProductQuery query, CancellationToken ct) =>
        Task.FromResult(new PageResult<ProductResponse>(Product is null ? [] : [Map(Product)], Product is null ? 0 : 1, query.Page, query.PageSize));
    public ProductResponse Map(Product p) => new(p.ProductId, p.ProductName, p.CategoryId, "Vegetables",
        p.Unit, p.Description, p.BaseRetailPrice, p.WholesaleMinQuantity, p.WholesalePrice,
        p.ApprovalStatus, p.IsPublished, p.RejectionReason, 20, 1, UnitLocked, Version,
        p.ProductImages.Select(x => new ProductImageInput(x.ImageUrl, x.IsPrimary, x.AltText)).ToList());
}

internal sealed class CartDouble : ICartRepository
{
    public Cart? Cart;
    public Dictionary<long, HarvestBatch> Offers = [];
    public int Adds, Removals;
    public Task<Cart?> GetOwnedAsync(long userId, CancellationToken ct) =>
        Task.FromResult(Cart?.UserId == userId ? Cart : null);
    public Task<HarvestBatch?> GetOfferAsync(long productId, long batchId, CancellationToken ct) =>
        Task.FromResult(Offers.TryGetValue(batchId, out var b) && b.ProductId == productId ? b : null);
    public void Add(Cart cart) { Adds++; cart.CartId = 601; Cart = cart; }
    public void Remove(CartItem item) { Removals++; }
}

internal sealed class OrderDouble : IOrderRepository
{
    public required SellerOrder Order;
    public bool Approved = true;
    public HashSet<string> Requests = [];
    public List<InventoryMovement> Movements = [];
    public List<AuditLog> Audits = [];
    public List<DisputeEvidence> Evidence = [];
    public List<(long UserId, string Title, string Content)> Notifications = [];
    public int FinanceRequests, ReleasedHolds, SupportRequests;
    public Task<SellerOrder?> GetOwnedAsync(long userId, long id, CancellationToken ct) =>
        Task.FromResult(Order.SellerOrderId == id && Order.FarmerProfile.UserId == userId ? Order : null);
    public Task<bool> IsApprovedFarmerAsync(long userId, CancellationToken ct) => Task.FromResult(Approved);
    public Task<bool> HasRequestAsync(string key, CancellationToken ct) => Task.FromResult(Requests.Contains(key));
    public Task ReleasePurchaseHoldsAsync(SellerOrder order, CancellationToken ct) { ReleasedHolds++; return Task.CompletedTask; }
    public Task QueueFinanceReviewAsync(SellerOrder order, long actorId, CancellationToken ct) { FinanceRequests++; return Task.CompletedTask; }
    public Task NotifySupportAsync(long id, string content, CancellationToken ct) { SupportRequests++; return Task.CompletedTask; }
    public void AddMovement(InventoryMovement movement) => Movements.Add(movement);
    public void AddAudit(AuditLog audit) { Audits.Add(audit); if (audit.TraceId is not null) Requests.Add(audit.TraceId); }
    public void Notify(long id, string title, string content, long sellerOrderId) => Notifications.Add((id, title, content));
    public void AddEvidence(DisputeEvidence evidence) => Evidence.Add(evidence);
    public Task<PageResult<SellerOrderResponse>> SearchOwnedAsync(long userId, SellerOrderQuery q, CancellationToken ct) =>
        Task.FromResult(new PageResult<SellerOrderResponse>([Map(Order)], 1, q.Page, q.PageSize));
    public SellerOrderResponse Map(SellerOrder o) => new(o.SellerOrderId, o.SellerOrderCode,
        o.OrderId, o.Order.OrderCode, o.Order.OrderType, o.Order.OrderDate, o.Status, o.Order.OrderStatus,
        "Buyer", "0912345678", "Test address", null, o.Subtotal, o.DiscountShare, o.DeliveryFee,
        o.FinalAmount, "Paid", o.Order.PaymentTransactions.Sum(x => x.Amount), o.FinalAmount,
        o.DeliveredAt, o.DeliveredAt?.AddHours(48), o.ReceivedAt, FulfillmentRules.Version(o),
        false, false, false, false, [], [], [], [], o.Order.VoucherCodeSnapshot);
}

internal sealed class CommerceFixture
{
    public readonly CommerceClock Clock = new();
    public readonly RecordingUnitOfWork Work = new();
    public readonly OwnedFiles Files = new();
    public readonly FarmerProfile Farmer;
    public readonly Product Product;
    public readonly HarvestBatch Batch;
    public readonly ProductDouble Products;
    public readonly CartDouble Carts;
    public readonly OrderDouble Orders;
    public readonly ProductService ProductService;
    public readonly CartPricingService Pricing;
    public readonly CartService CartService;
    public readonly OrderFulfillmentService OrderService;
    public SellerOrder Seller => Orders.Order;
    public OrderItem Line => Seller.OrderItems.Single();
    public OrderItemBatchAllocation Allocation => Line.OrderItemBatchAllocations.Single();
    public Cart Cart => Carts.Cart!;
    public const string ImageUrl = "/media/101/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg";
    public const string EvidenceUrl = "/api/files/evidence/101/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.pdf";

    public CommerceFixture()
    {
        Farmer = new FarmerProfile
        {
            FarmerProfileId = 1,
            UserId = 101,
            FarmName = "Test Farm",
            VerificationStatus = "Approved",
            User = new User { UserId = 101, Status = "Active" }
        };
        Product = new Product
        {
            ProductId = 301,
            FarmerProfileId = 1,
            FarmerProfile = Farmer,
            CategoryId = 201,
            Category = new Category { CategoryId = 201, IsActive = true },
            ProductName = "Tomato",
            Description = "Fresh tomato",
            Unit = "kg",
            BaseRetailPrice = 30000,
            WholesaleMinQuantity = 10,
            WholesalePrice = 25000,
            ApprovalStatus = "Approved",
            IsPublished = true
        };
        Product.ProductImages.Add(new ProductImage { ImageUrl = ImageUrl, IsPrimary = true });
        Batch = NewBatch(401);
        Products = new ProductDouble { Farmer = Farmer, Product = Product };
        Carts = new CartDouble { Cart = new Cart { CartId = 601, UserId = 102 }, Offers = { [401] = Batch } };
        var parent = new Order
        {
            OrderId = 1,
            BuyerUserId = 102,
            OrderCode = "ORD-1",
            OrderType = "Retail",
            OrderStatus = "Processing",
            FinalAmount = 150000,
            TotalAmount = 150000
        };
        var seller = new SellerOrder
        {
            SellerOrderId = 501,
            OrderId = 1,
            Order = parent,
            SellerOrderCode = "SO-1",
            FarmerProfileId = 1,
            FarmerProfile = Farmer,
            Status = "Pending",
            Subtotal = 150000,
            FinalAmount = 150000
        };
        parent.SellerOrders.Add(seller);
        parent.PaymentTransactions.Add(new PaymentTransaction { Amount = 150000, PaymentStatus = "Success" });
        var line = new OrderItem
        {
            OrderItemId = 1,
            ProductId = 301,
            Product = Product,
            SellerOrder = seller,
            SellerOrderId = 501,
            ProductNameSnapshot = "Tomato",
            UnitSnapshot = "kg",
            Quantity = 5,
            UnitPriceSnapshot = 30000,
            SubTotal = 150000
        };
        line.OrderItemBatchAllocations.Add(new OrderItemBatchAllocation
        {
            OrderItemBatchAllocationId = 1,
            HarvestBatchId = 401,
            HarvestBatch = Batch,
            OrderItemId = 1,
            OrderItem = line,
            AllocatedQuantity = 5,
            AllocationStatus = "Reserved",
            ExpiresAt = Clock.Now.AddDays(1).UtcDateTime
        });
        seller.OrderItems.Add(line);
        Batch.ReservedQuantity = 5;
        Orders = new OrderDouble { Order = seller };
        ProductService = new ProductService(Products, Work, new ProductRequestValidator(), new ProductQueryValidator(), Clock, Files);
        Pricing = new CartPricingService(Options.Create(new QuantityOptions()), Clock);
        CartService = new CartService(Carts, Work, Pricing, Clock);
        OrderService = new OrderFulfillmentService(Orders, Work, new FulfillmentRules(Clock), Clock, Files);
    }
    public ProductRequest Request => new("Tomato", 201, "kg", "Fresh tomato", 30000, 10, 25000,
        [new ProductImageInput(ImageUrl, true, null)], ProductDouble.Version);
    public HarvestBatch NewBatch(long id) => new()
    {
        HarvestBatchId = id,
        ProductId = Product.ProductId,
        Product = Product,
        BatchCode = "BATCH-" + id,
        Status = "Available",
        StockEnteredAt = Clock.Now.UtcDateTime,
        ActualHarvestDate = DateOnly.FromDateTime(Clock.Now.AddDays(-1).DateTime),
        ExpirationDate = DateOnly.FromDateTime(Clock.Now.AddDays(10).DateTime),
        AvailableQuantity = 20
    };
    public CartItem AddCartLine(long id = 701, decimal quantity = 3, bool selected = true, HarvestBatch? batch = null, Product? product = null)
    {
        var item = new CartItem
        {
            CartItemId = id,
            CartId = Cart.CartId,
            Product = product ?? Product,
            ProductId = (product ?? Product).ProductId,
            HarvestBatch = batch ?? Batch,
            HarvestBatchId = (batch ?? Batch).HarvestBatchId,
            Quantity = quantity,
            IsSelected = selected
        };
        Cart.CartItems.Add(item);
        return item;
    }
    public OrderActionRequest Action(string? reason = null, string? explanation = null, string[]? evidence = null, long? requestId = null) =>
        new(FulfillmentRules.Version(Seller), Guid.NewGuid().ToString("D"), reason, explanation, evidence, requestId);
}
