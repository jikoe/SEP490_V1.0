using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;

namespace AgriSustain.Infrastructure.Banking;

public sealed class CassoBankAccountLookup(
    HttpClient http,
    IConfiguration configuration,
    IMemoryCache cache) : IBankAccountLookup
{
    public async Task<BankLookupResponse> LookupAsync(
        string bankCode, string accountNumber, CancellationToken cancellationToken)
    {
        var appId = configuration["CASSO_APP_ID"];
        var appSecret = configuration["CASSO_APP_SECRET"];
        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appSecret))
            return new(BankLookupStatuses.Unavailable, null);

        var banks = await cache.GetOrCreateAsync("Casso.BankDirectory", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
            var directory = await http.GetFromJsonAsync<BankDirectory>(
                "https://api.vietqr.io/v2/banks", cancellationToken);
            return directory?.Data is { Length: > 0 } data
                ? data
                : throw new InvalidOperationException("VietQR bank directory is unavailable.");
        });

        var bank = banks?.FirstOrDefault(bank =>
            string.Equals(bank.Code, bankCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(bank.Bin, bankCode, StringComparison.Ordinal));
        if (bank is null || !int.TryParse(bank.Bin, out var bin))
            return new(BankLookupStatuses.Unavailable, null);

        const string tokenCacheKey = "Casso.LookupAccessToken";
        var token = await cache.GetOrCreateAsync(tokenCacheKey, async entry =>
        {
            using var response = await http.PostAsJsonAsync(
                "https://openapi.casso.vn/oauth/token",
                new { app_id = appId, app_secret = appSecret }, cancellationToken);
            response.EnsureSuccessStatusCode();
            var auth = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
            if (string.IsNullOrWhiteSpace(auth?.AccessToken))
                throw new InvalidOperationException("Casso did not return an access token.");

            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(
                auth.ExpiresIn > 30 ? auth.ExpiresIn - 30 : 1);
            return auth.AccessToken;
        });

        using var request = new HttpRequestMessage(
            HttpMethod.Post, "https://openapi.casso.vn/paykit/v1/lookup");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { bin, accountNumber });
        using var lookup = await http.SendAsync(request, cancellationToken);
        if (lookup.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            cache.Remove(tokenCacheKey);

        if (!lookup.IsSuccessStatusCode)
            return new(BankLookupStatuses.Unavailable, null);

        var result = await lookup.Content.ReadFromJsonAsync<LookupResult>(cancellationToken);
        return result switch
        {
            { Code: "00", Data.AccountName: { } name } when !string.IsNullOrWhiteSpace(name)
                => new(BankLookupStatuses.Found, name.Trim()),
            { Code: "42" } => new(BankLookupStatuses.NotFound, null),
            _ => new(BankLookupStatuses.Unavailable, null)
        };
    }

    private sealed record BankDirectory(BankEntry[]? Data);
    private sealed record BankEntry(string Code, string Bin);
    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
    private sealed record LookupResult(string Code, HolderData? Data);
    private sealed record HolderData(string? AccountName);
}

