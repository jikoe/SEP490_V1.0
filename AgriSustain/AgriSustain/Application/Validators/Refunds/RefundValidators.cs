using AgriSustain.Application.DTOs.Refunds;
using FluentValidation;

namespace AgriSustain.Application.Validators.Refunds;

/// <summary>BR-RF-02; messages follow MSG-RF-02 ("Vui lòng sửa {Field}: {ValidationReason}.").</summary>
public sealed class CompleteRefundRequestValidator : AbstractValidator<CompleteRefundRequest>
{
    public const int MaxReferenceLength = 150;
    public const int MaxNoteLength = 500;

    public CompleteRefundRequestValidator()
    {
        RuleFor(x => x.RefundMethod)
            .Must(value => value is RefundMethods.BankTransfer or RefundMethods.GatewayRefund)
            .WithMessage("Vui lòng sửa Hình thức hoàn tiền: chọn Chuyển khoản ngân hàng hoặc "
                + "Hoàn qua cổng thanh toán.");

        RuleFor(x => x.TransferReference)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Vui lòng sửa Mã giao dịch ngân hàng: bắt buộc nhập.")
            .MaximumLength(MaxReferenceLength)
            .WithMessage($"Vui lòng sửa Mã giao dịch ngân hàng: tối đa {MaxReferenceLength} ký tự.")
            .Matches(@"^[A-Za-z0-9._\-/ ]*$")
            .WithMessage("Vui lòng sửa Mã giao dịch ngân hàng: chỉ gồm chữ, số, khoảng trắng và . _ - /.");

        RuleFor(x => x.Note)
            .MaximumLength(MaxNoteLength)
            .WithMessage($"Vui lòng sửa Ghi chú: tối đa {MaxNoteLength} ký tự.");
    }
}

public sealed class RefundTransferFailedRequestValidator : AbstractValidator<RefundTransferFailedRequest>
{
    public RefundTransferFailedRequestValidator()
    {
        RuleFor(x => x.Reason)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Vui lòng sửa Lý do thất bại: bắt buộc nhập.")
            .MaximumLength(CompleteRefundRequestValidator.MaxNoteLength)
            .WithMessage($"Vui lòng sửa Lý do thất bại: tối đa {CompleteRefundRequestValidator.MaxNoteLength} ký tự.");
    }
}

public sealed class RefundReconciliationRequestValidator : AbstractValidator<RefundReconciliationRequest>
{
    public RefundReconciliationRequestValidator()
    {
        RuleFor(x => x.Note)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Vui lòng sửa Ghi chú đối soát: bắt buộc nhập.")
            .MaximumLength(CompleteRefundRequestValidator.MaxNoteLength)
            .WithMessage($"Vui lòng sửa Ghi chú đối soát: tối đa {CompleteRefundRequestValidator.MaxNoteLength} ký tự.");
    }
}
