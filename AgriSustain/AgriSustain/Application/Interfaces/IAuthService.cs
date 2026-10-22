using AgriSustain.Application.DTOs.Auth;

namespace AgriSustain.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken);
}
