using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class ShipmentItem
{
    public long ShipmentItemId { get; set; }

    public long ShipmentId { get; set; }

    public long OrderItemBatchAllocationId { get; set; }

    public decimal ShippedQuantity { get; set; }

    public decimal? ReceivedQuantity { get; set; }

    public decimal? AcceptedQuantity { get; set; }

    public string? QualityResult { get; set; }

    public string ReconciliationStatus { get; set; } = null!;

    public DateTime? ReconciledAt { get; set; }

    public virtual ICollection<InvoiceLine> InvoiceLine { get; set; } = new List<InvoiceLine>();

    public virtual OrderItemBatchAllocation OrderItemBatchAllocation { get; set; } = null!;

    public virtual Shipment Shipment { get; set; } = null!;
}
