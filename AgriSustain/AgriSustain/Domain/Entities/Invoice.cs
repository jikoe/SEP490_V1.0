using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class Invoice
{
    public long InvoiceId { get; set; }

    public long? SellerOrderId { get; set; }

    public long? ContractMilestoneId { get; set; }

    public string InvoiceNumber { get; set; } = null!;

    public DateTime IssuedAt { get; set; }

    public DateTime? DueAt { get; set; }

    public decimal TotalAmount { get; set; }

    public string InvoiceStatus { get; set; } = null!;

    public virtual ContractMilestone? ContractMilestone { get; set; }

    public virtual ICollection<InvoiceLine> InvoiceLines { get; set; } = new List<InvoiceLine>();

    public virtual SellerOrder? SellerOrder { get; set; }
}
