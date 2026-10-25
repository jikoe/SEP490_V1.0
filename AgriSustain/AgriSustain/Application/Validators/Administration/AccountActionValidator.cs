using AgriSustain.Application.DTOs.Administration;
using FluentValidation;

namespace AgriSustain.Application.Validators.Administration;

public sealed class AccountActionValidator : AbstractValidator<AccountActionRequest>
{
    public AccountActionValidator()
    {
        RuleFor(x => x.Action).Must(x => x is "suspend" or "activate" or "unlock");
        RuleFor(x => x.Confirmed).Equal(true);
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.Reason).NotEmpty().When(x => x.Action != "unlock");
    }
}
