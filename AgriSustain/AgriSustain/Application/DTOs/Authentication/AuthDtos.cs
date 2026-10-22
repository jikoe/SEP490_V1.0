namespace AgriSustain.Application.DTOs.Authentication;

public sealed record RegisterRequest(
    string FullName,
    string PhoneNumber,
    string Email,
    string Password,
    string ConfirmPassword,
    string Role,
    bool AgreeTerms);

public sealed record VerifyOtpRequest(
    string Email,
    string OtpCode);

public sealed record ResendOtpRequest(
    string Email);

public sealed record AuthResponse(
    long UserId,
    string FullName,
    string Email,
    string Role,
    string? Token,
    string Message);
