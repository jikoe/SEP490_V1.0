using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Complaints;
using AgriSustain.Application.Validators.Complaints;
using AgriSustain.Domain.Entities;
using AgriSustain.UnitTests.DuIte1;
using Xunit;

namespace AgriSustain.UnitTests.Complaints;

/// <summary>UC-CS-01 Resolve Complaint.</summary>
public sealed class ComplaintResolutionServiceTests
{
    private const long SupportId = 90;
    private const long BuyerId = 10;
    private const long FarmerUserId = 20;
    private const long FinanceId = 91;
    private static readonly DateTimeOffset T0 = new(2026, 10, 27, 1, 0, 0, TimeSpan.Zero);

    private readonly FakeClock _clock = new(T0);
    private readonly ComplaintStore _store = new();

    private ComplaintResolutionService Service() => new(
        _store, new Actor(), new ResolveComplaintRequestValidator(),
        new RequestComplaintInfoRequestValidator(), _clock);

    private static DisputeTicket Ticket(string paymentStatus = "Success", decimal paid = 300_000m)
    {
        var order = new Order
        {
            OrderId = 1, OrderCode = "ORD-1", BuyerUserId = BuyerId, OrderStatus = "Delivered",
            TotalAmount = 300_000m, FinalAmount = 300_000m
        };
        order.PaymentTransactions.Add(new PaymentTransaction
        {
            PaymentTransactionId = 5, Amount = paid, PaymentStatus = paymentStatus
        });
        // Seller order 3 is this farm's 200 000 ₫ share of a 300 000 ₫ order with another farm.
        var sellerOrder = new SellerOrder
        {
            SellerOrderId = 3, SellerOrderCode = "SO-3", Status = "Delivered", Subtotal = 200_000m,
            FinalAmount = 200_000m, Order = order, FarmerProfile = new FarmerProfile { UserId = FarmerUserId }
        };
        sellerOrder.OrderItems.Add(new OrderItem
        {
            OrderItemId = 31, ProductNameSnapshot = "Cải bó xôi", Quantity = 10m, UnitSnapshot = "kg",
            UnitPriceSnapshot = 15_000m, SubTotal = 150_000m
        });
        sellerOrder.OrderItems.Add(new OrderItem
        {
            OrderItemId = 32, ProductNameSnapshot = "Cà chua", Quantity = 5m, UnitSnapshot = "kg",
            UnitPriceSnapshot = 10_000m, SubTotal = 50_000m
        });
        order.SellerOrders.Add(sellerOrder);
        order.SellerOrders.Add(new SellerOrder
        {
            SellerOrderId = 4, SellerOrderCode = "SO-4", Status = "Delivered", FinalAmount = 100_000m, Order = order
        });
        return new DisputeTicket
        {
            DisputeTicketId = 7, SubmittedByUserId = BuyerId, DisputeReason = "Damaged",
            TicketStatus = DisputeTicket.StatusOpen, SellerOrder = sellerOrder, SellerOrderId = 3
        };
    }

    private static ResolveComplaintRequest Decision(string resolution,
        IReadOnlyList<RefundItemRequest>? items = null,
        string fault = ComplaintParties.Seller, string reason = "Hàng dập nát theo ảnh") =>
        new(resolution, fault, items, reason);

    private static RefundItemRequest[] Items(params (long Id, decimal Quantity)[] lines) =>
        lines.Select(line => new RefundItemRequest(line.Id, line.Quantity)).ToArray();

    [Fact(DisplayName = "UTC-CS01-01 Full refund creates a Pending refund request and RefundPending order")]
    public async Task Resolve_FullRefund_CreatesRefundRequest()
    {
        var ticket = _store.Ticket = Ticket();

        var result = await Service().ResolveAsync(7, Decision(ComplaintResolutions.FullRefund), default);

        Assert.True(result.IsSuccess);
        var refund = Assert.Single(_store.Refunds);
        Assert.Equal(200_000m, refund.Amount);                      // this farm's seller order only
        Assert.Equal(RefundTransaction.StatusPending, refund.RefundStatus);
        Assert.Equal("complaint:7", refund.RequestKey);
        Assert.Equal(DisputeTicket.StatusRefundPending, ticket.TicketStatus);
        Assert.Equal(ComplaintParties.Seller, ticket.FaultParty);
        Assert.Equal(SupportId, ticket.AssignedStaffUserId);
        Assert.Equal("RefundPending", ticket.SellerOrder!.Status);
        Assert.Equal("RefundPending", ticket.SellerOrder.Order.OrderStatus);
        Assert.Equal("Delivered", ticket.SellerOrder.Order.SellerOrders.Single(x => x.SellerOrderId == 4).Status);
        Assert.Single(ticket.SellerOrder.OrderStatusHistories);
        Assert.Contains(_store.Notifications, n => n.UserId == FinanceId);
        Assert.Contains(_store.Notifications, n => n.UserId == BuyerId);
        Assert.Contains(_store.Notifications, n => n.UserId == FarmerUserId);
        Assert.Equal("ResolveComplaint", Assert.Single(_store.Audits).Action);
        Assert.Equal(1, _store.Saves);
    }

    [Theory(DisplayName = "UTC-CS01-02 Partial refund is calculated from the selected items and quantities")]
    [InlineData(31, 4, 60_000)]                // 4 of 10 kg × 150 000 ₫
    [InlineData(31, 10, 150_000)]
    [InlineData(32, 2.5, 25_000)]
    public async Task Resolve_PartialRefund_CalculatedFromItems(long itemId, decimal quantity, decimal expected)
    {
        _store.Ticket = Ticket();

        var result = await Service().ResolveAsync(
            7, Decision(ComplaintResolutions.PartialRefund, Items((itemId, quantity))), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, Assert.Single(_store.Refunds).Amount);
        Assert.Equal(expected, _store.Ticket!.RefundAmount);
    }

    [Theory(DisplayName = "UTC-CS01-03 Partial refund items must belong to the seller order and stay within delivered quantity")]
    [InlineData(31, 10.001)]                   // more than delivered
    [InlineData(99, 1)]                        // not in this seller order
    [InlineData(31, 0)]                        // quantity must be positive
    public async Task Resolve_PartialRefund_InvalidItems(long itemId, decimal quantity)
    {
        _store.Ticket = Ticket();

        var result = await Service().ResolveAsync(
            7, Decision(ComplaintResolutions.PartialRefund, Items((itemId, quantity))), default);

        Assert.False(result.IsSuccess);
        Assert.Empty(_store.Refunds);
        Assert.Equal(DisputeTicket.StatusOpen, _store.Ticket!.TicketStatus);
    }

    [Fact(DisplayName = "UTC-CS01-03b Partial refund requires items; full refund must not send any")]
    public async Task Resolve_ItemRules()
    {
        _store.Ticket = Ticket();

        var missing = await Service().ResolveAsync(7, Decision(ComplaintResolutions.PartialRefund), default);
        var extra = await Service().ResolveAsync(
            7, Decision(ComplaintResolutions.FullRefund, Items((31, 1))), default);

        Assert.Contains("items", missing.Error!.Fields!.Keys);
        Assert.Contains("items", extra.Error!.Fields!.Keys);
        Assert.Equal(0, _store.Saves);
    }

    [Fact(DisplayName = "UTC-CS01-04 Reject keeps the order Delivered and creates no refund")]
    public async Task Resolve_Reject_KeepsOrder()
    {
        var ticket = _store.Ticket = Ticket();

        var result = await Service().ResolveAsync(
            7, Decision(ComplaintResolutions.RejectClaim, fault: ComplaintParties.Buyer), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(DisputeTicket.StatusRejected, ticket.TicketStatus);
        Assert.Equal("Delivered", ticket.SellerOrder!.Status);
        Assert.Equal("Delivered", ticket.SellerOrder.Order.OrderStatus);
        Assert.Empty(_store.Refunds);
        Assert.DoesNotContain(_store.Notifications, n => n.UserId == FinanceId);
    }

    [Theory(DisplayName = "UTC-CS01-05 Missing resolution, fault party or reason is rejected")]
    [InlineData("Refund", ComplaintParties.Seller, "ok", "resolution")]
    [InlineData(ComplaintResolutions.RejectClaim, "Platform", "ok", "faultParty")]
    [InlineData(ComplaintResolutions.RejectClaim, ComplaintParties.Seller, "  ", "reason")]
    public async Task Resolve_InvalidInput_Rejected(string resolution, string fault, string reason,
        string field)
    {
        _store.Ticket = Ticket();

        var result = await Service().ResolveAsync(7, new(resolution, fault, null, reason), default);

        Assert.Contains(field, result.Error!.Fields!.Keys);
        Assert.Equal(DisputeTicket.StatusOpen, _store.Ticket.TicketStatus);
    }

    [Fact(DisplayName = "UTC-CS01-06 A decided complaint cannot be decided again")]
    public async Task Resolve_Twice_Conflict()
    {
        _store.Ticket = Ticket();
        await Service().ResolveAsync(7, Decision(ComplaintResolutions.FullRefund), default);

        var second = await Service().ResolveAsync(7, Decision(ComplaintResolutions.RejectClaim), default);

        Assert.Equal(409, second.StatusCode);
        Assert.Single(_store.Refunds);
    }

    [Fact(DisplayName = "UTC-CS01-07 Refund needs a captured payment with remaining balance")]
    public async Task Resolve_NoCapturedPayment_Conflict()
    {
        var ticket = _store.Ticket = Ticket(paymentStatus: "Pending");

        var result = await Service().ResolveAsync(7, Decision(ComplaintResolutions.FullRefund), default);

        Assert.Equal("NO_REFUNDABLE_PAYMENT", result.Error!.Code);
        Assert.Equal(DisputeTicket.StatusOpen, ticket.TicketStatus);
        Assert.Equal("Delivered", ticket.SellerOrder!.Status);
    }

    [Fact(DisplayName = "UTC-CS01-08 Earlier refunds reduce the refundable amount")]
    public async Task Resolve_EarlierRefunds_ReduceLimit()
    {
        var ticket = _store.Ticket = Ticket(paid: 250_000m);
        ticket.SellerOrder!.Order.PaymentTransactions.Single().RefundTransactions.Add(
            new RefundTransaction { Amount = 100_000m, RefundStatus = RefundTransaction.StatusPending });

        var tooMuch = await Service().ResolveAsync(
            7, Decision(ComplaintResolutions.PartialRefund, Items((31, 10), (32, 5))), default);
        var full = await Service().ResolveAsync(7, Decision(ComplaintResolutions.FullRefund), default);

        Assert.Equal("REFUND_AMOUNT_EXCEEDED", tooMuch.Error!.Code);  // 200 000 > 150 000 left on payment
        Assert.True(full.IsSuccess);
        Assert.Equal(150_000m, _store.Refunds.Single().Amount);       // 250 000 paid − 100 000 refunded
    }

    [Fact(DisplayName = "UTC-CS01-13 Refund cap is what the buyer paid for this seller order after its discount share")]
    public async Task Resolve_CapIsSellerOrderFinalAmount()
    {
        var ticket = _store.Ticket = Ticket();
        ticket.SellerOrder!.DiscountShare = 20_000m;                  // voucher share of this farm
        ticket.SellerOrder.FinalAmount = 180_000m;

        var partial = await Service().ResolveAsync(
            7, Decision(ComplaintResolutions.PartialRefund, Items((31, 10))), default);

        Assert.True(partial.IsSuccess);
        Assert.Equal(135_000m, _store.Refunds.Single().Amount);       // 150 000 − 15 000 discount share
    }

    [Fact(DisplayName = "UTC-CS01-13b Earlier refunds for the same seller order reduce its cap")]
    public async Task Resolve_EarlierSellerOrderRefunds_ReduceCap()
    {
        var ticket = _store.Ticket = Ticket();
        var earlier = new DisputeTicket { DisputeTicketId = 6, SellerOrderId = 3, SellerOrder = ticket.SellerOrder };
        ticket.SellerOrder!.Order.PaymentTransactions.Single().RefundTransactions.Add(new RefundTransaction
        {
            Amount = 50_000m, RefundStatus = RefundTransaction.StatusCompleted, DisputeTicket = earlier
        });

        var result = await Service().ResolveAsync(7, Decision(ComplaintResolutions.FullRefund), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(150_000m, _store.Refunds.Single().Amount);       // 200 000 − 50 000 already refunded
    }

    [Fact(DisplayName = "UTC-CS01-09 Request information opens a 48-hour request and notifies the party")]
    public async Task RequestInformation_CreatesRequest()
    {
        var ticket = _store.Ticket = Ticket();

        var result = await Service().RequestInformationAsync(
            7, new(ComplaintParties.Seller, "Gửi ảnh đóng gói lúc giao"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(DisputeTicket.StatusWaitingInfo, ticket.TicketStatus);
        var request = Assert.Single(ticket.DisputeEvidenceRequests);
        Assert.Equal(FarmerUserId, request.RequestedPartyUserId);
        Assert.Equal(T0.UtcDateTime.AddHours(48), request.DueAt);
        Assert.Equal(DisputeTicket.EvidenceRequestPending, request.RequestStatus);
        Assert.Equal(FarmerUserId, Assert.Single(_store.Notifications).UserId);
    }

    [Fact(DisplayName = "UTC-CS01-10 A second open request to the same party is refused")]
    public async Task RequestInformation_Duplicate_Conflict()
    {
        _store.Ticket = Ticket();
        await Service().RequestInformationAsync(7, new(ComplaintParties.Buyer, "Ảnh sản phẩm"), default);

        var again = await Service().RequestInformationAsync(7, new(ComplaintParties.Buyer, "Thêm ảnh"), default);
        _clock.Advance(TimeSpan.FromHours(49));
        var afterDue = await Service().RequestInformationAsync(7, new(ComplaintParties.Buyer, "Lần 2"), default);

        Assert.Equal("INFO_REQUEST_PENDING", again.Error!.Code);
        Assert.True(afterDue.IsSuccess);
    }

    [Fact(DisplayName = "UTC-CS01-11 Resolving closes open information requests")]
    public async Task Resolve_ClosesOpenRequests()
    {
        var ticket = _store.Ticket = Ticket();
        await Service().RequestInformationAsync(7, new(ComplaintParties.Buyer, "Ảnh sản phẩm"), default);

        await Service().ResolveAsync(7, Decision(ComplaintResolutions.RejectClaim), default);

        Assert.Equal(DisputeTicket.EvidenceRequestClosed, ticket.DisputeEvidenceRequests.Single().RequestStatus);
    }

    [Fact(DisplayName = "UTC-CS01-12 Unknown complaint returns 404")]
    public async Task Resolve_Unknown_NotFound()
    {
        var result = await Service().ResolveAsync(99, Decision(ComplaintResolutions.RejectClaim), default);

        Assert.Equal(404, result.StatusCode);
    }

    private async Task<DisputeTicket> AskAsync(DisputeTicket ticket, params string[] parties)
    {
        _store.Ticket = ticket;
        foreach (string party in parties)
        {
            await Service().RequestInformationAsync(7, new(party, "Gửi thêm ảnh"), default);
        }

        _store.Notifications.Clear();
        _store.Audits.Clear();
        return ticket;
    }

    [Fact(DisplayName = "UTC-CS01-14 Seller silent for 48 hours: automatic full refund (Evidence Overdue)")]
    public async Task Overdue_Seller_FullRefund()
    {
        var ticket = await AskAsync(Ticket(), ComplaintParties.Seller);
        _clock.Advance(TimeSpan.FromHours(48).Add(TimeSpan.FromMinutes(1)));

        int resolved = await Service().ResolveOverdueAsync(default);

        Assert.Equal(1, resolved);
        Assert.Equal(DisputeTicket.StatusRefundPending, ticket.TicketStatus);
        Assert.Equal(ComplaintParties.Seller, ticket.FaultParty);
        var refund = Assert.Single(_store.Refunds);
        Assert.Equal(200_000m, refund.Amount);
        Assert.Equal(SupportId, refund.ProcessedByUserId);               // the staff who asked
        Assert.Equal(DisputeTicket.EvidenceRequestExpired, ticket.DisputeEvidenceRequests.Single().RequestStatus);
        var audit = Assert.Single(_store.Audits);
        Assert.Equal("AutoResolveComplaint", audit.Action);
        Assert.Null(audit.PerformedByUserId);                            // decided by the system
        Assert.StartsWith("Evidence Overdue", audit.Reason);
    }

    [Theory(DisplayName = "UTC-CS01-15 Buyer silent, or both silent: automatic rejection")]
    [InlineData(new[] { ComplaintParties.Buyer }, ComplaintParties.Buyer)]
    [InlineData(new[] { ComplaintParties.Buyer, ComplaintParties.Seller }, ComplaintParties.NotDetermined)]
    public async Task Overdue_BuyerOrBoth_Rejected(string[] parties, string fault)
    {
        var ticket = await AskAsync(Ticket(), parties);
        _clock.Advance(TimeSpan.FromHours(49));

        await Service().ResolveOverdueAsync(default);

        Assert.Equal(DisputeTicket.StatusRejected, ticket.TicketStatus);
        Assert.Equal(fault, ticket.FaultParty);
        Assert.Equal("Delivered", ticket.SellerOrder!.Status);
        Assert.Empty(_store.Refunds);
    }

    [Fact(DisplayName = "UTC-CS01-16 Nothing happens before 48 hours or while another party can still answer")]
    public async Task Overdue_NotYet_NoChange()
    {
        var ticket = await AskAsync(Ticket(), ComplaintParties.Buyer);
        _clock.Advance(TimeSpan.FromHours(47));
        await Service().ResolveOverdueAsync(default);
        Assert.Equal(DisputeTicket.StatusWaitingInfo, ticket.TicketStatus);

        _clock.Advance(TimeSpan.FromHours(2));                           // buyer now overdue
        await Service().RequestInformationAsync(7, new(ComplaintParties.Seller, "Giải trình"), default);
        int resolved = await Service().ResolveOverdueAsync(default);

        Assert.Equal(0, resolved);                                       // seller still has time
        Assert.Equal(DisputeTicket.StatusWaitingInfo, ticket.TicketStatus);
    }

    [Fact(DisplayName = "UTC-CS01-17 Seller silent but nothing refundable: handed back to support once")]
    public async Task Overdue_NoPayment_HandedToSupport()
    {
        var ticket = await AskAsync(Ticket(paymentStatus: "Pending"), ComplaintParties.Seller);
        _clock.Advance(TimeSpan.FromHours(49));

        int resolved = await Service().ResolveOverdueAsync(default);

        Assert.Equal(0, resolved);
        Assert.Equal(DisputeTicket.StatusWaitingInfo, ticket.TicketStatus);
        Assert.Equal(DisputeTicket.EvidenceRequestExpired, ticket.DisputeEvidenceRequests.Single().RequestStatus);
        Assert.Equal(SupportId, Assert.Single(_store.Notifications).UserId);
    }

    private sealed class Actor : IActorContext
    {
        public long UserId => SupportId;
    }

    private sealed class ComplaintStore : IComplaintRepository
    {
        public DisputeTicket? Ticket { get; set; }
        public List<RefundTransaction> Refunds { get; } = [];
        public List<AuditLog> Audits { get; } = [];
        public List<Notification> Notifications { get; } = [];
        public int Saves { get; private set; }

        public Task<(int Total, IReadOnlyList<ComplaintListItemDto> Items)> SearchAsync(
            ComplaintQuery query, DateTime now, CancellationToken ct) =>
            Task.FromResult<(int, IReadOnlyList<ComplaintListItemDto>)>((0, []));
        public Task<IReadOnlyList<string>> GetReasonsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);
        public Task<ComplaintDetailDto?> GetDetailAsync(long id, DateTime now, CancellationToken ct) =>
            Task.FromResult<ComplaintDetailDto?>(null);
        public Task<DisputeTicket?> GetForDecisionAsync(long id, CancellationToken ct) =>
            Task.FromResult(Ticket?.DisputeTicketId == id ? Ticket : null);
        public Task<string?> GetEvidenceFileUrlAsync(long id, long evidenceId, CancellationToken ct) =>
            Task.FromResult<string?>(null);
        public Task<IReadOnlyList<long>> GetActiveUserIdsByRolesAsync(
            IReadOnlyCollection<string> roles, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<long>>(roles.Contains("FinanceStaff") ? [FinanceId]
                : roles.Contains("CustomerSupport") ? [SupportId] : []);
        public Task<IReadOnlyList<long>> GetOverdueTicketIdsAsync(
            DateTime now, TimeSpan window, int take, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<long>>(
                Ticket?.TicketStatus == DisputeTicket.StatusWaitingInfo ? [Ticket.DisputeTicketId] : []);

        public void AddRefund(RefundTransaction refund)
        {
            Refunds.Add(refund);
            refund.PaymentTransaction.RefundTransactions.Add(refund);
        }

        public void AddAudit(AuditLog audit) => Audits.Add(audit);
        public void AddNotification(Notification notification) => Notifications.Add(notification);
        public Task SaveChangesAsync(CancellationToken ct) { Saves++; return Task.CompletedTask; }
    }
}
