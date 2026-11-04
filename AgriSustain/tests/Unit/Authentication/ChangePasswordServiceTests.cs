using AgriSustain.Api.Middlewares;
using AgriSustain.Application.DTOs.Auth;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Application.Validators.Authentication;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Auth;
using AgriSustain.Infrastructure.Data;
using AgriSustain.UnitTests.DuIte1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Linq.Expressions;
using System.Text.Json;
using Xunit;

namespace AgriSustain.UnitTests.Authentication;

/// <summary>UC-AUTH-06 unit tests of services, validation and domain rules.</summary>
[Trait("UseCase", "UC-AUTH-06")]
public sealed class ChangePasswordServiceTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private readonly PasswordStore _store = new();

    private AuthService Service(long? userId = 20) => new(new UnusedRepository(), _store.Passwords,
        new UnusedIssuer(), new FakeClock(T0), _store.Context, new CurrentUser(userId),
        new ChangePasswordRequestValidator());
    public void Dispose() => _store.Dispose();

    private static readonly ChangePasswordRequest Valid = new(PasswordStore.OldPassword, "Changed123!", "Changed123!");
    private static bool IsValid(ChangePasswordRequest request) => new ChangePasswordRequestValidator().Validate(request).IsValid;

    // ---------- UC-AUTH-06: AuthService.ChangePasswordAsync ----------

    [Fact(DisplayName = "[UC-AUTH-06 #001] Save a new hash that verifies only the new password")]
    public async Task ChangePasswordAsync_ChangePassword_SavesSecureHash()
    {
        var previous = _store.User.PasswordHash;
        var response = await Service().ChangePasswordAsync(Valid, default);

        Assert.True(response.IsSuccess);
        Assert.NotEqual(previous, _store.User.PasswordHash);
        Assert.NotEqual(Valid.NewPassword, _store.User.PasswordHash);
        Assert.True(_store.Passwords.Verify(_store.User, Valid.NewPassword).IsValid);
        Assert.False(_store.Passwords.Verify(_store.User, PasswordStore.OldPassword).IsValid);
        Assert.Equal(1, _store.Context.Saves);
    }

    [Fact(DisplayName = "[UC-AUTH-06 #002] Return field-specific error and keep original hash")]
    public async Task ChangePasswordAsync_WrongCurrentPassword_PreservesExistingHash()
    {
        var previous = _store.User.PasswordHash;
        var result = await Service().ChangePasswordAsync(Valid with { CurrentPassword = "Wrong123!" }, default);

        Assert.Equal("INVALID_CURRENT_PASSWORD", result.Error!.Code);
        Assert.Contains("CurrentPassword", result.Error.Fields!.Keys);
        Assert.Equal(previous, _store.User.PasswordHash);
        Assert.Equal(0, _store.Context.Saves);
    }

    [Fact(DisplayName = "[UC-AUTH-06 #003] Return 401 without saving")]
    public async Task ChangePasswordAsync_Anonymous_RejectsAnonymous()
    {
        var response = await Service(null).ChangePasswordAsync(Valid, default);

        Assert.Equal(401, response.StatusCode);
        Assert.Equal(0, _store.Context.Saves);
    }

    [Fact(DisplayName = "[UC-AUTH-06 #004] Return 401 without changing another user's password")]
    public async Task ChangePasswordAsync_UnknownUser_RejectsUnknownUser()
    {
        var response = await Service(999).ChangePasswordAsync(Valid, default);

        Assert.Equal(401, response.StatusCode);
        Assert.Equal(0, _store.Context.Saves);
    }

    [Fact(DisplayName = "[UC-AUTH-06 #005] Reject suspended user with 401")]
    public async Task ChangePasswordAsync_Suspended_RejectsSuspendedUser()
    {
        _store.User.Status = "Suspended";
        var response = await Service().ChangePasswordAsync(Valid, default);

        Assert.Equal(401, response.StatusCode);
        Assert.Equal(0, _store.Context.Saves);
    }

    // ---------- UC-AUTH-06: ChangePasswordRequestValidator ----------

    [Fact(DisplayName = "[UC-AUTH-06 #006] Require current password")]
    public void Validate_MissingCurrent_RequiresCurrentPassword() => Assert.False(IsValid(Valid with { CurrentPassword = "" }));

    [Fact(DisplayName = "[UC-AUTH-06 #007] Require new password")]
    public void Validate_MissingNew_RequiresNewPassword() => Assert.False(IsValid(Valid with { NewPassword = "", ConfirmPassword = "" }));

    [Fact(DisplayName = "[UC-AUTH-06 #008] Require password confirmation")]
    public void Validate_MissingConfirmation_RequiresConfirmation() => Assert.False(IsValid(Valid with { ConfirmPassword = "" }));

    [Fact(DisplayName = "[UC-AUTH-06 #009] Reject mismatched confirmation")]
    public void Validate_Mismatch_RejectsMismatch() => Assert.False(IsValid(Valid with { ConfirmPassword = "Different123!" }));

    [Fact(DisplayName = "[UC-AUTH-06 #010] Reject reuse of current password")]
    public void Validate_SamePassword_RejectsReuse() => Assert.False(IsValid(Valid with { NewPassword = PasswordStore.OldPassword, ConfirmPassword = PasswordStore.OldPassword }));

    [Fact(DisplayName = "[UC-AUTH-06 #011] Reject seven-character password")]
    public void Validate_SevenCharacters_RejectsShortPassword() => Assert.False(IsValid(Valid with { NewPassword = "Abc1234", ConfirmPassword = "Abc1234" }));

    [Fact(DisplayName = "[UC-AUTH-06 #012] Accept eight-character password with required character groups")]
    public void Validate_EightCharacters_AcceptsMinimumLength() => Assert.True(IsValid(Valid with { NewPassword = "Abcd1234", ConfirmPassword = "Abcd1234" }));

    [Fact(DisplayName = "[UC-AUTH-06 #013] Reject missing uppercase character")]
    public void Validate_NoUppercase_RequiresUppercase() => Assert.False(IsValid(Valid with { NewPassword = "lowercase123", ConfirmPassword = "lowercase123" }));

    [Fact(DisplayName = "[UC-AUTH-06 #014] Reject missing lowercase character")]
    public void Validate_NoLowercase_RequiresLowercase() => Assert.False(IsValid(Valid with { NewPassword = "UPPERCASE123", ConfirmPassword = "UPPERCASE123" }));

    [Fact(DisplayName = "[UC-AUTH-06 #015] Reject missing digit")]
    public void Validate_NoDigit_RequiresDigit() => Assert.False(IsValid(Valid with { NewPassword = "NoDigitsHere", ConfirmPassword = "NoDigitsHere" }));

    // ---------- UC-AUTH-06: AuthService.ChangePasswordAsync ----------

    [Fact(DisplayName = "[UC-AUTH-06 #016] Reject validation before modifying or saving hash")]
    public async Task ChangePasswordAsync_InvalidRequestDoesNotMutate_RejectsBeforeMutation()
    {
        var hash = _store.User.PasswordHash;
        var response = await Service().ChangePasswordAsync(Valid with { ConfirmPassword = "bad" }, default);

        Assert.False(response.IsSuccess);
        Assert.Equal(hash, _store.User.PasswordHash);
        Assert.Equal(0, _store.Context.Saves);
    }

    [Fact(DisplayName = "[UC-AUTH-06 #017] Propagate save failure to middleware without returning success or completing a save")]
    public async Task ChangePasswordAsync_SaveFailure_PropagatesSaveFailure()
    {
        _store.Context.SaveError = new IOException("offline");
        await Assert.ThrowsAsync<IOException>(() => Service().ChangePasswordAsync(Valid, default));

        Assert.Equal(0, _store.Context.Saves);
    }

    [Fact(DisplayName = "[UC-AUTH-06 #018] Propagate cancellation without saving")]
    public async Task ChangePasswordAsync_Cancellation_PropagatesCancellation()
    {
        var store = _store; using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service().ChangePasswordAsync(Valid, cancellation.Token));

        Assert.Equal(0, _store.Context.Saves);
    }

    // ---------- UC-AUTH-06: FeatureExceptionMiddleware.InvokeAsync ----------

    [Fact(DisplayName = "[UC-AUTH-06 #019] Return HTTP 500 ProblemDetails with retry message and no internal exception details")]
    public async Task InvokeAsync_SaveFailureResponse_ReturnsSafeErrorResponse()
    {
        _store.Context.SaveError = new IOException("private storage detail");
        using var services = new ServiceCollection().AddOptions().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = "POST";
        context.Request.Path = "/api/auth/change-password";
        using var output = new MemoryStream();
        context.Response.Body = output;
        var middleware = new FeatureExceptionMiddleware(async _ =>
            { await Service().ChangePasswordAsync(Valid, default); }, NullLogger<FeatureExceptionMiddleware>.Instance);
        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);
        output.Position = 0; using var body = await JsonDocument.ParseAsync(output);

        Assert.Equal(500, body.RootElement.GetProperty("status").GetInt32());
        var title = body.RootElement.GetProperty("title").GetString()!;

        Assert.Contains("Vui lòng thử lại", title);
        Assert.DoesNotContain("private storage detail", title);
        Assert.Equal(0, _store.Context.Saves);
    }

    // ---------- Test doubles ----------

    // Only the EF query/save boundary is replaced; password validation, verification and hashing are real.
    private sealed class PasswordStore : IDisposable
    {
        public const string OldPassword = "Original123!";
        public User User { get; } = new() { UserId = 20, Username = "unit-buyer", Status = "Active" };
        public PasswordService Passwords { get; } = new(new PasswordHasher<User>(
            Options.Create(new PasswordHasherOptions { IterationCount = 1000 })));
        public PasswordContext Context { get; }
        public PasswordStore()
        {
            User.PasswordHash = Passwords.Hash(User, OldPassword);
            Context = new PasswordContext([User]);
        }
        public void Dispose() => Context.Dispose();
    }

    private sealed class CurrentUser(long? id) : ICurrentUser { public long? UserId => id; }
    private sealed class UnusedIssuer : ITokenIssuer
    {
        public IssuedToken Issue(User user, DateTimeOffset at) => throw new InvalidOperationException("Password change must not issue a login token.");
    }
    private sealed class UnusedRepository : IUserRepository
    {
        public Task<User?> FindByUsernameOrEmailAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<User?> FindByEmailAsync(string email, CancellationToken ct) => throw new NotSupportedException();
        public void AddAudit(AuditLog audit) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct) => throw new NotSupportedException();
    }


    private sealed class PasswordContext : AgriSustainContext
    {
        public int Saves { get; private set; }
        public Exception? SaveError { get; set; }
        public PasswordContext(IEnumerable<User> users) => Users = new UserSet(users);
        public override DbSet<User> Users { get; set; }
        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (SaveError != null) throw SaveError;
            Saves++; return Task.FromResult(1);
        }
        private sealed class UserSet(IEnumerable<User> users) : DbSet<User>, IQueryable<User>
        {
            private readonly IQueryable<User> query = users.AsQueryable();
            public override Microsoft.EntityFrameworkCore.Metadata.IEntityType EntityType => throw new NotSupportedException();
            Type IQueryable.ElementType => typeof(User);
            Expression IQueryable.Expression => query.Expression;
            IQueryProvider IQueryable.Provider => new UserQueryProvider(query.Provider);
            IEnumerator<User> IEnumerable<User>.GetEnumerator() => query.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => query.GetEnumerator();
        }
        private sealed class UserQueryProvider(IQueryProvider inner) : IAsyncQueryProvider
        {
            public IQueryable CreateQuery(Expression expression) => inner.CreateQuery(expression);
            public IQueryable<T> CreateQuery<T>(Expression expression) => inner.CreateQuery<T>(expression);
            public object? Execute(Expression expression) => inner.Execute(expression);
            public T Execute<T>(Expression expression) => inner.Execute<T>(expression);
            public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken ct = default)
            {
                ct.ThrowIfCancellationRequested();
                // AuthService's single terminal EF operation is SingleOrDefaultAsync<User>.
                return (TResult)(object)Task.FromResult(inner.Execute<User?>(expression));
            }
        }
    }
}
