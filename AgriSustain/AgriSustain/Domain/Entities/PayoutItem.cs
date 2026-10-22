using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class PayoutItem
{
    public long PayoutItemId { get; set; }

    public long FarmerPayoutId { get; set; }

    public long? OrderItemId { get; set; }

    public long? ContractMilestoneId { get; set; }

    public long? CommissionPolicyId { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal CommissionRateSnapshot { get; set; }

    public decimal CommissionAmount { get; set; }

    public decimal NetAmount { get; set; }

    public decimal SourceAmountSettled { get; set; }

    public long InvoiceLineId { get; set; }

    public virtual CommissionPolicy? CommissionPolicy { get; set; }

    public virtual ContractMilestone? ContractMilestone { get; set; }

    public virtual FarmerPayout FarmerPayout { get; set; } = null!;

    public virtual InvoiceLine InvoiceLine { get; set; } = null!;

    public virtual OrderItem? OrderItem { get; set; }
}
