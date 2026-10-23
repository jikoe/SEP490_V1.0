using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class PaymentTransaction
{
    public long PaymentTransactionId { get; set; }

    public long? OrderId { get; set; }

    public long? B2BSubscriptionId { get; set; }

    public long? ContractMilestoneId { get; set; }

    public long? B2BContractId { get; set; }

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

    public virtual B2BContract? B2BContract { get; set; }

    public virtual B2BSubscription? B2BSubscription { get; set; }

    public virtual ContractMilestone? ContractMilestone { get; set; }

    public virtual Order? Order { get; set; }

    public virtual ICollection<RefundTransaction> RefundTransaction { get; set; } = new List<RefundTransaction>();
}
