using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class RegionalPriceBenchmark
{
    public long BenchmarkId { get; set; }

    public long CategoryId { get; set; }

    public string RegionName { get; set; } = null!;

    public string ProvinceName { get; set; } = null!;

    public string CommodityName { get; set; } = null!;

    public decimal MinPrice { get; set; }

    public decimal MaxPrice { get; set; }

    public decimal AveragePrice { get; set; }

    public DateOnly RecordedDate { get; set; }

    public virtual Category Category { get; set; } = null!;
}
