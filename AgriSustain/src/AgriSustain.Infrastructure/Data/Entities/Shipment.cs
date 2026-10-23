using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class Shipment
{
    public long ShipmentId { get; set; }

    public long SellerOrderId { get; set; }

    public long? ContractMilestoneId { get; set; }

    public string? TrackingNumber { get; set; }

    public DateTime? PlannedDeliveryAt { get; set; }

    public DateTime? ShippedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public string ShipmentStatus { get; set; } = null!;

    public virtual ContractMilestone? ContractMilestone { get; set; }

    public virtual SellerOrder SellerOrder { get; set; } = null!;

    public virtual ICollection<ShipmentItem> ShipmentItem { get; set; } = new List<ShipmentItem>();
}
