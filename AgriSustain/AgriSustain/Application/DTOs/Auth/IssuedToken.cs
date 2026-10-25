namespace AgriSustain.Application.DTOs.Auth;

public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt);
