using System.Security.Cryptography;
using System.Text;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Auth;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Auth;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    private readonly AgriSustainContext _context;
    private readonly ITokenIssuer _tokenIssuer;
    private readonly TimeProvider _timeProvider;

    public RefreshTokenService(
        AgriSustainContext context,
        ITokenIssuer tokenIssuer,
        TimeProvider timeProvider)
    {
        _context = context;
        _tokenIssuer = tokenIssuer;
        _timeProvider = timeProvider;
    }

    public async Task<string> GenerateAndSaveRefreshTokenAsync(long userId, CancellationToken cancellationToken = default)
    {
        var rawToken = GenerateSecureToken();
        var hash = HashToken(rawToken);
        var now = _timeProvider.GetUtcNow();

        var record = new RefreshToken
        {
            UserId = userId,
            TokenHash = hash,
            ExpiresAt = now.Add(RefreshTokenLifetime),
            CreatedAt = now
        };

        _context.RefreshTokens.Add(record);
        await _context.SaveChangesAsync(cancellationToken);

        return rawToken;
    }

    public async Task<Result<LoginResponseDto>> RotateRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result<LoginResponseDto>.FailureResult(new Error(
                "INVALID_REFRESH_TOKEN",
                "Refresh token không được để trống.",
                "Unauthorized"));
        }

        var hash = HashToken(refreshToken.Trim());
        var tokenRecord = await _context.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (tokenRecord is null)
        {
            return Result<LoginResponseDto>.FailureResult(new Error(
                "TOKEN_NOT_FOUND",
                "Refresh token không hợp lệ hoặc không tồn tại.",
                "Unauthorized"));
        }

        var now = _timeProvider.GetUtcNow();

        // Token Reuse Detection: nếu token đã bị thu hồi trước đó mà lại được gửi lên,
        // nghi vấn kẻ gian đã đánh cắp token. Thu hồi ngay lập tức toàn bộ refresh token của tài khoản này!
        if (tokenRecord.RevokedAt is not null)
        {
            await RevokeAllForUserAsync(tokenRecord.UserId, cancellationToken);
            return Result<LoginResponseDto>.FailureResult(new Error(
                "TOKEN_REUSED",
                "Phát hiện phiên đăng nhập không an toàn (token đã thu hồi được sử dụng lại). Mọi phiên đăng nhập của tài khoản đã bị hủy để bảo mật. Vui lòng đăng nhập lại.",
                "Unauthorized"));
        }

        if (tokenRecord.ExpiresAt <= now)
        {
            return Result<LoginResponseDto>.FailureResult(new Error(
                "TOKEN_EXPIRED",
                "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.",
                "Unauthorized"));
        }

        if (tokenRecord.User is null || tokenRecord.User.Status != "Active")
        {
            return Result<LoginResponseDto>.FailureResult(new Error(
                "ACCOUNT_NOT_ACTIVE",
                "Tài khoản hiện không ở trạng thái hoạt động.",
                "Forbidden"));
        }

        // Thực hiện xoay vòng token (Token Rotation):
        // 1. Thu hồi token hiện tại
        tokenRecord.RevokedAt = now;

        // 2. Tạo refresh token mới
        var newRawToken = GenerateSecureToken();
        var newHash = HashToken(newRawToken);
        tokenRecord.ReplacedByHash = newHash;

        var newRecord = new RefreshToken
        {
            UserId = tokenRecord.UserId,
            TokenHash = newHash,
            ExpiresAt = now.Add(RefreshTokenLifetime),
            CreatedAt = now
        };

        _context.RefreshTokens.Add(newRecord);
        await _context.SaveChangesAsync(cancellationToken);

        // 3. Cấp access token mới
        var issuedToken = _tokenIssuer.Issue(tokenRecord.User, now);

        var response = new LoginResponseDto(
            issuedToken.AccessToken,
            issuedToken.ExpiresAt.UtcDateTime,
            new AuthenticatedUserDto(
                tokenRecord.User.UserId,
                tokenRecord.User.Username,
                tokenRecord.User.Email,
                tokenRecord.User.FullName,
                tokenRecord.User.Role),
            newRawToken);

        return Result<LoginResponseDto>.SuccessResult(response);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;

        var hash = HashToken(refreshToken.Trim());
        var tokenRecord = await _context.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null, cancellationToken);

        if (tokenRecord is not null)
        {
            tokenRecord.RevokedAt = _timeProvider.GetUtcNow();
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeAllForUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        if (activeTokens.Count > 0)
        {
            foreach (var token in activeTokens)
            {
                token.RevokedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private static string GenerateSecureToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}

