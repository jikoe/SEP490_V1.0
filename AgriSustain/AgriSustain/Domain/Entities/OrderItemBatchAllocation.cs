using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

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

    public virtual ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();

    public virtual OrderItem OrderItem { get; set; } = null!;

    public virtual ICollection<ShipmentItem> ShipmentItems { get; set; } = new List<ShipmentItem>();
}
