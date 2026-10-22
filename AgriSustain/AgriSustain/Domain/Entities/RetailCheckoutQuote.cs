using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class RetailCheckoutQuote
{
    public Guid QuoteId { get; set; }

    public long BuyerUserId { get; set; }

    public string AcceptedSnapshotJson { get; set; } = null!;

    public string Fingerprint { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public virtual User BuyerUser { get; set; } = null!;

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
