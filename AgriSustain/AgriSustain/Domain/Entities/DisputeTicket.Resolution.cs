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

    /// <summary>
    /// Writes the decision on the complaint and closes its open information requests. Throws when the complaint is
    /// already decided; callers check <see cref="IsAwaitingDecision"/> first, so the exception only guards against bugs.
    /// </summary>
    /// <param name="newStatus">Rejected or RefundPending.</param>
    /// <param name="faultParty">Seller, Buyer or NotDetermined.</param>
    /// <param name="refundAmount">Approved amount, or null for a rejection.</param>
    /// <param name="notes">Decision reason.</param>
    /// <param name="staffUserId">Deciding staff (or the staff of the expired request).</param>
    /// <param name="now">Decision time (UTC).</param>
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

    /// <summary>
    /// Adds an information request for one party with DueAt = now + response window, sets the complaint to
    /// WaitingCustomerInfo and assigns the staff member.
    /// </summary>
    /// <param name="partyUserId">User who must answer.</param>
    /// <param name="instructions">What to provide.</param>
    /// <param name="staffUserId">Requesting staff.</param>
    /// <param name="now">Request time (UTC).</param>
    /// <param name="responseWindow">Time allowed (48 hours).</param>
    /// <returns>The new request, already attached to the complaint.</returns>
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

    /// <summary>
    /// BR-CS-02 "one open request per party": true while the party has a Pending, unanswered request that is not yet due.
    /// </summary>
    /// <param name="partyUserId">Party to check.</param>
    /// <param name="now">Current UTC time.</param>
    /// <returns>True when a new request must be refused.</returns>
    public bool HasOpenRequestFor(long partyUserId, DateTime now) =>
        DisputeEvidenceRequests.Any(request => request.RequestedPartyUserId == partyUserId
            && request.RequestStatus == EvidenceRequestPending
            && request.RespondedAt == null
            && (request.DueAt == null || request.DueAt > now));

    /// <summary>
    /// Marks every still-open request Closed once the complaint is decided.
    /// </summary>
    private void CloseOpenEvidenceRequests()
    {
        foreach (var request in DisputeEvidenceRequests.Where(request =>
            request.RequestStatus == EvidenceRequestPending && request.RespondedAt == null))
        {
            request.RequestStatus = EvidenceRequestClosed;
        }
    }
}
