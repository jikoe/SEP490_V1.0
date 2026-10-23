using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class HarvestBatch
{
    public long HarvestBatchId { get; set; }

    public long ProductId { get; set; }

    public long CropSeasonId { get; set; }

    public long? PreOrderCampaignId { get; set; }

    public string BatchCode { get; set; } = null!;

    public DateOnly? EstimatedHarvestDate { get; set; }

    public DateOnly? ActualHarvestDate { get; set; }

    public decimal HarvestYield { get; set; }

    public DateOnly? ExpirationDate { get; set; }

    public string? QualityNotes { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? StockEnteredAt { get; set; }

    public decimal ReservedQuantity { get; set; }

    public decimal AvailableQuantity { get; set; }

    public virtual CropSeason CropSeason { get; set; } = null!;

    public virtual ICollection<InventoryMovement> InventoryMovement { get; set; } = new List<InventoryMovement>();

    public virtual ICollection<OrderItemBatchAllocation> OrderItemBatchAllocation { get; set; } = new List<OrderItemBatchAllocation>();

    public virtual PreOrderCampaign? PreOrderCampaign { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual TraceabilityQRCode? TraceabilityQRCode { get; set; }
}
