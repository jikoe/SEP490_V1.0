using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class DisputeEvidenceRequest
{
    public long EvidenceRequestId { get; set; }

    public long DisputeTicketId { get; set; }

    public long RequestedPartyUserId { get; set; }

    public long RequestedByUserId { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? DueAt { get; set; }

    public string RequestStatus { get; set; } = null!;

    public DateTime? RespondedAt { get; set; }

    public string? Instructions { get; set; }

    public virtual ICollection<DisputeEvidence> DisputeEvidences { get; set; } = new List<DisputeEvidence>();

    public virtual DisputeTicket DisputeTicket { get; set; } = null!;

    public virtual User RequestedByUser { get; set; } = null!;

    public virtual User RequestedPartyUser { get; set; } = null!;
}
