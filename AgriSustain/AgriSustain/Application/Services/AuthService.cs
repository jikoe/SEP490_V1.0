using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AgriSustain.Application.DTOs.Auth;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
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

    public AuthService(
        AgriSustainContext context,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
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

        var identityHash = user.PasswordHash.StartsWith("AQAAAA", StringComparison.Ordinal);
        var verification = identityHash
            ? _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
            : BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)
                ? PasswordVerificationResult.SuccessRehashNeeded
                : PasswordVerificationResult.Failed;
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
        if (!identityHash || verification == PasswordVerificationResult.SuccessRehashNeeded)
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
}
