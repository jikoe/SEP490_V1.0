using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Auth;

namespace AgriSustain.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken);

    Task<Result<string>> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken);
}
