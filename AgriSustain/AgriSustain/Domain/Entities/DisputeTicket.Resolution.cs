namespace AgriSustain.Domain.Entities;

/// <summary>
/// UC-CS-01 Resolve Complaint. Lifecycle (SRS entity DisputeTicket):
/// Open → UnderReview → WaitingCustomerInfo → RefundPending / Rejected → Closed.
/// WaitingCustomerInfo is used for a request to either party, as in the SRS lifecycle.
/// </summary>
public partial class DisputeTicket
{
    public const string StatusOpen = "Open";
    public const string StatusUnderReview = "UnderReview";
    public const string StatusWaitingInfo = "WaitingCustomerInfo";
    public const string StatusRefundPending = "RefundPending";
    public const string StatusRejected = "Rejected";
    public const string StatusClosed = "Closed";

    public const string EvidenceRequestPending = "Pending";
    public const string EvidenceRequestClosed = "Closed";
    public const string EvidenceRequestExpired = "Expired";

    /// <summary>A decision or an information request may still be recorded.</summary>
    public bool IsAwaitingDecision =>
        TicketStatus is StatusOpen or StatusUnderReview or StatusWaitingInfo;

    public void RecordDecision(
        string newStatus, string faultParty, decimal? refundAmount, string notes,
        long staffUserId, DateTime now)
    {
        if (!IsAwaitingDecision)
        {
            throw new InvalidOperationException($"Complaint {DisputeTicketId} is already decided.");
        }

        TicketStatus = newStatus;
        FaultParty = faultParty;
        RefundAmount = refundAmount;
        ResolutionNotes = notes;
        AssignedStaffUserId = staffUserId;
        DecisionAt = now;
        ResolvedAt = now;
        CloseOpenEvidenceRequests();
    }

    public DisputeEvidenceRequest RequestInformation(
        long partyUserId, string instructions, long staffUserId, DateTime now, TimeSpan responseWindow)
    {
        if (!IsAwaitingDecision)
        {
            throw new InvalidOperationException($"Complaint {DisputeTicketId} is already decided.");
        }

        var request = new DisputeEvidenceRequest
        {
            RequestedPartyUserId = partyUserId,
            RequestedByUserId = staffUserId,
            RequestedAt = now,
            DueAt = now.Add(responseWindow),
            RequestStatus = EvidenceRequestPending,
            Instructions = instructions
        };
        DisputeEvidenceRequests.Add(request);
        TicketStatus = StatusWaitingInfo;
        AssignedStaffUserId = staffUserId;
        return request;
    }

    /// <summary>UC-FIN-03: the refund decided for this complaint has been transferred.</summary>
    public void CloseAfterRefund()
    {
        if (TicketStatus != StatusRefundPending)
        {
            throw new InvalidOperationException($"Complaint {DisputeTicketId} is not awaiting a refund.");
        }

        TicketStatus = StatusClosed;
    }

    public bool HasOpenRequestFor(long partyUserId, DateTime now) =>
        DisputeEvidenceRequests.Any(request => request.RequestedPartyUserId == partyUserId
            && request.RequestStatus == EvidenceRequestPending
            && request.RespondedAt == null
            && (request.DueAt == null || request.DueAt > now));

    private void CloseOpenEvidenceRequests()
    {
        foreach (var request in DisputeEvidenceRequests.Where(request =>
            request.RequestStatus == EvidenceRequestPending && request.RespondedAt == null))
        {
            request.RequestStatus = EvidenceRequestClosed;
        }
    }
}
