using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class OrderStatusHistory
{
    public long OrderStatusHistoryId { get; set; }

    public long SellerOrderId { get; set; }

    public long? ChangedByUserId { get; set; }

    public string? OldStatus { get; set; }

    public string NewStatus { get; set; } = null!;

    public string? Reason { get; set; }

    public DateTime OccurredAt { get; set; }

    public virtual User? ChangedByUser { get; set; }

    public virtual SellerOrder SellerOrder { get; set; } = null!;
}
