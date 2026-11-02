using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Infrastructure.Data.Demo;
using AgriSustain.IntegrationTests.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgriSustain.IntegrationTests.UseCaseApi;

/// <summary>UC-AUTH-08 Verify Email OTP and UC-AUTH-07 Reset Password through AuthController.</summary>
[Trait("Layer", "SqlIntegration")]
public sealed class AuthOtpApiTests : IAsyncLifetime
{
    private const string NewEmail = "newbuyer@gmail.com";
    private ApiHost _api = null!;

    public async Task InitializeAsync() => _api = await ApiHost.StartAsync();

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private static RegisterRequest Registration(string email = NewEmail) =>
        new("Nguyen Van B", "0987654321", email, "StrongPass1", "StrongPass1", "RetailCustomer", true);

    private async Task<OtpChallenge?> ChallengeAsync(string purpose, string email)
    {
        var store = _api.Service(sp => sp.GetRequiredService<IOtpChallengeStore>(), out var scope);
        using (scope)
        {
            return await store.FindAsync(purpose, email, default);
        }
    }

    private Task<HttpResponseMessage> Post<T>(string url, T body) => _api.Anonymous().PostAsJsonAsync(url, body);

    // ---------- UC-AUTH-08 ----------

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-08 #02] Gmail delivery failure returns 503 and stores nothing")]
    public async Task Register_GmailUnreachable_ServiceUnavailable()
    {
        await using var gmail = await ApiHost.StartAsync(unreachableGmail: true);

        var response = await gmail.Anonymous().PostAsJsonAsync("api/auth/register", Registration());

        await ApiAssert.Problem(response, HttpStatusCode.ServiceUnavailable, "EMAIL_DELIVERY_FAILED");
        var demo = gmail.Service(sp => sp.GetRequiredService<SqlDemoDependencies>(), out var scope);
        using (scope)
        {
            Assert.Empty(demo.OtpChallenges);
            Assert.Empty(demo.PendingRegistrations);
        }
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-08 #04] Register issues one OTP with a 5-minute lifetime")]
    public async Task Register_Valid_IssuesOtp()
    {
        _api.Otp.Next("482913");

        var response = await Post("api/auth/register", Registration());

        string body = (await ApiAssert.Status(response, HttpStatusCode.OK)).ToString();
        Assert.DoesNotContain("482913", body);
        var challenge = await ChallengeAsync(OtpPurposes.Registration, NewEmail);
        Assert.NotNull(challenge);
        Assert.Equal((ApiHost.T0.AddMinutes(5), 0), (challenge!.ExpiresAt, challenge.AttemptCount));
        var sent = Assert.Single(_api.Email.Sent);
        Assert.Equal((NewEmail, "482913"), (sent.Email, sent.Otp));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-08 #05] Correct OTP creates an active, verified account in SQL")]
    public async Task VerifyOtp_Correct_CreatesUser()
    {
        _api.Otp.Next("482913");
        await Post("api/auth/register", Registration());

        await ApiAssert.Status(await Post("api/auth/verify-otp", new VerifyOtpRequest(NewEmail, "482913")), HttpStatusCode.OK);

        await using var db = _api.Open();
        var user = await db.Users.SingleAsync(candidate => candidate.Email == NewEmail);
        Assert.Equal(("Active", true), (user.Status, user.IsEmailVerified));
        Assert.Null(await ChallengeAsync(OtpPurposes.Registration, NewEmail));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-08 #06] Wrong OTP is counted")]
    public async Task VerifyOtp_Wrong_Counted()
    {
        _api.Otp.Next("482913");
        await Post("api/auth/register", Registration());

        var response = await Post("api/auth/verify-otp", new VerifyOtpRequest(NewEmail, "000000"));

        await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "INVALID_OTP");
        Assert.Equal(1, (await ChallengeAsync(OtpPurposes.Registration, NewEmail))!.AttemptCount);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-08 #07] After five wrong codes even the correct code is refused")]
    public async Task VerifyOtp_FiveWrong_Locked()
    {
        _api.Otp.Next("482913");
        await Post("api/auth/register", Registration());
        for (int attempt = 0; attempt < 5; attempt++)
        {
            await Post("api/auth/verify-otp", new VerifyOtpRequest(NewEmail, "000000"));
        }

        var response = await Post("api/auth/verify-otp", new VerifyOtpRequest(NewEmail, "482913"));

        await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "MAX_ATTEMPTS_EXCEEDED");
        await using var db = _api.Open();
        Assert.False(await db.Users.AnyAsync(user => user.Email == NewEmail));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-08 #08] OTP is expired one second after five minutes")]
    public async Task VerifyOtp_AfterLifetime_Expired()
    {
        _api.Otp.Next("482913");
        await Post("api/auth/register", Registration());
        _api.Clock.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));

        var response = await Post("api/auth/verify-otp", new VerifyOtpRequest(NewEmail, "482913"));

        await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "OTP_EXPIRED");
        Assert.NotNull(await ChallengeAsync(OtpPurposes.Registration, NewEmail));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-08 #09] Resend 59 seconds after issue is refused")]
    public async Task ResendOtp_Within60s_TooManyRequests()
    {
        await Post("api/auth/register", Registration());
        _api.Clock.Advance(TimeSpan.FromSeconds(59));

        var response = await Post("api/auth/resend-otp", new ResendOtpRequest(NewEmail));

        await ApiAssert.Problem(response, HttpStatusCode.TooManyRequests, "RESEND_COOLDOWN");
        Assert.Single(_api.Email.Sent);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-08 #10] Resend at 60 seconds replaces the code")]
    public async Task ResendOtp_At60s_ReplacesCode()
    {
        _api.Otp.Next("111111", "222222");
        await Post("api/auth/register", Registration());
        _api.Clock.Advance(TimeSpan.FromSeconds(60));

        await ApiAssert.Status(await Post("api/auth/resend-otp", new ResendOtpRequest(NewEmail)), HttpStatusCode.OK);
        var oldCode = await Post("api/auth/verify-otp", new VerifyOtpRequest(NewEmail, "111111"));
        var newCode = await Post("api/auth/verify-otp", new VerifyOtpRequest(NewEmail, "222222"));

        await ApiAssert.Problem(oldCode, HttpStatusCode.BadRequest, "INVALID_OTP");
        await ApiAssert.Status(newCode, HttpStatusCode.OK);
        Assert.Equal(2, _api.Email.Sent.Count);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-08 #11] Non-digit OTP is a field error and uses no attempt")]
    public async Task VerifyOtp_NonDigit_FieldError()
    {
        await Post("api/auth/register", Registration());

        var response = await Post("api/auth/verify-otp", new VerifyOtpRequest(NewEmail, "12a456"));

        ApiAssert.HasFieldError(await ApiAssert.Status(response, HttpStatusCode.BadRequest), "otpCode");
        Assert.Equal(0, (await ChallengeAsync(OtpPurposes.Registration, NewEmail))!.AttemptCount);
    }

    // ---------- UC-AUTH-07 ----------

    private async Task<string> ResetTokenAsync()
    {
        _api.Otp.Next("654321");
        await Post("api/auth/forgot-password", new ForgotPasswordRequest(ApiHost.ResetEmail));
        var grant = await ApiAssert.Status(
            await Post("api/auth/forgot-password/verify-otp", new VerifyOtpRequest(ApiHost.ResetEmail, "654321")),
            HttpStatusCode.OK);
        return grant.GetProperty("resetToken").GetString()!;
    }

    private Task<HttpResponseMessage> ResetAsync(string token, string password, string? confirm = null) =>
        Post("api/auth/reset-password", new ResetPasswordRequest(ApiHost.ResetEmail, token, password, confirm ?? password));

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-07 #01] Registered e-mail (any case, spaces) receives a reset OTP")]
    public async Task ForgotPassword_Registered_SendsOtp()
    {
        var response = await Post("api/auth/forgot-password", new ForgotPasswordRequest(" VanA@Gmail.com "));

        await ApiAssert.Status(response, HttpStatusCode.OK);
        Assert.NotNull(await ChallengeAsync(OtpPurposes.PasswordReset, ApiHost.ResetEmail));
        var sent = Assert.Single(_api.Email.Sent);
        Assert.Equal((ApiHost.ResetEmail, OtpPurposes.PasswordReset), (sent.Email, sent.Purpose));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-07 #02] Unknown e-mail gets the same answer and no e-mail")]
    public async Task ForgotPassword_Unknown_SameAnswer()
    {
        string known = (await ApiAssert.Status(await Post("api/auth/forgot-password",
            new ForgotPasswordRequest(ApiHost.ResetEmail)), HttpStatusCode.OK)).GetString()!;
        string unknown = (await ApiAssert.Status(await Post("api/auth/forgot-password",
            new ForgotPasswordRequest("ghost@gmail.com")), HttpStatusCode.OK)).GetString()!;

        Assert.Equal(known.Replace(ApiHost.ResetEmail, "ghost@gmail.com"), unknown);
        Assert.Single(_api.Email.Sent);
        Assert.Null(await ChallengeAsync(OtpPurposes.PasswordReset, "ghost@gmail.com"));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-07 #03] E-mail failure returns 503 and stores no code")]
    public async Task ForgotPassword_EmailFails_ServiceUnavailable()
    {
        _api.Email.Succeeds = false;

        var response = await Post("api/auth/forgot-password", new ForgotPasswordRequest(ApiHost.ResetEmail));

        await ApiAssert.Problem(response, HttpStatusCode.ServiceUnavailable, "EMAIL_DELIVERY_FAILED");
        Assert.Null(await ChallengeAsync(OtpPurposes.PasswordReset, ApiHost.ResetEmail));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-07 #04] Resend inside the cooldown is refused")]
    public async Task ResendResetOtp_Within60s_TooManyRequests()
    {
        await Post("api/auth/forgot-password", new ForgotPasswordRequest(ApiHost.ResetEmail));
        _api.Clock.Advance(TimeSpan.FromSeconds(30));

        var response = await Post("api/auth/forgot-password/resend-otp", new ForgotPasswordRequest(ApiHost.ResetEmail));

        await ApiAssert.Problem(response, HttpStatusCode.TooManyRequests, "RESEND_COOLDOWN");
        Assert.Single(_api.Email.Sent);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-07 #05] Correct code returns a reset token stored only as a hash")]
    public async Task VerifyResetOtp_Correct_ReturnsHashedGrant()
    {
        _api.Otp.Next("654321");
        await Post("api/auth/forgot-password", new ForgotPasswordRequest(ApiHost.ResetEmail));

        var grant = await ApiAssert.Status(await Post("api/auth/forgot-password/verify-otp",
            new VerifyOtpRequest(ApiHost.ResetEmail, "654321")), HttpStatusCode.OK);

        string token = grant.GetProperty("resetToken").GetString()!;
        Assert.Equal(64, token.Length);
        Assert.Equal(ApiHost.T0.AddMinutes(10), grant.GetProperty("expiresAt").GetDateTimeOffset());
        var stored = await ChallengeAsync(OtpPurposes.PasswordResetGrant, ApiHost.ResetEmail);
        Assert.NotNull(stored);
        Assert.NotEqual(token, stored!.OtpCode);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-07 #06] Wrong reset code issues no grant")]
    public async Task VerifyResetOtp_Wrong_NoGrant()
    {
        await Post("api/auth/forgot-password", new ForgotPasswordRequest(ApiHost.ResetEmail));

        var response = await Post("api/auth/forgot-password/verify-otp", new VerifyOtpRequest(ApiHost.ResetEmail, "000000"));

        await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "INVALID_OTP");
        Assert.Null(await ChallengeAsync(OtpPurposes.PasswordResetGrant, ApiHost.ResetEmail));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-07 #07] Reset stores a new hash; login works with the new password only")]
    public async Task ResetPassword_Valid_LoginWithNewPassword()
    {
        string token = await ResetTokenAsync();

        await ApiAssert.Status(await ResetAsync(token, "NewPass123"), HttpStatusCode.OK);

        await using (var db = _api.Open())
        {
            var user = await db.Users.SingleAsync(candidate => candidate.UserId == _api.ResetUserId);
            Assert.DoesNotContain("NewPass123", user.PasswordHash);
            Assert.Contains(await db.AuditLogs.ToListAsync(), log => log.Action == "ResetPassword" && log.EntityId == _api.ResetUserId);
        }

        var newLogin = await Post("api/auth/login", new { identifier = ApiHost.ResetEmail, password = "NewPass123" });
        var oldLogin = await Post("api/auth/login", new { identifier = ApiHost.ResetEmail, password = ApiHost.ResetPassword });
        await ApiAssert.Status(newLogin, HttpStatusCode.OK);
        await ApiAssert.Status(oldLogin, HttpStatusCode.Unauthorized);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-07 #08] Reset token cannot be used twice")]
    public async Task ResetPassword_TokenReused_Unauthorized()
    {
        string token = await ResetTokenAsync();
        await ResetAsync(token, "NewPass123");

        var response = await ResetAsync(token, "Another123");

        await ApiAssert.Problem(response, HttpStatusCode.Unauthorized, "RESET_SESSION_INVALID");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-07 #09] Weak new password is a field error")]
    public async Task ResetPassword_Weak_FieldError()
    {
        string token = await ResetTokenAsync();

        var response = await ResetAsync(token, "nouppercase1");

        ApiAssert.HasFieldError(await ApiAssert.Status(response, HttpStatusCode.BadRequest), "newPassword");
        Assert.NotNull(await ChallengeAsync(OtpPurposes.PasswordResetGrant, ApiHost.ResetEmail));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-07 #10] Current password cannot be reused")]
    public async Task ResetPassword_SameAsCurrent_Rejected()
    {
        string token = await ResetTokenAsync();

        var response = await ResetAsync(token, ApiHost.ResetPassword);

        await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "PASSWORD_REUSED");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-07 #11] Reset token is refused one second after 10 minutes")]
    public async Task ResetPassword_AfterLifetime_Unauthorized()
    {
        string token = await ResetTokenAsync();
        _api.Clock.Advance(TimeSpan.FromMinutes(10).Add(TimeSpan.FromSeconds(1)));

        var response = await ResetAsync(token, "NewPass123");

        await ApiAssert.Problem(response, HttpStatusCode.Unauthorized, "RESET_SESSION_INVALID");
    }
}
