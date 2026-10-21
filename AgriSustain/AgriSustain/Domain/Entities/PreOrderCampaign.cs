using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class PreOrderCampaign
{
    public long PreOrderCampaignId { get; set; }

    public long ProductId { get; set; }

    public long? CropSeasonId { get; set; }

    public string CampaignName { get; set; } = null!;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public decimal TargetYield { get; set; }

    public decimal CommittedYield { get; set; }

    public decimal DepositPercentage { get; set; }

    public decimal PreOrderPrice { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual CropSeason? CropSeason { get; set; }

    public virtual ICollection<HarvestBatch> HarvestBatches { get; set; } = new List<HarvestBatch>();

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual Product Product { get; set; } = null!;
}
