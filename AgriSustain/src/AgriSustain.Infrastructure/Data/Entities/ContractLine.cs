using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class ContractLine
{
    public long ContractLineId { get; set; }

    public long B2BContractId { get; set; }

    public long ProductId { get; set; }

    public long? CropSeasonId { get; set; }

    public decimal CommittedQuantity { get; set; }

    public string UnitSnapshot { get; set; } = null!;

    public string? QualitySpecification { get; set; }

    public string PriceType { get; set; } = null!;

    public decimal? AgreedPrice { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public string? PriceFormula { get; set; }

    public virtual B2BContract B2BContract { get; set; } = null!;

    public virtual ICollection<ContractMilestone> ContractMilestone { get; set; } = new List<ContractMilestone>();

    public virtual CropSeason? CropSeason { get; set; }

    public virtual ICollection<OrderItem> OrderItem { get; set; } = new List<OrderItem>();

    public virtual Product Product { get; set; } = null!;
}
