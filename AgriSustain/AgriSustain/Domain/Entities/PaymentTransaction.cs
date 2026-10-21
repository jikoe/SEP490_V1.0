using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class PaymentTransaction
{
    public long PaymentTransactionId { get; set; }

    public long? OrderId { get; set; }

    public long? B2bsubscriptionId { get; set; }

    public long? ContractMilestoneId { get; set; }

    public long? B2bcontractId { get; set; }

    public string TransactionCode { get; set; } = null!;

    public string GatewayProvider { get; set; } = null!;

    public decimal Amount { get; set; }

    public string PaymentType { get; set; } = null!;

    public string PaymentStatus { get; set; } = null!;

    public string? WebhookPayload { get; set; }

    public DateTime? PaidAt { get; set; }

    public string PaymentPurpose { get; set; } = null!;

    public DateTime RequestedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? ReconciledAt { get; set; }

    public string RequestKey { get; set; } = null!;

    public string? ProviderTransactionReference { get; set; }

    public virtual B2bcontract? B2bcontract { get; set; }

    public virtual B2bsubscription? B2bsubscription { get; set; }

    public virtual ICollection<ContractDepositAllocation> ContractDepositAllocations { get; set; } = new List<ContractDepositAllocation>();

    public virtual ContractMilestone? ContractMilestone { get; set; }

    public virtual Order? Order { get; set; }

    public virtual ICollection<PaymentGatewayEvent> PaymentGatewayEvents { get; set; } = new List<PaymentGatewayEvent>();

    public virtual ICollection<PaymentInvoiceAllocation> PaymentInvoiceAllocations { get; set; } = new List<PaymentInvoiceAllocation>();

    public virtual ICollection<RefundTransaction> RefundTransactions { get; set; } = new List<RefundTransaction>();
}
