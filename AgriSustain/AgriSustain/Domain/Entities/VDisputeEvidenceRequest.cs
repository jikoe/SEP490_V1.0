using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class VDisputeEvidenceRequest
{
    public long EvidenceRequestId { get; set; }

    public long DisputeTicketId { get; set; }

    public long RequestedPartyUserId { get; set; }

    public long RequestedByUserId { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? DueAt { get; set; }

    public string EffectiveStatus { get; set; } = null!;

    public DateTime? RespondedAt { get; set; }

    public string? Instructions { get; set; }
}
