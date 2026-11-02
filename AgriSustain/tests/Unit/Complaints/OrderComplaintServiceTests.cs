using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.UnitTests.DuIte1;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgriSustain.UnitTests.Complaints;

/// <summary>UC-RC-06 unit tests of services, validation and domain rules.</summary>
[Trait("UseCase", "UC-RC-06")]
public sealed class OrderComplaintServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private readonly FeedbackStore _store = new();

    private OrderFeedbackService Service() => new(_store, _store, _store, _store, _store.Clock,
        NullLogger<OrderFeedbackService>.Instance);

    private FeedbackStore Eligible()
    {
        _store.Deliver(); return _store;
    }
    private static ComplaintSubmission Request(params long[] ids) => new(ids, "Damaged", "  Packaging was damaged.  ");
    private static FeedbackFile Photo(long length = 100) => new("evidence.png", length, Stream.Null);

    // ---------- UC-RC-06: OrderFeedbackService.ComplainAsync ----------

    [Fact(DisplayName = "[UC-RC-06 #001] Create open ticket with buyer text and selected item metadata")]
    public async Task ComplainAsync_SingleProduct_CreatesOpenTicket()
    {
        Eligible();
        var result = await Service().ComplainAsync(20, 1, Request(11), [], default);

        Assert.True(result.IsSuccess);
        var ticket = Assert.Single(_store.Tickets);

        Assert.Equal("Open", ticket.TicketStatus);
        Assert.Equal("Damaged", ticket.DisputeReason);
        Assert.Contains("#11): Product 11", ticket.Description);
        Assert.EndsWith("\n\nPackaging was damaged.", ticket.Description);
        Assert.DoesNotContain("Số lượng", ticket.Description);
        Assert.Equal(1, _store.Commits);
    }

    [Fact(DisplayName = "[UC-RC-06 #002] Create one ticket containing both selected products")]
    public async Task ComplainAsync_SameSeller_GroupsSameSeller()
    {
        Eligible();
        _store.AddItem(12, 1);
        var response = await Service().ComplainAsync(20, 1, Request(12, 11), [], default);

        Assert.True(response.IsSuccess);
        var text = Assert.Single(_store.Tickets).Description!;

        Assert.Contains("#11):", text);
        Assert.Contains("#12):", text);
        Assert.True(text.IndexOf("#11", StringComparison.Ordinal) < text.IndexOf("#12", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "[UC-RC-06 #003] Create one ticket per seller with shared evidence and one commit")]
    public async Task ComplainAsync_DifferentSellers_SeparatesSellerTickets()
    {
        Eligible();
        _store.AddItem(12, 2);
        _store.Deliver();
        var result = await Service().ComplainAsync(20, 1, Request(11, 12), [Photo()], default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Ids.Count);
        Assert.Equal(2, _store.Tickets.Count);
        Assert.Equal(1, _store.Saves);
        Assert.Equal(1, _store.Commits);
        Assert.All(_store.Tickets, t => Assert.Equal(_store.SavedFiles.Single(), Assert.Single(t.DisputeEvidences).FileUrl));
        Assert.Empty(_store.DeletedFiles);
    }

    [Fact(DisplayName = "[UC-RC-06 #004] Reject empty selection without saving")]
    public async Task ComplainAsync_EmptySelection_RejectsEmptySelection()
    {
        Eligible();
        var response = await Service().ComplainAsync(20, 1, Request(), [], default);

        Assert.Equal("INVALID_COMPLAINT", response.Error!.Code);
        Assert.Equal(0, _store.Saves);
    }

    [Fact(DisplayName = "[UC-RC-06 #005] Reject duplicate selection")]
    public async Task ComplainAsync_DuplicateIds_RejectsDuplicateIds()
    {
        Eligible();
        var response = await Service().ComplainAsync(20, 1, Request(11, 11), [], default);

        Assert.False(response.IsSuccess);
    }

    [Fact(DisplayName = "[UC-RC-06 #006] Reject access to another buyer's order")]
    public async Task ComplainAsync_WrongBuyer_RejectsAnotherBuyer()
    {
        Eligible();
        var response = await Service().ComplainAsync(21, 1, Request(11), [], default);

        Assert.Equal("COMPLAINT_NOT_ELIGIBLE", response.Error!.Code);
        Assert.Empty(_store.Tickets);
    }

    [Fact(DisplayName = "[UC-RC-06 #007] Reject entire selection before uploading evidence")]
    public async Task ComplainAsync_UnknownSelectedItem_RejectsEntireSelection()
    {
        Eligible();
        var response = await Service().ComplainAsync(20, 1, Request(11, 99), [Photo()], default);

        Assert.False(response.IsSuccess);
        Assert.Empty(_store.Tickets);
        Assert.Empty(_store.SavedFiles);
        Assert.Equal(0, _store.Commits);
    }

    // ---------- UC-RC-06: OrderFeedbackRules.CanComplain ----------

    [Fact(DisplayName = "[UC-RC-06 #008] Accept the inclusive 48-hour deadline")]
    public async Task CanComplain_ExactDeadline_AcceptsDeadline()
    {
        Eligible();
        _store.Item.SellerOrder.DeliveredAt = _store.Clock.Now.UtcDateTime.AddHours(-48);
        var response = await Service().ComplainAsync(20, 1, Request(11), [], default);

        Assert.True(response.IsSuccess);
    }

    [Fact(DisplayName = "[UC-RC-06 #009] Reject expired complaint window")]
    public async Task CanComplain_AfterDeadline_RejectsExpiredWindow()
    {
        Eligible();
        _store.Item.SellerOrder.DeliveredAt = _store.Clock.Now.UtcDateTime.AddHours(-48).AddTicks(-1);
        var response = await Service().ComplainAsync(20, 1, Request(11), [], default);

        Assert.False(response.IsSuccess);
    }

    [Fact(DisplayName = "[UC-RC-06 #010] Reject a future delivery timestamp")]
    public async Task CanComplain_FutureDelivery_RejectsFutureDelivery()
    {
        Eligible();
        _store.Item.SellerOrder.DeliveredAt = _store.Clock.Now.UtcDateTime.AddTicks(1);
        var response = await Service().ComplainAsync(20, 1, Request(11), [], default);

        Assert.False(response.IsSuccess);
    }

    [Fact(DisplayName = "[UC-RC-06 #011] Accept completed order within 48 hours after delivery")]
    public async Task CanComplain_CompletedOrder_AcceptsWithinDeliveryWindow()
    {
        var response = await Service().ComplainAsync(20, 1, Request(11), [], default);

        Assert.True(response.IsSuccess);
        Assert.Equal("Open", Assert.Single(_store.Tickets).TicketStatus);
        Assert.Equal(1, _store.Commits);
    }

    // ---------- UC-RC-06: OrderFeedbackRules.DeliveredAt ----------

    [Fact(DisplayName = "[UC-RC-06 #012] Use delivery history for complaint eligibility")]
    public async Task DeliveredAt_HistoryFallback_UsesDeliveryHistory()
    {
        Eligible();
        _store.Item.SellerOrder.DeliveredAt = null;
        _store.Item.SellerOrder.OrderStatusHistories.Add(new() { NewStatus = "Delivered", OccurredAt = _store.Clock.Now.UtcDateTime.AddHours(-1) });
        var response = await Service().ComplainAsync(20, 1, Request(11), [], default);

        Assert.True(response.IsSuccess);
    }

    // ---------- UC-RC-06: OrderFeedbackRules.CanComplain ----------

    [Fact(DisplayName = "[UC-RC-06 #013] Block another active complaint for the same item")]
    public async Task CanComplain_ActiveComplaint_BlocksDuplicateComplaint()
    {
        Eligible();
        _store.Item.SellerOrder.DisputeTickets.Add(new() { TicketStatus = "Open", Description = OrderFeedbackRules.DescribeComplaint([_store.Item], "Damaged") });
        var response = await Service().ComplainAsync(20, 1, Request(11), [], default);

        Assert.False(response.IsSuccess);
    }

    [Fact(DisplayName = "[UC-RC-06 #014] Permit a complaint for an unaffected line")]
    public async Task CanComplain_OtherLineComplaint_AllowsUnaffectedLine()
    {
        Eligible();
        var other = _store.AddItem(12, 1);
        _store.Item.SellerOrder.DisputeTickets.Add(new() { TicketStatus = "Reviewing", Description = OrderFeedbackRules.DescribeComplaint([other], "Text mentions #11 but not metadata") });
        var response = await Service().ComplainAsync(20, 1, Request(11), [], default);

        Assert.True(response.IsSuccess);
    }

    [Fact(DisplayName = "[UC-RC-06 #015] Treat legacy complaint as covering the whole seller order")]
    public async Task CanComplain_LegacyComplaint_BlocksWholeSellerOrder()
    {
        Eligible();
        _store.Item.SellerOrder.DisputeTickets.Add(new() { TicketStatus = "Open", Description = "Legacy text" });
        var response = await Service().ComplainAsync(20, 1, Request(11), [], default);

        Assert.False(response.IsSuccess);
    }

    // ---------- UC-RC-06: OrderFeedbackService.ComplainAsync ----------

    [Fact(DisplayName = "[UC-RC-06 #016] Accept maximum description length")]
    public async Task ComplainAsync_MaximumDescription_AcceptsMaximumLength()
    {
        Eligible();
        var response = await Service().ComplainAsync(20, 1, Request(11) with { Description = new string('a', 500) }, [], default);

        Assert.True(response.IsSuccess);
    }

    [Fact(DisplayName = "[UC-RC-06 #017] Reject overlong description")]
    public async Task ComplainAsync_OverlongDescription_RejectsOverlongDescription()
    {
        Eligible();
        var response = await Service().ComplainAsync(20, 1, Request(11) with { Description = new string('a', 501) }, [], default);

        Assert.False(response.IsSuccess);
    }

    [Fact(DisplayName = "[UC-RC-06 #018] Reject an unsupported reason")]
    public async Task ComplainAsync_InvalidReason_RejectsUnknownReason()
    {
        Eligible();
        var response = await Service().ComplainAsync(20, 1, Request(11) with { Reason = "Unknown" }, [], default);

        Assert.False(response.IsSuccess);
    }

    // ---------- UC-RC-06: OrderFeedbackValidator.AreFilesValid ----------

    [Fact(DisplayName = "[UC-RC-06 #019] Accept attachment count and size boundaries")]
    public async Task AreFilesValid_MaximumEvidence_AcceptsAttachmentLimits()
    {
        Eligible();
        var response = await Service().ComplainAsync(20, 1, Request(11), Enumerable.Range(1, 5).Select(_ => Photo(5 * 1024 * 1024)).ToArray(), default);

        Assert.True(response.IsSuccess);
        Assert.Equal(5, _store.SavedFiles.Count);
    }

    [Fact(DisplayName = "[UC-RC-06 #020] Reject before any upload")]
    public async Task AreFilesValid_TooManyImages_RejectsBeforeUpload()
    {
        Eligible();
        var response = await Service().ComplainAsync(20, 1, Request(11), Enumerable.Range(1, 6).Select(_ => Photo()).ToArray(), default);

        Assert.Equal("INVALID_EVIDENCE", response.Error!.Code);
        Assert.Empty(_store.SavedFiles);
    }

    [Fact(DisplayName = "[UC-RC-06 #021] Reject an oversized image")]
    public async Task AreFilesValid_OversizedImage_RejectsOversizedFile()
    {
        Eligible();
        var response = await Service().ComplainAsync(20, 1, Request(11), [Photo(5 * 1024 * 1024 + 1)], default);

        Assert.False(response.IsSuccess);
    }

    // ---------- UC-RC-06: OrderFeedbackService.ComplainAsync ----------

    [Fact(DisplayName = "[UC-RC-06 #022] Delete saved evidence and do not commit")]
    public async Task ComplainAsync_UploadFailure_CleansSavedFiles()
    {
        Eligible();
        _store.UploadFailureAt = 2;
        var response = await Service().ComplainAsync(20, 1, Request(11), [Photo(), Photo()], default);

        Assert.Equal("INVALID_EVIDENCE", response.Error!.Code);
        Assert.Equal(_store.SavedFiles, _store.DeletedFiles);
        Assert.Equal(0, _store.Commits);
    }

    [Fact(DisplayName = "[UC-RC-06 #023] Return save error, clean files and leave transaction uncommitted")]
    public async Task ComplainAsync_SaveFailure_CleansWithoutCommit()
    {
        Eligible();
        _store.SaveError = new IOException("offline");
        var response = await Service().ComplainAsync(20, 1, Request(11), [Photo()], default);

        Assert.Equal("COMPLAINT_SAVE_FAILED", response.Error!.Code);
        Assert.Equal(_store.SavedFiles, _store.DeletedFiles);
        Assert.Equal(0, _store.Commits);
        Assert.Equal(1, _store.UncommittedDisposals);
    }

    [Fact(DisplayName = "[UC-RC-06 #024] Propagate cancellation, clean saved files and do not commit")]
    public async Task ComplainAsync_Cancellation_PropagatesAndCleansFiles()
    {
        Eligible();
        _store.SaveError = new OperationCanceledException();
        await Assert.ThrowsAsync<OperationCanceledException>(() => Service().ComplainAsync(20, 1, Request(11), [Photo()], default));

        Assert.Equal(_store.SavedFiles, _store.DeletedFiles);
        Assert.Equal(0, _store.Commits);
    }

    // ---------- UC-RC-06: Completed orders ----------

    [Fact(DisplayName = "[UC-RC-06 #025] Completing an order does not restart the complaint deadline")]
    public async Task ComplainAsync_CompletedAfterDeadline_RejectsExpiredWindow()
    {
        _store.Item.SellerOrder.DeliveredAt = T0.UtcDateTime.AddHours(-48).AddTicks(-1);
        _store.Item.SellerOrder.ReceivedAt = T0.UtcDateTime;

        var result = await Service().ComplainAsync(20, 1, Request(11), [], default);

        Assert.Equal("COMPLAINT_NOT_ELIGIBLE", result.Error?.Code);
        Assert.Empty(_store.Tickets);
        Assert.Equal(0, _store.Commits);
    }

    [Fact(DisplayName = "[UC-RC-06 #026] A completed order still requires a delivery timestamp")]
    public async Task ComplainAsync_CompletedWithoutDelivery_RejectsMissingTimestamp()
    {
        _store.Item.SellerOrder.DeliveredAt = null;

        var result = await Service().ComplainAsync(20, 1, Request(11), [], default);

        Assert.Equal("COMPLAINT_NOT_ELIGIBLE", result.Error?.Code);
        Assert.Empty(_store.Tickets);
    }

    [Fact(DisplayName = "[UC-RC-06 #027] Completed orders cannot duplicate an active line complaint")]
    public async Task ComplainAsync_CompletedWithActiveComplaint_RejectsDuplicate()
    {
        _store.Item.SellerOrder.DisputeTickets.Add(new DisputeTicket
        {
            TicketStatus = "Open",
            Description = OrderFeedbackRules.DescribeComplaint([_store.Item], "Damaged")
        });

        var result = await Service().ComplainAsync(20, 1, Request(11), [], default);

        Assert.Equal("COMPLAINT_NOT_ELIGIBLE", result.Error?.Code);
        Assert.Empty(_store.Tickets);
    }

    [Fact(DisplayName = "[UC-RC-06 #028] Completed orders remain restricted to their buyer")]
    public async Task ComplainAsync_CompletedOwnedByAnotherBuyer_RejectsAccess()
    {
        var result = await Service().ComplainAsync(21, 1, Request(11), [], default);

        Assert.Equal("COMPLAINT_NOT_ELIGIBLE", result.Error?.Code);
        Assert.Empty(_store.Tickets);
    }

    [Fact(DisplayName = "[UC-RC-06 #029] Submit complaints for multiple products in a completed order")]
    public async Task ComplainAsync_CompletedMultipleSellers_CreatesSellerTickets()
    {
        _store.AddItem(12, 2);

        var result = await Service().ComplainAsync(20, 1, Request(11, 12), [], default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Ids.Count);
        Assert.Equal(2, _store.Tickets.Count);
        Assert.Equal(1, _store.Commits);
    }

    [Fact(DisplayName = "[UC-RC-06 #030] Feedback context enables complaint and review for an eligible completed order")]
    public async Task GetContextAsync_CompletedWithinWindow_ExposesComplaintAction()
    {
        var result = await Service().GetContextAsync(20, 1, default);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.True(item.CanComplain);
        Assert.True(item.CanReview);
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
