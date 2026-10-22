using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class VInvoiceFund
{
    public long InvoiceId { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal? CollectedAmount { get; set; }

    public decimal? RefundReservedAmount { get; set; }

    public decimal? PayoutReservedAmount { get; set; }
}
