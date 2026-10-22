using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class VRewardPointBalance
{
    public long UserId { get; set; }

    public long? AvailablePoints { get; set; }

    public long? HeldPoints { get; set; }
}
