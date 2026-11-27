using AgriSustain.Application.Common;
using AgriSustain.Infrastructure.Payments;
using AgriSustain.Application.DTOs.Payouts;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AgriSustain.UnitTests.UT3X;

public sealed class FarmerPayoutWorkflowTests
{
    [Fact]
    public async Task UT_FAR_09_007_PreviewAndSubmitReserveExactlyOnce()
    {
        var f = new Fixture();
        var input = f.Input();
        var preview = (await f.Farmer.PreviewAsync(7, input, default)).Value!;
        Assert.True(preview.CanCreate);
        Assert.Equal(100000, preview.Gross);
        Assert.Equal(5000, preview.Commission);
        Assert.Equal(95000, preview.Net);
        Assert.Empty(f.Store.Notifications);
        Assert.Null(f.Store.Payout);

        input.ExpectedBalance = preview.ExpectedBalance;
        var result = await f.Farmer.SubmitAsync(7, input, default);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Pending", result.Value!.Status);
        Assert.Equal(result.Value.Id, (await f.Farmer.SubmitAsync(7, input, default)).Value?.Id);
        Assert.Single(f.Store.Payout!.PayoutItems);
        Assert.Equal(100000, Assert.Single(f.Rules.ValidateEligibleProceeds(
            f.Store.Context(), null, DateTime.UtcNow)).Held);
        Assert.Equal(new[] { "Email", "InApp" },
            f.Store.Notifications.Select(x => x.DeliveryChannel).Order());
        Assert.All(f.Store.Notifications, x => Assert.Equal(8, x.UserId));
        Assert.Equal("FORBIDDEN", (await f.Farmer.DetailAsync(99, result.Value.Id, default)).Error?.Code);
        Assert.True((await f.Farmer.DetailAsync(7, result.Value.Id, default)).IsSuccess);
    }

    [Fact]
    public async Task UT_FAR_09_008_RejectsEmptyStaleAndHeldRequests()
    {
        var f = new Fixture();
        Assert.Equal("FORBIDDEN", (await f.Farmer.ContextAsync(99, default)).Error?.Code);
        var input = f.Input();
        input.SelectedSourceIds.Clear();
        Assert.Equal("MSG02", (await f.Farmer.SubmitAsync(7, input, default)).Error?.Code);
        input = f.Input();
        input.ExpectedBalance = new string('0', 64);
        Assert.Equal("CONFLICT", (await f.Farmer.SubmitAsync(7, input, default)).Error?.Code);
        f.Store.Bank.PayoutHoldUntil = DateTime.UtcNow.AddHours(1);
        Assert.Equal("MSG172", (await f.Farmer.SubmitAsync(7, input, default)).Error?.Code);
        Assert.Null(f.Store.Payout);
        Assert.Empty(f.Store.Notifications);
    }

    [Fact]
    public async Task UT_FIN_04_001_QrDoesNotSettleAndRejectIsBlockedAfterOpening()
    {
        var f = new Fixture();
        await f.Submit();
        f.Gateway.Available = false;
        Assert.Equal("SOURCE_CONFIG",
            (await f.Finance.GetPaymentQrPreviewAsync(8, 1, default)).Error?.Code);
        Assert.Equal("Pending", f.Store.Payout!.PayoutStatus);
        f.Gateway.Available = true;
        Assert.True((await f.Finance.GetPaymentQrPreviewAsync(8, 1, default)).IsSuccess);
        var request = f.ProcessInput();
        request.ConfirmedNetAmount++;
        Assert.Equal("CONFLICT",
            (await f.Finance.ProcessPayoutAsync(8, 1, request, default)).Error?.Code);
        request = f.ProcessInput();
        Assert.Equal("Processing", (await f.Finance.ProcessPayoutAsync(8, 1, request, default)).Value?.Status);
        Assert.Equal("Processing", (await f.Finance.ProcessPayoutAsync(8, 1, request, default)).Value?.Status);
        Assert.Single(f.Store.History, x => x.Action == "OpenQr");
        Assert.Equal(0, Assert.Single(f.Store.Payout.PayoutItems).SourceAmountSettled);
        Assert.Equal("CONFLICT",
            (await f.Finance.RejectRequestAsync(8, 1, f.RejectInput("Too late"), default)).Error?.Code);
    }

    [Fact]
    public async Task UT_FIN_04_002_ReconcileNoMatchMismatchAndVerifiedReplay()
    {
        var f = new Fixture();
        await f.Submit();
        await f.Finance.ProcessPayoutAsync(8, 1, f.ProcessInput(), default);
        var operation = f.Store.Payout!.PayoutCode;
        var query = new ReconcileFarmerPayoutRequest { ExistingOperationKey = operation };
        Assert.Equal("Processing", (await f.Finance.ReconcilePayoutAsync(8, 1, query, default)).Value?.Status);
        var debit = new PayoutOutgoingTransfer("good", "MB", "333333333", 95000,
            DateTime.UtcNow, operation, "BANK-1", "verified");
        f.Gateway.Transfers = [debit with { EventId = "wrong", Amount = 1 }];
        Assert.Equal("NeedsReconciliation",
            (await f.Finance.ReconcilePayoutAsync(8, 1, query, default)).Value?.Status);
        Assert.Equal(0, Assert.Single(f.Store.Payout.PayoutItems).SourceAmountSettled);
        f.Gateway.Transfers = [debit];
        Assert.Equal("Processed", (await f.Finance.ReconcilePayoutAsync(8, 1, query, default)).Value?.Status);
        Assert.Equal("BANK-1", f.Store.Payout.BankTransferRef);
        Assert.Equal(100000, Assert.Single(f.Store.Payout.PayoutItems).SourceAmountSettled);
        Assert.True(await f.Finance.RecordOutgoingAsync(debit, default));
        Assert.False(await f.Finance.RecordOutgoingAsync(debit with { RawPayload = "conflicting" }, default));
        Assert.Single(f.Store.History, x => x.Action == "Settled");
        Assert.Equal(2, f.Store.Notifications.Count(x => x.UserId == 7));
    }

    [Fact]
    public async Task UT_FIN_04_003_RejectsBeforeQrAtReasonBoundaryAndReleasesSources()
    {
        var f = new Fixture();
        await f.Submit();
        Assert.Equal("MSG02", (await f.Finance.RejectRequestAsync(
            8, 1, f.RejectInput("  "), default)).Error?.Code);
        Assert.Equal("MSG120", (await f.Finance.RejectRequestAsync(
            8, 1, f.RejectInput(new string('x', 501)), default)).Error?.Code);
        var request = f.RejectInput(" " + new string('x', 500) + " ");
        Assert.Equal("Rejected", (await f.Finance.RejectRequestAsync(8, 1, request, default)).Value?.Status);
        Assert.Equal("Rejected", (await f.Finance.RejectRequestAsync(8, 1, request, default)).Value?.Status);
        Assert.Equal(500, Assert.Single(f.Store.History, x => x.Action == "Rejected").Reason!.Length);
        Assert.Equal(0, Assert.Single(f.Rules.ValidateEligibleProceeds(
            f.Store.Context(), null, DateTime.UtcNow)).Held);
        Assert.Equal(2, f.Store.Notifications.Count(x => x.UserId == 7));
    }

    [Fact]
    public async Task UT_FIN_04_004_FiltersAccessAndChangedRecipient()
    {
        var f = new Fixture();
        await f.Submit();
        Assert.Equal("FORBIDDEN", (await f.Finance.GetPayoutsAsync(7, new(), default)).Error?.Code);
        var query = new FarmerPayoutQuery { From = new(2025, 1, 2), To = new(2025, 1, 1) };
        Assert.Equal("INVALID_DATES", (await f.Finance.GetPayoutsAsync(8, query, default)).Error?.Code);
        Assert.True((await f.Finance.GetPayoutsAsync(8, new(), default)).IsSuccess);
        f.Store.Bank.AccountNumber = "12345678";
        Assert.Equal("ACCOUNT_CHANGED",
            (await f.Finance.GetPaymentQrPreviewAsync(8, 1, default)).Error?.Code);
        Assert.Equal("Pending", f.Store.Payout!.PayoutStatus);
    }

    [Theory]
    [InlineData("reference")]
    [InlineData("account")]
    [InlineData("bank")]
    [InlineData("amount")]
    [InlineData("early")]
    [InlineData("future")]
    public async Task UT_FIN_04_005_DebitMustMatchAllTransferEvidence(string mismatch)
    {
        var f = new Fixture();
        await f.Submit();
        Assert.True((await f.Finance.ProcessPayoutAsync(8, 1, f.ProcessInput(), default)).IsSuccess);
        var payout = f.Store.Payout!;
        var now = DateTime.UtcNow;
        var debit = new PayoutOutgoingTransfer(
            "mismatch", "MB", "333333333", payout.NetAmount, now, payout.PayoutCode,
            "BANK-TEST", "synthetic");
        debit = mismatch switch
        {
            "reference" => debit with { Content = "FPOFFFFFFFFFFFF" },
            "account" => debit with { SourceAccountNumber = "444444444" },
            "bank" => debit with { Gateway = "VCB" },
            "amount" => debit with { Amount = payout.NetAmount + 1 },
            "early" => debit with { At = now.AddHours(-1) },
            _ => debit with { At = now.AddHours(1) }
        };
        f.Gateway.Transfers = [debit];
        var result = await f.Finance.ReconcilePayoutAsync(8, 1, new()
        {
            ExistingOperationKey = payout.PayoutCode
        }, default);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(mismatch == "reference" ? "Processing" : "NeedsReconciliation", result.Value!.Status);
        Assert.Equal(0, Assert.Single(payout.PayoutItems).SourceAmountSettled);
        Assert.Null(payout.BankTransferRef);
        Assert.DoesNotContain(f.Store.History, x => x.Action == "Settled");
        Assert.DoesNotContain(f.Store.Notifications, x => x.UserId == 7);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(101)]
    public async Task NonPositiveNetCannotBeReserved(int rate)
    {
        var f = new Fixture();
        var request = f.Input();
        request.ExpectedBalance = (await f.Farmer.PreviewAsync(7, request, default)).Value!.ExpectedBalance;
        f.Store.CommissionRate = rate;

        var preview = await f.Farmer.PreviewAsync(7, request, default);
        var result = await f.Farmer.SubmitAsync(7, request, default);

        Assert.Equal("MSG172", preview.Error?.Code);
        Assert.Equal("MSG172", result.Error?.Code);
        Assert.Null(f.Store.Payout);
        Assert.Empty(f.Store.History);
        Assert.Empty(f.Store.Notifications);
    }

    [Fact]
    public async Task ReservedSourceCannotBeSubmittedUnderAnotherRequestKey()
    {
        var f = new Fixture();
        var second = f.Input();
        second.ExpectedBalance = (await f.Farmer.PreviewAsync(7, second, default)).Value!.ExpectedBalance;
        await f.Submit();
        var original = f.Store.Payout;

        var result = await f.Farmer.SubmitAsync(7, second, default);

        Assert.Same(original, f.Store.Payout);
        Assert.Single(f.Store.History);
        Assert.Equal(2, f.Store.Notifications.Count);
        Assert.Equal("CONFLICT", result.Error?.Code);
        Assert.Equal(FinancePayoutMessages.Text("MSG174"), result.Error?.Message);
    }

    [Theory]
    [InlineData("unverified")]
    [InlineData("unsupported")]
    [InlineData("malformed")]
    public async Task InvalidRecipientCannotReserveProceeds(string condition)
    {
        var f = new Fixture();
        var request = f.Input();
        request.ExpectedBalance = (await f.Farmer.PreviewAsync(7, request, default)).Value!.ExpectedBalance;
        switch (condition)
        {
            case "unverified": f.Store.Bank.VerificationStatus = "Pending"; break;
            case "unsupported": f.Store.Bank.BankCodeNavigation.IsActive = false; break;
            case "malformed": f.Store.Bank.AccountNumber = "NOT-DIGITS"; break;
        }

        var result = await f.Farmer.SubmitAsync(7, request, default);

        Assert.Equal("MSG172", result.Error?.Code);
        Assert.Null(f.Store.Payout);
        Assert.Empty(f.Store.History);
        Assert.Empty(f.Store.Notifications);
    }

    [Fact]
    public async Task SePayTimeoutReturnsUnknownWithoutVerifiedDebit()
    {
        using var http = new HttpClient(new TimeoutHandler());
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Payouts:SePayApiToken"] = "fixture-token" }).Build();
        var gateway = new SePayPayoutGateway(http, config);
        var plan = new PayoutTransferPlan(
            "FPO0123456789AB", "MB", "MB", "333333333", "VCB", "0123456789",
            95000, DateTime.UtcNow, "https://example.test/qr");

        var lookup = await gateway.GetTransferStatusAsync(plan, default);

        Assert.False(lookup.LookupAvailable);
        Assert.Empty(lookup.Transfers);
        Assert.Equal(FinancePayoutMessages.Text("MSG175"), lookup.Message);
    }

    [Fact]
    public async Task UnavailableLookupAfterQrRetainsPendingSourceReservation()
    {
        var f = new Fixture();
        await f.Submit();
        await f.Finance.ProcessPayoutAsync(8, 1, f.ProcessInput(), default);
        f.Gateway.Available = false;
        var result = await f.Finance.ReconcilePayoutAsync(8, 1, new()
        {
            ExistingOperationKey = f.Store.Payout!.PayoutCode
        }, default);

        Assert.Equal("Processing", result.Value?.Status);
        Assert.Equal(FinancePayoutMessages.Text("MSG175"), result.Value?.Message);
        Assert.Equal(0, Assert.Single(f.Store.Payout.PayoutItems).SourceAmountSettled);
        Assert.DoesNotContain(f.Store.History, x => x.Action == "Settled");
        Assert.DoesNotContain(f.Store.Notifications, x => x.UserId == 7);
    }

    [Fact]
    public async Task DetailPreservesPreviouslyVerifiedFailedState()
    {
        var f = new Fixture();
        await f.Submit();
        f.Store.Payout!.PayoutStatus = "Failed";
        f.Store.NotificationFailed = true;

        var result = await f.Finance.GetContextAsync(8, 1, default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var detail = System.Text.Json.JsonSerializer.SerializeToElement(result.Value);
        Assert.Equal("Failed", detail.GetProperty("status").GetString());
        Assert.True(detail.GetProperty("notificationFailed").GetBoolean());
        Assert.False(detail.GetProperty("canProcess").GetBoolean());
        Assert.False(detail.GetProperty("canReconcile").GetBoolean());
        Assert.Null(f.Store.Payout.BankTransferRef);
    }

    [Fact]
    public async Task UT_FAR_09_017_PositiveNetBoundaryAt99PercentCommissionAccepted()
    {
        var f = new Fixture();
        f.Store.CommissionRate = 99;
        var request = f.Input();
        var preview = await f.Farmer.PreviewAsync(7, request, default);
        Assert.True(preview.IsSuccess, preview.Error?.Message);
        Assert.Equal(1000, preview.Value!.Net);

        request.ExpectedBalance = preview.Value.ExpectedBalance;
        var result = await f.Farmer.SubmitAsync(7, request, default);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Pending", result.Value!.Status);
    }

    [Fact]
    public async Task UT_FAR_09_018_UnauthorizedFarmerBlockedFromSubmitAndPreview()
    {
        var f = new Fixture();
        var request = f.Input();
        Assert.Equal("FORBIDDEN", (await f.Farmer.PreviewAsync(99, request, default)).Error?.Code);
        Assert.Equal("FORBIDDEN", (await f.Farmer.SubmitAsync(99, request, default)).Error?.Code);
        Assert.Null(f.Store.Payout);
        Assert.Empty(f.Store.Notifications);
    }

    [Fact]
    public async Task UT_FIN_04_014_UnauthorizedActorBlockedFromAllFinancePayoutActions()
    {
        var f = new Fixture();
        await f.Submit();
        Assert.Equal("FORBIDDEN", (await f.Finance.GetContextAsync(7, 1, default)).Error?.Code);
        Assert.Equal("FORBIDDEN", (await f.Finance.GetPaymentQrPreviewAsync(7, 1, default)).Error?.Code);
        Assert.Equal("FORBIDDEN", (await f.Finance.ProcessPayoutAsync(7, 1, f.ProcessInput(), default)).Error?.Code);
        Assert.Equal("FORBIDDEN", (await f.Finance.ReconcilePayoutAsync(7, 1, new() { ExistingOperationKey = f.Store.Payout!.PayoutCode }, default)).Error?.Code);
        Assert.Equal("FORBIDDEN", (await f.Finance.RejectRequestAsync(7, 1, f.RejectInput("Denied"), default)).Error?.Code);
        Assert.Equal("Pending", f.Store.Payout.PayoutStatus);
    }

    [Fact]
    public async Task UT_FIN_04_015_StaleVersionBlocksProcessAndReject()
    {
        var f = new Fixture();
        await f.Submit();
        string stale = new('0', 64);
        var process = f.ProcessInput();
        process.ExpectedVersion = stale;
        Assert.Equal("CONFLICT", (await f.Finance.ProcessPayoutAsync(8, 1, process, default)).Error?.Code);

        var reject = f.RejectInput("Valid reason");
        reject.ExpectedVersion = stale;
        Assert.Equal("CONFLICT", (await f.Finance.RejectRequestAsync(8, 1, reject, default)).Error?.Code);
        Assert.Equal("Pending", f.Store.Payout!.PayoutStatus);
    }

    [Fact]
    public async Task UT_FIN_04_016_SameDayFilterAcceptedInFinancePayoutList()
    {
        var f = new Fixture();
        await f.Submit();
        var day = new DateOnly(2026, 11, 2);
        var result = await f.Finance.GetPayoutsAsync(8, new FarmerPayoutQuery { From = day, To = day }, default);
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException("Simulated HTTP timeout"));
    }

    private sealed class Fixture
    {
        public Store Store { get; } = new();
        public Gateway Gateway { get; } = new();
        public FarmerPayoutRules Rules { get; }
        public FarmerPaymentRequestService Farmer { get; }
        public FinanceFarmerPayoutService Finance { get; }

        public Fixture()
        {
            var settings = new ConfigurationBuilder().Build();
            Rules = new FarmerPayoutRules(settings);
            var notices = new PayoutNotificationService(Store, Store, settings);
            Farmer = new FarmerPaymentRequestService(Store, Store, Rules, notices);
            Finance = new FinanceFarmerPayoutService(Store, Rules, Gateway, notices);
        }

        public CreateFarmerPayoutRequest Input() => new()
        {
            SelectedSourceIds = [11], RequestKey = Guid.NewGuid().ToString()
        };

        public async Task Submit()
        {
            var request = Input();
            request.ExpectedBalance = (await Farmer.PreviewAsync(7, request, default)).Value!.ExpectedBalance;
            var result = await Farmer.SubmitAsync(7, request, default);
            Assert.True(result.IsSuccess, result.Error?.Message);
        }

        public ProcessFarmerPayoutRequest ProcessInput() => new()
        {
            RequestKey = Guid.NewGuid().ToString(), ExpectedVersion = Rules.Version(Store.Context()),
            ConfirmedNetAmount = Store.Payout!.NetAmount
        };

        public RejectFarmerPayoutRequest RejectInput(string reason) => new()
        {
            RequestKey = Guid.NewGuid().ToString(), ExpectedVersion = Rules.Version(Store.Context()),
            Reason = reason
        };
    }

    private sealed class Gateway : IPayoutGateway
    {
        public bool Available { get; set; } = true;
        public IReadOnlyList<PayoutOutgoingTransfer> Transfers { get; set; } = [];
        public PayoutTransferPlan? CreateQr(FarmerPayout payout) => new(
            payout.PayoutCode, "MB", "MB", "333333333", payout.RecipientBankCode!,
            payout.RecipientAccountNumber!, payout.NetAmount, DateTime.UtcNow, "https://example.test/qr");
        public bool AuthenticateWebhook(string authorization, string alternateKey) => false;
        public Task<PayoutGatewayLookup> GetTransferStatusAsync(PayoutTransferPlan plan, CancellationToken ct) =>
            Task.FromResult(new PayoutGatewayLookup(Transfers, Available, null));
    }

    private sealed class Store : IFinanceFarmerPayoutRepository, IFarmerPaymentRequestRepository
    {
        private readonly FarmerProfile farmer = new()
        {
            FarmerProfileId = 13, UserId = 7, FarmName = "Test Farm", VerificationStatus = "Approved",
            User = new User { Status = "Active" }
        };
        private readonly InvoiceLine line;

        public Store()
        {
            var invoice = new Invoice { InvoiceId = 12, InvoiceNumber = "INV-TEST", IssuedAt = new(2025, 1, 1) };
            var seller = new SellerOrder
            {
                FarmerProfileId = 13, ReceivedAt = new(2025, 1, 1), Status = "Completed",
                Order = new Order { OrderType = "Retail" }
            };
            invoice.PaymentInvoiceAllocations.Add(new PaymentInvoiceAllocation
            {
                Amount = 100000,
                PaymentTransaction = new PaymentTransaction
                {
                    Amount = 100000, PaymentStatus = "Success", PaidAt = new(2025, 1, 1)
                }
            });
            line = new InvoiceLine
            {
                InvoiceLineId = 11, InvoiceId = 12, Invoice = invoice, LineAmount = 100000,
                Quantity = 10, UnitPriceSnapshot = 10000,
                OrderItem = new OrderItem { SellerOrder = seller, Product = new Product { CategoryId = 2 } }
            };
        }

        public UserBankAccount Bank { get; } = new()
        {
            BankCode = "VCB", AccountNumber = "0123456789", AccountHolder = "TEST FARM",
            VerificationStatus = "Verified", BankCodeNavigation = new SupportedBank { IsActive = true }
        };

        public decimal CommissionRate { get; set; } = 5;
        public bool NotificationFailed { get; set; }
        public FarmerPayout? Payout { get; private set; }
        public List<AuditLog> History { get; } = [];
        public List<Notification> Notifications { get; } = [];

        public FarmerPayoutContext Context() => new()
        {
            Farmer = farmer, Bank = Bank, Lines = [line],
            Claims = Payout?.PayoutItems.ToList() ?? [],
            Disputes = [], Refunds = [], History = History, Payout = Payout, NotificationFailed = NotificationFailed,
            Policies = [new CommissionPolicy
            {
                CategoryId = 2, ApplicableOrderType = "All", EffectiveDate = new(2024, 1, 1),
                CommissionRate = CommissionRate
            }],
            InvoiceTotals = new Dictionary<long, decimal> { [12] = 100000 }
        };

        public Task<bool> AuthorizedAsync(long actor, CancellationToken ct) => Task.FromResult(actor == 8);
        public Task<long?> FarmerIdAsync(long actor, CancellationToken ct) =>
            Task.FromResult<long?>(actor == 7 ? 13 : null);
        public Task<IReadOnlyList<long>> FinanceRecipientsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<long>>([8, 8]);
        public Task<object> SearchOwnedAsync(FarmerPayoutQuery query, CancellationToken ct) =>
            Task.FromResult<object>(Array.Empty<object>());
        public Task<object> FarmersAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<long>> PendingAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<FarmerPayoutContext?> LoadContextAsync(long farmerId, long? payoutId, bool update,
            CancellationToken ct) => Task.FromResult<FarmerPayoutContext?>(farmerId == 13 ? Context() : null);
        public Task<FarmerPayout?> FindRequestAsync(string key, CancellationToken ct) =>
            Task.FromResult(Payout?.RequestKey == key ? Payout : null);
        public Task<long?> FarmerForPayoutAsync(long id, CancellationToken ct) =>
            Task.FromResult<long?>(Payout?.FarmerPayoutId == id ? 13 : null);
        public Task<long?> FindOperationAsync(string key, CancellationToken ct) =>
            Task.FromResult<long?>(Payout?.PayoutCode == key ? 1 : null);
        public Task<AuditLog?> FindTraceAsync(string trace, CancellationToken ct) =>
            Task.FromResult(History.SingleOrDefault(x => x.TraceId == trace));
        public Task<T> SaveAtomicAsync<T>(Func<Task<T>> operation, CancellationToken ct) => operation();
        public Task FlushAsync(CancellationToken ct) => Task.CompletedTask;
        public void Add(FarmerPayout payout)
        {
            payout.FarmerPayoutId = 1;
            foreach (var item in payout.PayoutItems)
            {
                item.FarmerPayoutId = 1;
                item.FarmerPayout = payout;
            }
            Payout = payout;
        }
        public void Record(AuditLog entry)
        {
            entry.AuditLogId = History.Count + 1;
            History.Add(entry);
        }
        public void Queue(IEnumerable<Notification> notices) => Notifications.AddRange(notices);
    }
}



