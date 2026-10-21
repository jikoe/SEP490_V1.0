using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class B2bsubscriptionPackage
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

    public virtual ICollection<B2bsubscription> B2bsubscriptions { get; set; } = new List<B2bsubscription>();
}
