using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class PaymentInvoiceAllocation
{
    public long PaymentInvoiceAllocationId { get; set; }

    public long PaymentTransactionId { get; set; }

    public long InvoiceId { get; set; }

    public decimal Amount { get; set; }

    public long? ContractDepositAllocationId { get; set; }

    public string RequestKey { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ContractDepositAllocation? ContractDepositAllocation { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;

    public virtual PaymentTransaction PaymentTransaction { get; set; } = null!;

    public virtual ICollection<RefundTransaction> RefundTransactions { get; set; } = new List<RefundTransaction>();
}
