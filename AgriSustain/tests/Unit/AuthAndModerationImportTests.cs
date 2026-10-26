using System.Text;
using AgriSustain.Application.DTOs.Auth;
using AgriSustain.Application.DTOs.Moderation;
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

public sealed class AuthAndModerationImportTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Successful_login_migrates_bcrypt_and_preserves_the_database_role()
    {
        var user = NewUser();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("demo-test-password");
        user.FailedLoginCount = 4;
        var repository = new Users(user);
        var issuer = new Tokens();
        using var context = UnusedContext();
        var service = Auth(repository, issuer, context);

        var result = await service.LoginAsync(new LoginRequestDto
        {
            Identifier = " moderator@example.test ", Password = "demo-test-password"
        }, default);

        Assert.Equal(LoginResultStatus.Success, result.Status);
        Assert.Equal("moderator@example.test", repository.LastIdentifier);
        Assert.Equal("ContentModerator", result.Response?.User.Role);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockedUntil);
        Assert.StartsWith("AQAAAA", user.PasswordHash);
        Assert.Equal(1, repository.Saves);
        Assert.Equal(1, issuer.Calls);
    }

    [Fact]
    public async Task Fifth_failed_login_locks_account_without_issuing_a_token()
    {
        var user = NewUser();
        user.FailedLoginCount = 4;
        var repository = new Users(user);
        var issuer = new Tokens();
        using var context = UnusedContext();
        var result = await Auth(repository, issuer, context).LoginAsync(new LoginRequestDto
        {
            Identifier = user.Username, Password = "incorrect"
        }, default);

        Assert.Equal(LoginResultStatus.InvalidCredentials, result.Status);
        Assert.Equal(5, user.FailedLoginCount);
        Assert.Equal(Now.AddMinutes(15).UtcDateTime, user.LockedUntil);
        Assert.Equal(1, repository.Saves);
        Assert.Equal(0, issuer.Calls);
    }

    [Theory]
    [InlineData("Suspended", false, LoginResultStatus.AccountSuspended)]
    [InlineData("Active", true, LoginResultStatus.AccountLocked)]
    public async Task Suspended_or_locked_accounts_cannot_login(
        string status,
        bool locked,
        LoginResultStatus expectedStatus)
    {
        var user = NewUser();
        user.Status = status;
        user.LockedUntil = locked ? Now.AddMinutes(1).UtcDateTime : null;
        var repository = new Users(user);
        var issuer = new Tokens();
        using var context = UnusedContext();
        var result = await Auth(repository, issuer, context).LoginAsync(new LoginRequestDto
        {
            Identifier = user.Username, Password = "demo-test-password"
        }, default);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(0, repository.Saves);
        Assert.Equal(0, issuer.Calls);
    }

    [Fact]
    public async Task Imported_token_issuer_uses_the_existing_JWT_configuration_contract()
    {
        var issuer = new JwtTokenIssuer(Options.Create(new JwtOptions
        {
            ValidIssuer = "agrisustain-tests", ValidAudience = "agrisustain-web-tests",
            Secret = "test-only-signing-key-at-least-32-bytes-long"
        }));
        var issued = issuer.Issue(NewUser(), Now);
        // JwtBearer on .NET 8 validates with JsonWebTokenHandler.
        var handler = new JsonWebTokenHandler();
        var token = handler.ReadJsonWebToken(issued.AccessToken);
        var validation = await handler.ValidateTokenAsync(issued.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = "agrisustain-tests", ValidAudience = "agrisustain-web-tests",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                "test-only-signing-key-at-least-32-bytes-long")),
            ValidateIssuerSigningKey = true, ValidateLifetime = false
        });

        Assert.Equal("agrisustain-tests", token.Issuer);
        Assert.True(validation.IsValid, validation.Exception?.ToString());
        Assert.Contains("agrisustain-web-tests", token.Audiences);
        Assert.Equal(Now.UtcDateTime, token.ValidFrom);
        Assert.Equal(Now.AddHours(1).UtcDateTime, token.ValidTo);
        Assert.Contains(token.Claims, claim => claim.Value == "ContentModerator");
    }

    [Fact]
    public async Task Approval_publishes_product_and_notifies_its_farmer()
    {
        var repository = new Moderation { Product = NewProduct() };
        var result = await new ProductModerationService(repository, new Actor(), new Clock())
            .ModerateAsync(9, true, null, default);

        Assert.Equal(ProductModerationOutcome.Success, result);
        Assert.Equal("Approved", repository.Product.ApprovalStatus);
        Assert.True(repository.Product.IsPublished);
        var notification = Assert.Single(repository.Notifications);
        Assert.Equal(42, notification.UserId);
        Assert.Equal(Now.UtcDateTime, notification.SentAt);
        var audit = Assert.Single(repository.Audits);
        Assert.Equal(7, audit.PerformedByUserId);
        Assert.Equal("Product", audit.EntityType);
        Assert.Equal(9, audit.EntityId);
        Assert.Equal("ApproveProduct", audit.Action);
        Assert.Equal("Pending", audit.OldValue);
        Assert.Equal("Approved", audit.NewValue);
        Assert.Equal(Now.UtcDateTime, audit.OccurredAt);
        Assert.Equal("moderation-test-trace", audit.TraceId);
        Assert.Equal(1, repository.Saves);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Rejected")]
    public async Task Approval_is_blocked_when_farmer_is_not_verified(string verificationStatus)
    {
        var repository = new Moderation { Product = NewProduct() };
        repository.Product.FarmerProfile.VerificationStatus = verificationStatus;

        var result = await new ProductModerationService(repository, new Actor(), new Clock())
            .ModerateAsync(9, true, null, default);

        Assert.Equal(ProductModerationOutcome.FarmerNotVerified, result);
        Assert.Equal("Pending", repository.Product.ApprovalStatus);
        Assert.False(repository.Product.IsPublished);
        Assert.Equal(0, repository.Saves);
        Assert.Empty(repository.Notifications);
    }

    [Fact]
    public async Task Rejection_records_trimmed_reason_and_keeps_product_unpublished()
    {
        var repository = new Moderation { Product = NewProduct() };
        var result = await new ProductModerationService(repository, new Actor(), new Clock())
            .ModerateAsync(9, false, "  Thiếu thông tin nguồn gốc  ", default);

        Assert.Equal(ProductModerationOutcome.Success, result);
        Assert.Equal("Rejected", repository.Product.ApprovalStatus);
        Assert.False(repository.Product.IsPublished);
        Assert.Equal("Thiếu thông tin nguồn gốc", repository.Product.RejectionReason);
        Assert.Contains("Thiếu thông tin nguồn gốc", Assert.Single(repository.Notifications).Content);
        var audit = Assert.Single(repository.Audits);
        Assert.Equal("RejectProduct", audit.Action);
        Assert.Equal("Pending", audit.OldValue);
        Assert.Equal("Rejected", audit.NewValue);
        Assert.Equal("Thiếu thông tin nguồn gốc", audit.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rejection_without_reason_never_changes_product(string? reason)
    {
        var repository = new Moderation { Product = NewProduct() };
        var result = await new ProductModerationService(repository, new Actor(), new Clock())
            .ModerateAsync(9, false, reason, default);

        Assert.Equal(ProductModerationOutcome.InvalidReason, result);
        Assert.Equal("Pending", repository.Product.ApprovalStatus);
        Assert.Equal(0, repository.Saves);
        Assert.Empty(repository.Notifications);
        Assert.Empty(repository.Audits);
    }

    [Fact]
    public async Task Previously_reviewed_product_cannot_be_moderated_again()
    {
        var repository = new Moderation { Product = NewProduct() };
        repository.Product.ApprovalStatus = "Approved";
        var result = await new ProductModerationService(repository, new Actor(), new Clock())
            .ModerateAsync(9, false, "A different decision", default);

        Assert.Equal(ProductModerationOutcome.Conflict, result);
        Assert.Equal("Approved", repository.Product.ApprovalStatus);
        Assert.Equal(0, repository.Saves);
        Assert.Empty(repository.Notifications);
        Assert.Empty(repository.Audits);
    }

    private static User NewUser() => new()
    {
        UserId = 7, Username = "moderator", Email = "moderator@example.test",
        FullName = "Moderator Test", Role = "ContentModerator", Status = "Active",
        PasswordHash = new PasswordHasher<User>().HashPassword(new User(), "demo-test-password")
    };

    private static Product NewProduct() => new()
    {
        ProductId = 9, ProductName = "Nông sản kiểm thử", ApprovalStatus = "Pending",
        FarmerProfile = new FarmerProfile { UserId = 42, VerificationStatus = "Approved" }
    };

    // Login uses the imported repository; this context is never opened by these tests.
    private static AgriSustainContext UnusedContext() => new(new DbContextOptionsBuilder<AgriSustainContext>()
        .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=UnusedAuthTest;Integrated Security=true")
        .Options);

    private static AuthService Auth(Users users, Tokens tokens, AgriSustainContext context) =>
        new(users, new PasswordService(new PasswordHasher<User>()), tokens, new Clock(),
            context, new CurrentUser(), new InlineValidator<ChangePasswordRequest>());

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Actor : IActorContext
    {
        public long UserId => 7;
        public string? TraceId => "moderation-test-trace";
    }

    private sealed class CurrentUser : ICurrentUser
    {
        public long? UserId => 7;
    }

    private sealed class Users(User user) : IUserRepository
    {
        public string? LastIdentifier { get; private set; }
        public int Saves { get; private set; }
        public Task<User?> FindByUsernameOrEmailAsync(string identifier, CancellationToken token)
        {
            LastIdentifier = identifier;
            return Task.FromResult<User?>(user);
        }
        public Task SaveChangesAsync(CancellationToken token) { Saves++; return Task.CompletedTask; }
    }

    private sealed class Tokens : ITokenIssuer
    {
        public int Calls { get; private set; }
        public IssuedToken Issue(User user, DateTimeOffset issuedAt)
        {
            Calls++;
            return new IssuedToken("test-token", issuedAt.AddHours(1));
        }
    }

    private sealed class Moderation : IProductModerationRepository
    {
        public Product Product { get; init; } = null!;
        public List<Notification> Notifications { get; } = [];
        public List<AuditLog> Audits { get; } = [];
        public int Saves { get; private set; }
        public Task<(int Total, IReadOnlyList<ProductModerationListItemDto> Items)> GetListAsync(
            string status, string? keyword, int page, int pageSize, CancellationToken token) =>
            Task.FromResult<(int, IReadOnlyList<ProductModerationListItemDto>)>((0, []));
        public Task<ProductModerationDetailDto?> GetDetailAsync(long id, CancellationToken token) =>
            Task.FromResult<ProductModerationDetailDto?>(null);
        public Task<Product?> GetForModerationAsync(long id, CancellationToken token) =>
            Task.FromResult<Product?>(Product);
        public void AddAudit(AuditLog audit) => Audits.Add(audit);
        public void AddNotification(Notification notification) => Notifications.Add(notification);
        public Task SaveChangesAsync(CancellationToken token) { Saves++; return Task.CompletedTask; }
    }
}
