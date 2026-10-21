using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class B2bcontract
{
    public long B2bcontractId { get; set; }

    public long WholesaleBuyerProfileId { get; set; }

    public long FarmerProfileId { get; set; }

    public string ContractNumber { get; set; } = null!;

    public decimal DepositAmount { get; set; }

    public string? PaymentTerms { get; set; }

    public string DigitalSignatureStatus { get; set; } = null!;

    public DateTime? SignedDate { get; set; }

    public string Status { get; set; } = null!;

    public int CurrentVersionNo { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly ValidUntil { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual ICollection<ContractLine> ContractLines { get; set; } = new List<ContractLine>();

    public virtual ICollection<ContractMilestone> ContractMilestones { get; set; } = new List<ContractMilestone>();

    public virtual ICollection<ContractTerminationRequest> ContractTerminationRequests { get; set; } = new List<ContractTerminationRequest>();

    public virtual ICollection<ContractVersion> ContractVersions { get; set; } = new List<ContractVersion>();

    public virtual ICollection<DisputeTicket> DisputeTickets { get; set; } = new List<DisputeTicket>();

    public virtual FarmerProfile FarmerProfile { get; set; } = null!;

    public virtual ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();

    public virtual ICollection<SellerOrder> SellerOrders { get; set; } = new List<SellerOrder>();

    public virtual WholesaleBuyerProfile WholesaleBuyerProfile { get; set; } = null!;
}
