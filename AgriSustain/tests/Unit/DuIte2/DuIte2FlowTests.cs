using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Services.Moderation;
using AgriSustain.Application.Services.Refunds;
using AgriSustain.Application.Services.Traceability;
using AgriSustain.UnitTests.DuIte1;
using Xunit;

namespace AgriSustain.UnitTests.DuIte2;

/// <summary>UC-FIN-08 Provide Refund Account: ProvideAccountAsync (Steps 4-7, AF1-AF3).</summary>
public sealed class RefundAccountFlowTests
{
    private const long Customer = 10;
    private readonly FakeRefundRepository _refunds = new();
    private readonly FakeBanks _banks = new();
    private readonly FakeLookup _lookup = new();
    private readonly FakeOtp _otp = new();

    public RefundAccountFlowTests()
    {
        _banks.Owner = new BankAccountOwner(Customer, "RetailCustomer", "c@test.vn", "Khach Hang", null);
        _refunds.Row = new RefundRequestRecord(1, Customer, "RFD-1", "ORD-1", "Nông trại", 85000m, "Rau dập",
            "Pending", null, null, null, null, null, null, null, null);
    }

    private RefundAccountService Service(string role = "RetailCustomer") => new(_refunds, _banks, _lookup, _otp,
        new FakeCurrentUser(Customer, role), new FakeClock(new DateTimeOffset(2026, 11, 1, 3, 0, 0, TimeSpan.Zero)));

    private static ProvideRefundAccountRequest NewAccount(string number = "1023456789", bool confirm = true,
        bool saveAsDefault = false, string? otp = null) =>
        new(false, "VCB", number, null, confirm, saveAsDefault, otp);

    [Fact(DisplayName = "UTC-RFA05 New account found at the bank is saved; refund becomes ReadyForTransfer")]
    public async Task NewAccount_Saved()
    {
        var result = await Service().ProvideAccountAsync(1, NewAccount(), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(RefundAccountStates.ReadyForTransfer, result.Value!.AccountState);
        Assert.Equal(FakeLookup.Holder, _refunds.Row!.ReceiverAccountHolder);
        Assert.Null(_banks.Saved);
    }

    [Fact(DisplayName = "UTC-RFA06 Use the saved default account")]
    public async Task DefaultAccount_Used()
    {
        _banks.Saved = ("VBA", "5555666677", "KHACH HANG");

        var result = await Service().ProvideAccountAsync(1, new(true, null, null, null, true, false, null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("5555666677", _refunds.Row!.ReceiverAccountNumber);
    }

    [Fact(DisplayName = "UTC-RFA07 Use default account when none is saved is rejected")]
    public async Task DefaultAccount_Missing()
    {
        var result = await Service().ProvideAccountAsync(1, new(true, null, null, null, true, false, null), default);

        Assert.True(result.Error!.Fields!.ContainsKey("useDefaultAccount"));
    }

    [Fact(DisplayName = "UTC-RFA08 Recipient not confirmed is rejected")]
    public async Task NotConfirmed() =>
        Assert.True((await Service().ProvideAccountAsync(1, NewAccount(confirm: false), default))
            .Error!.Fields!.ContainsKey("confirmRecipient"));

    [Theory(DisplayName = "UTC-RFA09 Account number must be 6..19 digits (AF1)")]
    [InlineData("12345")]
    [InlineData("12345678901234567890")]
    [InlineData("10234AB789")]
    public async Task InvalidNumber(string number)
    {
        var result = await Service().ProvideAccountAsync(1, NewAccount(number), default);

        Assert.True(result.Error!.Fields!.ContainsKey("accountNumber"));
        Assert.Null(_refunds.Row!.ReceiverAccountNumber);
    }

    [Theory(DisplayName = "UTC-RFA10 Boundary 6 and 19 digits are accepted")]
    [InlineData("123456")]
    [InlineData("1234567890123456789")]
    public async Task NumberBoundary(string number) =>
        Assert.True((await Service().ProvideAccountAsync(1, NewAccount(number), default)).IsSuccess);

    [Fact(DisplayName = "UTC-RFA11 Account not found at the bank is rejected (AF2)")]
    public async Task NotFoundAtBank()
    {
        _lookup.Next = BankLookupStatuses.NotFound;

        var result = await Service().ProvideAccountAsync(1, NewAccount(), default);

        Assert.True(result.Error!.Fields!.ContainsKey("accountNumber"));
    }

    [Fact(DisplayName = "UTC-RFA12 Save as default needs a valid OTP")]
    public async Task SaveAsDefault_Otp()
    {
        var noOtp = await Service().ProvideAccountAsync(1, NewAccount(saveAsDefault: true), default);
        var wrong = await Service().ProvideAccountAsync(1, NewAccount(saveAsDefault: true, otp: "000000"), default);
        var right = await Service().ProvideAccountAsync(1, NewAccount(saveAsDefault: true, otp: FakeOtp.Code), default);

        Assert.True(noOtp.Error!.Fields!.ContainsKey("otpCode"));
        Assert.True(wrong.Error!.Fields!.ContainsKey("otpCode"));
        Assert.True(right.IsSuccess);
        Assert.Equal("1023456789", _banks.Saved!.Value.Number);
    }

    [Fact(DisplayName = "UTC-RFA13 Refund no longer Pending cannot change account (AF3)")]
    public async Task Locked()
    {
        _refunds.Row = _refunds.Row! with { RefundStatus = "Completed" };

        Assert.Equal("REFUND_LOCKED", (await Service().ProvideAccountAsync(1, NewAccount(), default)).Error!.Code);
    }

    [Fact(DisplayName = "UTC-RFA14 Refund of another user is not found; Farmer is forbidden")]
    public async Task NotMine_Forbidden()
    {
        Assert.Equal("REFUND_NOT_FOUND", (await Service().ProvideAccountAsync(99, NewAccount(), default)).Error!.Code);
        Assert.Equal("FORBIDDEN", (await Service("Farmer").ProvideAccountAsync(1, NewAccount(), default)).Error!.Code);
    }

    private sealed class FakeRefundRepository : IRefundAccountRepository
    {
        public RefundRequestRecord? Row { get; set; }

        public Task<IReadOnlyList<RefundRequestRecord>> ListMineAsync(long userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RefundRequestRecord>>(Row is null ? [] : [Row]);

        public Task<RefundRequestRecord?> FindMineAsync(long userId, long refundId, CancellationToken ct) =>
            Task.FromResult(Row is not null && Row.RefundId == refundId && Row.OwnerUserId == userId ? Row : null);

        public Task<IReadOnlyList<RefundHistoryDto>> HistoryAsync(long refundId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RefundHistoryDto>>([]);

        public Task<bool> SaveReceiverAsync(long refundId, string bankCode, string accountNumber, string accountHolder,
            long userId, DateTime now, CancellationToken ct)
        {
            Row = Row! with
            {
                ReceiverBankCode = bankCode,
                ReceiverBankShortName = bankCode,
                ReceiverAccountNumber = accountNumber,
                ReceiverAccountHolder = accountHolder,
                ReceiverAccountProvidedAt = now
            };
            return Task.FromResult(true);
        }
    }

    private sealed class FakeBanks : IBankAccountRepository
    {
        public BankAccountOwner? Owner { get; set; }
        public (string BankCode, string Number, string Holder)? Saved { get; set; }

        private static readonly IReadOnlyList<SupportedBankDto> s_banks =
            [new("VCB", "Vietcombank", "Vietcombank"), new("VBA", "Agribank", "Agribank")];

        public Task<BankAccountOwner?> GetOwnerAsync(long userId, CancellationToken ct) => Task.FromResult(Owner);

        public Task<IReadOnlyList<SupportedBankDto>> GetActiveBanksAsync(CancellationToken ct) => Task.FromResult(s_banks);

        public Task<BankAccountDto?> GetDefaultAccountAsync(long userId, CancellationToken ct) =>
            Task.FromResult(Saved is { } a
                ? new BankAccountDto(a.BankCode, a.BankCode, a.BankCode, "•••• " + a.Number[^4..], a.Number[^4..],
                    a.Holder, "Verified", null, null, null)
                : null);

        public Task SaveDefaultAccountAsync(long userId, string bankCode, string accountNumber, string accountHolder,
            string verificationStatus, DateTime now, DateTime? payoutHoldUntil, CancellationToken ct)
        {
            Saved = (bankCode, accountNumber, accountHolder);
            return Task.CompletedTask;
        }

        public Task<bool> RemoveDefaultAccountAsync(long userId, DateTime now, CancellationToken ct) =>
            Task.FromResult(true);

        public Task<(string BankCode, string AccountNumber)?> GetDefaultAccountKeyAsync(long userId, CancellationToken ct) =>
            Task.FromResult<(string, string)?>(Saved is { } a ? (a.BankCode, a.Number) : null);
    }

    private sealed class FakeLookup : IBankAccountLookup
    {
        public const string Holder = "KHACH HANG TEST";
        public string Next { get; set; } = BankLookupStatuses.Found;

        public Task<BankLookupResponse> LookupAsync(string bankCode, string accountNumber, CancellationToken ct) =>
            Task.FromResult(new BankLookupResponse(Next, Next == BankLookupStatuses.Found ? Holder : null));
    }

    private sealed class FakeOtp : IEmailOtpVerifier
    {
        public const string Code = "482913";

        public Task<Result<string>> IssueAsync(string purpose, string email, CancellationToken ct) =>
            Task.FromResult(Result<string>.SuccessResult("sent"));

        public Task<Result<bool>> VerifyAsync(string purpose, string email, string otpCode, CancellationToken ct) =>
            Task.FromResult(otpCode == Code
                ? Result<bool>.SuccessResult(true)
                : Result<bool>.FailureResult(new Error("INVALID_OTP", "Mã OTP không đúng.", "Validation")));

        public Task<Result<string>> ResendAsync(string purpose, string email, CancellationToken ct) =>
            Task.FromResult(Result<string>.SuccessResult("sent"));

        public Task InvalidateAsync(string purpose, string email, CancellationToken ct) => Task.CompletedTask;
    }
}

/// <summary>UC-CM-02 extra branches of ModerateAsync.</summary>
public sealed class ReviewModerationBranchTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc);
    private readonly FakeReviewRepository _repo = new();

    private ReviewModerationService Service() =>
        new(_repo, new FakeCurrentUser(9, "ContentModerator"), new FakeClock(new DateTimeOffset(Now)));

    [Fact(DisplayName = "UTC-REV06 Unknown review is NotFound")]
    public async Task NotFound()
    {
        Assert.Equal("REVIEW_NOT_FOUND", (await Service().GetAsync(404, default)).Error!.Code);
        Assert.Equal("REVIEW_NOT_FOUND",
            (await Service().ModerateAsync(404, new(ReviewDecisions.Hide, "Spam"), default)).Error!.Code);
    }

    [Fact(DisplayName = "UTC-REV07 Restore a visible review and KeepVisible a hidden one are blocked")]
    public async Task InvalidTransitions()
    {
        _repo.Add(1, ReviewStatuses.Visible);
        _repo.Add(2, ReviewStatuses.Hidden);

        Assert.Equal("NOT_HIDDEN", (await Service().ModerateAsync(1, new(ReviewDecisions.Restore, null), default)).Error!.Code);
        Assert.Equal("USE_RESTORE", (await Service().ModerateAsync(2, new(ReviewDecisions.KeepVisible, null), default)).Error!.Code);
        Assert.Equal("ALREADY_HIDDEN", (await Service().ModerateAsync(2, new(ReviewDecisions.Hide, "Spam"), default)).Error!.Code);
    }

    [Fact(DisplayName = "UTC-REV08 Unknown decision is rejected")]
    public async Task UnknownDecision()
    {
        _repo.Add(1, ReviewStatuses.Visible);

        Assert.Equal("INVALID_DECISION", (await Service().ModerateAsync(1, new("Delete", "x"), default)).Error!.Code);
    }

    [Theory(DisplayName = "UTC-REV09 Hide reason length 500 is accepted, 501 is rejected")]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public async Task ReasonBoundary(int length, bool ok)
    {
        _repo.Add(1, ReviewStatuses.Visible);

        var result = await Service().ModerateAsync(1, new(ReviewDecisions.Hide, new string('a', length)), default);

        Assert.Equal(ok, result.IsSuccess);
    }

    private sealed class FakeReviewRepository : IReviewModerationRepository
    {
        private readonly Dictionary<long, ReviewRecord> _rows = [];

        public void Add(long id, string status) =>
            _rows[id] = new ReviewRecord(id, 100 + id, "Khách " + id, 1, "Rau cải", "ORD-" + id, 4, "Ngon",
                Now.AddHours(-id), status, null, false);

        public Task<IReadOnlyList<ReviewRecord>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ReviewRecord>>(_rows.Values.ToList());

        public Task<ReviewRecord?> FindAsync(long id, CancellationToken ct) => Task.FromResult(_rows.GetValueOrDefault(id));

        public Task<IReadOnlyList<(long UserId, DateTime ReviewDate)>> ReviewDatesAsync(
            IReadOnlyCollection<long> userIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<(long, DateTime)>>(_rows.Values.Select(r => (r.UserId, r.ReviewDate)).ToList());

        public Task ApplyDecisionAsync(long id, string decision, string newStatus, string? reason, long moderator,
            DateTime now, CancellationToken ct)
        {
            _rows[id] = _rows[id] with { ModerationStatus = newStatus, ModerationReason = reason, IsReviewed = true };
            return Task.CompletedTask;
        }
    }
}

/// <summary>UC-TRC-01 successful lookup and empty code.</summary>
public sealed class TraceabilityLookupTests
{
    private TraceabilityService Service(FakeTrace repo) => new(repo, new FakeClock(DateTimeOffset.UtcNow));

    [Fact(DisplayName = "UTC-TRC04 Valid code returns origin, cultivation log and counts the scan")]
    public async Task Found()
    {
        var repo = new FakeTrace();

        var result = await Service(repo).LookupAsync("https://agrisustain.vn/trace/BATCH-DL-001", default);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsRevoked);
        Assert.Null(result.Value.RecallMessage);
        Assert.Single(result.Value.CultivationLogs);
        Assert.Equal("BATCH-DL-001", repo.LastCode);
    }

    [Theory(DisplayName = "UTC-TRC05 Empty code is rejected without querying")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Empty(string? code)
    {
        var repo = new FakeTrace();

        var result = await Service(repo).LookupAsync(code, default);

        Assert.Equal("INVALID_CODE", result.Error!.Code);
        Assert.Null(repo.LastCode);
    }

    private sealed class FakeTrace : ITraceabilityRepository
    {
        public string? LastCode { get; private set; }

        public Task<TraceabilityDto?> FindAndCountScanAsync(string code, DateOnly today, CancellationToken ct)
        {
            LastCode = code;
            return Task.FromResult<TraceabilityDto?>(code != "BATCH-DL-001" ? null : new TraceabilityDto(code, false,
                null, 1, "Bơ 034", null, "kg", "Lâm Đồng", new DateOnly(2026, 10, 1), false, null, "Loại 1",
                "Nông trại Bơ", "Bảo Lộc", "Thôn 4", 4.8m, "Vụ 2026", "Bơ 034", new DateOnly(2026, 3, 1), 3,
                [new TraceCultivationLogDto(new DateTime(2026, 4, 1), "Bón phân", null, null, null, null, null)], []));
        }
    }
}
