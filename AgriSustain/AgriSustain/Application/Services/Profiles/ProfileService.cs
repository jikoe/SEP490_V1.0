using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Interfaces.UnitOfWork;
using FluentValidation;
using IAddressCatalog = AgriSustain.Application.Interfaces.IAddressCatalog;

namespace AgriSustain.Application.Services.Profiles;

public sealed class ProfileService(
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IAddressCatalog addressCatalog,
    IValidator<UpdateConsumerProfileRequest> consumerValidator,
    IValidator<UpdateFarmerProfileRequest> farmerValidator) : IProfileService
{
    public Task<Result<UserProfileResponse>> GetCurrentProfileAsync(
        CancellationToken cancellationToken) =>
        unitOfWork.ReadAsync(async uow =>
        {
            if (currentUser.UserId == null)
            {
                return Result<UserProfileResponse>.FailureResult(new Error(
                    "UNAUTHORIZED", "Vui lòng đăng nhập để xem thông tin hồ sơ.", "Unauthorized"));
            }

            var profile = await uow.Profiles.GetProfileAsync(currentUser.UserId.Value, cancellationToken);
            return profile == null
                ? Result<UserProfileResponse>.FailureResult(new Error(
                    "USER_NOT_FOUND", "Không tìm thấy thông tin tài khoản người dùng.", "NotFound"))
                : Result<UserProfileResponse>.SuccessResult(profile);
        }, cancellationToken);

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
        Error? error = await RequestValidation.CheckAsync(consumerValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<UserProfileResponse>.FailureResult(error);
        }

        // Saved addresses use the same province/ward catalog as checkout so they can prefill it.
        if (!string.IsNullOrWhiteSpace(request.ProvinceCode))
        {
            var address = addressCatalog.Normalize(new CheckoutAddress(
                request.FullName,
                request.PhoneNumber,
                request.ProvinceCode,
                "",
                null,
                request.WardCode ?? "",
                "",
                request.AddressDetail ?? ""));
            if (address == null)
            {
                return Result<UserProfileResponse>.FailureResult(new Error("ADDRESS_INVALID",
                    "Tỉnh/thành hoặc phường/xã không hợp lệ. Vui lòng chọn lại địa chỉ."));
            }

            request = request with
            {
                ProvinceCity = address.Province,
                District = null,
                Ward = address.Ward
            };
        }

        return await unitOfWork.ExecuteAsync(async uow =>
        {
            if (currentUser.UserId == null)
            {
                return Result<UserProfileResponse>.FailureResult(new Error(
                    "UNAUTHORIZED", "Vui lòng đăng nhập trước khi cập nhật hồ sơ.", "Unauthorized"));
            }

            var updated = await uow.Profiles.UpdateConsumerProfileAsync(
                currentUser.UserId.Value, request, cancellationToken);

            return updated == null
                ? Result<UserProfileResponse>.FailureResult(new Error(
                    "UPDATE_FAILED", "Không thể cập nhật hồ sơ người tiêu dùng.", "NotFound"))
                : Result<UserProfileResponse>.SuccessResult(updated);
        }, cancellationToken);
    }

    public async Task<Result<UserProfileResponse>> UpdateFarmerProfileAsync(
        UpdateFarmerProfileRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(farmerValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<UserProfileResponse>.FailureResult(error);
        }

        return await unitOfWork.ExecuteAsync(async uow =>
        {
            if (currentUser.UserId == null)
            {
                return Result<UserProfileResponse>.FailureResult(new Error(
                    "UNAUTHORIZED", "Vui lòng đăng nhập trước khi cập nhật hồ sơ.", "Unauthorized"));
            }

            var updated = await uow.Profiles.UpdateFarmerProfileAsync(
                currentUser.UserId.Value, request, cancellationToken);

            return updated == null
                ? Result<UserProfileResponse>.FailureResult(new Error(
                    "UPDATE_FAILED", "Không thể cập nhật hồ sơ nông hộ.", "NotFound"))
                : Result<UserProfileResponse>.SuccessResult(updated);
        }, cancellationToken);
    }

    public Task<Result<UserProfileResponse>> SubmitFarmerKycAsync(
        SubmitKycRequest request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async uow =>
        {
            if (currentUser.UserId == null)
            {
                return Result<UserProfileResponse>.FailureResult(new Error(
                    "UNAUTHORIZED", "Vui lòng đăng nhập trước khi nộp hồ sơ KYC.", "Unauthorized"));
            }

            var updated = await uow.Profiles.SubmitFarmerKycAsync(
                currentUser.UserId.Value, request, cancellationToken);

            return updated == null
                ? Result<UserProfileResponse>.FailureResult(new Error(
                    "KYC_FAILED", "Không thể nộp hồ sơ xác thực nông hộ.", "NotFound"))
                : Result<UserProfileResponse>.SuccessResult(updated);
        }, cancellationToken);
}
