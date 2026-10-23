using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class FarmerPayout
{
    public long FarmerPayoutId { get; set; }

    public long FarmerProfileId { get; set; }

    public long? ApprovedByUserId { get; set; }

    public string PayoutCode { get; set; } = null!;

    public decimal Amount { get; set; }

    public decimal DeductedCommission { get; set; }

    public decimal NetAmount { get; set; }

    public string PayoutStatus { get; set; } = null!;

    public string? BankTransferRef { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? EligibleAt { get; set; }

    public virtual User? ApprovedByUser { get; set; }

    public virtual FarmerProfile FarmerProfile { get; set; } = null!;

    public virtual ICollection<PayoutItem> PayoutItem { get; set; } = new List<PayoutItem>();
}
