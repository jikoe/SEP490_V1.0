using AgriSustain.Application.DTOs.Checkout;
using FluentValidation;

namespace AgriSustain.Application.Validators.Checkout;

public sealed class CheckoutValidator : AbstractValidator<CheckoutRequest>
{
    /// <summary>
    /// Form rules: recipient 2–100 characters; Vietnamese mobile phone (0 or +84 followed by 3/5/7/8/9 and 8 digits);
    /// address detail up to 255; province, district and ward codes required; delivery method Standard or FreshExpress;
    /// payment method SePay only; points not negative; notes up to 300; voucher code up to 100.
    /// </summary>
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
            RuleFor(x => x.Address.DistrictCode).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Address.WardCode).NotEmpty().MaximumLength(20);
        });
        RuleFor(x => x.DeliveryMethod).Must(x => x is "Standard" or "FreshExpress");
        // UC-RC-02 / UC-RC-03: SePay bank-transfer QR is the only payment gateway.
        RuleFor(x => x.PaymentMethod).Must(x => x is "SePay");
        RuleFor(x => x.Points).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Notes).MaximumLength(300);
        RuleFor(x => x.VoucherCode).MaximumLength(100);
    }
}
