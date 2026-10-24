using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Auth;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AgriSustain.Application.Services;

public sealed class AuthService : IAuthService
{
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);
    private readonly AgriSustainContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<ChangePasswordRequest> _changePasswordValidator;

    public AuthService(
        AgriSustainContext context,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration,
        ICurrentUser currentUser,
        IValidator<ChangePasswordRequest> changePasswordValidator)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _currentUser = currentUser;
        _changePasswordValidator = changePasswordValidator;
    }

    public async Task<LoginResponseDto?> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();
        var user = await _context.Users.SingleOrDefaultAsync(
            candidate => candidate.Username == identifier,
            cancellationToken);
        user ??= await _context.Users.SingleOrDefaultAsync(
            candidate => candidate.Email == identifier,
            cancellationToken);

        if (user is null || user.Status != "Active")
            return null;

        var now = DateTime.UtcNow;
        if (user.LockedUntil > now)
            return null;

        if (user.LockedUntil.HasValue)
        {
            user.LockedUntil = null;
            user.FailedLoginCount = 0;
        }

        var verification = VerifyPassword(user, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedLoginAttempts)
                user.LockedUntil = now.Add(LockoutDuration);

            await _context.SaveChangesAsync(cancellationToken);
            return null;
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await _context.SaveChangesAsync(cancellationToken);

        var expiresAt = now.Add(TokenLifetime);
        var issuer = _configuration["JWT:ValidIssuer"]
            ?? throw new InvalidOperationException("JWT:ValidIssuer is not configured.");
        var audience = _configuration["JWT:ValidAudience"]
            ?? throw new InvalidOperationException("JWT:ValidAudience is not configured.");
        var secret = _configuration["JWT:Secret"]
            ?? throw new InvalidOperationException("JWT:Secret is not configured.");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new LoginResponseDto(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt,
            new AuthenticatedUserDto(user.UserId, user.Username, user.Email, user.FullName, user.Role));
    }

    public async Task<Result<string>> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var error = await RequestValidation.CheckAsync(
            _changePasswordValidator,
            request,
            cancellationToken);
        if (error != null)
        {
            return Result<string>.FailureResult(error);
        }

        var userId = _currentUser.UserId;
        var user = userId is null
            ? null
            : await _context.Users.SingleOrDefaultAsync(
                candidate => candidate.UserId == userId,
                cancellationToken);
        if (user is null || user.Status != "Active")
        {
            return Result<string>.FailureResult(new Error(
                "UNAUTHORIZED",
                "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.",
                "Unauthorized"));
        }

        if (VerifyPassword(user, request.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            return Result<string>.FailureResult(new Error(
                "INVALID_CURRENT_PASSWORD", "Mật khẩu hiện tại không chính xác."));
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<string>.SuccessResult(
            "Đổi mật khẩu thành công. Hãy dùng mật khẩu mới cho lần đăng nhập sau.");
    }

    // BCrypt hashes from seed/registration are migrated to the Identity format on next success.
    private PasswordVerificationResult VerifyPassword(User user, string password) =>
        user.PasswordHash.StartsWith("AQAAAA", StringComparison.Ordinal)
            ? _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password)
            : BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)
                ? PasswordVerificationResult.SuccessRehashNeeded
                : PasswordVerificationResult.Failed;
}
