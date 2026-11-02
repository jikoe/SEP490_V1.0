using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Complaints;
using AgriSustain.Application.Services.Refunds;
using AgriSustain.Application.Validators.Complaints;
using AgriSustain.Application.Validators.Refunds;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.Complaints;

/// <summary>
/// UC-CS-01 and UC-FIN-03 against SQL Server: one order with two farms, a complaint about the
/// first farm's seller order, a partial refund by item and its manual transfer.
/// </summary>
public sealed class ComplaintRefundSqlTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 10, 30, 2, 0, 0, DateTimeKind.Utc);
    private readonly string _databaseName = $"AgriSustain_UcTests_{Guid.NewGuid():N}";
    private AgriSustainContext _context = null!;
    private long _supportId;
    private long _financeId;
    private long _ticketId;
    private long _sellerOrderId;
    private long _spinachItemId;

    public async Task InitializeAsync()
    {
        _context = NewContext();
        await _context.Database.EnsureCreatedAsync();
        User NewUser(string name, string role) => new()
        {
            Username = name, Email = $"{name}@example.test", FullName = name, PasswordHash = "test-only",
            Role = role, Status = "Active", CreatedAt = Now
        };
        var buyer = NewUser("buyer", "RetailCustomer");
        var farmerA = NewUser("farmer-a", "Farmer");
        var farmerB = NewUser("farmer-b", "Farmer");
        var support = NewUser("support", "CustomerSupport");
        var finance = NewUser("finance", "FinanceStaff");
        _context.Users.AddRange(buyer, farmerA, farmerB, support, finance);
        await _context.SaveChangesAsync();
        _supportId = support.UserId;
        _financeId = finance.UserId;

        FarmerProfile Farm(User user) => new()
        {
            UserId = user.UserId, FarmName = $"{user.Username} farm", RepresentativeName = user.FullName,
            Address = "Test address", ProvinceCode = "TEST", VerificationStatus = "Approved"
        };
        var category = new Category { CategoryName = "Test produce", IsActive = true };
        var farmA = Farm(farmerA);
        var farmB = Farm(farmerB);
        _context.AddRange(category, farmA, farmB);
        await _context.SaveChangesAsync();
        Product NewProduct(FarmerProfile farm, string name) => new()
        {
            CategoryId = category.CategoryId, FarmerProfileId = farm.FarmerProfileId, ProductName = name,
            Unit = "kg", BaseRetailPrice = 15_000, ApprovalStatus = "Approved", IsPublished = true,
            CreatedAt = Now
        };
        var spinach = NewProduct(farmA, "Cải bó xôi");
        var tomato = NewProduct(farmA, "Cà chua");
        var rice = NewProduct(farmB, "Gạo");
        _context.Products.AddRange(spinach, tomato, rice);
        await _context.SaveChangesAsync();

        // 200 000 ₫ from farm A (20 000 ₫ voucher share) + 100 000 ₫ from farm B, paid 280 000 ₫.
        var order = new Order
        {
            BuyerUserId = buyer.UserId, OrderCode = "RC-TEST", OrderType = "Retail", OrderDate = Now,
            ShippingAddress = "Test address", OrderStatus = "Delivered", RecipientNameSnapshot = "Buyer",
            RecipientPhoneSnapshot = "0912345678", CheckoutRequestKey = Guid.NewGuid().ToString("D"),
            TotalAmount = 300_000, DiscountAmount = 20_000, FinalAmount = 280_000
        };
        var sellerA = new SellerOrder
        {
            FarmerProfileId = farmA.FarmerProfileId, SellerOrderCode = "SO-A", Status = "Delivered",
            Subtotal = 200_000, DiscountShare = 20_000, FinalAmount = 180_000, DeliveredAt = Now.AddHours(-2)
        };
        sellerA.OrderItems.Add(new OrderItem
        {
            ProductId = spinach.ProductId, ProductNameSnapshot = "Cải bó xôi", Quantity = 10,
            UnitSnapshot = "kg", UnitPriceSnapshot = 15_000, SubTotal = 150_000, ItemStatus = "Delivered"
        });
        sellerA.OrderItems.Add(new OrderItem
        {
            ProductId = tomato.ProductId, ProductNameSnapshot = "Cà chua", Quantity = 5,
            UnitSnapshot = "kg", UnitPriceSnapshot = 10_000, SubTotal = 50_000, ItemStatus = "Delivered"
        });
        var sellerB = new SellerOrder
        {
            FarmerProfileId = farmB.FarmerProfileId, SellerOrderCode = "SO-B", Status = "Delivered",
            Subtotal = 100_000, FinalAmount = 100_000, DeliveredAt = Now.AddHours(-2)
        };
        sellerB.OrderItems.Add(new OrderItem
        {
            ProductId = rice.ProductId, ProductNameSnapshot = "Gạo", Quantity = 5, UnitSnapshot = "kg",
            UnitPriceSnapshot = 20_000, SubTotal = 100_000, ItemStatus = "Delivered"
        });
        order.SellerOrders.Add(sellerA);
        order.SellerOrders.Add(sellerB);
        order.PaymentTransactions.Add(new PaymentTransaction
        {
            TransactionCode = "PAY-TEST", GatewayProvider = "PayOS", Amount = 280_000, PaymentType = "VietQR",
            PaymentStatus = "Success", PaymentPurpose = "Order", RequestedAt = Now.AddHours(-3),
            PaidAt = Now.AddHours(-3), RequestKey = "pay-test"
        });
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        _sellerOrderId = sellerA.SellerOrderId;
        _spinachItemId = sellerA.OrderItems.Single(item => item.ProductNameSnapshot == "Cải bó xôi").OrderItemId;

        var ticket = new DisputeTicket
        {
            SubmittedByUserId = buyer.UserId, SellerOrderId = sellerA.SellerOrderId,
            DisputeReason = "Damaged", TicketStatus = DisputeTicket.StatusOpen, CreatedAt = Now.AddHours(-1)
        };
        _context.DisputeTickets.Add(ticket);
        await _context.SaveChangesAsync();
        _ticketId = ticket.DisputeTicketId;
        _context.ChangeTracker.Clear();
    }

    public async Task DisposeAsync()
    {
        if (!_context.Database.GetDbConnection().Database.StartsWith("AgriSustain_UcTests_",
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing to delete a non-test database.");
        }

        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Detail_ListsSellerOrderItems_AndCapsRefundAtSellerOrder()
    {
        var detail = await Complaints().GetDetailAsync(_ticketId, default);

        Assert.True(detail.IsSuccess, detail.Error?.Message);
        Assert.Equal(2, detail.Value!.Items.Count);
        Assert.Equal(135_000m, detail.Value.Items.Single(item => item.OrderItemId == _spinachItemId).PaidValue);
        Assert.Equal(180_000m, detail.Value.MaxRefundAmount);          // farm A's part, not the 280 000 order
    }

    [Fact]
    public async Task Search_FlagsOverdueEvidenceRequests()
    {
        _context.DisputeEvidenceRequests.Add(new DisputeEvidenceRequest
        {
            DisputeTicketId = _ticketId, RequestedPartyUserId = _supportId, RequestedByUserId = _supportId,
            RequestedAt = Now.AddHours(-50), DueAt = Now.AddHours(-2),
            RequestStatus = DisputeTicket.EvidenceRequestPending, Instructions = "Gửi ảnh"
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var list = await Complaints().SearchAsync(new ComplaintQuery(null, null, null), default);

        Assert.True(Assert.Single(list.Value!.Items).HasOverdueEvidence);
    }

    [Fact]
    public async Task PartialRefundByItem_ThenTransfer_RefundsOnlyThatSellerOrder()
    {
        var decided = await Complaints().ResolveAsync(_ticketId, new ResolveComplaintRequest(
            ComplaintResolutions.PartialRefund, ComplaintParties.Seller,
            [new RefundItemRequest(_spinachItemId, 4)], "4 kg rau bị dập"), default);
        Assert.True(decided.IsSuccess, decided.Error?.Message);
        _context.ChangeTracker.Clear();

        var refund = await _context.RefundTransactions.SingleAsync();
        Assert.Equal(54_000m, refund.Amount);                            // 4/10 × 135 000 ₫
        Assert.Equal("RefundPending", (await _context.SellerOrders.FindAsync(_sellerOrderId))!.Status);
        Assert.Equal("Delivered", (await _context.SellerOrders.SingleAsync(x => x.SellerOrderCode == "SO-B")).Status);
        Assert.Equal("RefundPending", (await _context.Orders.SingleAsync()).OrderStatus);

        var completed = await Refunds().CompleteAsync(refund.RefundTransactionId,
            new CompleteRefundRequest(RefundMethods.BankTransfer, "VCB-TEST-1", null), default);
        Assert.True(completed.IsSuccess, completed.Error?.Message);
        _context.ChangeTracker.Clear();

        Assert.Equal("PartiallyRefunded", (await _context.SellerOrders.FindAsync(_sellerOrderId))!.Status);
        Assert.Equal("PartiallyRefunded", (await _context.Orders.SingleAsync()).OrderStatus);
        Assert.Equal(DisputeTicket.StatusClosed, (await _context.DisputeTickets.SingleAsync()).TicketStatus);
        Assert.Equal("Success", (await _context.PaymentTransactions.SingleAsync()).PaymentStatus);
    }

    [Fact]
    public async Task FullRefund_ThenTransfer_MarksSellerOrderRefunded()
    {
        var decided = await Complaints().ResolveAsync(_ticketId, new ResolveComplaintRequest(
            ComplaintResolutions.FullRefund, ComplaintParties.Seller, null, "Toàn bộ hàng hỏng"), default);
        Assert.True(decided.IsSuccess, decided.Error?.Message);
        _context.ChangeTracker.Clear();
        var refund = await _context.RefundTransactions.SingleAsync();
        Assert.Equal(180_000m, refund.Amount);

        await Refunds().CompleteAsync(refund.RefundTransactionId,
            new CompleteRefundRequest(RefundMethods.BankTransfer, "VCB-TEST-2", null), default);
        _context.ChangeTracker.Clear();

        Assert.Equal("Refunded", (await _context.SellerOrders.FindAsync(_sellerOrderId))!.Status);
        Assert.Equal("PartiallyRefunded", (await _context.Orders.SingleAsync()).OrderStatus);
    }

    private ComplaintResolutionService Complaints() => new(new ComplaintRepository(_context),
        new Actor(_supportId), new ResolveComplaintRequestValidator(),
        new RequestComplaintInfoRequestValidator(), new FixedClock());

    private RefundProcessingService Refunds() => new(new RefundRepository(_context),
        new Actor(_financeId), new CompleteRefundRequestValidator(),
        new RefundTransferFailedRequestValidator(), new FixedClock());

    private AgriSustainContext NewContext()
    {
        var server = Environment.GetEnvironmentVariable("AGRISUSTAIN_TEST_SQL_SERVER")
            ?? "(localdb)\\MSSQLLocalDB";
        return new AgriSustainContext(new DbContextOptionsBuilder<AgriSustainContext>()
            .UseSqlServer($"Server={server};Database={_databaseName};Integrated Security=True;"
                + "TrustServerCertificate=True;Connect Timeout=30").Options);
    }

    private sealed class Actor(long userId) : IActorContext
    {
        public long UserId => userId;
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
}
