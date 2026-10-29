using AgriSustain.Application.DTOs.Complaints;
using FluentValidation;

namespace AgriSustain.Application.Validators.Complaints;

public sealed class ResolveComplaintRequestValidator : AbstractValidator<ResolveComplaintRequest>
{
    public const int MaxReasonLength = 1000;

    public ResolveComplaintRequestValidator()
    {
        RuleFor(x => x.Resolution)
            .Must(value => value is ComplaintResolutions.FullRefund
                or ComplaintResolutions.PartialRefund or ComplaintResolutions.RejectClaim)
            .WithMessage("Vui lòng chọn phương án xử lý: Hoàn tiền toàn bộ, Hoàn tiền một phần "
                + "hoặc Từ chối khiếu nại.");

        RuleFor(x => x.FaultParty)
            .Must(value => value is ComplaintParties.Seller
                or ComplaintParties.Buyer or ComplaintParties.NotDetermined)
            .WithMessage("Vui lòng chọn bên có lỗi: Nông dân/Người bán, Người mua hoặc Chưa xác định.");

        RuleFor(x => x.Reason)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Vui lòng nhập lý do quyết định.")
            .MaximumLength(MaxReasonLength)
            .WithMessage($"Lý do quyết định không được vượt quá {MaxReasonLength} ký tự.");

        RuleFor(x => x.RefundAmount)
            .NotNull().WithMessage("Vui lòng nhập số tiền hoàn.")
            .GreaterThan(0).WithMessage("Số tiền hoàn phải lớn hơn 0.")
            .PrecisionScale(18, 2, true).WithMessage("Số tiền hoàn tối đa 2 chữ số thập phân.")
            .When(x => x.Resolution == ComplaintResolutions.PartialRefund);

        RuleFor(x => x.RefundAmount)
            .Null().WithMessage("Chỉ nhập số tiền hoàn khi chọn Hoàn tiền một phần.")
            .When(x => x.Resolution is ComplaintResolutions.FullRefund or ComplaintResolutions.RejectClaim);
    }
}

public sealed class RequestComplaintInfoRequestValidator : AbstractValidator<RequestComplaintInfoRequest>
{
    public const int MaxInstructionsLength = 1000;

    public RequestComplaintInfoRequestValidator()
    {
        RuleFor(x => x.Party)
            .Must(value => value is ComplaintParties.Buyer or ComplaintParties.Seller)
            .WithMessage("Vui lòng chọn bên cần bổ sung thông tin: Người mua hoặc Nông dân/Người bán.");

        RuleFor(x => x.Instructions)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Vui lòng nhập nội dung thông tin cần bổ sung.")
            .MaximumLength(MaxInstructionsLength)
            .WithMessage($"Nội dung yêu cầu không được vượt quá {MaxInstructionsLength} ký tự.");
    }
}
