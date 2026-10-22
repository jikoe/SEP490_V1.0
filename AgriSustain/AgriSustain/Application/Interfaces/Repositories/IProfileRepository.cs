using AgriSustain.Application.DTOs.Profiles;

namespace AgriSustain.Application.Interfaces.Repositories;

public interface IProfileRepository
{
    Task<UserProfileResponse?> GetProfileAsync(long userId, CancellationToken cancellationToken);
    Task<UserProfileResponse?> UpdateConsumerProfileAsync(long userId, UpdateConsumerProfileRequest request, CancellationToken cancellationToken);
    Task<UserProfileResponse?> UpdateFarmerProfileAsync(long userId, UpdateFarmerProfileRequest request, CancellationToken cancellationToken);
    Task<UserProfileResponse?> SubmitFarmerKycAsync(long userId, SubmitKycRequest request, CancellationToken cancellationToken);
}
