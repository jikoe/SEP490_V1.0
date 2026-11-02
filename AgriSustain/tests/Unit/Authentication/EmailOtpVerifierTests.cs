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
}
