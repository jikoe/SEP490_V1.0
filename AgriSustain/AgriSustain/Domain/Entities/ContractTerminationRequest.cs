using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class ContractTerminationRequest
{
    public long ContractTerminationRequestId { get; set; }

    public long B2bcontractId { get; set; }

    public long RequestedByUserId { get; set; }

    public string Reason { get; set; } = null!;

    public string? EvidenceUrl { get; set; }

    public DateTime RequestedAt { get; set; }

    public string RequestStatus { get; set; } = null!;

    public decimal? SettlementAmount { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public virtual B2bcontract B2bcontract { get; set; } = null!;

    public virtual ICollection<RefundTransaction> RefundTransactions { get; set; } = new List<RefundTransaction>();

    public virtual User RequestedByUser { get; set; } = null!;
}
