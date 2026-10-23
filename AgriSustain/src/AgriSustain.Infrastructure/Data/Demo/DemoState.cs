using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Domain.Entities.Accounts;
using AgriSustain.Domain.Entities.Catalog;
using AgriSustain.Domain.Entities.Ordering;
using AgriSustain.Domain.ValueObjects;

namespace AgriSustain.Infrastructure.Data.Demo;

// DEMO-PERSISTENCE: synthetic fixtures, never imported from the supplied SQL database.
// This snapshot is process-local and is lost on restart. It is not an EF InMemory test
// and cannot establish SQL Server constraints, isolation or durable idempotency.
// Replacement: implement the repository contracts with a single scoped SQL transaction.
internal sealed class DemoState
{
    public Dictionary<long, UserAccount> Accounts { get; init; } = [];
    public Dictionary<long, Category> Categories { get; init; } = [];
    // DEMO-CATALOG: IDs referenced by RegionalPriceBenchmark/CommissionPolicy.
    public HashSet<long> CategoryPolicyReferences { get; init; } = [];
    public Dictionary<long, ProductOffer> Products { get; init; } = [];
    public Dictionary<long, HarvestBatch> Batches { get; init; } = [];
    public Dictionary<long, List<CartLine>> Carts { get; init; } = [];
    public Dictionary<long, int> Points { get; init; } = [];
    public Dictionary<long, VoucherOffer> Vouchers { get; init; } = [];
    public Dictionary<Guid, CheckoutQuote> Quotes { get; init; } = [];
    public Dictionary<long, RetailOrder> Orders { get; init; } = [];
    public Dictionary<long, AddressSnapshot[]> Addresses { get; init; } = [];
    public List<AuditRecord> Audits { get; init; } = [];
    public long NextCategoryId { get; set; } = 10;
    public long NextOrderId { get; set; } = 1;

    /// <summary>
    /// Deep-copies mutable entities and collections so a failed operation or escaped callback
    /// reference cannot mutate the committed demo store.
    /// </summary>
    public DemoState Copy() => new()
    {
        Accounts = Accounts.ToDictionary(pair => pair.Key, pair => pair.Value.Copy()),
        Categories = Categories.ToDictionary(pair => pair.Key, pair => pair.Value.Copy()),
        CategoryPolicyReferences = new(CategoryPolicyReferences),
        Products = new(Products),
        Batches = Batches.ToDictionary(pair => pair.Key, pair => pair.Value.Copy()),
        Carts = Carts.ToDictionary(pair => pair.Key, pair => pair.Value.ToList()),
        Points = new(Points),
        Vouchers = new(Vouchers),
        Quotes = new(Quotes),
        Orders = new(Orders),
        Addresses = Addresses.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()),
        Audits = new(Audits),
        NextCategoryId = NextCategoryId,
        NextOrderId = NextOrderId
    };

    /// <summary>
    /// Builds synthetic actors, offers, product-based cart rows, batches, addresses and benefits
    /// for local demonstrations. It never imports or writes business SQL data.
    /// </summary>
    public static DemoState Seed(DateTimeOffset now)
    {
        var state = new DemoState();
        state.Accounts[1] = User(1, "Quản trị viên demo", "SystemAdmin", now);
        state.Accounts[2] = User(2, "Quản trị viên dự phòng", "SystemAdmin", now);
        state.Accounts[10] = User(10, "Khách mua demo", "RetailCustomer", now);
        state.Accounts[11] = User(11, "Khách mua đối chứng", "RetailCustomer", now);
        state.Accounts[12] = UserAccount.RestoreLock(
            User(12, "Tài khoản khóa tạm", "RetailCustomer", now), now.AddHours(4), 5);
        state.Accounts[13] = User(13, "Hỗ trợ khách hàng demo", "CustomerSupport", now);
        state.Accounts[20] = User(20, "HTX Rau Xanh", "Farmer", now);
        state.Accounts[21] = User(21, "Nông trại Mùa Vàng", "Farmer", now);
        state.Accounts[30] = User(30, "Doanh nghiệp thu mua", "WholesaleBuyer", now);
        state.Accounts[31] = User(31, "Tài khoản tạm ngưng", "RetailCustomer", now);
        state.Accounts[31].Suspend("Dữ liệu mẫu phục vụ UC-ADM-03");
        state.Categories[1] = Category(1, "Rau củ", null);
        state.Categories[2] = Category(2, "Trái cây", null);
        state.Categories[3] = Category(3, "Rau ăn lá", 1);
        state.Categories[4] = Category(4, "Danh mục chưa dùng", null);
        // DEMO-CATALOG (catalog team): these are read-only offers, not product CRUD.
        // Map SellerId from FarmerProfileId and SellerUserId from FarmerProfile.UserId;
        // their equal fixture values must NOT become a production identity assumption.
        state.Products[101] = new ProductOffer(
            101, 3, 20, "HTX Rau Xanh", "Cải ngọt hữu cơ", "kg", 30000, 25000, 5,
            SellerUserId: 20);
        state.Products[102] = new ProductOffer(
            102, 2, 21, "Nông trại Mùa Vàng", "Cam sành", "kg", 40000, 35000, 10,
            SellerUserId: 21);
        state.Products[103] = new ProductOffer(
            103, 1, 20, "HTX Rau Xanh", "Cà rốt", "kg", 20000, null, null,
            SellerUserId: 20);
        state.Batches[201] = Batch(201, 101, "RAU-DEMO-01", now);
        state.Batches[202] = Batch(202, 102, "CAM-DEMO-01", now);
        state.Batches[203] = Batch(203, 103, "CAROT-DEMO-01", now);
        // DEMO-CART: UC-RC-01 supplies selected product IDs/quantities, no batch IDs.
        // Checkout allocates eligible batches FEFO; the cart module is not implemented here.
        state.Carts[10] = [new(1, 101, 5, true), new(2, 102, 2, true),
            new(3, 103, 1, false)];
        state.Carts[11] = [new(4, 101, 5, true)];
        // DEMO-REWARDS: available balances only, not a loyalty ledger. The loyalty
        // adapter must supply durable order reservations and its own release lifecycle.
        state.Points[10] = 1000;
        state.Points[11] = 300;
        // DEMO-VOUCHER (promotion team): synthetic offers and process-local quota.
        // ReservedCount is a fixture projection, not an existing Voucher SQL column;
        // derive/lock active VoucherRedemption reservations in the real adapter.
        state.Vouchers[1] = new VoucherOffer(
            1, "WELCOME10", null, "Percent", 10, 50000, 100000, 100, 0, 0,
            now.AddDays(-1), now.AddDays(30), true);
        state.Vouchers[2] = new VoucherOffer(
            2, "RAUXANH20", 20, "Fixed", 20000, null, 100000, 20, 0, 0,
            now.AddDays(-1), now.AddDays(30), true);
        // DEMO-ADDRESS: synthetic saved addresses, scoped by RetailCustomer ID.
        // Replace with owned DeliveryAddress rows; names are normalized from the real catalog.
        state.Addresses[10] = [Address("12", "268"), Address("11", "267")];
        state.Addresses[11] = [Address("12", "268")];
        return state;
    }

    /// <summary>
    /// Creates a synthetic account with a stable identity and role. Example contact details are
    /// fixtures, not credentials or copied personal data.
    /// </summary>
    private static UserAccount User(long id, string name, string role, DateTimeOffset now) => new()
    {
        Id = id,
        Name = name,
        Role = role,
        Email = $"user{id}@example.test",
        Phone = "0901234567",
        OrganizationName = role == "Farmer" ? name : null,
        VerificationStatus = role == "Farmer" ? "Approved" : null,
        CreatedAt = now.AddDays(-10)
    };

    /// <summary>
    /// Builds a fixture category using only the approved persisted fields; has no image, archive,
    /// creation-date or Version assumption.
    /// </summary>
    private static Category Category(long id, string name, long? parentId)
    {
        var category = new Category { Id = id };
        category.Update(name, parentId, null, 0, true);
        return category;
    }

    /// <summary>
    /// Creates an available fixture lot with a known expiry date. It stands in for the
    /// catalog/inventory module read model, not a batch-management UC.
    /// </summary>
    private static HarvestBatch Batch(long id, long productId, string code, DateTimeOffset now) =>
        new(20)
        {
            Id = id,
            ProductId = productId,
            Code = code,
            ExpirationDate = DateOnly.FromDateTime(now.AddDays(30).DateTime)
        };

    /// <summary>
    /// Creates buyer-owned fixture address codes; the real local JSON catalog normalizes the
    /// province/ward names when quoting.
    /// </summary>
    private static AddressSnapshot Address(string provinceCode, string wardCode) => new(
        "Khách mua demo", "0901234567",
        provinceCode == "12" ? "Thành phố Hồ Chí Minh" : "Thành phố Hà Nội",
        wardCode == "268" ? "Xã Thạnh An" : "Xã Minh Châu", "123 Đường Mẫu",
        provinceCode, wardCode);
}
