namespace AgriSustain.Application.DTOs.Auth;

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword);
