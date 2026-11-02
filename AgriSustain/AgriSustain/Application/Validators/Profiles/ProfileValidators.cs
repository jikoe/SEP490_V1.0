using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Profiles;
using FluentValidation;

namespace AgriSustain.Application.Validators.Profiles;

/// <summary>Field rules from SRS III.6.2.1 (Information Consumer).</summary>
public sealed class UpdateConsumerProfileValidator : AbstractValidator<UpdateConsumerProfileRequest>
{
    public UpdateConsumerProfileValidator()
    {
        RuleFor(x => x.FullName).ValidFullName();
        RuleFor(x => x.PhoneNumber).ValidPhone();

        RuleFor(x => x.Gender)
            .Must(g => g is null or "" or "Male" or "Female" or "Other" or "Nam" or "Nữ" or "Khác")
            .WithMessage("Giới tính không hợp lệ.");

        RuleFor(x => x.BirthDate)
            .Must(d => d is null || d.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Ngày sinh không được ở tương lai.");

        RuleFor(x => x.RecipientName)
            .MaximumLength(50).WithMessage("Họ tên người nhận không quá 50 ký tự.");

        RuleFor(x => x.RecipientPhone)
            .Matches(@"^0\d{9}$").When(x => !string.IsNullOrWhiteSpace(x.RecipientPhone))
            .WithMessage(SystemMessages.Msg180InvalidPhone);

        When(x => !string.IsNullOrWhiteSpace(x.ProvinceCode) || !string.IsNullOrWhiteSpace(x.DistrictCode)
            || !string.IsNullOrWhiteSpace(x.WardCode) || !string.IsNullOrWhiteSpace(x.AddressDetail), () =>
        {
            RuleFor(x => x.ProvinceCode).NotEmpty().WithMessage("Vui lòng chọn tỉnh / thành phố.");
            RuleFor(x => x.DistrictCode).NotEmpty().WithMessage("Vui lòng chọn quận / huyện.");
            RuleFor(x => x.WardCode).NotEmpty().WithMessage("Vui lòng chọn phường / xã.");
            RuleFor(x => x.AddressDetail)
                .Must(d => !string.IsNullOrWhiteSpace(d)).WithMessage("Địa chỉ chi tiết không được để trống.")
                .MaximumLength(150).WithMessage("Địa chỉ chi tiết không quá 150 ký tự.");
        });
    }
}

/// <summary>Field rules from SRS III.3.2 (Information Farmer).</summary>
public sealed class UpdateFarmerProfileValidator : AbstractValidator<UpdateFarmerProfileRequest>
{
    public UpdateFarmerProfileValidator()
    {
        RuleFor(x => x.FullName).ValidFullName();
        RuleFor(x => x.PhoneNumber).ValidPhone();

        RuleFor(x => x.FarmName)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Tên nông trại / HTX không được để trống.")
            .MaximumLength(100).WithMessage("Tên nông trại không quá 100 ký tự.");

        RuleFor(x => x.RepresentativeName)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Tên người đại diện không được để trống.")
            .MaximumLength(50).WithMessage("Tên người đại diện không quá 50 ký tự.");

        RuleFor(x => x.Address)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Địa chỉ nông trại không được để trống.")
            .MaximumLength(200).WithMessage("Địa chỉ nông trại không quá 200 ký tự.");

        RuleFor(x => x.CultivationZone)
            .MaximumLength(200).WithMessage("Vùng trồng không quá 200 ký tự.");

        RuleFor(x => x.ProductionScale)
            .MaximumLength(200).WithMessage("Quy mô / loại hình không quá 200 ký tự.");

        RuleFor(x => x.BusinessLicenseNumber)
            .MaximumLength(100).WithMessage("Số giấy phép không quá 100 ký tự.");

        RuleFor(x => x.ProductionModel)
            .Must(v => string.IsNullOrWhiteSpace(v) || v is "Individual" or "Cooperative")
            .WithMessage("Loại hình phải là Cá nhân hoặc Hợp tác xã.");

        RuleFor(x => x.ShortDescription)
            .MaximumLength(300).WithMessage("Mô tả ngắn không quá 300 ký tự.");

        RuleFor(x => x.CultivatedArea)
            .GreaterThan(0).When(x => x.CultivatedArea.HasValue)
            .WithMessage("Diện tích canh tác phải là số dương (> 0).");

        RuleFor(x => x.GPSCoordinates)
            .Matches(@"^\s*-?\d{1,2}(\.\d+)?\s*,\s*-?\d{1,3}(\.\d+)?\s*$")
            .When(x => !string.IsNullOrWhiteSpace(x.GPSCoordinates))
            .WithMessage("Tọa độ phải có dạng: vĩ độ, kinh độ (ví dụ 11.940400, 108.458300).");
    }
}

public sealed class SubmitKycRequestValidator : AbstractValidator<SubmitKycRequest>
{
    public SubmitKycRequestValidator()
    {
        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Ghi chú không quá 1000 ký tự.");
    }
}

internal static class ProfileRules
{
    public static IRuleBuilderOptions<T, string> ValidFullName<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Họ và tên không được để trống.")
            .Must(v => v is null || v.Trim().Length is >= 2 and <= 50)
            .WithMessage("Họ và tên phải từ 2 đến 50 ký tự.");

    public static IRuleBuilderOptions<T, string> ValidPhone<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Số điện thoại không được để trống.")
            .Must(v => v is null || System.Text.RegularExpressions.Regex.IsMatch(v.Trim(), @"^0\d{9}$"))
            .WithMessage(SystemMessages.Msg180InvalidPhone);
}
