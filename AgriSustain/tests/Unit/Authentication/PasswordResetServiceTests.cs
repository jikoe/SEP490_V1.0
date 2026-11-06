using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Authentication;
using AgriSustain.Application.Validators.Authentication;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data.Demo;
using AgriSustain.Infrastructure.Data.InMemoryStore;
using AgriSustain.UnitTests.DuIte1;
using Xunit;

namespace AgriSustain.UnitTests.Authentication;

/// <summary>UC-AUTH-07 Reset Password (includes UC-AUTH-08 Verify Email OTP).</summary>
public sealed class PasswordResetServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 27, 1, 0, 0, TimeSpan.Zero);

    private readonly FakeEmailService _email = new();
    private readonly FakeClock _clock = new(T0);
    private readonly SqlDemoDependencies _store = new();
    private readonly UserStore _users = new();

    private PasswordResetService Service(params string[] otps)
    {
        var store = new InMemoryOtpChallengeStore(_store);
        return new PasswordResetService(
            _users,
            new PlainPasswords(),
            new EmailOtpVerifier(
                store, _email, new FakeOtpGenerator(otps.Length == 0 ? ["123456"] : otps), _clock),
            store,
            new ForgotPasswordRequestValidator(),
            new VerifyOtpRequestValidator(),
            new ResetPasswordRequestValidator(),
            _clock);
    }

    private async Task<(PasswordResetService Service, string Token)> VerifiedAsync()
    {
        var service = Service();
        await service.RequestCodeAsync(new(" VanA@Gmail.com "), default);
        var grant = await service.VerifyCodeAsync(new("vana@gmail.com", "123456"), default);
        return (service, grant.Value!.ResetToken);
    }

    private static ResetPasswordRequest Reset(string token, string password = "NewPass123",
        string? confirm = null) => new("vana@gmail.com", token, password, confirm ?? password);

    [Fact(DisplayName = "UTC-AUTH07-01 Registered e-mail receives a password-reset OTP")]
    public async Task RequestCode_RegisteredEmail_SendsOtp()
    {
        var result = await Service("482913").RequestCodeAsync(new(" VanA@Gmail.com "), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(("vana@gmail.com", "482913"), Assert.Single(_email.Sent));
        Assert.Equal(OtpPurposes.PasswordReset, _email.Purposes[0]);
        Assert.DoesNotContain("482913", result.Value);
    }

    [Fact(DisplayName = "UTC-AUTH07-02 Unknown e-mail gets the same answer and no e-mail")]
    public async Task RequestCode_UnknownEmail_SameAnswerNoEmail()
    {
        var known = await Service().RequestCodeAsync(new("vana@gmail.com"), default);
        var unknown = await Service().RequestCodeAsync(new("ghost@gmail.com"), default);

        Assert.True(unknown.IsSuccess);
        Assert.Equal(known.Value!.Replace("vana", "ghost"), unknown.Value);
        Assert.Single(_email.Sent);
    }

    [Fact(DisplayName = "UTC-AUTH07-03 E-mail delivery failure returns MSG26 (503)")]
    public async Task RequestCode_DeliveryFailure_ServiceUnavailable()
    {
        _email.Succeeds = false;

        var result = await Service().RequestCodeAsync(new("vana@gmail.com"), default);

        Assert.Equal(503, result.StatusCode);
        Assert.Empty(_store.OtpChallenges);
    }

    [Fact(DisplayName = "UTC-AUTH07-04 Wrong or unknown code does not reveal the reset fields")]
    public async Task VerifyCode_WrongCode_NoGrant()
    {
        var service = Service();
        await service.RequestCodeAsync(new("vana@gmail.com"), default);

        var wrong = await service.VerifyCodeAsync(new("vana@gmail.com", "000000"), default);
        var unknown = await service.VerifyCodeAsync(new("ghost@gmail.com", "123456"), default);

        Assert.Equal("INVALID_OTP", wrong.Error!.Code);
        Assert.Equal("INVALID_OTP", unknown.Error!.Code);
        Assert.Equal(400, unknown.StatusCode);
        Assert.DoesNotContain(_store.OtpChallenges.Keys, key => key.StartsWith(OtpPurposes.PasswordResetGrant));
    }

    [Fact(DisplayName = "UTC-AUTH07-05 A registration OTP cannot be used to reset the password")]
    public async Task VerifyCode_RegistrationCode_Rejected()
    {
        _store.OtpChallenges[$"{OtpPurposes.Registration}|vana@gmail.com"] =
            new(OtpPurposes.Registration, "vana@gmail.com", "123456", T0.AddMinutes(5), 0, T0);

        var result = await Service().VerifyCodeAsync(new("vana@gmail.com", "123456"), default);

        Assert.False(result.IsSuccess);
    }

    [Fact(DisplayName = "UTC-AUTH07-06 Valid code and password reset the password and invalidate the token")]
    public async Task ResetPassword_Valid_UpdatesHashOnce()
    {
        var (service, token) = await VerifiedAsync();

        var result = await service.ResetPasswordAsync(Reset(token), default);
        var reused = await service.ResetPasswordAsync(Reset(token, "Another123"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("hash:NewPass123", _users.User.PasswordHash);
        Assert.Equal(1, _users.Saves);
        Assert.Equal("ResetPassword", Assert.Single(_users.Audits).Action);
        Assert.Equal("RESET_SESSION_INVALID", reused.Error!.Code);
        Assert.Empty(_store.OtpChallenges);
    }

    [Theory(DisplayName = "UTC-AUTH07-07 Weak password or mismatch keeps the old password")]
    [InlineData("short1A", null, "newPassword")]
    [InlineData("nouppercase1", null, "newPassword")]
    [InlineData("NewPass123", "NewPass124", "confirmPassword")]
    public async Task ResetPassword_InvalidPassword_Rejected(string password, string? confirm, string field)
    {
        var (service, token) = await VerifiedAsync();

        var result = await service.ResetPasswordAsync(Reset(token, password, confirm), default);

        Assert.Contains(field, result.Error!.Fields!.Keys);
        Assert.Equal("hash:OldPass123", _users.User.PasswordHash);
        Assert.Single(_store.OtpChallenges);                         // the user may correct and retry
    }

    [Fact(DisplayName = "UTC-AUTH07-08 New password equal to the current one is rejected")]
    public async Task ResetPassword_SamePassword_Rejected()
    {
        var (service, token) = await VerifiedAsync();

        var result = await service.ResetPasswordAsync(Reset(token, "OldPass123"), default);

        Assert.Equal("PASSWORD_REUSED", result.Error!.Code);
        Assert.Equal(0, _users.Saves);
    }

    [Fact(DisplayName = "UTC-AUTH07-09 Forged or expired reset token is refused")]
    public async Task ResetPassword_BadToken_Refused()
    {
        var (service, token) = await VerifiedAsync();

        var forged = await service.ResetPasswordAsync(Reset("ABCDEF"), default);
        _clock.Advance(PasswordResetService.ResetTokenLifetime.Add(TimeSpan.FromSeconds(1)));
        var expired = await service.ResetPasswordAsync(Reset(token), default);

        Assert.Equal(401, forged.StatusCode);
        Assert.Equal(401, expired.StatusCode);
        Assert.Equal("hash:OldPass123", _users.User.PasswordHash);
    }

    [Fact(DisplayName = "UTC-AUTH07-10 Resend for an unknown e-mail answers like a known one")]
    public async Task ResendCode_UnknownEmail_Generic()
    {
        var result = await Service().ResendCodeAsync(new("ghost@gmail.com"), default);

        Assert.True(result.IsSuccess);
        Assert.Empty(_email.Sent);
    }

    private sealed class UserStore : IUserRepository
    {
        public User User { get; } = new()
        {
            UserId = 7, Email = "vana@gmail.com", Username = "vana", FullName = "Nguyen Van A",
            PasswordHash = "hash:OldPass123", Role = "RetailCustomer", Status = "Active"
        };
        public List<AuditLog> Audits { get; } = [];
        public int Saves { get; private set; }

        public Task<User?> FindByUsernameOrEmailAsync(string identifier, CancellationToken ct) =>
            FindByEmailAsync(identifier, ct);
        public Task<User?> FindByEmailAsync(string email, CancellationToken ct) =>
            Task.FromResult(User.Email == email ? User : null);
        public void AddAudit(AuditLog audit) => Audits.Add(audit);
        public Task SaveChangesAsync(CancellationToken ct) { Saves++; return Task.CompletedTask; }
    }

    private sealed class PlainPasswords : IPasswordService
    {
        public PasswordVerification Verify(User user, string password) =>
            new(user.PasswordHash == Hash(user, password), false);
        public string Hash(User user, string password) => $"hash:{password}";
    }
}
