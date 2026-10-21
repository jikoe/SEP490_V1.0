using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class CartItem
{
    public long CartItemId { get; set; }

    public long CartId { get; set; }

    public long ProductId { get; set; }

    public long? PreOrderCampaignId { get; set; }

    public decimal Quantity { get; set; }

    public bool IsSelected { get; set; }

    public DateTime AddedAt { get; set; }

    public virtual Cart Cart { get; set; } = null!;

    public virtual PreOrderCampaign? PreOrderCampaign { get; set; }

    public virtual Product Product { get; set; } = null!;
}
