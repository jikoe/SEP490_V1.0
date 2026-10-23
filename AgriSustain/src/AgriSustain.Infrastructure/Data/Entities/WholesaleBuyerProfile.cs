using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

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

    public virtual ICollection<B2BContract> B2BContract { get; set; } = new List<B2BContract>();

    public virtual ICollection<B2BSubscription> B2BSubscription { get; set; } = new List<B2BSubscription>();

    public virtual User User { get; set; } = null!;
}
