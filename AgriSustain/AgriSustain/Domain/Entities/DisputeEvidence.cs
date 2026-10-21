using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class DisputeEvidence
{
    public long DisputeEvidenceId { get; set; }

    public long DisputeTicketId { get; set; }

    public long SubmittedByUserId { get; set; }

    public string FileUrl { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime SubmittedAt { get; set; }

    public long? EvidenceRequestId { get; set; }

    public virtual DisputeTicket DisputeTicket { get; set; } = null!;

    public virtual DisputeEvidenceRequest? EvidenceRequest { get; set; }

    public virtual User SubmittedByUser { get; set; } = null!;
}
