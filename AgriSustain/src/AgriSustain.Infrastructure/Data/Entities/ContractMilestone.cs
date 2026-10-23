using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class ContractMilestone
{
    public long ContractMilestoneId { get; set; }

    public long B2BContractId { get; set; }

    public long? ContractLineId { get; set; }

    public string MilestoneTitle { get; set; } = null!;

    public string MilestoneType { get; set; } = null!;

    public DateOnly DueDate { get; set; }

    public decimal Amount { get; set; }

    public decimal? DeliveryQuantity { get; set; }

    public string FulfillmentStatus { get; set; } = null!;

    public string PaymentStatus { get; set; } = null!;

    public decimal DeliveredQuantity { get; set; }

    public decimal AcceptedQuantity { get; set; }

    public decimal PaidAmount { get; set; }

    public virtual B2BContract B2BContract { get; set; } = null!;

    public virtual ContractLine? ContractLine { get; set; }

    public virtual ICollection<Invoice> Invoice { get; set; } = new List<Invoice>();

    public virtual ICollection<PaymentTransaction> PaymentTransaction { get; set; } = new List<PaymentTransaction>();

    public virtual ICollection<PayoutItem> PayoutItem { get; set; } = new List<PayoutItem>();

    public virtual ICollection<Shipment> Shipment { get; set; } = new List<Shipment>();
}
