using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Interfaces.UnitOfWork;
using FluentValidation;
using ICurrentActor = AgriSustain.Application.Interfaces.ICurrentActor;
using IAddressCatalog = AgriSustain.Application.Interfaces.IAddressCatalog;

namespace AgriSustain.Application.Services.Profiles;

/// <summary>UC-AUTH-03 Manage Profile.</summary>
public sealed class ProfileService(
    IUnitOfWork unitOfWork,
    ICurrentActor currentActor,
    IAddressCatalog addressCatalog,
    IValidator<UpdateConsumerProfileRequest> consumerValidator,
    IValidator<UpdateFarmerProfileRequest> farmerValidator) : IProfileService
{
    /// <summary>Signed-in user id from the validated JWT, or null for an anonymous caller
    /// (ICurrentActor reports 0 when nobody is signed in).</summary>
    private long? SignedInUserId() => currentActor.UserId > 0 ? currentActor.UserId : null;

    private static readonly Error s_signInRequired = new(
        "UNAUTHORIZED", "Vui lòng đăng nhập để sử dụng chức năng hồ sơ.", "Unauthorized");

    private static readonly Error s_phoneTaken = new(
        "PHONE_EXISTS", "Số điện thoại này đã được tài khoản khác sử dụng.", "Conflict",
        new Dictionary<string, string[]> { ["phoneNumber"] = ["Số điện thoại này đã được sử dụng."] });

    public Task<Result<UserProfileResponse>> GetCurrentProfileAsync(
        CancellationToken cancellationToken) =>
        unitOfWork.ReadAsync(async uow =>
        {
            if (SignedInUserId() is not long userId)
            {
                return Result<UserProfileResponse>.FailureResult(s_signInRequired);
            }

            var profile = await uow.Profiles.GetProfileAsync(userId, cancellationToken);
            return profile == null
                ? Result<UserProfileResponse>.FailureResult(new Error(
                    "USER_NOT_FOUND", "Không tìm thấy thông tin tài khoản người dùng.", "NotFound"))
                : Result<UserProfileResponse>.SuccessResult(profile);
        }, cancellationToken);

    /// <summary>Back-office lookup. Access is restricted to Administrator / Verification Officer (GBR-08) by the controller.</summary>
    public Task<Result<UserProfileResponse>> GetProfileByIdAsync(
        long userId, CancellationToken cancellationToken) =>
        unitOfWork.ReadAsync(async uow =>
        {
            var profile = await uow.Profiles.GetProfileAsync(userId, cancellationToken);
            return profile == null
                ? Result<UserProfileResponse>.FailureResult(new Error(
                    "USER_NOT_FOUND", $"Không tìm thấy người dùng có mã ID #{userId}.", "NotFound"))
                : Result<UserProfileResponse>.SuccessResult(profile);
        }, cancellationToken);

    public async Task<Result<UserProfileResponse>> UpdateConsumerProfileAsync(
        UpdateConsumerProfileRequest request, CancellationToken cancellationToken)
    {
        if (SignedInUserId() is not long userId)
        {
            return Result<UserProfileResponse>.FailureResult(s_signInRequired);
        }

        Error? error = await RequestValidation.CheckAsync(consumerValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<UserProfileResponse>.FailureResult(error);
        }

        // Saved addresses use the same province/district/ward catalog as checkout so they can prefill it.
        if (!string.IsNullOrWhiteSpace(request.ProvinceCode))
        {
            var address = await addressCatalog.NormalizeAsync(new CheckoutAddress(
                request.FullName,
                request.PhoneNumber,
                request.ProvinceCode,
                "",
                request.DistrictCode ?? "",
                "",
                request.WardCode ?? "",
                "",
                request.AddressDetail ?? ""), cancellationToken);
            if (address == null)
            {
                return Result<UserProfileResponse>.FailureResult(new Error("ADDRESS_INVALID",
                    "Tỉnh/thành, quận/huyện hoặc phường/xã không hợp lệ. Vui lòng chọn lại địa chỉ."));
            }

            request = request with
            {
                ProvinceCity = address.Province,
                District = address.District,
                Ward = address.Ward,
                CatalogVersion = address.CatalogVersion
            };
        }

        return await unitOfWork.ExecuteAsync(async uow =>
        {
            var account = await uow.Profiles.GetAccountAsync(userId, cancellationToken);
            if (account == null)
            {
                return Result<UserProfileResponse>.FailureResult(new Error(
                    "USER_NOT_FOUND", "Không tìm thấy thông tin tài khoản người dùng.", "NotFound"));
            }

            // Farmers keep their contact data together with the farm profile (PUT /api/profile/farmer).
            if (account.Role == "Farmer")
            {
                return Result<UserProfileResponse>.FailureResult(new Error(
                    "WRONG_PROFILE_TYPE", "Tài khoản nông hộ cập nhật hồ sơ tại mục Hồ sơ nông trại.", "Forbidden"));
            }

            if (await uow.Profiles.PhoneUsedByAnotherUserAsync(request.PhoneNumber.Trim(), userId, cancellationToken))
            {
                return Result<UserProfileResponse>.FailureResult(s_phoneTaken);
            }

            var updated = await uow.Profiles.UpdateConsumerProfileAsync(userId, request, cancellationToken);
            return updated == null
                ? Result<UserProfileResponse>.FailureResult(new Error(
                    "USER_NOT_FOUND", "Không tìm thấy thông tin tài khoản người dùng.", "NotFound"))
                : Result<UserProfileResponse>.SuccessResult(updated);
        }, cancellationToken);
    }

    public async Task<Result<UserProfileResponse>> UpdateFarmerProfileAsync(
        UpdateFarmerProfileRequest request, CancellationToken cancellationToken)
    {
        if (SignedInUserId() is not long userId)
        {
            return Result<UserProfileResponse>.FailureResult(s_signInRequired);
        }

        Error? error = await RequestValidation.CheckAsync(farmerValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<UserProfileResponse>.FailureResult(error);
        }

        return await unitOfWork.ExecuteAsync(async uow =>
        {
            if (await FarmerOnlyErrorAsync(uow, userId, cancellationToken) is { } roleError)
            {
                return Result<UserProfileResponse>.FailureResult(roleError);
            }

            if (await uow.Profiles.PhoneUsedByAnotherUserAsync(request.PhoneNumber.Trim(), userId, cancellationToken))
            {
                return Result<UserProfileResponse>.FailureResult(s_phoneTaken);
            }

            var updated = await uow.Profiles.UpdateFarmerProfileAsync(userId, request, cancellationToken);
            return updated == null
                ? Result<UserProfileResponse>.FailureResult(new Error(
                    "FARMER_PROFILE_NOT_FOUND", "Không tìm thấy hồ sơ nông trại của tài khoản.", "NotFound"))
                : Result<UserProfileResponse>.SuccessResult(updated);
        }, cancellationToken);
    }

    /// <summary>Saves the avatar / farm banner URL produced by the upload endpoint (UC-AUTH-03 step 3).</summary>
    public Task<Result<UserProfileResponse>> UpdateAvatarAsync(
        string avatarUrl, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async uow =>
        {
            if (SignedInUserId() is not long userId)
            {
                return Result<UserProfileResponse>.FailureResult(s_signInRequired);
            }

            var updated = await uow.Profiles.UpdateAvatarAsync(userId, avatarUrl, cancellationToken);
            return updated == null
                ? Result<UserProfileResponse>.FailureResult(new Error(
                    "USER_NOT_FOUND", "Không tìm thấy thông tin tài khoản người dùng.", "NotFound"))
                : Result<UserProfileResponse>.SuccessResult(updated);
        }, cancellationToken);

    public Task<Result<UserProfileResponse>> SubmitFarmerKycAsync(
        SubmitKycRequest request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async uow =>
        {
            if (SignedInUserId() is not long userId)
            {
                return Result<UserProfileResponse>.FailureResult(s_signInRequired);
            }

            if (await FarmerOnlyErrorAsync(uow, userId, cancellationToken) is { } roleError)
            {
                return Result<UserProfileResponse>.FailureResult(roleError);
            }

            var result = await uow.Profiles.SubmitFarmerKycAsync(userId, request, cancellationToken);
            return result.Outcome switch
            {
                KycSubmissionOutcome.Submitted => Result<UserProfileResponse>.SuccessResult(result.Profile!),
                KycSubmissionOutcome.NotFound => Result<UserProfileResponse>.FailureResult(new Error(
                    "FARMER_PROFILE_NOT_FOUND", "Không tìm thấy hồ sơ nông trại của tài khoản.", "NotFound")),
                KycSubmissionOutcome.InvalidState => Result<UserProfileResponse>.FailureResult(new Error(
                    "KYC_INVALID_STATE",
                    $"Hồ sơ đang ở trạng thái '{result.CurrentStatus}' nên không thể gửi xác minh.", "Conflict")),
                _ => Result<UserProfileResponse>.FailureResult(new Error(
                    "KYC_INCOMPLETE",
                    "Vui lòng hoàn thiện các trường bắt buộc trước khi gửi xác minh.", "Validation",
                    result.MissingFields.ToDictionary(
                        field => field, _ => new[] { "Trường này bắt buộc để gửi xác minh." })))
            };
        }, cancellationToken);

    private static async Task<Error?> FarmerOnlyErrorAsync(
        IUnitOfWork uow, long userId, CancellationToken cancellationToken)
    {
        var account = await uow.Profiles.GetAccountAsync(userId, cancellationToken);
        if (account == null)
        {
            return new Error("USER_NOT_FOUND", "Không tìm thấy thông tin tài khoản người dùng.", "NotFound");
        }

        return account.Role == "Farmer"
            ? null
            : new Error("FORBIDDEN", "Chỉ tài khoản nông hộ (Farmer) mới được sử dụng chức năng này.", "Forbidden");
    }
}
