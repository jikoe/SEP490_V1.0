using AgriSustain.Application.DTOs.Profiles;

namespace AgriSustain.Application.Interfaces.Repositories;

public interface IProfileRepository
{
    Task<UserProfileResponse?> GetProfileAsync(long userId, CancellationToken cancellationToken);
    Task<ProfileAccount?> GetAccountAsync(long userId, CancellationToken cancellationToken);
    Task<bool> PhoneUsedByAnotherUserAsync(string phone, long userId, CancellationToken cancellationToken);
    Task<UserProfileResponse?> UpdateConsumerProfileAsync(long userId, UpdateConsumerProfileRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the farm profile. When an Approved farmer changes KYC-sensitive fields the profile is
    /// moved back to PendingApproval for Verification Officer re-review (SRS UC-AUTH-03 AF Step 6).
    /// </summary>
    Task<UserProfileResponse?> UpdateFarmerProfileAsync(long userId, UpdateFarmerProfileRequest request, CancellationToken cancellationToken);

    /// <summary>Stores the uploaded avatar / farm banner URL on the user account.</summary>
    Task<UserProfileResponse?> UpdateAvatarAsync(long userId, string avatarUrl, CancellationToken cancellationToken);

    Task<KycSubmissionResult> SubmitFarmerKycAsync(long userId, SubmitKycRequest request, CancellationToken cancellationToken);
}
