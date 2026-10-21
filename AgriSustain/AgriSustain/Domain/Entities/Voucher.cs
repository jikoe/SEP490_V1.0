using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class Voucher
{
    public long VoucherId { get; set; }

    public long? FarmerProfileId { get; set; }

    public string VoucherCode { get; set; } = null!;

    public string DiscountType { get; set; } = null!;

    public decimal DiscountValue { get; set; }

    public decimal? MaxDiscountAmount { get; set; }

    public decimal MinOrderAmount { get; set; }

    public int? UsageLimit { get; set; }

    public int UsedCount { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; }

    public virtual FarmerProfile? FarmerProfile { get; set; }

    public virtual ICollection<VoucherRedemption> VoucherRedemptions { get; set; } = new List<VoucherRedemption>();
}
