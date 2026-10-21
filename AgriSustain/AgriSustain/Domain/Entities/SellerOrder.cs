using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class SellerOrder
{
    public long SellerOrderId { get; set; }

    public long OrderId { get; set; }

    public long FarmerProfileId { get; set; }

    public string? SellerNameSnapshot { get; set; }

    public long? B2bcontractId { get; set; }

    public string SellerOrderCode { get; set; } = null!;

    public decimal Subtotal { get; set; }

    public decimal DiscountShare { get; set; }

    public decimal DeliveryFee { get; set; }

    public decimal FinalAmount { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? DeliveredAt { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public virtual B2bcontract? B2bcontract { get; set; }

    public virtual ICollection<DisputeTicket> DisputeTickets { get; set; } = new List<DisputeTicket>();

    public virtual FarmerProfile FarmerProfile { get; set; } = null!;

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual Order Order { get; set; } = null!;

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<OrderStatusHistory> OrderStatusHistories { get; set; } = new List<OrderStatusHistory>();

    public virtual ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();

    public virtual ICollection<VoucherRedemption> VoucherRedemptions { get; set; } = new List<VoucherRedemption>();
}
