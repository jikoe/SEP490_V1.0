using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class RewardPointEntry
{
    public long RewardPointEntryId { get; set; }

    public long UserId { get; set; }

    public long? OrderId { get; set; }

    public string EntryType { get; set; } = null!;

    public int Points { get; set; }

    public string RequestKey { get; set; } = null!;

    public long? AvailableDelta { get; set; }

    public long? HeldDelta { get; set; }

    public DateTime OccurredAt { get; set; }

    public virtual Order? Order { get; set; }

    public virtual User User { get; set; } = null!;
}
