using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class B2BSubscriptionPackage
{
    public long PackageId { get; set; }

    public string PackageName { get; set; } = null!;

    public string? PackageDescription { get; set; }

    public int DurationDays { get; set; }

    public decimal Price { get; set; }

    public bool UnlocksFarmerDirectContact { get; set; }

    public bool CanDownloadContractTemplate { get; set; }

    public bool IsActive { get; set; }

    public int? TransactionLimit { get; set; }

    public virtual ICollection<B2BSubscription> B2BSubscription { get; set; } = new List<B2BSubscription>();
}
