using AgriSustain.Application.DTOs.Checkout;
using FluentValidation;

namespace AgriSustain.Application.Validators.Checkout;

public sealed class CheckoutValidator : AbstractValidator<CheckoutRequest>
{
    public CheckoutValidator()
    {
        RuleFor(x => x.Address).NotNull();
        When(x => x.Address is not null, () =>
        {
            RuleFor(x => x.Address.RecipientName).NotEmpty().Length(2, 100);
            RuleFor(x => x.Address.Phone).NotEmpty()
                .Matches("^(0[35789][0-9]{8}|\\+84[35789][0-9]{8})$")
                .WithMessage("Số điện thoại di động Việt Nam không hợp lệ.");
            RuleFor(x => x.Address.Detail).NotEmpty().MaximumLength(255);
            RuleFor(x => x.Address.ProvinceCode).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Address.District).MaximumLength(100);
            RuleFor(x => x.Address.WardCode).NotEmpty().MaximumLength(20);
        });
        RuleFor(x => x.DeliveryMethod).Must(x => x is "Standard" or "FreshExpress");
        RuleFor(x => x.PaymentMethod).Must(x => x is "PayOS" or "VietQR");
        RuleFor(x => x.Points).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Notes).MaximumLength(300);
        RuleFor(x => x.VoucherCode).MaximumLength(100);
    }
}
