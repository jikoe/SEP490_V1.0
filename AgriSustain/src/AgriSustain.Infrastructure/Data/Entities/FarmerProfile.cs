using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class FarmerProfile
{
    public long FarmerProfileId { get; set; }

    public long UserId { get; set; }

    public string FarmName { get; set; } = null!;

    public string RepresentativeName { get; set; } = null!;

    public string? ProductionScale { get; set; }

    public string? BusinessLicenseNumber { get; set; }

    public string Address { get; set; } = null!;

    public string ProvinceCode { get; set; } = null!;

    public string? CultivationZone { get; set; }

    public string? GPSCoordinates { get; set; }

    public string VerificationStatus { get; set; } = null!;

    public DateTime? ApprovedAt { get; set; }

    public string? RejectReason { get; set; }

    public decimal ReputationScore { get; set; }

    public virtual ICollection<B2BContract> B2BContract { get; set; } = new List<B2BContract>();

    public virtual ICollection<CropSeason> CropSeason { get; set; } = new List<CropSeason>();

    public virtual ICollection<FarmCertificate> FarmCertificate { get; set; } = new List<FarmCertificate>();

    public virtual ICollection<FarmerPayout> FarmerPayout { get; set; } = new List<FarmerPayout>();

    public virtual ICollection<Product> Product { get; set; } = new List<Product>();

    public virtual ICollection<SellerOrder> SellerOrder { get; set; } = new List<SellerOrder>();

    public virtual User User { get; set; } = null!;

    public virtual ICollection<Voucher> Voucher { get; set; } = new List<Voucher>();
}
