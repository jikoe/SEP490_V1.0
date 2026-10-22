using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Authentication;

namespace AgriSustain.Application.Interfaces.Services;

public interface IAuthenticationService
{
    Task<Result<string>> InitiateRegistrationAsync(
        RegisterRequest request, CancellationToken cancellationToken);

    Task<Result<AuthResponse>> VerifyOtpAndCompleteRegistrationAsync(
        VerifyOtpRequest request, CancellationToken cancellationToken);

    Task<Result<string>> ResendOtpAsync(
        ResendOtpRequest request, CancellationToken cancellationToken);
}
