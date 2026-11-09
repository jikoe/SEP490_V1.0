using AgriSustain.Application.DTOs.B2B;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.B2B;
using AgriSustain.Domain.Entities;
using Xunit;

namespace AgriSustain.UnitTests.B2B;

public sealed class WholesaleOrderServiceTests
{
    private sealed class FakeWholesaleOrderRepository : IWholesaleOrderRepository
    {
        public readonly Dictionary<long, Product> Products = new();
        public readonly Dictionary<long, WholesaleBuyerProfile> BuyerProfiles = new();
        public readonly Dictionary<long, User> Users = new();
        public readonly Dictionary<long, List<HarvestBatch>> ProductBatches = new();
        public readonly List<Order> Orders = new();
        public readonly List<Notification> Notifications = new();

        public Task<Product?> GetWholesaleProductAsync(long productId, CancellationToken cancellationToken)
        {
            Products.TryGetValue(productId, out var product);
            return Task.FromResult(product);
        }

        public Task<WholesaleBuyerProfile?> GetBuyerKycProfileAsync(long userId, CancellationToken cancellationToken)
        {
            BuyerProfiles.TryGetValue(userId, out var profile);
            return Task.FromResult(profile);
        }

        public Task<User?> GetUserByIdAsync(long userId, CancellationToken cancellationToken)
        {
            Users.TryGetValue(userId, out var user);
            return Task.FromResult(user);
        }

        public Task<IReadOnlyList<HarvestBatch>> GetAvailableHarvestBatchesAsync(long productId, CancellationToken cancellationToken)
        {
            if (ProductBatches.TryGetValue(productId, out var batches))
            {
                var available = batches.Where(b => b.AvailableQuantity > 0).ToList();
                return Task.FromResult<IReadOnlyList<HarvestBatch>>(available);
            }
            return Task.FromResult<IReadOnlyList<HarvestBatch>>(new List<HarvestBatch>());
        }

        public Task<Order> CreateOrderAsync(Order order, CancellationToken cancellationToken)
        {
            order.OrderId = Orders.Count + 1;
            Orders.Add(order);
            return Task.FromResult(order);
        }

        public Task AddNotificationAsync(Notification notification, CancellationToken cancellationToken)
        {
            notification.NotificationId = Notifications.Count + 1;
            Notifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task<Order?> GetOrderByCodeAsync(string orderCode, CancellationToken cancellationToken)
        {
            var order = Orders.FirstOrDefault(o => o.OrderCode == orderCode);
            return Task.FromResult(order);
        }

        public Task<IReadOnlyList<Order>> GetBuyerWholesaleOrdersAsync(long buyerUserId, CancellationToken cancellationToken)
        {
            var list = Orders.Where(o => buyerUserId <= 0 || o.BuyerUserId == buyerUserId).ToList();
            return Task.FromResult<IReadOnlyList<Order>>(list);
        }

        public Task<Order?> GetWholesaleOrderForPaymentAsync(string orderCode, CancellationToken cancellationToken)
        {
            var order = Orders.FirstOrDefault(o => o.OrderCode == orderCode);
            return Task.FromResult(order);
        }

        public Task AddPaymentTransactionAsync(PaymentTransaction transaction, CancellationToken cancellationToken)
        {
            var order = Orders.FirstOrDefault(o => o.OrderId == transaction.OrderId);
            order?.PaymentTransactions.Add(transaction);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private static (FakeWholesaleOrderRepository Repo, Product Product, User Buyer, WholesaleBuyerProfile Kyc) SetupTestData()
    {
        var repo = new FakeWholesaleOrderRepository();

        var farmerUser = new User { UserId = 10, FullName = "Nguyễn Văn Nông", Email = "farmer@example.com" };
        var farmerProfile = new FarmerProfile
        {
            FarmerProfileId = 1,
            UserId = 10,
            FarmName = "Nông Trại Rau Sạch Đà Lạt",
            RepresentativeName = "Nguyễn Văn Nông",
            Address = "Xuân Thọ, Đà Lạt, Lâm Đồng",
            User = farmerUser,
            FarmCertificates = new List<FarmCertificate>
            {
                new() { CertificateType = "VietGAP", VerificationStatus = "Verified" }
            }
        };

        var product = new Product
        {
            ProductId = 100,
            ProductName = "Cà Chua Beef Xuất Khẩu",
            Unit = "kg",
            BaseRetailPrice = 45000m,
            WholesalePrice = 30000m,
            WholesaleMinQuantity = 100m,
            ApprovalStatus = "Approved",
            IsPublished = true,
            FarmerProfileId = 1,
            FarmerProfile = farmerProfile,
            Category = new Category { CategoryId = 1, CategoryName = "Rau củ quả" }
        };
        repo.Products[100] = product;

        var batch1 = new HarvestBatch
        {
            HarvestBatchId = 201,
            ProductId = 100,
            BatchCode = "BATCH-CC-01",
            AvailableQuantity = 500m,
            Status = "InStock",
            ActualHarvestDate = new DateOnly(2026, 11, 1),
            StockEnteredAt = new DateTime(2026, 11, 1)
        };
        var batch2 = new HarvestBatch
        {
            HarvestBatchId = 202,
            ProductId = 100,
            BatchCode = "BATCH-CC-02",
            AvailableQuantity = 1000m,
            Status = "InStock",
            ActualHarvestDate = new DateOnly(2026, 11, 3),
            StockEnteredAt = new DateTime(2026, 11, 3)
        };
        repo.ProductBatches[100] = new List<HarvestBatch> { batch1, batch2 };

        var buyerUser = new User
        {
            UserId = 20,
            FullName = "Trần Doanh Nghiệp",
            Email = "buyer@corp.vn",
            PhoneNumber = "0912345678",
            Status = "Active",
            Role = "WholesaleBuyer"
        };
        repo.Users[20] = buyerUser;

        var buyerKyc = new WholesaleBuyerProfile
        {
            WholesaleBuyerProfileId = 5,
            UserId = 20,
            CompanyName = "Công Ty Thực Phẩm Xanh Á Châu",
            TaxCode = "0312345678",
            RepresentativeName = "Trần Doanh Nghiệp",
            BusinessAddress = "Khu Công Nghiệp Tân Bình, TP. Hồ Chí Minh",
            VerificationStatus = "Approved"
        };
        repo.BuyerProfiles[20] = buyerKyc;

        return (repo, product, buyerUser, buyerKyc);
    }

    [Fact]
    public async Task GetWholesaleProductInfoAsync_ApprovedKyc_ReturnsFullInfoAndTiers()
    {
        var (repo, _, _, _) = SetupTestData();
        var service = new WholesaleOrderService(repo);

        var result = await service.GetWholesaleProductInfoAsync(100, 20, default);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("Cà Chua Beef Xuất Khẩu", result.Data!.ProductName);
        Assert.Equal(100m, result.Data.WholesaleMinQuantity);
        Assert.Equal(30000m, result.Data.WholesalePrice);
        Assert.Equal(1500m, result.Data.AvailableStock);
        Assert.True(result.Data.BuyerKyc.IsApprovedKyc);
        Assert.Equal("Công Ty Thực Phẩm Xanh Á Châu", result.Data.BuyerKyc.CompanyName);
        Assert.Equal(3, result.Data.PriceTiers.Count);
    }

    [Fact]
    public async Task GetWholesaleProductInfoAsync_ProductNotWholesaleEligible_Fails()
    {
        var (repo, product, _, _) = SetupTestData();
        product.WholesalePrice = null; // Disable wholesale
        var service = new WholesaleOrderService(repo);

        var result = await service.GetWholesaleProductInfoAsync(100, 20, default);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task CalculatePricingAsync_Tier1_ReturnsBaseWholesalePrice()
    {
        var (repo, _, _, _) = SetupTestData();
        var service = new WholesaleOrderService(repo);

        var request = new WholesalePriceCalculationRequest(100, 150m); // between 100 and 200
        var result = await service.CalculatePricingAsync(request, default);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.IsValidQuantity);
        Assert.Equal(30000m, result.Data.AppliedUnitPrice);
        Assert.Equal(0m, result.Data.TierDiscountPercent);
        Assert.Equal(4500000m, result.Data.LineSubtotal); // 150 * 30,000
    }

    [Fact]
    public async Task CalculatePricingAsync_Tier2_Returns4PercentDiscount()
    {
        var (repo, _, _, _) = SetupTestData();
        var service = new WholesaleOrderService(repo);

        var request = new WholesalePriceCalculationRequest(100, 300m); // between 200 and 500
        var result = await service.CalculatePricingAsync(request, default);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.IsValidQuantity);
        Assert.Equal(4m, result.Data.TierDiscountPercent);
        Assert.Equal(28800m, result.Data.AppliedUnitPrice); // 30,000 * 0.96
        Assert.Equal(8640000m, result.Data.LineSubtotal);
    }

    [Fact]
    public async Task CalculatePricingAsync_Tier3_Returns8PercentDiscount()
    {
        var (repo, _, _, _) = SetupTestData();
        var service = new WholesaleOrderService(repo);

        var request = new WholesalePriceCalculationRequest(100, 600m); // >= 500
        var result = await service.CalculatePricingAsync(request, default);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.IsValidQuantity);
        Assert.Equal(8m, result.Data.TierDiscountPercent);
        Assert.Equal(27600m, result.Data.AppliedUnitPrice); // 30,000 * 0.92
    }

    [Fact]
    public async Task CalculatePricingAsync_QuantityBelowMoq_AF1_ReturnsInvalid()
    {
        var (repo, _, _, _) = SetupTestData();
        var service = new WholesaleOrderService(repo);

        var request = new WholesalePriceCalculationRequest(100, 50m); // MOQ is 100
        var result = await service.CalculatePricingAsync(request, default);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.IsValidQuantity);
        Assert.NotNull(result.Data.ErrorMessage);
        Assert.Contains("100", result.Data.ErrorMessage);
    }

    [Fact]
    public async Task CalculatePricingAsync_QuantityExceedingStock_AF1_ReturnsInvalid()
    {
        var (repo, _, _, _) = SetupTestData();
        var service = new WholesaleOrderService(repo);

        var request = new WholesalePriceCalculationRequest(100, 2000m); // Available is 1500
        var result = await service.CalculatePricingAsync(request, default);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.IsValidQuantity);
        Assert.NotNull(result.Data.ErrorMessage);
        Assert.Contains("1,500", result.Data.ErrorMessage);
    }

    [Fact]
    public async Task CreateWholesaleOrderAsync_ValidRequest_CreatesOrderAndReservesBatches()
    {
        var (repo, _, _, _) = SetupTestData();
        var service = new WholesaleOrderService(repo);

        var request = new CreateWholesaleOrderRequest(
            ProductId: 100,
            Quantity: 600m, // will take 500 from batch1, 100 from batch2
            WarehouseAddress: "Kho Tổng A1, Cụm Logistics Sóng Thần, Bình Dương",
            ProvinceCode: "74",
            ProvinceName: "Bình Dương",
            DistrictCode: null,
            DistrictName: null,
            WardCode: null,
            WardName: null,
            RecipientName: "Trần Doanh Nghiệp",
            RecipientPhone: "0912345678",
            CompanyName: "Công Ty Thực Phẩm Xanh Á Châu",
            TaxCode: "0312345678",
            ExpectedDeliveryDate: DateTime.UtcNow.AddDays(2),
            PaymentMethod: "BankTransfer",
            OrderNotes: "Yêu cầu xe lạnh bảo quản dưới 12 độ C",
            RequestKey: "REQ-001");

        var result = await service.CreateWholesaleOrderAsync(request, 20, default);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("PendingConfirmation", result.Data!.OrderStatus);
        Assert.StartsWith("WS-", result.Data.OrderCode);
        Assert.Equal(600m, result.Data.Quantity);
        Assert.Equal(2, result.Data.BatchAllocations.Count); // 2 batches allocated
        Assert.Equal(500m, result.Data.BatchAllocations[0].AllocatedQuantity);
        Assert.Equal(100m, result.Data.BatchAllocations[1].AllocatedQuantity);

        // Verify batches reserved in repo
        var batches = repo.ProductBatches[100];
        Assert.Equal(0m, batches[0].AvailableQuantity); // 500 - 500
        Assert.Equal(500m, batches[0].ReservedQuantity);
        Assert.Equal(900m, batches[1].AvailableQuantity); // 1000 - 100
        Assert.Equal(100m, batches[1].ReservedQuantity);

        // Verify notifications sent to farmer and buyer
        Assert.Equal(2, repo.Notifications.Count);
        Assert.Contains(repo.Notifications, n => n.UserId == 10 && n.DeliveryChannel == "Email");
        Assert.Contains(repo.Notifications, n => n.UserId == 20 && n.DeliveryChannel == "InApp");
    }

    [Fact]
    public async Task CreateWholesaleOrderAsync_KycNotApproved_Precondition2_Fails()
    {
        var (repo, _, _, buyerKyc) = SetupTestData();
        buyerKyc.VerificationStatus = "PendingReview"; // Not approved
        var service = new WholesaleOrderService(repo);

        var request = new CreateWholesaleOrderRequest(
            ProductId: 100,
            Quantity: 200m,
            WarehouseAddress: "Kho Tân Bình",
            ProvinceCode: null, ProvinceName: null, DistrictCode: null, DistrictName: null, WardCode: null, WardName: null,
            RecipientName: "Trần Doanh Nghiệp",
            RecipientPhone: "0912345678",
            CompanyName: "Công Ty Thực Phẩm Xanh",
            TaxCode: "0312345678",
            ExpectedDeliveryDate: null,
            PaymentMethod: "BankTransfer",
            OrderNotes: null,
            RequestKey: null);

        var result = await service.CreateWholesaleOrderAsync(request, 20, default);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Contains("KYC", result.Message);
    }

    [Fact]
    public async Task CreateWholesaleOrderAsync_MissingDeliveryOrInvoice_AF4_Fails()
    {
        var (repo, _, _, _) = SetupTestData();
        var service = new WholesaleOrderService(repo);

        var request = new CreateWholesaleOrderRequest(
            ProductId: 100,
            Quantity: 200m,
            WarehouseAddress: "", // Missing
            ProvinceCode: null, ProvinceName: null, DistrictCode: null, DistrictName: null, WardCode: null, WardName: null,
            RecipientName: "Trần Doanh Nghiệp",
            RecipientPhone: "0912345678",
            CompanyName: "", // Missing
            TaxCode: "0312345678",
            ExpectedDeliveryDate: null,
            PaymentMethod: "BankTransfer",
            OrderNotes: null,
            RequestKey: null);

        var result = await service.CreateWholesaleOrderAsync(request, 20, default);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("đầy đủ thông tin", result.Message);
    }

    [Fact]
    public async Task CreateWholesaleOrderAsync_StockOut_AF3_Fails()
    {
        var (repo, _, _, _) = SetupTestData();
        repo.ProductBatches[100].Clear(); // Depleted
        var service = new WholesaleOrderService(repo);

        var request = new CreateWholesaleOrderRequest(
            ProductId: 100,
            Quantity: 200m,
            WarehouseAddress: "Kho Tân Bình",
            ProvinceCode: null, ProvinceName: null, DistrictCode: null, DistrictName: null, WardCode: null, WardName: null,
            RecipientName: "Trần Doanh Nghiệp",
            RecipientPhone: "0912345678",
            CompanyName: "Công Ty Xanh",
            TaxCode: "0312345678",
            ExpectedDeliveryDate: null,
            PaymentMethod: "BankTransfer",
            OrderNotes: null,
            RequestKey: null);

        var result = await service.CreateWholesaleOrderAsync(request, 20, default);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("hết hàng", result.Message);
    }
}

