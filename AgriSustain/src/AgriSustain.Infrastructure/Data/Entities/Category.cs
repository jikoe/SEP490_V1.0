using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class Category
{
    public long CategoryId { get; set; }

    public long? ParentCategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public string? Description { get; set; }

    public int Priority { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<CommissionPolicy> CommissionPolicy { get; set; } = new List<CommissionPolicy>();

    public virtual ICollection<Category> InverseParentCategory { get; set; } = new List<Category>();

    public virtual Category? ParentCategory { get; set; }

    public virtual ICollection<Product> Product { get; set; } = new List<Product>();

    public virtual ICollection<RegionalPriceBenchmark> RegionalPriceBenchmark { get; set; } = new List<RegionalPriceBenchmark>();
}
