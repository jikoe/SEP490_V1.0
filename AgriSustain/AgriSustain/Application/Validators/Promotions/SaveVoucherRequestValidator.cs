using AgriSustain.Application.DTOs.Promotions;
using AgriSustain.Domain.Entities;
using FluentValidation;

namespace AgriSustain.Application.Validators.Promotions;

public sealed class SaveVoucherRequestValidator : AbstractValidator<SaveVoucherRequest>
{
    public const int MinCodeLength = 3;
    public const int MaxCodeLength = 50;

    public SaveVoucherRequestValidator()
    {
        RuleFor(x => x.VoucherCode)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Vui lòng nhập mã voucher.")
            .Must(value => value is null
                || value.Trim().Length is >= MinCodeLength and <= MaxCodeLength)
            .WithMessage($"Mã voucher phải từ {MinCodeLength} đến {MaxCodeLength} ký tự.")
            .Matches("^\\s*[A-Za-z0-9_-]*\\s*$")
            .WithMessage("Mã voucher chỉ gồm chữ không dấu, chữ số, dấu gạch ngang hoặc gạch dưới.");

        RuleFor(x => x.DiscountType)
            .Must(value => value is Voucher.TypePercentage or Voucher.TypeFixedAmount)
            .WithMessage("Vui lòng chọn kiểu giảm giá: theo phần trăm hoặc số tiền cố định.");

        RuleFor(x => x.DiscountValue)
            .GreaterThan(0).WithMessage("Giá trị giảm phải lớn hơn 0.")
            .PrecisionScale(18, 2, true).WithMessage("Giá trị giảm tối đa 2 chữ số thập phân.");

        RuleFor(x => x.DiscountValue)
            .LessThanOrEqualTo(100).WithMessage("Giảm theo phần trăm không được vượt quá 100%.")
            .When(x => x.DiscountType == Voucher.TypePercentage);

        RuleFor(x => x.MaxDiscountAmount)
            .GreaterThan(0).WithMessage("Mức giảm tối đa phải lớn hơn 0.")
            .PrecisionScale(18, 2, true).WithMessage("Mức giảm tối đa có tối đa 2 chữ số thập phân.")
            .When(x => x.MaxDiscountAmount.HasValue);

        RuleFor(x => x.MinOrderAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Giá trị đơn tối thiểu không được âm.")
            .PrecisionScale(18, 2, true).WithMessage("Giá trị đơn tối thiểu có tối đa 2 chữ số thập phân.");

        RuleFor(x => x.UsageLimit)
            .GreaterThanOrEqualTo(1).WithMessage("Giới hạn lượt dùng phải từ 1 trở lên.")
            .When(x => x.UsageLimit.HasValue);

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("Ngày kết thúc phải sau ngày bắt đầu.");
    }
}
