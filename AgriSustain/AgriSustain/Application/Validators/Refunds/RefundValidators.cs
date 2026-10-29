using AgriSustain.Application.DTOs.Refunds;
using FluentValidation;

namespace AgriSustain.Application.Validators.Refunds;

public sealed class CompleteRefundRequestValidator : AbstractValidator<CompleteRefundRequest>
{
    public const int MaxReferenceLength = 150;
    public const int MaxNoteLength = 500;

    public CompleteRefundRequestValidator()
    {
        RuleFor(x => x.RefundMethod)
            .Must(value => value is RefundMethods.BankTransfer or RefundMethods.GatewayRefund)
            .WithMessage("Vui lòng chọn hình thức hoàn tiền: Chuyển khoản ngân hàng hoặc "
                + "Hoàn qua cổng thanh toán.");

        RuleFor(x => x.TransferReference)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Vui lòng nhập mã giao dịch chuyển tiền.")
            .MaximumLength(MaxReferenceLength)
            .WithMessage($"Mã giao dịch không được vượt quá {MaxReferenceLength} ký tự.")
            .Matches(@"^[A-Za-z0-9._\-/ ]*$")
            .WithMessage("Mã giao dịch chỉ gồm chữ, số và các ký tự . _ - /.");

        RuleFor(x => x.Note)
            .MaximumLength(MaxNoteLength)
            .WithMessage($"Ghi chú không được vượt quá {MaxNoteLength} ký tự.");
    }
}

public sealed class RefundTransferFailedRequestValidator : AbstractValidator<RefundTransferFailedRequest>
{
    public RefundTransferFailedRequestValidator()
    {
        RuleFor(x => x.Reason)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Vui lòng nhập lý do chuyển tiền thất bại.")
            .MaximumLength(CompleteRefundRequestValidator.MaxNoteLength)
            .WithMessage($"Lý do không được vượt quá {CompleteRefundRequestValidator.MaxNoteLength} ký tự.");
    }
}
