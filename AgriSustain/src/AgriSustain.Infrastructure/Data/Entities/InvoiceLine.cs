using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class InvoiceLine
{
    public long InvoiceLineId { get; set; }

    public long InvoiceId { get; set; }

    public long? OrderItemId { get; set; }

    public long? ShipmentItemId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPriceSnapshot { get; set; }

    public decimal LineAmount { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;

    public virtual OrderItem? OrderItem { get; set; }

    public virtual ShipmentItem? ShipmentItem { get; set; }
}
