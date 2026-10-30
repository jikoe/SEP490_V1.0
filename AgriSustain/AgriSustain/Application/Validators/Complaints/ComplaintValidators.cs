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

        // MSG-CS-04: a partial refund needs at least one affected item with a valid quantity.
        RuleFor(x => x.Items)
            .Must(items => items is { Count: > 0 })
            .WithMessage("Vui lòng chọn ít nhất một sản phẩm bị ảnh hưởng để hoàn tiền một phần.")
            .Must(items => items is null
                || items.Select(item => item.OrderItemId).Distinct().Count() == items.Count)
            .WithMessage("Mỗi sản phẩm chỉ được chọn một lần.")
            .When(x => x.Resolution == ComplaintResolutions.PartialRefund);

        RuleForEach(x => x.Items)
            .Must(item => item.Quantity > 0 && item.Quantity == decimal.Round(item.Quantity, 3))
            .WithMessage("Số lượng hoàn phải lớn hơn 0 và có tối đa 3 chữ số thập phân.")
            .When(x => x.Resolution == ComplaintResolutions.PartialRefund);

        RuleFor(x => x.Items)
            .Must(items => items is null || items.Count == 0)
            .WithMessage("Chỉ chọn sản phẩm khi Hoàn tiền một phần.")
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
