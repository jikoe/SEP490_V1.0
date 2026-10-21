using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class OrderItem
{
    public long OrderItemId { get; set; }

    public long SellerOrderId { get; set; }

    public long ProductId { get; set; }

    public long? PreOrderCampaignId { get; set; }

    public long? ContractLineId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPriceSnapshot { get; set; }

    public string UnitSnapshot { get; set; } = null!;

    public decimal DiscountApplied { get; set; }

    public decimal SubTotal { get; set; }

    public string ItemStatus { get; set; } = null!;

    public string? ProductNameSnapshot { get; set; }

    public virtual ContractLine? ContractLine { get; set; }

    public virtual ICollection<InvoiceLine> InvoiceLines { get; set; } = new List<InvoiceLine>();

    public virtual ICollection<OrderItemBatchAllocation> OrderItemBatchAllocations { get; set; } = new List<OrderItemBatchAllocation>();

    public virtual ICollection<PayoutItem> PayoutItems { get; set; } = new List<PayoutItem>();

    public virtual PreOrderCampaign? PreOrderCampaign { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual ProductReview? ProductReview { get; set; }

    public virtual SellerOrder SellerOrder { get; set; } = null!;
}
