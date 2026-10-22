namespace AgriSustain.Application.DTOs.Auth;

public sealed record LoginResponseDto(
    string AccessToken,
    DateTime ExpiresAtUtc,
    AuthenticatedUserDto User);

public sealed record AuthenticatedUserDto(
    long Id,
    string Username,
    string Email,
    string FullName,
    string Role);
