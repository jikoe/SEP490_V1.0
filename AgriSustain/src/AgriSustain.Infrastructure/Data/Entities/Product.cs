using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class Product
{
    public long ProductId { get; set; }

    public long CategoryId { get; set; }

    public long FarmerProfileId { get; set; }

    public string ProductName { get; set; } = null!;

    public string? Description { get; set; }

    public string Unit { get; set; } = null!;

    public string? OriginProvince { get; set; }

    public decimal BaseRetailPrice { get; set; }

    public decimal? WholesaleMinQuantity { get; set; }

    public decimal? WholesalePrice { get; set; }

    public string ApprovalStatus { get; set; } = null!;

    public bool IsPublished { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<CartItem> CartItem { get; set; } = new List<CartItem>();

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<ContractLine> ContractLine { get; set; } = new List<ContractLine>();

    public virtual FarmerProfile FarmerProfile { get; set; } = null!;

    public virtual ICollection<HarvestBatch> HarvestBatch { get; set; } = new List<HarvestBatch>();

    public virtual ICollection<OrderItem> OrderItem { get; set; } = new List<OrderItem>();

    public virtual ICollection<PreOrderCampaign> PreOrderCampaign { get; set; } = new List<PreOrderCampaign>();

    public virtual ICollection<ProductImage> ProductImage { get; set; } = new List<ProductImage>();
}
