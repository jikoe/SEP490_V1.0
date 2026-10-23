using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class CommissionPolicy
{
    public long CommissionPolicyId { get; set; }

    public long CategoryId { get; set; }

    public string ApplicableOrderType { get; set; } = null!;

    public decimal CommissionRate { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public string? Note { get; set; }

    public bool IsActive { get; set; }

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<PayoutItem> PayoutItem { get; set; } = new List<PayoutItem>();
}
