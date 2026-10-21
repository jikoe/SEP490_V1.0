using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class WholesaleBuyerProfile
{
    public long WholesaleBuyerProfileId { get; set; }

    public long UserId { get; set; }

    public string CompanyName { get; set; } = null!;

    public string TaxCode { get; set; } = null!;

    public string RepresentativeName { get; set; } = null!;

    public string? BusinessType { get; set; }

    public string BusinessAddress { get; set; } = null!;

    public string? DocumentUrl { get; set; }

    public string VerificationStatus { get; set; } = null!;

    public DateTime? ApprovedAt { get; set; }

    public string? RejectReason { get; set; }

    public virtual ICollection<B2bcontract> B2bcontracts { get; set; } = new List<B2bcontract>();

    public virtual B2bsubscription? B2bsubscription { get; set; }

    public virtual User User { get; set; } = null!;
}
