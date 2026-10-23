using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class DisputeEvidence
{
    public long DisputeEvidenceId { get; set; }

    public long DisputeTicketId { get; set; }

    public long SubmittedByUserId { get; set; }

    public string FileUrl { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime SubmittedAt { get; set; }

    public virtual DisputeTicket DisputeTicket { get; set; } = null!;

    public virtual User SubmittedByUser { get; set; } = null!;
}
