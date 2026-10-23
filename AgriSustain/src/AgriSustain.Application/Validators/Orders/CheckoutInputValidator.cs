using AgriSustain.Application.DTOs.Orders;
using FluentValidation;

namespace AgriSustain.Application.Validators.Orders;

public sealed class CheckoutInputValidator : AbstractValidator<CheckoutInput>
{
    /// <summary>
    /// Validates required recipient/address-code fields, the agreed domestic mobile format,
    /// allowed delivery/payment choices and nonnegative requested points; the catalog separately
    /// validates the geographic relationship.
    /// </summary>
    public CheckoutInputValidator()
    {
        RuleFor(request => request.Address).NotNull();
        When(request => request.Address != null, () =>
        {
            RuleFor(request => request.Address.RecipientName).NotEmpty().Length(2, 100);
            // Approved domestic mobile format. +84/landline support is not in this contract.
            RuleFor(request => request.Address.Phone).NotEmpty().Matches(@"^0[35789]\d{8}$")
                .WithMessage("Số điện thoại gồm 10 chữ số, bắt đầu bằng 03/05/07/08/09.");
            RuleFor(request => request.Address.Province).NotEmpty().MaximumLength(100);
            RuleFor(request => request.Address.ProvinceCode).NotEmpty().MaximumLength(20);
            RuleFor(request => request.Address.WardCode).NotEmpty().MaximumLength(20);
            RuleFor(request => request.Address.Ward).NotEmpty().MaximumLength(100);
            RuleFor(request => request.Address.Detail).NotEmpty().MaximumLength(255);
        });
        RuleFor(request => request.DeliveryMethod).Must(method =>
            method is "Standard" or "FreshExpress");
        RuleFor(request => request.PaymentMethod).Must(method => method is "PayOS" or "VietQR");
        RuleFor(request => request.VoucherCode).MaximumLength(100);
        RuleFor(request => request.Points).GreaterThanOrEqualTo(0);
        RuleFor(request => request.Notes).MaximumLength(300);
    }
}
