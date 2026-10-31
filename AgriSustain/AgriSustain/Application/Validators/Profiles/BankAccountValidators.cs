using AgriSustain.Application.DTOs.Profiles;
using FluentValidation;

namespace AgriSustain.Application.Validators.Profiles;

/// <summary>BR-BA-01: bank required, account number 6-19 digits, holder at most 100 characters.</summary>
public static class BankAccountRules
{
    // MSG02
    public const string Required = "Trường này là bắt buộc.";

    // MSG-RF-02
    public const string InvalidAccountNumber =
        "Vui lòng sửa Số tài khoản: số tài khoản chỉ gồm chữ số, dài từ 6 đến 19 ký tự.";

    public const string InvalidHolder =
        "Vui lòng sửa Chủ tài khoản: tối đa 100 ký tự.";

    public const string InvalidOtp = "Mã OTP gồm 6 chữ số.";

    public static IRuleBuilderOptions<T, string> ValidBankCode<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage(Required)
            .MaximumLength(20).WithMessage(Required);

    public static IRuleBuilderOptions<T, string> ValidAccountNumber<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage(Required)
            .Matches(@"^\d{6,19}$").WithMessage(InvalidAccountNumber);

    public static IRuleBuilderOptions<T, string> ValidOtp<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage(Required)
            .Matches(@"^\d{6}$").WithMessage(InvalidOtp);
}

public sealed class BankLookupRequestValidator : AbstractValidator<BankLookupRequest>
{
    public BankLookupRequestValidator()
    {
        RuleFor(x => x.BankCode).ValidBankCode();
        RuleFor(x => x.AccountNumber).ValidAccountNumber();
    }
}

public sealed class SaveBankAccountRequestValidator : AbstractValidator<SaveBankAccountRequest>
{
    public SaveBankAccountRequestValidator()
    {
        RuleFor(x => x.BankCode).ValidBankCode();
        RuleFor(x => x.AccountNumber).ValidAccountNumber();
        RuleFor(x => x.AccountHolder)
            .MaximumLength(100).WithMessage(BankAccountRules.InvalidHolder);
        RuleFor(x => x.ConfirmRecipient)
            .Equal(true).WithMessage("Vui lòng xác nhận đây là đúng tài khoản nhận tiền.");
        RuleFor(x => x.OtpCode).ValidOtp();
    }
}

public sealed class RemoveBankAccountRequestValidator : AbstractValidator<RemoveBankAccountRequest>
{
    public RemoveBankAccountRequestValidator()
    {
        RuleFor(x => x.OtpCode).ValidOtp();
    }
}
