using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class ProductImage
{
    public long ProductImageId { get; set; }

    public long ProductId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public bool IsPrimary { get; set; }

    public string? AltText { get; set; }

    public virtual Product Product { get; set; } = null!;
}
