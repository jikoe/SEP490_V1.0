using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace AgriSustain.Api.Auth;

public sealed class RevokedTokenStore
{
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public RevokedTokenStore(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public bool IsRevoked(string tokenId)
    {
        if (string.IsNullOrWhiteSpace(tokenId))
        {
            return false;
        }

        if (_cache.TryGetValue(GetCacheKey(tokenId), out _))
        {
            return true;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgriSustainContext>();
        bool exists = db.RevokedTokens.AsNoTracking().Any(t => t.TokenId == tokenId && t.ExpiresAt > DateTimeOffset.UtcNow);
        if (exists)
        {
            _cache.Set(GetCacheKey(tokenId), true, TimeSpan.FromMinutes(10));
        }

        return exists;
    }

    public async Task<bool> IsRevokedAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenId))
        {
            return false;
        }

        if (_cache.TryGetValue(GetCacheKey(tokenId), out _))
        {
            return true;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgriSustainContext>();
        bool exists = await db.RevokedTokens.AsNoTracking()
            .AnyAsync(t => t.TokenId == tokenId && t.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken);
        if (exists)
        {
            _cache.Set(GetCacheKey(tokenId), true, TimeSpan.FromMinutes(10));
        }

        return exists;
    }

    public async Task RevokeAsync(string tokenId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenId) || expiresAt <= DateTimeOffset.UtcNow)
        {
            return;
        }

        _cache.Set(GetCacheKey(tokenId), true, expiresAt);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgriSustainContext>();
        if (!await db.RevokedTokens.AnyAsync(t => t.TokenId == tokenId, cancellationToken))
        {
            db.RevokedTokens.Add(new RevokedToken
            {
                TokenId = tokenId,
                ExpiresAt = expiresAt,
                RevokedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public void Revoke(string tokenId, DateTimeOffset expiresAt)
    {
        RevokeAsync(tokenId, expiresAt).GetAwaiter().GetResult();
    }

    private static string GetCacheKey(string tokenId) => $"revoked-token:{tokenId}";
}
