using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Profiles;

namespace AgriSustain.Application.Interfaces.Services;

public interface IProfileService
{
    Task<Result<UserProfileResponse>> GetCurrentProfileAsync(
        CancellationToken cancellationToken);

    Task<Result<UserProfileResponse>> GetProfileByIdAsync(
        long userId, CancellationToken cancellationToken);

    Task<Result<UserProfileResponse>> UpdateConsumerProfileAsync(
        UpdateConsumerProfileRequest request, CancellationToken cancellationToken);

    Task<Result<UserProfileResponse>> UpdateFarmerProfileAsync(
        UpdateFarmerProfileRequest request, CancellationToken cancellationToken);

    Task<Result<UserProfileResponse>> UpdateAvatarAsync(
        string avatarUrl, CancellationToken cancellationToken);

    Task<Result<UserProfileResponse>> SubmitFarmerKycAsync(
        SubmitKycRequest request, CancellationToken cancellationToken);
}
