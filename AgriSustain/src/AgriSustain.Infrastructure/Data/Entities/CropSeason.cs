using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

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

    public virtual ICollection<ContractLine> ContractLine { get; set; } = new List<ContractLine>();

    public virtual ICollection<CultivationLog> CultivationLog { get; set; } = new List<CultivationLog>();

    public virtual FarmerProfile FarmerProfile { get; set; } = null!;

    public virtual ICollection<HarvestBatch> HarvestBatch { get; set; } = new List<HarvestBatch>();

    public virtual ICollection<PreOrderCampaign> PreOrderCampaign { get; set; } = new List<PreOrderCampaign>();
}
