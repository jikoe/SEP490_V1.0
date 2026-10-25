namespace AgriSustain.Application.DTOs.Auth;

public enum LoginResultStatus
{
    Success,
    InvalidCredentials,
    AccountLocked,
    AccountSuspended
}

public sealed record LoginResultDto(
    LoginResultStatus Status,
    LoginResponseDto? Response = null);
