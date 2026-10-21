using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class Cart
{
    public long CartId { get; set; }

    public long UserId { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual User User { get; set; } = null!;
}
