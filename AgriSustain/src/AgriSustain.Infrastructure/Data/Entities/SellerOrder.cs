using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class SellerOrder
{
    public long SellerOrderId { get; set; }

    public long OrderId { get; set; }

    public long FarmerProfileId { get; set; }

    public long? B2BContractId { get; set; }

    public string SellerOrderCode { get; set; } = null!;

    public decimal Subtotal { get; set; }

    public decimal DiscountShare { get; set; }

    public decimal DeliveryFee { get; set; }

    public decimal FinalAmount { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? DeliveredAt { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public string? SellerNameSnapshot { get; set; }

    public virtual B2BContract? B2BContract { get; set; }

    public virtual ICollection<DisputeTicket> DisputeTicket { get; set; } = new List<DisputeTicket>();

    public virtual FarmerProfile FarmerProfile { get; set; } = null!;

    public virtual ICollection<Invoice> Invoice { get; set; } = new List<Invoice>();

    public virtual Order Order { get; set; } = null!;

    public virtual ICollection<OrderItem> OrderItem { get; set; } = new List<OrderItem>();

    public virtual ICollection<OrderStatusHistory> OrderStatusHistory { get; set; } = new List<OrderStatusHistory>();

    public virtual ICollection<Shipment> Shipment { get; set; } = new List<Shipment>();

    public virtual ICollection<VoucherRedemption> VoucherRedemption { get; set; } = new List<VoucherRedemption>();
}
