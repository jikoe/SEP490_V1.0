using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class PaymentGatewayEvent
{
    public long PaymentGatewayEventId { get; set; }

    public string GatewayProvider { get; set; } = null!;

    public string ProviderEventId { get; set; } = null!;

    public long PaymentTransactionId { get; set; }

    public string ProviderTransactionReference { get; set; } = null!;

    public decimal Amount { get; set; }

    public string EventStatus { get; set; } = null!;

    public string PayloadHash { get; set; } = null!;

    public DateTime ReceivedAt { get; set; }

    public virtual PaymentTransaction PaymentTransaction { get; set; } = null!;
}
