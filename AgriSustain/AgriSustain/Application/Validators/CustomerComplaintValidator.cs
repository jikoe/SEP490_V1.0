using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Validators;

internal static class CustomerComplaintValidator
{
    public static bool IsValid(CustomerComplaintQuery query) =>
        query.Page is >= 1 and <= 100_000 && query.OrderId is null or > 0
        && query.Status is null or "" or DisputeTicket.StatusOpen or DisputeTicket.StatusUnderReview
            or DisputeTicket.StatusWaitingInfo or DisputeTicket.StatusRefundPending
            or DisputeTicket.StatusRejected or DisputeTicket.StatusClosed;

    public static bool IsValid(CustomerComplaintResponseRequest input, string explanation) =>
        input.EvidenceRequestId > 0 && input.Version?.Length == 64
        && explanation.Length is >= 15 and <= 2000
        && FarmerComplaintEvidenceRules.IsValidSelection(input.EvidenceUrls);
}
