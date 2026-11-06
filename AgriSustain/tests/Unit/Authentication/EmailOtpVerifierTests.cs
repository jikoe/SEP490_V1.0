using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Services.Authentication;
using AgriSustain.Application.Validators.Authentication;
using AgriSustain.Infrastructure.Data.Demo;
using AgriSustain.Infrastructure.Data.InMemoryStore;
using AgriSustain.UnitTests.DuIte1;
using Xunit;

namespace AgriSustain.UnitTests.Authentication;

/// <summary>UC-AUTH-08 Verify Email OTP: request validation and the verifier used directly.</summary>
public sealed class EmailOtpVerifierTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 27, 1, 0, 0, TimeSpan.Zero);

    [Theory(DisplayName = "[UC-AUTH-08 #01-#05] OTP code must be exactly six digits")]
    [InlineData("", false, "Mã OTP không được để trống.")]
    [InlineData("12345", false, "Mã OTP phải có đúng 6 ký tự.")]
    [InlineData("123456", true, null)]
    [InlineData("1234567", false, "Mã OTP phải có đúng 6 ký tự.")]
    [InlineData("12a456", false, "Mã OTP chỉ bao gồm chữ số.")]
    public void Validator_OtpCodeFormat(string code, bool valid, string? message)
    {
        var result = new VerifyOtpRequestValidator().Validate(new VerifyOtpRequest("vana@gmail.com", code));

        Assert.Equal(valid, result.IsValid);
        if (message is not null)
        {
            Assert.Contains(result.Errors, error => error.PropertyName == "OtpCode" && error.ErrorMessage == message);
        }
    }

    [Fact(DisplayName = "[UC-AUTH-08 #18] Surrounding spaces are trimmed before comparison")]
    public async Task Verify_CodeWithSpaces_Accepted()
    {
        var verifier = new EmailOtpVerifier(new InMemoryOtpChallengeStore(new SqlDemoDependencies()),
            new FakeEmailService(), new FakeOtpGenerator("123456"), new FakeClock(T0));
        await verifier.IssueAsync(OtpPurposes.Registration, "vana@gmail.com", default);

        var result = await verifier.VerifyAsync(OtpPurposes.Registration, "vana@gmail.com", " 123456 ", default);

        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    // ---------- BR-OTP-02 hourly send limit (MSG-OTP-06) ----------

    private readonly FakeClock _clock = new(T0);
    private readonly FakeEmailService _email = new();
    private readonly InMemoryOtpChallengeStore _store = new(new SqlDemoDependencies());

    private EmailOtpVerifier Verifier() => new(_store, _email, new FakeOtpGenerator(), _clock);

    /// <summary>Sends n codes to the address, waiting out the 60-second cooldown between them.</summary>
    private async Task SendAsync(EmailOtpVerifier verifier, int count, string purpose = OtpPurposes.Registration)
    {
        for (int i = 0; i < count; i++)
        {
            var sent = await verifier.IssueAsync(purpose, "vana@gmail.com", default);
            Assert.True(sent.IsSuccess, sent.Error?.Message);
            _clock.Advance(TimeSpan.FromSeconds(61));
        }
    }

    [Fact(DisplayName = "[UC-AUTH-08 #30] The sixth code within one hour is refused with MSG-OTP-06")]
    public async Task Issue_SixthCodeWithinHour_Refused()
    {
        var verifier = Verifier();
        await SendAsync(verifier, 5);

        var sixth = await verifier.IssueAsync(OtpPurposes.Registration, "vana@gmail.com", default);
        var resend = await verifier.ResendAsync(OtpPurposes.Registration, "vana@gmail.com", default);

        Assert.Equal("OTP_SEND_LIMIT", sixth.Error?.Code);
        Assert.Equal("TooManyRequests", sixth.Error!.Kind);
        Assert.Contains("55 phút", sixth.Error.Message);      // first code was sent 5 x 61 s ago
        Assert.Equal("OTP_SEND_LIMIT", resend.Error?.Code);
        Assert.Equal(5, _email.Sent.Count);
    }

    [Fact(DisplayName = "[UC-AUTH-08 #31] A code is allowed again once the oldest send leaves the hour")]
    public async Task Issue_AfterWindow_Allowed()
    {
        var verifier = Verifier();
        await SendAsync(verifier, 5);
        _clock.Advance(TimeSpan.FromMinutes(55));

        var next = await verifier.IssueAsync(OtpPurposes.Registration, "vana@gmail.com", default);

        Assert.True(next.IsSuccess, next.Error?.Message);
    }

    [Fact(DisplayName = "[UC-AUTH-08 #32] The limit counts every purpose sent to the same address")]
    public async Task Issue_LimitAcrossPurposes()
    {
        var verifier = Verifier();
        await SendAsync(verifier, 3, OtpPurposes.Registration);
        await SendAsync(verifier, 2, OtpPurposes.PasswordReset);

        var next = await verifier.IssueAsync(OtpPurposes.Registration, "VanA@gmail.com", default);

        Assert.Equal("OTP_SEND_LIMIT", next.Error?.Code);
    }

    [Fact(DisplayName = "[UC-AUTH-08 #33] An undelivered code (MSG26) does not count towards the limit")]
    public async Task Issue_FailedDelivery_NotCounted()
    {
        var verifier = Verifier();
        _email.Succeeds = false;
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal("EMAIL_DELIVERY_FAILED",
                (await verifier.IssueAsync(OtpPurposes.Registration, "vana@gmail.com", default)).Error?.Code);
        }
        _email.Succeeds = true;

        var delivered = await verifier.IssueAsync(OtpPurposes.Registration, "vana@gmail.com", default);

        Assert.True(delivered.IsSuccess, delivered.Error?.Message);
    }

    [Fact(DisplayName = "[UC-AUTH-08 #34] Expired and locked codes use the MSG-OTP-03 text")]
    public async Task Verify_ExpiredOrLocked_MsgOtp03()
    {
        var verifier = Verifier();
        await verifier.IssueAsync(OtpPurposes.Registration, "vana@gmail.com", default);
        _clock.Advance(EmailOtpVerifier.OtpLifetime + TimeSpan.FromSeconds(1));

        var expired = await verifier.VerifyAsync(OtpPurposes.Registration, "vana@gmail.com", "000000", default);

        Assert.Equal("OTP_EXPIRED", expired.Error?.Code);
        Assert.Equal("Mã xác thực này không còn dùng được. Hãy chọn \"Gửi lại mã\" để nhận mã mới.",
            expired.Error!.Message);
    }
}
