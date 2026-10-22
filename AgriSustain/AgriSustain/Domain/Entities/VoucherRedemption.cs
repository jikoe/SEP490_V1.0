using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class VoucherRedemption
{
    public long VoucherRedemptionId { get; set; }

    public long VoucherId { get; set; }

    public long OrderId { get; set; }

    public long? SellerOrderId { get; set; }

    public decimal DiscountSnapshot { get; set; }

    public string Status { get; set; } = null!;

    public DateTime ReservedAt { get; set; }

    public DateTime? AppliedAt { get; set; }

    public DateTime? ReleasedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual SellerOrder? SellerOrder { get; set; }

    public virtual Voucher Voucher { get; set; } = null!;
}
