using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class DisputeTicket
{
    public long DisputeTicketId { get; set; }

    public long SubmittedByUserId { get; set; }

    public long? AssignedStaffUserId { get; set; }

    public long? SellerOrderId { get; set; }

    public long? B2BContractId { get; set; }

    public string DisputeReason { get; set; } = null!;

    public string? Description { get; set; }

    public string TicketStatus { get; set; } = null!;

    public string? FaultParty { get; set; }

    public decimal? RefundAmount { get; set; }

    public string? ResolutionNotes { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? DecisionAt { get; set; }

    public virtual User? AssignedStaffUser { get; set; }

    public virtual B2BContract? B2BContract { get; set; }

    public virtual ICollection<DisputeEvidence> DisputeEvidence { get; set; } = new List<DisputeEvidence>();

    public virtual ICollection<RefundTransaction> RefundTransaction { get; set; } = new List<RefundTransaction>();

    public virtual SellerOrder? SellerOrder { get; set; }

    public virtual User SubmittedByUser { get; set; } = null!;
}
