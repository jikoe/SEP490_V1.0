using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class ContractVersion
{
    public long ContractVersionId { get; set; }

    public long B2bcontractId { get; set; }

    public long ProposedByUserId { get; set; }

    public int VersionNo { get; set; }

    public string TermsAndConditions { get; set; } = null!;

    public string? LineSnapshotJson { get; set; }

    public string? DocumentHash { get; set; }

    public string VersionStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public string? FeedbackReason { get; set; }

    public virtual B2bcontract B2bcontract { get; set; } = null!;

    public virtual ICollection<ContractSignature> ContractSignatures { get; set; } = new List<ContractSignature>();

    public virtual User ProposedByUser { get; set; } = null!;
}
