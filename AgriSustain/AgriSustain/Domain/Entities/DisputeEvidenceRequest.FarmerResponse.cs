namespace AgriSustain.Domain.Entities;

public partial class DisputeEvidenceRequest
{
    public DateTime ResponseDeadline => DateTime.SpecifyKind(DueAt ?? RequestedAt.AddHours(48), DateTimeKind.Utc);

    public bool IsOpenFor(long actor) => RequestedPartyUserId == actor
        && RequestStatus == DisputeTicket.EvidenceRequestPending
        && !RespondedAt.HasValue;

    public void MarkAnswered(DateTime now)
    {
        RequestStatus = "Responded";
        RespondedAt = now;
    }
}
