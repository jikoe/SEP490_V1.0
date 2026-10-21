using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class ContractDepositAllocation
{
    public long ContractDepositAllocationId { get; set; }

    public long PaymentTransactionId { get; set; }

    public long ContractMilestoneId { get; set; }

    public decimal Amount { get; set; }

    public string RequestKey { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ContractMilestone ContractMilestone { get; set; } = null!;

    public virtual ICollection<PaymentInvoiceAllocation> PaymentInvoiceAllocations { get; set; } = new List<PaymentInvoiceAllocation>();

    public virtual PaymentTransaction PaymentTransaction { get; set; } = null!;

    public virtual ICollection<RefundTransaction> RefundTransactions { get; set; } = new List<RefundTransaction>();
}
