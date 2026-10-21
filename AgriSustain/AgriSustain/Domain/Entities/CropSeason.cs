using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class CropSeason
{
    public long CropSeasonId { get; set; }

    public long FarmerProfileId { get; set; }

    public string SeasonName { get; set; } = null!;

    public string CropVariety { get; set; } = null!;

    public decimal CultivatedArea { get; set; }

    public DateOnly PlantingDate { get; set; }

    public DateOnly EstimatedHarvestDate { get; set; }

    public DateOnly? ActualHarvestDate { get; set; }

    public decimal EstimatedYield { get; set; }

    public decimal? ActualYield { get; set; }

    public string Status { get; set; } = null!;

    public long ProductId { get; set; }

    public string YieldUnit { get; set; } = null!;

    public virtual ICollection<ContractLine> ContractLines { get; set; } = new List<ContractLine>();

    public virtual ICollection<CultivationLog> CultivationLogs { get; set; } = new List<CultivationLog>();

    public virtual FarmerProfile FarmerProfile { get; set; } = null!;

    public virtual ICollection<HarvestBatch> HarvestBatches { get; set; } = new List<HarvestBatch>();

    public virtual ICollection<PreOrderCampaign> PreOrderCampaigns { get; set; } = new List<PreOrderCampaign>();

    public virtual Product Product { get; set; } = null!;
}
