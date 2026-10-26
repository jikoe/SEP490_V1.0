using AgriSustain.Application.DTOs.Profiles;
using FluentValidation;

namespace AgriSustain.Application.Validators.Profiles;

public sealed class UpdateConsumerProfileValidator : AbstractValidator<UpdateConsumerProfileRequest>
{
    public UpdateConsumerProfileValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(150).WithMessage("Họ và tên không quá 150 ký tự.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^0\d{9}$").WithMessage("Số điện thoại phải có 10 chữ số, bắt đầu bằng 0.");

        When(x => !string.IsNullOrWhiteSpace(x.ProvinceCode) || !string.IsNullOrWhiteSpace(x.DistrictCode)
            || !string.IsNullOrWhiteSpace(x.WardCode) || !string.IsNullOrWhiteSpace(x.AddressDetail), () =>
        {
            RuleFor(x => x.ProvinceCode).NotEmpty().WithMessage("Vui lòng chọn tỉnh / thành phố.");
            RuleFor(x => x.DistrictCode).NotEmpty().WithMessage("Vui lòng chọn quận / huyện.");
            RuleFor(x => x.WardCode).NotEmpty().WithMessage("Vui lòng chọn phường / xã.");
            RuleFor(x => x.AddressDetail)
                .NotEmpty().WithMessage("Địa chỉ chi tiết không được để trống.")
                .MaximumLength(255).WithMessage("Địa chỉ chi tiết không quá 255 ký tự.");
        });
    }
}

public sealed class UpdateFarmerProfileValidator : AbstractValidator<UpdateFarmerProfileRequest>
{
    public UpdateFarmerProfileValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên chủ hộ không được để trống.")
            .MaximumLength(150);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^0\d{9}$").WithMessage("Số điện thoại không hợp lệ.");

        RuleFor(x => x.FarmName)
            .NotEmpty().WithMessage("Tên trang trại / HTX không được để trống.")
            .MaximumLength(200);

        RuleFor(x => x.RepresentativeName)
            .NotEmpty().WithMessage("Tên người đại diện không được để trống.")
            .MaximumLength(150);

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Địa chỉ trang trại không được để trống.")
            .MaximumLength(500);

        RuleFor(x => x.CultivatedArea)
            .GreaterThanOrEqualTo(0).When(x => x.CultivatedArea.HasValue)
            .WithMessage("Diện tích canh tác không thể là số âm.");
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
