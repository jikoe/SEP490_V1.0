using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class ContractMilestone
{
    public long ContractMilestoneId { get; set; }

    public long B2bcontractId { get; set; }

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

    public long ContractVersionId { get; set; }

    public virtual B2bcontract B2bcontract { get; set; } = null!;

    public virtual ICollection<ContractDepositAllocation> ContractDepositAllocations { get; set; } = new List<ContractDepositAllocation>();

    public virtual ContractLine? ContractLine { get; set; }

    public virtual ContractVersion ContractVersion { get; set; } = null!;

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();

    public virtual ICollection<PayoutItem> PayoutItems { get; set; } = new List<PayoutItem>();

    public virtual ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();
}
