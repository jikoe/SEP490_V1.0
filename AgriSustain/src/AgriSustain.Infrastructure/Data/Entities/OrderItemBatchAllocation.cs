using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class OrderItemBatchAllocation
{
    public long OrderItemBatchAllocationId { get; set; }

    public long OrderItemId { get; set; }

    public long HarvestBatchId { get; set; }

    public int AllocationSequence { get; set; }

    public decimal AllocatedQuantity { get; set; }

    public string AllocationStatus { get; set; } = null!;

    public DateTime ReservedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? ReleasedAt { get; set; }

    public virtual HarvestBatch HarvestBatch { get; set; } = null!;

    public virtual ICollection<InventoryMovement> InventoryMovement { get; set; } = new List<InventoryMovement>();

    public virtual OrderItem OrderItem { get; set; } = null!;

    public virtual ICollection<ShipmentItem> ShipmentItem { get; set; } = new List<ShipmentItem>();
}
