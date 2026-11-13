namespace AgriSustain.Application.DTOs.Auth;

public sealed record LoginResponseDto(
    string AccessToken,
    DateTime ExpiresAtUtc,
    AuthenticatedUserDto User,
    string? RefreshToken = null);

public sealed record AuthenticatedUserDto(
    long Id,
    string Username,
    string Email,
    string FullName,
    string Role);

public sealed record RefreshTokenRequestDto(string RefreshToken);

public sealed record LogoutRequestDto(string? RefreshToken = null);
