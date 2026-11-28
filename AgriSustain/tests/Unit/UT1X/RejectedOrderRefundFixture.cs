using System.Text.Json;
using AgriSustain.Application.DTOs.Payouts;
using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Refunds;
using AgriSustain.Domain.Entities;
using Xunit;

namespace AgriSustain.UnitTests.UT1X;

internal sealed class RejectedRefundRepoDouble : IFinanceRejectedOrderRefundRepository
{
    public bool IsAuthorized = true;
    public bool EventAlreadyUsed = false;
    public bool ThrowOnAtomic = false;
    public RejectedOrderRefundContext? Context;
    public List<AuditLog> Audits = [];
    public List<Notification> Notifications = [];
    public int SearchCalls, AtomicCalls;

    public Task<bool> AuthorizedAsync(long userId, CancellationToken ct) =>
        Task.FromResult(IsAuthorized);

    public Task<object> SearchAsync(RejectedOrderRefundQuery query, CancellationToken ct)
    {
        SearchCalls++;
        return Task.FromResult<object>(new
        {
            items = Context is null
                ? Array.Empty<object>()
                : new object[] { new { id = Context.Refund.RefundTransactionId, code = Context.Refund.RefundCode } },
            total = Context is null ? 0 : 1,
            page = query.Page,
            pageSize = 10
        });
    }

    public Task<RejectedOrderRefundContext?> LoadAsync(long id, bool update, CancellationToken ct) =>
        Task.FromResult(Context is { } c && c.Refund.RefundTransactionId == id ? c : null);

    public Task<IReadOnlyList<long>> PendingAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<long>>(Context is null ? [] : [Context.Refund.RefundTransactionId]);

    public Task<bool> BankEventUsedAsync(string eventKey, string reference, CancellationToken ct) =>
        Task.FromResult(EventAlreadyUsed);

    public async Task<T> AtomicAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        AtomicCalls++;
        if (ThrowOnAtomic) throw new IOException("Injected persistence failure");
        return await operation();
    }

    public void Audit(AuditLog row)
    {
        Audits.Add(row);
        if (Context is not null)
        {
            var list = Context.History.ToList();
            list.Add(row);
            Context = Context with { History = list };
        }
    }

    public void Notify(Notification row) => Notifications.Add(row);
}

internal sealed class RejectedRefundGatewayDouble : IRejectedOrderRefundGateway
{
    public bool CanCreateQr = true;
    public bool ReadAccess = true;
    public string SourceBankCode = "MB";
    public string SourceAccountNumber = "0987654321";
    public PayoutGatewayLookup Lookup = new([], true, null);

    public PayoutTransferPlan? CreateQr(RefundTransaction refund) =>
        CanCreateQr
            ? new PayoutTransferPlan(
                refund.RefundCode,
                SourceBankCode,
                "MB Bank",
                SourceAccountNumber,
                refund.ReceiverBankCode ?? "VCB",
                refund.ReceiverAccountNumber ?? "0123456789",
                refund.Amount,
                DateTime.UtcNow,
                $"https://qr.sepay.vn/img?des={refund.RefundCode}&amount={refund.Amount}"
            )
            : null;

    public Task<bool> CheckReadAccessAsync(PayoutTransferPlan plan, CancellationToken ct) =>
        Task.FromResult(ReadAccess);

    public Task<PayoutGatewayLookup> GetSourceDebitsAsync(PayoutTransferPlan plan, CancellationToken ct) =>
        Task.FromResult(Lookup);
}

internal sealed class OrderFeeServiceDouble : IOrderFeeService
{
    public List<long> ReversedCancelledSellerOrderIds = [];
    public Task ApplyAsync(Order order, DateTime nowUtc, CancellationToken ct) => Task.CompletedTask;
    public Task ReverseCancelledAsync(long sellerOrderId, CancellationToken ct)
    {
        ReversedCancelledSellerOrderIds.Add(sellerOrderId);
        return Task.CompletedTask;
    }
    public Task ReverseRefundAsync(long sellerOrderId, decimal refundedTotal, decimal sellerOrderAmount, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class RejectedOrderRefundFixture
{
    public readonly CommerceClock Clock = new();
    public readonly RejectedRefundRepoDouble Repo = new();
    public readonly RejectedRefundGatewayDouble Gateway = new();
    public readonly OrderFeeServiceDouble Fees = new();
    public readonly RejectedOrderRefundRules Rules = new();
    public readonly FinanceRejectedOrderRefundService Service;
    public readonly RefundTransaction Refund;
    public readonly SellerOrder SellerOrder;
    public readonly Order Order;
    public readonly PaymentTransaction Payment;
    public readonly UserBankAccount Account;

    public RejectedOrderRefundFixture()
    {
        var buyer = new User { UserId = 102, FullName = "Nguyen Van A", Role = "RetailCustomer", Status = "Active" };
        var farmer = new FarmerProfile { FarmerProfileId = 1, UserId = 101, FarmName = "Green Farm", VerificationStatus = "Approved" };
        Order = new Order
        {
            OrderId = 10,
            BuyerUserId = 102,
            BuyerUser = buyer,
            OrderCode = "ORD-10",
            OrderType = "Retail",
            OrderStatus = "Cancelled",
            FinalAmount = 150000,
            TotalAmount = 150000
        };
        SellerOrder = new SellerOrder
        {
            SellerOrderId = 501,
            OrderId = 10,
            Order = Order,
            SellerOrderCode = "SO-501",
            FarmerProfileId = 1,
            FarmerProfile = farmer,
            Status = "Cancelled",
            Subtotal = 150000,
            DiscountShare = 0,
            DeliveryFee = 0,
            FinalAmount = 150000
        };
        SellerOrder.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OldStatus = "Pending",
            NewStatus = "Cancelled",
            Reason = "Out of stock",
            OccurredAt = Clock.Now.AddHours(-1).UtcDateTime
        });
        Order.SellerOrders.Add(SellerOrder);
        Payment = new PaymentTransaction
        {
            PaymentTransactionId = 701,
            OrderId = 10,
            Order = Order,
            TransactionCode = "PAY-701",
            Amount = 150000,
            PaymentStatus = "Paid",
            PaidAt = Clock.Now.AddHours(-2).UtcDateTime
        };
        Order.PaymentTransactions.Add(Payment);
        Account = new UserBankAccount
        {
            UserBankAccountId = 901,
            UserId = 102,
            BankCode = "VCB",
            BankCodeNavigation = new SupportedBank { BankCode = "VCB", IsActive = true },
            AccountNumber = "0123456789",
            AccountHolder = "NGUYEN VAN A",
            VerificationStatus = "Verified",
            VerifiedAt = Clock.Now.AddDays(-3).UtcDateTime,
            UpdatedAt = Clock.Now.AddDays(-1).UtcDateTime
        };
        Refund = new RefundTransaction
        {
            RefundTransactionId = 801,
            PaymentTransactionId = 701,
            PaymentTransaction = Payment,
            RefundCode = "RJR202610270001",
            Amount = 150000,
            Reason = "Out of stock",
            RefundStatus = RefundTransaction.StatusPending,
            RequestKey = "farmer-reject:501:701:0",
            ReceiverBankCode = "VCB",
            ReceiverAccountNumber = "0123456789",
            ReceiverAccountHolder = "NGUYEN VAN A",
            ReceiverAccountProvidedAt = Clock.Now.AddHours(-1).UtcDateTime
        };
        Payment.RefundTransactions.Add(Refund);
        Repo.Context = new RejectedOrderRefundContext(Refund, SellerOrder, Account, []);
        var notifications = new RejectedOrderRefundNotificationService(Repo);
        Service = new FinanceRejectedOrderRefundService(Repo, Gateway, Rules, notifications, Fees, Clock);
    }

    public string CurrentVersion => Rules.Version(Repo.Context!);

    public async Task<PayoutTransferPlan> OpenQrSuccessfullyAsync(long actor = 301)
    {
        var preview = await Service.PreviewAsync(actor, Refund.RefundTransactionId, default);
        Assert.True(preview.IsSuccess, preview.Error?.Message);
        var open = await Service.OpenQrAsync(actor, Refund.RefundTransactionId,
            new OpenRejectedOrderRefundRequest(preview.Value!.ExpectedVersion, Guid.NewGuid().ToString("D")), default);
        Assert.True(open.IsSuccess, open.Error?.Message);
        var attempt = JsonSerializer.Deserialize<RejectedOrderRefundAttempt>(
            Repo.Context!.History.Last(h => h.Action == "QrOpened").NewValue!)!;
        return attempt.Plan;
    }

    public PayoutOutgoingTransfer MatchingTransfer(PayoutTransferPlan plan, DateTime? at = null, decimal? amount = null,
        string? gateway = null, string? sourceAcc = null, string? bankRef = "FT26300123456", string eventId = "EVT-1") =>
        new(
            eventId,
            gateway ?? "MBBank",
            sourceAcc ?? plan.SourceAccountNumber,
            amount ?? plan.Amount,
            at ?? Clock.Now.UtcDateTime,
            $"HOAN TIEN {plan.OperationKey} DON SO501",
            bankRef,
            "{}"
        );
}
