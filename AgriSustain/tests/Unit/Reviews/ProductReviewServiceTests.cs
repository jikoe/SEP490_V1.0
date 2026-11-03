using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.UnitTests.DuIte1;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgriSustain.UnitTests.Reviews;

/// <summary>UC-RC-05 unit tests of services, validation and domain rules.</summary>
[Trait("UseCase", "UC-RC-05")]
public sealed class ProductReviewServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private readonly FeedbackStore _store = new();

    private OrderFeedbackService Service() => new(_store, _store, _store, _store, _store.Clock,
        NullLogger<OrderFeedbackService>.Instance);

    private static readonly ReviewSubmission Valid = new(5, "  Fresh and delicious  ");

    // ---------- UC-RC-05: OrderFeedbackService.ReviewAsync ----------

    [Fact(DisplayName = "[UC-RC-05 #001] Save a verified approved review and commit once")]
    public async Task ReviewAsync_Create_SavesVerifiedReview()
    {
        var result = await Service().ReviewAsync(20, 1, 11, Valid, default);

        Assert.True(result.IsSuccess);
        var review = Assert.Single(_store.Reviews);

        Assert.Equal(20, review.UserId);
        Assert.Equal(11, review.OrderItemId);
        Assert.True(review.IsPurchasedVerified);
        Assert.Equal("Approved", review.ModerationStatus);
        Assert.Equal("Fresh and delicious", review.Comment);
        Assert.Equal(_store.Clock.Now.UtcDateTime, review.ReviewDate);
        Assert.Equal(1, _store.Commits);
    }

    [Fact(DisplayName = "[UC-RC-05 #002] Accept the minimum rating")]
    public async Task ReviewAsync_MinimumRating_AcceptsMinimumRating()
    {
        var response = await Service().ReviewAsync(20, 1, 11, Valid with { Rating = 1 }, default);

        Assert.True(response.IsSuccess);
        Assert.Equal(1, _store.Reviews.Single().RatingStars);
    }

    [Fact(DisplayName = "[UC-RC-05 #003] Reject before saving or committing")]
    public async Task ReviewAsync_RatingZero_RejectsWithoutSave()
    {
        var response = await Service().ReviewAsync(20, 1, 11, Valid with { Rating = 0 }, default);

        Assert.Equal("INVALID_REVIEW", response.Error!.Code);
        Assert.Equal(0, _store.Saves);
        Assert.Equal(0, _store.Commits);
    }

    [Fact(DisplayName = "[UC-RC-05 #004] Reject rating above five")]
    public async Task ReviewAsync_RatingSix_RejectsRating()
    {
        var response = await Service().ReviewAsync(20, 1, 11, Valid with { Rating = 6 }, default);

        Assert.False(response.IsSuccess);
    }

    [Fact(DisplayName = "[UC-RC-05 #005] Require meaningful review text")]
    public async Task ReviewAsync_BlankComment_RequiresComment()
    {
        var response = await Service().ReviewAsync(20, 1, 11, Valid with { Comment = " \t " }, default);

        Assert.False(response.IsSuccess);
        Assert.Empty(_store.Reviews);
    }

    [Fact(DisplayName = "[UC-RC-05 #006] Accept maximum comment length")]
    public async Task ReviewAsync_MaximumComment_AcceptsMaximumLength()
    {
        var response = await Service().ReviewAsync(20, 1, 11, Valid with { Comment = new string('a', 1000) }, default);

        Assert.True(response.IsSuccess);
    }

    [Fact(DisplayName = "[UC-RC-05 #007] Reject overlong comment")]
    public async Task ReviewAsync_OverlongComment_RejectsOverlongComment()
    {
        var response = await Service().ReviewAsync(20, 1, 11, Valid with { Comment = new string('a', 1001) }, default);

        Assert.False(response.IsSuccess);
    }

    [Fact(DisplayName = "[UC-RC-05 #008] Reject cross-buyer access without saving")]
    public async Task ReviewAsync_WrongBuyer_RejectsWithoutSave()
    {
        var response = await Service().ReviewAsync(21, 1, 11, Valid, default);

        Assert.Equal("REVIEW_NOT_ELIGIBLE", response.Error!.Code);
        Assert.Equal(0, _store.Saves);
    }

    [Fact(DisplayName = "[UC-RC-05 #009] Reject an item outside the order")]
    public async Task ReviewAsync_UnknownItem_RejectsUnknownItem()
    {
        var response = await Service().ReviewAsync(20, 1, 99, Valid, default);

        Assert.False(response.IsSuccess);
    }

    [Fact(DisplayName = "[UC-RC-05 #010] Require completed and received order")]
    public async Task ReviewAsync_DeliveredNotCompleted_RejectsIneligibleStatus()
    {
        _store.Deliver();
        var response = await Service().ReviewAsync(20, 1, 11, Valid, default);

        Assert.False(response.IsSuccess);
    }

    [Fact(DisplayName = "[UC-RC-05 #011] Reject missing receipt confirmation")]
    public async Task ReviewAsync_NotReceived_RejectsMissingReceipt()
    {
        _store.Item.SellerOrder.ReceivedAt = null;
        var response = await Service().ReviewAsync(20, 1, 11, Valid, default);

        Assert.False(response.IsSuccess);
    }

    [Fact(DisplayName = "[UC-RC-05 #012] Reject cancelled product line")]
    public async Task ReviewAsync_CancelledItem_RejectsCancelledLine()
    {
        _store.Item.ItemStatus = "Cancelled";
        var response = await Service().ReviewAsync(20, 1, 11, Valid, default);

        Assert.False(response.IsSuccess);
    }

    [Fact(DisplayName = "[UC-RC-05 #013] Reject duplicate creation")]
    public async Task ReviewAsync_Duplicate_RejectsDuplicate()
    {
        _store.ExistingReview();
        var response = await Service().ReviewAsync(20, 1, 11, Valid, default);

        Assert.Equal("REVIEW_NOT_ELIGIBLE", response.Error!.Code);
        Assert.Empty(_store.Reviews);
    }

    // ---------- UC-RC-05: OrderFeedbackService.UpdateReviewAsync ----------

    [Fact(DisplayName = "[UC-RC-05 #014] Update existing record without changing creation date")]
    public async Task UpdateReviewAsync_Edit_UpdatesExistingReview()
    {
        _store.ExistingReview();
        var date = _store.Item.ProductReview!.ReviewDate;
        var result = await Service().UpdateReviewAsync(20, 1, 11, Valid, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value!.Id);
        Assert.Equal(5, _store.Item.ProductReview.RatingStars);
        Assert.Equal(date, _store.Item.ProductReview.ReviewDate);
        Assert.Empty(_store.Reviews);
        Assert.Equal(1, _store.Commits);
    }

    [Fact(DisplayName = "[UC-RC-05 #015] Reject editing another user's review")]
    public async Task UpdateReviewAsync_EditOtherReview_RejectsAnotherUserReview()
    {
        _store.ExistingReview(21);
        var response = await Service().UpdateReviewAsync(20, 1, 11, Valid, default);

        Assert.False(response.IsSuccess);
        Assert.Equal("Original comment", _store.Item.ProductReview!.Comment);
    }

    // ---------- UC-RC-05: OrderFeedbackService.ReviewAsync ----------

    [Fact(DisplayName = "[UC-RC-05 #016] Return conflict and do not commit")]
    public async Task ReviewAsync_ConcurrentReview_ReturnsConflict()
    {
        _store.SaveError = new InvalidOperationException("duplicate");
        var response = await Service().ReviewAsync(20, 1, 11, Valid, default);

        Assert.Equal("REVIEW_CONFLICT", response.Error!.Code);
        Assert.Equal(0, _store.Commits);
        Assert.Equal(1, _store.UncommittedDisposals);
    }

    [Fact(DisplayName = "[UC-RC-05 #017] Return save failure without successful commit")]
    public async Task ReviewAsync_SaveFailure_ReturnsSaveFailure()
    {
        _store.SaveError = new IOException("offline");
        var response = await Service().ReviewAsync(20, 1, 11, Valid, default);

        Assert.Equal("REVIEW_SAVE_FAILED", response.Error!.Code);
        Assert.Equal(0, _store.Commits);
    }

    // ---------- UC-RC-05: OrderFeedbackService.GetContextAsync ----------

    [Fact(DisplayName = "[UC-RC-05 #018] Expose edit action and existing review but disable create")]
    public async Task GetContextAsync_EditContext_ExposesEditAction()
    {
        _store.ExistingReview();
        var result = await Service().GetContextAsync(20, 1, default);
        var item = Assert.Single(result.Value!.Items);

        Assert.True(item.CanEditReview);
        Assert.False(item.CanReview);
        Assert.Equal(4, item.Review!.Rating);
    }

    // ---------- Test doubles ----------

    private sealed class FeedbackStore : IOrderFeedbackRepository, IOrderFeedbackStorage,
        IUseCaseUnitOfWork, IPersistenceErrorClassifier
    {
        public FakeClock Clock { get; } = new(T0);
        public Order Order { get; } = new()
        {
            OrderId = 1,
            OrderCode = "UT-ORDER-1",
            BuyerUserId = 20,
            OrderStatus = "Completed",
            OrderDate = new DateTime(2026, 11, 1),
            RecipientNameSnapshot = "Unit Buyer",
            RecipientPhoneSnapshot = "0900000000",
            ShippingAddress = "Unit address",
            FinalAmount = 100000
        };
        public List<ProductReview> Reviews { get; } = [];
        public List<DisputeTicket> Tickets { get; } = [];
        public List<string> SavedFiles { get; } = [];
        public List<string> DeletedFiles { get; } = [];
        public Exception? SaveError { get; set; }
        public int UploadFailureAt { get; set; }
        public int Saves { get; private set; }
        public int Commits { get; private set; }
        public int UncommittedDisposals { get; private set; }

        public FeedbackStore() => AddItem(11, 1);

        public OrderItem AddItem(long id, long sellerId)
        {
            var seller = Order.SellerOrders.SingleOrDefault(s => s.SellerOrderId == sellerId);
            if (seller == null)
            {
                seller = new SellerOrder
                {
                    SellerOrderId = sellerId,
                    OrderId = 1,
                    Order = Order,
                    SellerOrderCode = $"UT-SO-{sellerId}",
                    SellerNameSnapshot = $"Farm {sellerId}",
                    Status = "Completed",
                    ReceivedAt = Clock.Now.UtcDateTime,
                    DeliveredAt = Clock.Now.UtcDateTime.AddHours(-1),
                    FarmerProfile = new FarmerProfile { VerificationStatus = "Approved" }
                };
                Order.SellerOrders.Add(seller);
            }
            var item = new OrderItem
            {
                OrderItemId = id,
                ProductId = id,
                ProductNameSnapshot = $"Product {id}",
                Product = new Product
                {
                    ProductId = id,
                    ProductName = $"Product {id}",
                    Category = new Category { CategoryName = "Vegetables" }
                },
                SellerOrderId = sellerId,
                SellerOrder = seller,
                Quantity = 2,
                UnitSnapshot = "kg",
                UnitPriceSnapshot = 25000,
                SubTotal = 50000,
                ItemStatus = "Normal"
            };
            seller.OrderItems.Add(item);
            return item;
        }

        public OrderItem Item => Order.SellerOrders.First().OrderItems.First();
        public void Deliver()
        {
            Order.OrderStatus = "Delivered";
            foreach (var seller in Order.SellerOrders)
            {
                seller.Status = "Delivered";
                seller.ReceivedAt = null;
            }
        }

        public void ExistingReview(long userId = 20)
        {
            Item.ProductReview = new ProductReview
            {
                ProductReviewId = 100,
                OrderItemId = Item.OrderItemId,
                UserId = userId,
                RatingStars = 4,
                Comment = "Original comment",
                IsPurchasedVerified = true,
                ReviewDate = Clock.Now.UtcDateTime.AddDays(-1),
                ModerationStatus = "Approved"
            };
        }

        public Task<Order?> GetOwnedOrderAsync(long userId, long orderId, CancellationToken ct) =>
            Task.FromResult<Order?>(Order.BuyerUserId == userId && Order.OrderId == orderId ? Order : null);
        public Task<Dictionary<long, string>> GetImagesAsync(long[] ids, CancellationToken ct) =>
            Task.FromResult(new Dictionary<long, string>());
        public void Add(ProductReview review) { review.ProductReviewId = 101; Reviews.Add(review); }
        public void Add(DisputeTicket ticket) { ticket.DisputeTicketId = 200 + Tickets.Count; Tickets.Add(ticket); }
        public Task<ITransaction> BeginAsync(CancellationToken ct) =>
            Task.FromResult<ITransaction>(new Transaction(this));
        public Task SaveChangesAsync(CancellationToken ct)
        {
            Saves++;
            if (SaveError != null) throw SaveError;
            return Task.CompletedTask;
        }
        public Task<string> SaveAsync(long userId, FeedbackFile file, CancellationToken ct)
        {
            if (UploadFailureAt == SavedFiles.Count + 1) throw new InvalidDataException("Invalid image signature.");
            var url = $"/api/orders/feedback-files/{userId}/{SavedFiles.Count + 1}.png";
            SavedFiles.Add(url);
            return Task.FromResult(url);
        }
        public void Delete(long userId, IEnumerable<string> urls) => DeletedFiles.AddRange(urls);
        public FeedbackDownload? OpenEvidence(long userId, string key) => null;
        public bool IsConflict(Exception exception) => exception is InvalidOperationException;

        private sealed class Transaction(FeedbackStore owner) : ITransaction
        {
            private bool committed;
            public Task CommitAsync(CancellationToken ct)
            {
                owner.Commits++;
                committed = true;
                return Task.CompletedTask;
            }
            public ValueTask DisposeAsync()
            {
                if (!committed) owner.UncommittedDisposals++;
                return ValueTask.CompletedTask;
            }
        }
    }
}
