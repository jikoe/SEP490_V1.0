using AgriSustain.Application.DTOs.Promotions;
using AgriSustain.Domain.Entities;
using FluentValidation;

namespace AgriSustain.Application.Validators.Promotions;

/// <summary>BR-PV-01 to BR-PV-04; each field reports its MSG-PV message.</summary>
public sealed class SaveVoucherRequestValidator : AbstractValidator<SaveVoucherRequest>
{
    public const int MinCodeLength = 4;
    public const int MaxCodeLength = 20;
    public const int MaxNameLength = 150;
    public const int MaxDescriptionLength = 500;

    // MSG-PV-02 .. MSG-PV-06
    public const string CodeMessage = "Mã voucher phải gồm 4–20 chữ cái A–Z hoặc chữ số và chưa tồn tại.";
    public const string ValueMessage = "Nhập phần trăm từ 1 đến 100, hoặc số tiền cố định lớn hơn 0 đ.";
    public const string MaxDiscountMessage = "Voucher giảm theo phần trăm phải có mức giảm tối đa lớn hơn 0 đ.";
    public const string LimitMessage = "Giới hạn lượt dùng phải là số dương và lượt mỗi khách không vượt quá tổng lượt dùng.";
    public const string PeriodMessage = "Thời gian kết thúc phải sau thời gian bắt đầu và thời gian bắt đầu không được ở quá khứ.";

    public SaveVoucherRequestValidator()
    {
        RuleFor(x => x.VoucherName)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Vui lòng nhập tên voucher.")
            .MaximumLength(MaxNameLength)
            .WithMessage($"Tên voucher không được vượt quá {MaxNameLength} ký tự.");

        // BR-PV-01: stored in upper case, so lower-case input is accepted and normalised.
        RuleFor(x => x.VoucherCode)
            .Must(value => value is not null
                && System.Text.RegularExpressions.Regex.IsMatch(value.Trim(), "^[A-Za-z0-9]{4,20}$"))
            .WithMessage(CodeMessage);

        RuleFor(x => x.Description)
            .MaximumLength(MaxDescriptionLength)
            .WithMessage($"Mô tả không được vượt quá {MaxDescriptionLength} ký tự.");

        RuleFor(x => x.DiscountType)
            .Must(value => value is Voucher.TypePercentage or Voucher.TypeFixedAmount)
            .WithMessage("Vui lòng chọn loại giảm giá: phần trăm hoặc số tiền cố định.");

        // BR-PV-02
        RuleFor(x => x.DiscountValue)
            .Must((request, value) => request.DiscountType == Voucher.TypePercentage
                ? value is >= 1 and <= 100
                : value > 0)
            .WithMessage(ValueMessage)
            .PrecisionScale(18, 2, true).WithMessage("Giá trị giảm có tối đa 2 chữ số thập phân.");

        RuleFor(x => x.MaxDiscountAmount)
            .NotNull().WithMessage(MaxDiscountMessage)
            .GreaterThan(0).WithMessage(MaxDiscountMessage)
            .When(x => x.DiscountType == Voucher.TypePercentage);

        RuleFor(x => x.MaxDiscountAmount)
            .GreaterThan(0).WithMessage(MaxDiscountMessage)
            .PrecisionScale(18, 2, true).WithMessage("Mức giảm tối đa có tối đa 2 chữ số thập phân.")
            .When(x => x.MaxDiscountAmount.HasValue);

        // BR-PV-03
        RuleFor(x => x.MinOrderAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Giá trị đơn tối thiểu không được âm.")
            .PrecisionScale(18, 2, true).WithMessage("Giá trị đơn tối thiểu có tối đa 2 chữ số thập phân.");

        RuleFor(x => x.UsageLimit)
            .GreaterThanOrEqualTo(1).WithMessage(LimitMessage);

        RuleFor(x => x.PerCustomerLimit)
            .GreaterThanOrEqualTo(1).WithMessage(LimitMessage)
            .LessThanOrEqualTo(x => x.UsageLimit).WithMessage(LimitMessage);

        // BR-PV-04; "not in the past" depends on the clock and is checked by the service.
        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage(PeriodMessage);
    }
}
