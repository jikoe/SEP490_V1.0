using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class BusinessPolicy
{
    public byte PolicyId { get; set; }

    public int PayoutDelayHours { get; set; }
}
