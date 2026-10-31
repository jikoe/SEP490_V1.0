using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Services.Authentication;
using AgriSustain.Application.Services.Profiles;
using AgriSustain.Application.Validators.Profiles;
using AgriSustain.Infrastructure.Data.Demo;
using AgriSustain.Infrastructure.Data.InMemoryStore;
using Xunit;

namespace AgriSustain.UnitTests.DuIte1;

/// <summary>UC-AUTH-03 Manage Profile - Manage Bank Account (BR-BA-01..06).</summary>
public sealed class BankAccountServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 31, 3, 0, 0, TimeSpan.Zero);
    private const long Customer = 10, Farmer = 20, ApprovedFarmer = 21, Staff = 30;

    private readonly FakeBankRepository _repo = new();
    private readonly FakeEmailService _email = new();
    private readonly FakeBankEmailSender _changeMail = new();
    private readonly FakeLookup _lookup = new();
    private readonly FakeClock _clock = new(T0);
    private readonly SqlDemoDependencies _store = new();

    public BankAccountServiceTests()
    {
        _repo.Owners[Customer] = new(Customer, "RetailCustomer", "c@test.vn", "Khach Hang", null);
        _repo.Owners[Farmer] = new(Farmer, "Farmer", "f@test.vn", "Nong Ho", "NotSubmitted");
        _repo.Owners[ApprovedFarmer] = new(ApprovedFarmer, "Farmer", "af@test.vn", "Nong Ho 2", "Approved");
        _repo.Owners[Staff] = new(Staff, "FinanceStaff", "s@test.vn", "Nhan Vien", null);
    }

    private BankAccountService Service(long? userId, params string[] otps) => new(
        _repo, new FakeCurrentUser(userId), Verifier(otps), _lookup, _changeMail, _clock,
        new BankLookupRequestValidator(), new SaveBankAccountRequestValidator(),
        new RemoveBankAccountRequestValidator());

    private EmailOtpVerifier Verifier(string[] otps) => new(
        new InMemoryOtpChallengeStore(_store), _email,
        new FakeOtpGenerator(otps.Length == 0 ? ["123456"] : otps), _clock);

    private static SaveBankAccountRequest Save(
        string number = "1023456789", string otp = "123456", bool confirm = true,
        string bank = "VCB", string? holder = null) =>
        new(bank, number, holder, confirm, otp);

    private async Task<BankAccountService> WithOtp(long userId)
    {
        var service = Service(userId);
        Assert.True((await service.RequestOtpAsync(default)).IsSuccess);
        return service;
    }

    [Fact(DisplayName = "UTC-BA01 Customer sees refund purpose, supported banks and no account")]
    public async Task Get_Customer_NoAccount()
    {
        var result = await Service(Customer).GetAsync(default);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Account);
        Assert.True(result.Value.CanRemove);
        Assert.Contains(result.Value.Banks, bank => bank.BankCode == "VCB");
    }

    [Fact(DisplayName = "UTC-BA02 Staff roles cannot use the bank account tab")]
    public async Task Get_Staff_Forbidden()
    {
        var result = await Service(Staff).GetAsync(default);

        Assert.Equal("Forbidden", result.Error!.Kind);
    }

    [Theory(DisplayName = "UTC-BA03 Account number must be 6-19 digits (BR-BA-01)")]
    [InlineData("12345")]
    [InlineData("12345678901234567890")]
    [InlineData("12AB5678")]
    public async Task Lookup_InvalidNumber_Rejected(string number)
    {
        var result = await Service(Customer).LookupAsync(new BankLookupRequest("VCB", number), default);

        Assert.True(result.Error!.Fields!.ContainsKey("accountNumber"));
    }

    [Fact(DisplayName = "UTC-BA04 Unsupported bank is rejected")]
    public async Task Lookup_UnsupportedBank()
    {
        var result = await Service(Customer).LookupAsync(new BankLookupRequest("XYZ", "1023456789"), default);

        Assert.Equal("BANK_NOT_SUPPORTED", result.Error!.Code);
    }

    [Fact(DisplayName = "UTC-BA05 Save with correct OTP stores a Verified masked account and e-mails MSG-BA-05")]
    public async Task Save_Verified()
    {
        var service = await WithOtp(Customer);

        var result = await service.SaveAsync(Save(), default);

        Assert.True(result.IsSuccess);
        var account = result.Value!.Overview.Account!;
        Assert.Equal("Verified", account.VerificationStatus);
        Assert.Equal("•••• 6789", account.MaskedAccountNumber);
        Assert.Equal(FakeLookup.Holder, account.AccountHolder);
        Assert.Single(_changeMail.Sent);
    }

    [Fact(DisplayName = "UTC-BA06 Recipient confirmation tick is required (BR-BA-04)")]
    public async Task Save_WithoutConfirmation()
    {
        var result = await (await WithOtp(Customer)).SaveAsync(Save(confirm: false), default);

        Assert.True(result.Error!.Fields!.ContainsKey("confirmRecipient"));
        Assert.Null(_repo.Saved.GetValueOrDefault(Customer));
    }

    [Fact(DisplayName = "UTC-BA07 Wrong OTP does not save (MSG-BA-03)")]
    public async Task Save_WrongOtp()
    {
        var result = await (await WithOtp(Customer)).SaveAsync(Save(otp: "999999"), default);

        Assert.True(result.Error!.Fields!.ContainsKey("otpCode"));
        Assert.Empty(_repo.Saved);
    }

    [Fact(DisplayName = "UTC-BA08 Expired OTP does not save")]
    public async Task Save_ExpiredOtp()
    {
        var service = await WithOtp(Customer);
        _clock.Advance(TimeSpan.FromMinutes(6));

        var result = await service.SaveAsync(Save(), default);

        Assert.Equal("OTP_EXPIRED", result.Error!.Code);
    }

    [Fact(DisplayName = "UTC-BA09 Account not found at bank shows MSG-BA-04 and keeps the OTP")]
    public async Task Save_NotFound()
    {
        var service = await WithOtp(Customer);
        _lookup.Next = BankLookupStatuses.NotFound;

        var result = await service.SaveAsync(Save(), default);

        Assert.Equal("BANK_ACCOUNT_NOT_FOUND", result.Error!.Code);
        _lookup.Next = BankLookupStatuses.Found;
        Assert.True((await service.SaveAsync(Save(), default)).IsSuccess);
    }

    [Fact(DisplayName = "UTC-BA10 Lookup unavailable: saved as Unverified with the typed holder (BR-BA-06)")]
    public async Task Save_Unavailable_Unverified()
    {
        var service = await WithOtp(Customer);
        _lookup.Next = BankLookupStatuses.Unavailable;

        var result = await service.SaveAsync(Save(holder: "Do Mai Phuong"), default);

        Assert.Equal("Unverified", result.Value!.Overview.Account!.VerificationStatus);
        Assert.Equal("DO MAI PHUONG", result.Value.Overview.Account.AccountHolder);
    }

    [Fact(DisplayName = "UTC-BA11 Lookup unavailable without a holder name is rejected")]
    public async Task Save_Unavailable_NoHolder()
    {
        var service = await WithOtp(Customer);
        _lookup.Next = BankLookupStatuses.Unavailable;

        var result = await service.SaveAsync(Save(), default);

        Assert.True(result.Error!.Fields!.ContainsKey("accountHolder"));
    }

    [Fact(DisplayName = "UTC-BA12 Approved Farmer changing the account gets a 48-hour payout hold (BR-BA-05)")]
    public async Task Save_ApprovedFarmerChange_Hold()
    {
        _repo.Saved[ApprovedFarmer] = new("VBA", "5300205111111", "OLD", "Verified", null);
        var service = await WithOtp(ApprovedFarmer);

        var result = await service.SaveAsync(Save(number: "7400205222222", bank: "VBA"), default);

        Assert.Equal(T0.UtcDateTime.AddHours(48), result.Value!.Overview.Account!.PayoutHoldUntil);
    }

    [Fact(DisplayName = "UTC-BA13 Farmer adding a first account gets no payout hold")]
    public async Task Save_FarmerFirstAccount_NoHold()
    {
        var result = await (await WithOtp(Farmer)).SaveAsync(Save(), default);

        Assert.Null(result.Value!.Overview.Account!.PayoutHoldUntil);
    }

    [Fact(DisplayName = "UTC-BA14 Farmer cannot remove the payout account (MSG-BA-06)")]
    public async Task Remove_Farmer_Rejected()
    {
        _repo.Saved[Farmer] = new("VBA", "5300205111111", "X", "Verified", null);

        var result = await Service(Farmer).RemoveAsync(new RemoveBankAccountRequest("123456"), default);

        Assert.Equal("FARMER_ACCOUNT_REQUIRED", result.Error!.Code);
        Assert.True(_repo.Saved.ContainsKey(Farmer));
    }

    [Fact(DisplayName = "UTC-BA15 Customer removes the account with an OTP (MSG-BA-02)")]
    public async Task Remove_Customer()
    {
        _repo.Saved[Customer] = new("VCB", "1023456789", "X", "Verified", null);
        var service = await WithOtp(Customer);

        var result = await service.RemoveAsync(new RemoveBankAccountRequest("123456"), default);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Overview.Account);
        Assert.Single(_changeMail.Sent);
    }

    // ---------- Test doubles ----------

    private sealed record SavedAccount(
        string BankCode, string Number, string Holder, string Status, DateTime? HoldUntil);

    private sealed class FakeBankRepository : IBankAccountRepository
    {
        public Dictionary<long, BankAccountOwner> Owners { get; } = [];
        public Dictionary<long, SavedAccount> Saved { get; } = [];

        private static readonly IReadOnlyList<SupportedBankDto> s_banks =
        [
            new("VCB", "Vietcombank", "Ngân hàng TMCP Ngoại thương Việt Nam"),
            new("VBA", "Agribank", "Ngân hàng Nông nghiệp và Phát triển Nông thôn Việt Nam")
        ];

        public Task<BankAccountOwner?> GetOwnerAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult(Owners.GetValueOrDefault(userId));

        public Task<IReadOnlyList<SupportedBankDto>> GetActiveBanksAsync(CancellationToken cancellationToken) =>
            Task.FromResult(s_banks);

        public Task<BankAccountDto?> GetDefaultAccountAsync(long userId, CancellationToken cancellationToken)
        {
            if (!Saved.TryGetValue(userId, out var a)) return Task.FromResult<BankAccountDto?>(null);
            var bank = s_banks.First(b => b.BankCode == a.BankCode);
            string last4 = a.Number[^4..];
            return Task.FromResult<BankAccountDto?>(new BankAccountDto(a.BankCode, bank.ShortName, bank.FullName,
                "•••• " + last4, last4, a.Holder, a.Status, a.HoldUntil, null, null));
        }

        public Task SaveDefaultAccountAsync(long userId, string bankCode, string accountNumber,
            string accountHolder, string verificationStatus, DateTime now, DateTime? payoutHoldUntil,
            CancellationToken cancellationToken)
        {
            DateTime? hold = payoutHoldUntil ?? Saved.GetValueOrDefault(userId)?.HoldUntil;
            Saved[userId] = new(bankCode, accountNumber, accountHolder, verificationStatus, hold);
            return Task.CompletedTask;
        }

        public Task<bool> RemoveDefaultAccountAsync(long userId, DateTime now, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.Remove(userId));

        public Task<(string BankCode, string AccountNumber)?> GetDefaultAccountKeyAsync(
            long userId, CancellationToken cancellationToken) =>
            Task.FromResult<(string, string)?>(Saved.TryGetValue(userId, out var a) ? (a.BankCode, a.Number) : null);
    }

    private sealed class FakeLookup : IBankAccountLookup
    {
        public const string Holder = "KHACH HANG TEST";
        public string Next { get; set; } = BankLookupStatuses.Found;

        public Task<BankLookupResponse> LookupAsync(string bankCode, string accountNumber,
            CancellationToken cancellationToken) =>
            Task.FromResult(new BankLookupResponse(Next, Next == BankLookupStatuses.Found ? Holder : null));
    }

    private sealed class FakeBankEmailSender : IBankAccountEmailSender
    {
        public List<string> Sent { get; } = [];

        public Task<bool> SendBankAccountChangedAsync(string email, string? bankShortName, string? last4,
            DateTime changedAtUtc, CancellationToken cancellationToken)
        {
            Sent.Add(email);
            return Task.FromResult(true);
        }
    }
}
