using AgriSustain.Application.DTOs.Administration;
using FluentValidation;

namespace AgriSustain.Application.Validators.Administration;

/// <summary>BR-UM-04 reason rules; every failure is reported with MSG-UM-05.</summary>
public sealed class AccountActionValidator : AbstractValidator<AccountActionRequest>
{
    public AccountActionValidator()
    {
        RuleFor(x => x.Action).Must(x => x is "suspend" or "activate" or "unlock")
            .WithMessage("Vui lòng sửa Thao tác: chỉ chấp nhận tạm ngưng, kích hoạt lại hoặc mở khóa.");
        RuleFor(x => x.Confirmed).Equal(true)
            .WithMessage("Vui lòng sửa Xác nhận: hãy xác nhận thao tác trước khi gửi.");
        RuleFor(x => x.Reason).MaximumLength(500)
            .WithMessage("Vui lòng sửa Lý do: tối đa 500 ký tự.");
        RuleFor(x => x.Reason).NotEmpty().When(x => x.Action != "unlock")
            .WithMessage("Vui lòng sửa Lý do: chọn một lý do hoặc nhập giải thích cho lựa chọn Khác.");
        RuleFor(x => x.ExpectedStatus).NotEmpty()
            .WithMessage("Vui lòng sửa Trạng thái: tải lại tài khoản trước khi xác nhận.");
    }
}
