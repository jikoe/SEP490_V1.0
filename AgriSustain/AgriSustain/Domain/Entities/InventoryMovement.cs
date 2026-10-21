using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class InventoryMovement
{
    public long InventoryMovementId { get; set; }

    public long HarvestBatchId { get; set; }

    public long? OrderItemBatchAllocationId { get; set; }

    public long? RecordedByUserId { get; set; }

    public string MovementType { get; set; } = null!;

    public decimal QuantityChange { get; set; }

    public string? Reason { get; set; }

    public DateTime OccurredAt { get; set; }

    public string RequestKey { get; set; } = null!;

    public virtual HarvestBatch HarvestBatch { get; set; } = null!;

    public virtual OrderItemBatchAllocation? OrderItemBatchAllocation { get; set; }

    public virtual User? RecordedByUser { get; set; }
}
