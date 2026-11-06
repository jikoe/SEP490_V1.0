using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Refunds;
using AgriSustain.Application.Validators.Refunds;
using AgriSustain.Domain.Entities;
using AgriSustain.UnitTests.DuIte1;
using Xunit;

namespace AgriSustain.UnitTests.Refunds;

/// <summary>UC-FIN-03 Process Refund.</summary>
public sealed class RefundProcessingServiceTests
{
    private const long FinanceId = 91;
    private const long BuyerId = 10;
    private const long FarmerUserId = 20;
    private static readonly DateTimeOffset T0 = new(2026, 10, 27, 1, 0, 0, TimeSpan.Zero);

    private readonly RefundStore _store = new();

    private RefundProcessingService Service() => new(
        _store, new Actor(), new CompleteRefundRequestValidator(),
        new RefundTransferFailedRequestValidator(), new RefundReconciliationRequestValidator(), new FakeClock(T0));

    /// <summary>A complaint refund already started by Finance (Processing), with a receiving account.</summary>
    private static RefundTransaction Refund(decimal amount = 200_000m, decimal paid = 200_000m,
        string otherSellerStatus = "", string status = RefundTransaction.StatusProcessing)
    {
        var order = new Order { OrderId = 1, OrderCode = "ORD-1", BuyerUserId = BuyerId, OrderStatus = "RefundPending",
            FinalAmount = 200_000m };
        var payment = new PaymentTransaction { PaymentTransactionId = 5, Amount = paid, PaymentStatus = "Success" };
        order.PaymentTransactions.Add(payment);
        var sellerOrder = new SellerOrder
        {
            SellerOrderId = 3, SellerOrderCode = "SO-3", Status = "RefundPending", FinalAmount = 200_000m,
            Order = order, FarmerProfile = new FarmerProfile { UserId = FarmerUserId }
        };
        order.SellerOrders.Add(sellerOrder);
        if (otherSellerStatus.Length > 0)
        {
            order.SellerOrders.Add(new SellerOrder { SellerOrderId = 4, Status = otherSellerStatus, Order = order });
        }

        var ticket = new DisputeTicket
        {
            DisputeTicketId = 7, TicketStatus = DisputeTicket.StatusRefundPending, SellerOrder = sellerOrder
        };
        var refund = new RefundTransaction
        {
            RefundTransactionId = 11, RefundCode = "RF-7", Amount = amount,
            RefundStatus = status, ProcessedByUserId = 90,
            PaymentTransaction = payment, DisputeTicket = ticket, RequestKey = "complaint:7",
            ReceiverBankCode = "VCB", ReceiverAccountNumber = "0123456789", ReceiverAccountHolder = "NGUYEN VAN A",
            ReceiverAccountProvidedAt = T0.UtcDateTime
        };
        payment.RefundTransactions.Add(refund);
        return refund;
    }

    private static CompleteRefundRequest Transfer(string reference = "VCB-0001",
        string method = RefundMethods.BankTransfer) => new(method, reference, null);

    [Fact(DisplayName = "UTC-FIN03-01 Confirmed transfer completes refund, payment, complaint and order")]
    public async Task Complete_FullRefund_SettlesEverything()
    {
        var refund = _store.Refund = Refund();

        var result = await Service().CompleteAsync(11, Transfer(" VCB-0001 "), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(RefundTransaction.StatusCompleted, refund.RefundStatus);
        Assert.Equal("VCB-0001", refund.GatewayReference);
        Assert.Equal(RefundMethods.BankTransfer, refund.RefundMethod);
        Assert.Equal(FinanceId, refund.ProcessedByUserId);
        Assert.Equal(T0.UtcDateTime, refund.ProcessedAt);
        Assert.Equal(PaymentTransaction.StatusRefunded, refund.PaymentTransaction.PaymentStatus);
        Assert.Equal(DisputeTicket.StatusClosed, refund.DisputeTicket!.TicketStatus);
        Assert.Equal("Refunded", refund.DisputeTicket.SellerOrder!.Status);
        Assert.Equal("Refunded", refund.DisputeTicket.SellerOrder.Order.OrderStatus);
        Assert.Single(refund.DisputeTicket.SellerOrder.OrderStatusHistories);
        Assert.Contains(_store.Notifications, n => n.UserId == BuyerId);
        Assert.Contains("BankTransfer", Assert.Single(_store.Audits).NewValue);
        Assert.Equal(1, _store.Saves);
    }

    [Fact(DisplayName = "UTC-FIN03-02 Partial refund marks the order PartiallyRefunded and keeps the payment captured")]
    public async Task Complete_PartialRefund_PaymentStaysCaptured()
    {
        var refund = _store.Refund = Refund(amount: 50_000m);

        await Service().CompleteAsync(11, Transfer(), default);

        Assert.Equal("Success", refund.PaymentTransaction.PaymentStatus);
        Assert.Equal(RefundTransaction.StatusCompleted, refund.RefundStatus);
        Assert.Equal("PartiallyRefunded", refund.DisputeTicket!.SellerOrder!.Status);
        Assert.Equal("PartiallyRefunded", refund.DisputeTicket.SellerOrder.Order.OrderStatus);
    }

    [Theory(DisplayName = "UTC-FIN03-03 Parent order status follows all of its seller orders")]
    [InlineData("Delivered", "PartiallyRefunded")]   // the other farm's part was not refunded
    [InlineData("Cancelled", "Refunded")]            // cancelled parts are ignored
    [InlineData("RefundPending", "RefundPending")]   // another refund still awaits Finance
    public async Task Complete_MultiSellerOrder_ParentStatus(string otherSeller, string expected)
    {
        var refund = _store.Refund = Refund(otherSellerStatus: otherSeller);

        await Service().CompleteAsync(11, Transfer(), default);

        Assert.Equal(expected, refund.DisputeTicket!.SellerOrder!.Order.OrderStatus);
    }

    [Fact(DisplayName = "UTC-FIN03-03b A fully refunded seller order is Refunded even when the order has other farms")]
    public async Task Complete_FullSellerOrderRefund_InMultiFarmOrder()
    {
        var refund = _store.Refund = Refund(paid: 300_000m, otherSellerStatus: "Delivered");
        refund.DisputeTicket!.SellerOrder!.Order.FinalAmount = 300_000m;

        await Service().CompleteAsync(11, Transfer(), default);

        Assert.Equal("Refunded", refund.DisputeTicket.SellerOrder.Status);              // 200 000 of 200 000
        Assert.Equal("PartiallyRefunded", refund.DisputeTicket.SellerOrder.Order.OrderStatus);
        Assert.Equal("Success", refund.PaymentTransaction.PaymentStatus);             // 100 000 still paid
    }

    [Theory(DisplayName = "UTC-FIN03-04 Method and transfer reference are required")]
    [InlineData("Cash", "VCB-1", "refundMethod")]
    [InlineData(RefundMethods.BankTransfer, " ", "transferReference")]
    [InlineData(RefundMethods.BankTransfer, "VCB<script>", "transferReference")]
    public async Task Complete_InvalidInput_Rejected(string method, string reference, string field)
    {
        var refund = _store.Refund = Refund();

        var result = await Service().CompleteAsync(11, Transfer(reference, method), default);

        Assert.Contains(field, result.Error!.Fields!.Keys);
        Assert.Equal(RefundTransaction.StatusProcessing, refund.RefundStatus);
    }

    [Fact(DisplayName = "UTC-FIN03-05 A completed refund cannot be completed again")]
    public async Task Complete_Twice_Conflict()
    {
        _store.Refund = Refund();
        await Service().CompleteAsync(11, Transfer(), default);

        var again = await Service().CompleteAsync(11, Transfer("VCB-0002"), default);

        Assert.Equal("REFUND_NOT_PENDING", again.Error!.Code);
        Assert.Equal(409, again.StatusCode);
    }

    [Fact(DisplayName = "UTC-FIN03-06 A transfer reference used before is refused")]
    public async Task Complete_DuplicateReference_Conflict()
    {
        var refund = _store.Refund = Refund();
        _store.UsedReferences.Add("VCB-0001");

        var result = await Service().CompleteAsync(11, Transfer(), default);

        Assert.Equal("TRANSFER_REFERENCE_USED", result.Error!.Code);
        Assert.Equal(RefundTransaction.StatusProcessing, refund.RefundStatus);
    }

    [Fact(DisplayName = "UTC-FIN03-07 A rejected transfer is Failed, logged and the customer is asked to check the account")]
    public async Task TransferFailed_MarksFailed()
    {
        var refund = _store.Refund = Refund();

        var result = await Service().RecordTransferFailureAsync(11, new("Sai số tài khoản"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(RefundTransaction.StatusFailed, refund.RefundStatus);
        Assert.Contains(_store.Notifications, n => n.UserId == BuyerId);
        Assert.Equal("RefundPending", refund.DisputeTicket!.SellerOrder!.Status);
        var audit = Assert.Single(_store.Audits);
        Assert.Equal("RefundTransferFailed", audit.Action);
        Assert.Equal("Sai số tài khoản", audit.Reason);
    }

    [Fact(DisplayName = "UTC-FIN03-08 Unknown refund returns 404")]
    public async Task Complete_Unknown_NotFound()
    {
        var result = await Service().CompleteAsync(99, Transfer(), default);

        Assert.Equal(404, result.StatusCode);
    }

    [Fact(DisplayName = "UTC-FIN03-09 Start Transfer moves Pending to Processing; no receiving account blocks it")]
    public async Task StartTransfer_NeedsReceivingAccount()
    {
        var refund = _store.Refund = Refund(status: RefundTransaction.StatusPending);
        refund.ReceiverBankCode = refund.ReceiverAccountNumber = refund.ReceiverAccountHolder = null;
        refund.ReceiverAccountProvidedAt = null;

        var blocked = await Service().StartTransferAsync(11, new(RefundTransaction.StatusPending), default);
        refund.ReceiverBankCode = "VCB";
        refund.ReceiverAccountNumber = "0123456789";
        refund.ReceiverAccountHolder = "NGUYEN VAN A";
        refund.ReceiverAccountProvidedAt = T0.UtcDateTime;
        var started = await Service().StartTransferAsync(11, new(RefundTransaction.StatusPending), default);

        Assert.Equal("REFUND_ACCOUNT_MISSING", blocked.Error?.Code);
        Assert.True(started.IsSuccess, started.Error?.Message);
        Assert.Equal(RefundTransaction.StatusProcessing, refund.RefundStatus);
    }

    /// <summary>A Farmer-rejection refund (UC-FAR-06): no complaint, the seller order is Cancelled.</summary>
    private RefundTransaction FarmerRejectionRefund(string status)
    {
        var refund = Refund(status: status);
        var sellerOrder = refund.DisputeTicket!.SellerOrder!;
        sellerOrder.Status = "Cancelled";
        sellerOrder.Order.OrderStatus = "Cancelled";
        refund.DisputeTicket = null;
        refund.RequestKey = "farmer-reject:3:5:0";
        refund.RefundCode = "REF-3-5-0";
        _store.FarmerRejected = sellerOrder;
        return refund;
    }

    [Fact(DisplayName = "UTC-FIN03-10 Farmer-rejection refund: Start Transfer works without a complaint")]
    public async Task StartTransfer_FarmerRejection()
    {
        var refund = _store.Refund = FarmerRejectionRefund(RefundTransaction.StatusPending);

        var result = await Service().StartTransferAsync(11, new(RefundTransaction.StatusPending), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(RefundTransaction.StatusProcessing, refund.RefundStatus);
    }

    [Fact(DisplayName = "UTC-FIN03-11 Farmer-rejection refund succeeds, the seller order stays Cancelled and both parties are told")]
    public async Task Complete_FarmerRejection_StaysCancelled()
    {
        var refund = _store.Refund = FarmerRejectionRefund(RefundTransaction.StatusProcessing);
        var sellerOrder = _store.FarmerRejected!;

        var result = await Service().CompleteAsync(11, Transfer(), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(RefundTransaction.StatusCompleted, refund.RefundStatus);
        Assert.Equal("Cancelled", sellerOrder.Status);
        Assert.Equal("Cancelled", sellerOrder.Order.OrderStatus);
        Assert.Empty(sellerOrder.OrderStatusHistories);
        Assert.Equal(PaymentTransaction.StatusRefunded, refund.PaymentTransaction.PaymentStatus);
        Assert.Contains(_store.Notifications, n => n.UserId == BuyerId);
        Assert.Contains(_store.Notifications, n => n.UserId == FarmerUserId);
    }

    private sealed class Actor : IActorContext
    {
        public long UserId => FinanceId;
    }

    private sealed class RefundStore : IRefundRepository
    {
        public RefundTransaction? Refund { get; set; }
        public HashSet<string> UsedReferences { get; } = [];
        public List<AuditLog> Audits { get; } = [];
        public List<Notification> Notifications { get; } = [];
        public int Saves { get; private set; }

        public Task<(int Total, IReadOnlyList<RefundListItemDto> Items)> SearchAsync(
            RefundQuery query, CancellationToken ct) =>
            Task.FromResult<(int, IReadOnlyList<RefundListItemDto>)>((0, []));
        public Task<RefundDetailDto?> GetDetailAsync(long id, CancellationToken ct) =>
            Task.FromResult<RefundDetailDto?>(null);
        public Task<RefundTransaction?> GetForProcessingAsync(long id, CancellationToken ct) =>
            Task.FromResult(Refund?.RefundTransactionId == id ? Refund : null);
        public Task<bool> TransferReferenceExistsAsync(string reference, CancellationToken ct) =>
            Task.FromResult(UsedReferences.Contains(reference));
        public SellerOrder? FarmerRejected { get; set; }
        public Task<SellerOrder?> GetFarmerRejectedSellerOrderAsync(RefundTransaction refund, CancellationToken ct) =>
            Task.FromResult(refund.DisputeTicketId is null ? FarmerRejected : null);
        public void AddAudit(AuditLog audit) => Audits.Add(audit);
        public void AddNotification(Notification notification) => Notifications.Add(notification);

        public Task SaveChangesAsync(CancellationToken ct)
        {
            Saves++;
            if (Refund?.GatewayReference is { } reference)
            {
                UsedReferences.Add(reference);
            }

            return Task.CompletedTask;
        }
    }
}
