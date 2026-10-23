using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class B2BSubscription
{
    public long B2BSubscriptionId { get; set; }

    public long WholesaleBuyerProfileId { get; set; }

    public long PackageId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string SubscriptionStatus { get; set; } = null!;

    public bool AutoRenew { get; set; }

    public decimal PriceSnapshot { get; set; }

    public DateTime? LastRenewedAt { get; set; }

    public virtual B2BSubscriptionPackage Package { get; set; } = null!;

    public virtual ICollection<PaymentTransaction> PaymentTransaction { get; set; } = new List<PaymentTransaction>();

    public virtual WholesaleBuyerProfile WholesaleBuyerProfile { get; set; } = null!;
}
