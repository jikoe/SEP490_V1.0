using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Services.Authentication;
using AgriSustain.Application.Validators.Authentication;
using Xunit;

namespace AgriSustain.UnitTests.DuIte1;

/// <summary>UC-AUTH-01 Register RetailCustomer / Farmer (includes UC-AUTH-08 Verify Email OTP). Report 5.1 – Module 1.</summary>
public sealed class RegistrationServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 27, 1, 0, 0, TimeSpan.Zero);

    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeEmailService _email = new();
    private readonly FakeClock _clock = new(T0);

    private AuthenticationService Service(params string[] otps) => new(
        _uow, new RegisterRequestValidator(), new VerifyOtpRequestValidator(), new ResendOtpRequestValidator(),
        _email, new FakeOtpGenerator(otps.Length == 0 ? ["123456"] : otps), _clock);

    private static RegisterRequest Valid(string role = "RetailCustomer", string email = "vana@gmail.com",
        string phone = "0912345678", string password = "Agri1234", string? confirm = null, string name = "Nguyen Van A") =>
        new(name, phone, email, password, confirm ?? password, role, true);

    // ---------- InitiateRegistrationAsync ----------

    [Fact(DisplayName = "UTC001 Register RetailCustomer with valid data sends OTP")]
    public async Task UTC001_ValidRetailCustomer_SendsOtp()
    {
        var result = await Service("482913").InitiateRegistrationAsync(Valid(), default);

        Assert.True(result.IsSuccess);
        Assert.Single(_email.Sent);
        Assert.Equal(("vana@gmail.com", "482913"), _email.Sent[0]);
        Assert.DoesNotContain("482913", result.Value);              // OTP is never returned to the client
        var pending = _uow.AuthRepo.Pending["vana@gmail.com"];
        Assert.Equal(T0.AddMinutes(5), pending.OtpExpiresAt);       // valid 5 minutes
        Assert.NotEqual("Agri1234", pending.PasswordHash);          // password is hashed
        Assert.True(BCrypt.Net.BCrypt.Verify("Agri1234", pending.PasswordHash));
    }

    [Fact(DisplayName = "UTC002 Register Farmer with valid data sends OTP")]
    public async Task UTC002_ValidFarmer_SendsOtp()
    {
        var result = await Service().InitiateRegistrationAsync(
            Valid("Farmer", "htx@gmail.com", "0987654321", "Farmer123", name: "HTX Rau Sach"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Farmer", _uow.AuthRepo.Pending["htx@gmail.com"].Role);
    }

    [Fact(DisplayName = "UTC003 Empty full name and phone are rejected")]
    public async Task UTC003_EmptyRequiredFields_Rejected()
    {
        var result = await Service().InitiateRegistrationAsync(Valid(name: "", phone: ""), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Kind);
        Assert.Contains("fullName", result.Error.Fields!.Keys);
        Assert.Contains("phoneNumber", result.Error.Fields.Keys);
        Assert.Empty(_email.Sent);
    }

    [Fact(DisplayName = "UTC004 Password with 7 characters violates policy")]
    public async Task UTC004_ShortPassword_Rejected()
    {
        var result = await Service().InitiateRegistrationAsync(Valid(password: "A1bcdef"), default);

        Assert.False(result.IsSuccess);
        Assert.Contains("password", result.Error!.Fields!.Keys);
        Assert.Empty(_email.Sent);
    }

    [Fact(DisplayName = "UTC005 Password confirmation mismatch is rejected")]
    public async Task UTC005_ConfirmMismatch_Rejected()
    {
        var result = await Service().InitiateRegistrationAsync(Valid(confirm: "Agri9999"), default);

        Assert.False(result.IsSuccess);
        Assert.Contains("confirmPassword", result.Error!.Fields!.Keys);
    }

    [Theory(DisplayName = "UTC006 Existing email or phone returns Conflict")]
    [InlineData("existing@gmail.com", "0912345678", "email")]
    [InlineData("new@gmail.com", "0900000000", "phoneNumber")]
    public async Task UTC006_Duplicate_ReturnsConflict(string email, string phone, string field)
    {
        _uow.AuthRepo.ExistingEmails.Add("existing@gmail.com");
        _uow.AuthRepo.ExistingPhones.Add("0900000000");

        var result = await Service().InitiateRegistrationAsync(Valid(email: email, phone: phone), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("Conflict", result.Error!.Kind);
        Assert.Contains(field, result.Error.Fields!.Keys);
        Assert.Empty(_uow.AuthRepo.Pending);
    }

    [Fact(DisplayName = "UTC007 Email delivery failure returns 503 and keeps no pending registration")]
    public async Task UTC007_EmailFailure_ServiceUnavailable()
    {
        _email.Succeeds = false;

        var result = await Service().InitiateRegistrationAsync(Valid(), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("ServiceUnavailable", result.Error!.Kind);
        Assert.Equal(503, result.StatusCode);
        Assert.Empty(_uow.AuthRepo.Pending);
    }

    [Theory(DisplayName = "UTC-AUTH01-B1 Invalid role, terms not accepted or bad email are rejected")]
    [InlineData("Administrator", true, "vana@gmail.com", "role")]
    [InlineData("RetailCustomer", false, "vana@gmail.com", "agreeTerms")]
    [InlineData("RetailCustomer", true, "not-an-email", "email")]
    public async Task UTC_B1_InvalidInputs_Rejected(string role, bool agree, string email, string field)
    {
        var request = Valid(role, email) with { AgreeTerms = agree };

        var result = await Service().InitiateRegistrationAsync(request, default);

        Assert.False(result.IsSuccess);
        Assert.Contains(field, result.Error!.Fields!.Keys);
    }

    [Theory(DisplayName = "UTC-AUTH01-B2 Full name length boundary 2..50")]
    [InlineData("A", false)]
    [InlineData("AB", true)]
    [InlineData("50", true)]
    [InlineData("51", false)]
    public async Task UTC_B2_FullNameBoundary(string name, bool ok)
    {
        string value = name switch { "50" => new string('a', 50), "51" => new string('a', 51), _ => name };

        var result = await Service().InitiateRegistrationAsync(Valid(name: value), default);

        Assert.Equal(ok, result.IsSuccess);
    }

    [Fact(DisplayName = "UTC-AUTH01-B3 Email is trimmed and lower-cased before duplicate check")]
    public async Task UTC_B3_EmailNormalized()
    {
        _uow.AuthRepo.ExistingEmails.Add("vana@gmail.com");

        var result = await Service().InitiateRegistrationAsync(Valid(email: "  VanA@Gmail.com "), default);

        Assert.Equal("Conflict", result.Error!.Kind);
    }

    // ---------- VerifyOtpAndCompleteRegistrationAsync ----------

    private async Task<AuthenticationService> RegisteredAsync(string role = "RetailCustomer", string otp = "123456")
    {
        var service = Service(otp, "654321");
        var email = role == "Farmer" ? "htx@gmail.com" : "vana@gmail.com";
        var phone = role == "Farmer" ? "0987654321" : "0912345678";
        Assert.True((await service.InitiateRegistrationAsync(Valid(role, email, phone), default)).IsSuccess);
        return service;
    }

    [Fact(DisplayName = "UTC008 Correct OTP creates an active RetailCustomer")]
    public async Task UTC008_CorrectOtp_CreatesRetailCustomer()
    {
        var service = await RegisteredAsync();

        var result = await service.VerifyOtpAndCompleteRegistrationAsync(new("vana@gmail.com", "123456"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("RetailCustomer", result.Value!.Role);
        Assert.Null(result.Value.Token);                             // user must sign in afterwards
        var user = Assert.Single(_uow.AuthRepo.CreatedUsers);
        Assert.Equal("Active", user.Status);
        Assert.True(user.IsEmailVerified);
        Assert.Empty(_uow.AuthRepo.Pending);                         // OTP is single-use
        Assert.Equal(1, _uow.Commits);
    }

    [Fact(DisplayName = "UTC009 Correct OTP creates a Farmer account")]
    public async Task UTC009_CorrectOtp_CreatesFarmer()
    {
        var service = await RegisteredAsync("Farmer", "654321");

        var result = await service.VerifyOtpAndCompleteRegistrationAsync(new("htx@gmail.com", "654321"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Farmer", result.Value!.Role);
    }

    [Fact(DisplayName = "UTC010 Wrong OTP increments attempts and creates no user")]
    public async Task UTC010_WrongOtp_IncrementsAttempts()
    {
        var service = await RegisteredAsync();

        var result = await service.VerifyOtpAndCompleteRegistrationAsync(new("vana@gmail.com", "000000"), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_OTP", result.Error!.Code);
        Assert.Equal(1, _uow.AuthRepo.Pending["vana@gmail.com"].AttemptCount);
        Assert.Empty(_uow.AuthRepo.CreatedUsers);
    }

    [Fact(DisplayName = "UTC011 OTP older than 5 minutes is expired and invalidated")]
    public async Task UTC011_ExpiredOtp_Invalidated()
    {
        var service = await RegisteredAsync();
        _clock.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));

        var result = await service.VerifyOtpAndCompleteRegistrationAsync(new("vana@gmail.com", "123456"), default);

        Assert.Equal("OTP_EXPIRED", result.Error!.Code);
        Assert.Empty(_uow.AuthRepo.Pending);
        Assert.Empty(_uow.AuthRepo.CreatedUsers);
    }

    [Fact(DisplayName = "UTC-AUTH08-B1 OTP exactly at 5 minutes is still accepted")]
    public async Task UTC_AUTH08_B1_OtpAtExpiryBoundary_Accepted()
    {
        var service = await RegisteredAsync();
        _clock.Advance(TimeSpan.FromMinutes(5));

        var result = await service.VerifyOtpAndCompleteRegistrationAsync(new("vana@gmail.com", "123456"), default);

        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "UTC012 After 5 wrong attempts even the correct OTP is refused")]
    public async Task UTC012_FiveWrongAttempts_Locks()
    {
        var service = await RegisteredAsync();
        for (int i = 0; i < 5; i++)
        {
            var wrong = await service.VerifyOtpAndCompleteRegistrationAsync(new("vana@gmail.com", "000000"), default);
            Assert.Equal("INVALID_OTP", wrong.Error!.Code);
        }

        var sixth = await service.VerifyOtpAndCompleteRegistrationAsync(new("vana@gmail.com", "123456"), default);

        Assert.Equal("MAX_ATTEMPTS_EXCEEDED", sixth.Error!.Code);
        Assert.Empty(_uow.AuthRepo.Pending);
        Assert.Empty(_uow.AuthRepo.CreatedUsers);
    }

    [Fact(DisplayName = "UTC-AUTH08-B2 Legacy universal code 123456 no longer works")]
    public async Task UTC_AUTH08_B2_NoUniversalOtp()
    {
        var service = await RegisteredAsync(otp: "777777");

        var result = await service.VerifyOtpAndCompleteRegistrationAsync(new("vana@gmail.com", "123456"), default);

        Assert.Equal("INVALID_OTP", result.Error!.Code);
    }

    [Fact(DisplayName = "UTC-AUTH08-B3 Verify without a pending registration returns NotFound")]
    public async Task UTC_AUTH08_B3_NoPending_NotFound()
    {
        var result = await Service().VerifyOtpAndCompleteRegistrationAsync(new("ghost@gmail.com", "123456"), default);

        Assert.Equal("NotFound", result.Error!.Kind);
    }

    [Fact(DisplayName = "UTC-AUTH08-B4 Email taken while OTP was pending returns Conflict and invalidates OTP")]
    public async Task UTC_AUTH08_B4_RaceDuplicate_Conflict()
    {
        var service = await RegisteredAsync();
        _uow.AuthRepo.ExistingEmails.Add("vana@gmail.com");

        var result = await service.VerifyOtpAndCompleteRegistrationAsync(new("vana@gmail.com", "123456"), default);

        Assert.Equal("Conflict", result.Error!.Kind);
        Assert.Empty(_uow.AuthRepo.Pending);
        Assert.Equal(1, _uow.Rollbacks);
    }

    // ---------- ResendOtpAsync ----------

    [Fact(DisplayName = "UTC-AUTH08-B5 Resend within 60 seconds is refused (429)")]
    public async Task UTC_AUTH08_B5_ResendCooldown()
    {
        var service = await RegisteredAsync();
        _clock.Advance(TimeSpan.FromSeconds(59));

        var result = await service.ResendOtpAsync(new("vana@gmail.com"), default);

        Assert.Equal("TooManyRequests", result.Error!.Kind);
        Assert.Equal(429, result.StatusCode);
        Assert.Single(_email.Sent);
    }

    [Fact(DisplayName = "UTC-AUTH08-B6 Resend after 60 seconds issues a new OTP and invalidates the old one")]
    public async Task UTC_AUTH08_B6_ResendAfterCooldown()
    {
        var service = await RegisteredAsync();                       // first OTP 123456, next 654321
        await service.VerifyOtpAndCompleteRegistrationAsync(new("vana@gmail.com", "000000"), default);
        _clock.Advance(TimeSpan.FromSeconds(60));

        var resend = await service.ResendOtpAsync(new("vana@gmail.com"), default);
        var oldCode = await service.VerifyOtpAndCompleteRegistrationAsync(new("vana@gmail.com", "123456"), default);
        var newCode = await service.VerifyOtpAndCompleteRegistrationAsync(new("vana@gmail.com", "654321"), default);

        Assert.True(resend.IsSuccess);
        Assert.Equal(2, _email.Sent.Count);
        Assert.Equal("INVALID_OTP", oldCode.Error!.Code);
        Assert.True(newCode.IsSuccess);
    }
}
