using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class PreOrderReservation
{
    public long PreOrderReservationId { get; set; }

    public long OrderItemId { get; set; }

    public long PreOrderCampaignId { get; set; }

    public decimal Quantity { get; set; }

    public string ReservationStatus { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public decimal PriceSnapshot { get; set; }

    public decimal DepositPercentageSnapshot { get; set; }

    public string UnitSnapshot { get; set; } = null!;

    public DateOnly EstimatedHarvestDateSnapshot { get; set; }

    public string RequestKey { get; set; } = null!;

    public virtual OrderItem OrderItem { get; set; } = null!;

    public virtual PreOrderCampaign PreOrderCampaign { get; set; } = null!;
}
