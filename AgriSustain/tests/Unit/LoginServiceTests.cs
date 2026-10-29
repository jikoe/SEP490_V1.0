using System.Security.Claims;
using AgriSustain.Application.DTOs.Auth;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Auth;
using AgriSustain.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace AgriSustain.UnitTests;

public sealed class LoginServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 2, 0, 0, TimeSpan.Zero);
    private const string ValidPassword = "ValidPass123!";
    private const string SigningKey = "test-only-signing-key-at-least-32-bytes-long";

    [Fact]
    public async Task UTC01_Login_with_username_returns_user_and_jwt_with_expected_claims_and_expiry()
    {
        var user = NewUser();
        var repository = new Users(user);
        using var context = UnusedContext();
        var result = await CreateService(repository, context).LoginAsync(Request("mai.tran"), default);

        Assert.Equal(LoginResultStatus.Success, result.Status);
        var response = Assert.IsType<LoginResponseDto>(result.Response);
        Assert.Equal(user.UserId, response.User.Id);
        Assert.Equal(user.Username, response.User.Username);
        Assert.Equal(user.Email, response.User.Email);
        Assert.Equal(user.FullName, response.User.FullName);
        Assert.Equal(user.Role, response.User.Role);
        Assert.Equal(Now.AddHours(1).UtcDateTime, response.ExpiresAtUtc);
        Assert.Equal(1, repository.Saves);

        var token = new JsonWebTokenHandler().ReadJsonWebToken(response.AccessToken);
        Assert.Equal(Now.UtcDateTime, token.ValidFrom);
        Assert.Equal(Now.AddHours(1).UtcDateTime, token.ValidTo);
        Assert.Contains(token.Claims, claim => claim.Type == "nameid" && claim.Value == user.UserId.ToString());
        Assert.Contains(token.Claims, claim => claim.Type == "unique_name" && claim.Value == user.Username);
        Assert.Contains(token.Claims, claim => claim.Type == "email" && claim.Value == user.Email);
        Assert.Contains(token.Claims, claim => claim.Type == "role" && claim.Value == user.Role);
    }

    [Fact]
    public async Task UTC02_Login_with_email_finds_the_matching_user()
    {
        var user = NewUser();
        var repository = new Users(user);
        using var context = UnusedContext();

        var result = await CreateService(repository, context).LoginAsync(Request(user.Email), default);

        Assert.Equal(LoginResultStatus.Success, result.Status);
        Assert.Equal(user.Email, repository.LastIdentifier);
        Assert.Equal(user.UserId, result.Response?.User.Id);
        Assert.Equal(user.Role, result.Response?.User.Role);
        Assert.Equal(1, repository.Saves);
    }

    [Fact]
    public async Task UTC03_Login_trims_whitespace_from_identifier()
    {
        var user = NewUser();
        var repository = new Users(user);
        using var context = UnusedContext();

        var result = await CreateService(repository, context)
            .LoginAsync(Request("  mai.tran  "), default);

        Assert.Equal(LoginResultStatus.Success, result.Status);
        Assert.Equal(user.Username, repository.LastIdentifier);
        Assert.Equal(user.UserId, result.Response?.User.Id);
    }

    [Fact]
    public async Task UTC04_Login_upgrades_a_valid_bcrypt_hash_to_identity_hash()
    {
        var user = NewUser();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(ValidPassword);
        var oldHash = user.PasswordHash;
        var repository = new Users(user);
        using var context = UnusedContext();

        var result = await CreateService(repository, context).LoginAsync(Request(user.Username), default);

        Assert.Equal(LoginResultStatus.Success, result.Status);
        Assert.NotEqual(oldHash, user.PasswordHash);
        Assert.StartsWith("AQAAAA", user.PasswordHash);
        Assert.Equal(1, repository.Saves);
        Assert.False(string.IsNullOrWhiteSpace(result.Response?.AccessToken));
    }

    [Fact]
    public async Task UTC05_Login_resets_failed_count_and_expired_lock()
    {
        var user = NewUser();
        user.LockedUntil = Now.AddMinutes(-1).UtcDateTime;
        user.FailedLoginCount = 5;
        var repository = new Users(user);
        using var context = UnusedContext();

        var result = await CreateService(repository, context).LoginAsync(Request(user.Username), default);

        Assert.Equal(LoginResultStatus.Success, result.Status);
        Assert.Null(user.LockedUntil);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Equal(1, repository.Saves);
        Assert.False(string.IsNullOrWhiteSpace(result.Response?.AccessToken));
    }

    [Fact]
    public async Task UTC06_Unknown_identifier_returns_invalid_credentials_without_saving()
    {
        var repository = new Users(null);
        using var context = UnusedContext();

        var result = await CreateService(repository, context)
            .LoginAsync(Request("unknown@example.test"), default);

        Assert.Equal(LoginResultStatus.InvalidCredentials, result.Status);
        Assert.Null(result.Response);
        Assert.Equal("unknown@example.test", repository.LastIdentifier);
        Assert.Equal(0, repository.Saves);
    }

    [Fact]
    public async Task UTC07_Suspended_user_cannot_login_or_update_failed_attempts()
    {
        var user = NewUser();
        user.Status = "Suspended";
        user.FailedLoginCount = 2;
        var repository = new Users(user);
        using var context = UnusedContext();

        var result = await CreateService(repository, context).LoginAsync(Request(user.Email), default);

        Assert.Equal(LoginResultStatus.AccountSuspended, result.Status);
        Assert.Null(result.Response);
        Assert.Equal(2, user.FailedLoginCount);
        Assert.Equal(0, repository.Saves);
    }

    [Fact]
    public async Task UTC08_Wrong_password_increments_failed_count_without_locking_before_threshold()
    {
        var user = NewUser();
        user.FailedLoginCount = 2;
        var repository = new Users(user);
        using var context = UnusedContext();

        var result = await CreateService(repository, context)
            .LoginAsync(Request(user.Username, "WrongPass123!"), default);

        Assert.Equal(LoginResultStatus.InvalidCredentials, result.Status);
        Assert.Null(result.Response);
        Assert.Equal(3, user.FailedLoginCount);
        Assert.Null(user.LockedUntil);
        Assert.Equal(1, repository.Saves);
    }

    [Fact]
    public async Task UTC09_Future_locked_account_is_rejected_without_verifying_password_or_saving()
    {
        var user = NewUser();
        user.LockedUntil = Now.AddMinutes(1).UtcDateTime;
        user.FailedLoginCount = 5;
        var repository = new Users(user);
        var passwords = new Passwords(new PasswordVerification(true, false));
        using var context = UnusedContext();

        var result = await CreateService(repository, context, passwords)
            .LoginAsync(Request(user.Username), default);

        Assert.Equal(LoginResultStatus.AccountLocked, result.Status);
        Assert.Null(result.Response);
        Assert.Equal(0, passwords.VerifyCalls);
        Assert.Equal(5, user.FailedLoginCount);
        Assert.Equal(Now.AddMinutes(1).UtcDateTime, user.LockedUntil);
        Assert.Equal(0, repository.Saves);
    }

    [Fact]
    public async Task UTC10_Fifth_failed_password_attempt_locks_account_for_fifteen_minutes()
    {
        var user = NewUser();
        user.FailedLoginCount = 4;
        var repository = new Users(user);
        using var context = UnusedContext();

        var result = await CreateService(repository, context)
            .LoginAsync(Request(user.Username, "WrongPass123!"), default);

        Assert.Equal(LoginResultStatus.InvalidCredentials, result.Status);
        Assert.Null(result.Response);
        Assert.Equal(5, user.FailedLoginCount);
        Assert.Equal(Now.AddMinutes(15).UtcDateTime, user.LockedUntil);
        Assert.Equal(1, repository.Saves);
    }

    private static LoginRequestDto Request(string identifier, string password = ValidPassword) =>
        new() { Identifier = identifier, Password = password };

    private static User NewUser() => new()
    {
        UserId = 10,
        Username = "mai.tran",
        Email = "mai@example.test",
        FullName = "Mai Tran",
        Role = "Customer",
        Status = "Active",
        PasswordHash = new PasswordHasher<User>().HashPassword(new User(), ValidPassword)
    };

    private static AuthService CreateService(
        Users users,
        AgriSustainContext context,
        IPasswordService? passwordService = null) =>
        new(
            users,
            passwordService ?? new PasswordService(new PasswordHasher<User>()),
            new JwtTokenIssuer(Options.Create(new JwtOptions
            {
                ValidIssuer = "agrisustain-tests",
                ValidAudience = "agrisustain-web-tests",
                Secret = SigningKey
            })),
            new Clock(),
            context,
            new CurrentUser(),
            new InlineValidator<ChangePasswordRequest>());

    private static AgriSustainContext UnusedContext() =>
        new(new DbContextOptionsBuilder<AgriSustainContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=UnusedLoginTest;Integrated Security=true")
            .Options);

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class CurrentUser : ICurrentUser
    {
        public long? UserId => 10;
    }

    private sealed class Users(User? user) : IUserRepository
    {
        public string? LastIdentifier { get; private set; }
        public int Saves { get; private set; }

        public Task<User?> FindByUsernameOrEmailAsync(string identifier, CancellationToken cancellationToken)
        {
            LastIdentifier = identifier;
            return Task.FromResult(user);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saves++;
            return Task.CompletedTask;
        }
    }

    private sealed class Passwords(PasswordVerification verification) : IPasswordService
    {
        public int VerifyCalls { get; private set; }

        public PasswordVerification Verify(User user, string password)
        {
            VerifyCalls++;
            return verification;
        }

        public string Hash(User user, string password) => "identity-hash";
    }
}
