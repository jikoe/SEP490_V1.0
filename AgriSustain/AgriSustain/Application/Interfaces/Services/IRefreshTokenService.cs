using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Auth;

namespace AgriSustain.Application.Interfaces.Services;

public interface IRefreshTokenService
{
    Task<string> GenerateAndSaveRefreshTokenAsync(long userId, CancellationToken cancellationToken = default);

    Task<Result<LoginResponseDto>> RotateRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(long userId, CancellationToken cancellationToken = default);
}

