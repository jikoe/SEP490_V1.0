using AgriSustain.Application.DTOs.Administration;
using FluentValidation;

namespace AgriSustain.Application.Validators.Administration;

public sealed class AccountActionValidator : AbstractValidator<AccountActionRequest>
{
    /// <summary>
    /// Requires an allowed action, explicit confirmation and a reason for suspend/activate. It
    /// does not require a removed concurrency Version field.
    /// </summary>
    public AccountActionValidator()
    {
        RuleFor(request => request.Action).Must(action =>
            action is "suspend" or "activate" or "unlock").WithMessage("Thao tác không hợp lệ.");
        RuleFor(request => request.Confirmed).Equal(true).WithMessage("Cần xác nhận thao tác.");
        RuleFor(request => request.Reason).MaximumLength(500);
        RuleFor(request => request.Reason).NotEmpty()
            .When(request => request.Action is "suspend" or "activate")
            .WithMessage("Vui lòng nhập lý do thay đổi trạng thái.");
    }
}
