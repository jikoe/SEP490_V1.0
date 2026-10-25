using AgriSustain.Application.DTOs.Auth;

namespace AgriSustain.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResultDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken);
}
