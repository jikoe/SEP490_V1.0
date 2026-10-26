using Microsoft.Extensions.Caching.Memory;

namespace AgriSustain.Api.Auth;

public sealed class RevokedTokenStore
{
    private readonly IMemoryCache _cache;

    public RevokedTokenStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool IsRevoked(string tokenId)
    {
        return _cache.TryGetValue(GetCacheKey(tokenId), out _);
    }

    public void Revoke(string tokenId, DateTimeOffset expiresAt)
    {
        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            return;
        }

        _cache.Set(GetCacheKey(tokenId), true, expiresAt);
    }

    private static string GetCacheKey(string tokenId) => $"revoked-token:{tokenId}";
}
