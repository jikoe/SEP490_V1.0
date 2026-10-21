using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class DeliveryAddress
{
    public long DeliveryAddressId { get; set; }

    public long UserId { get; set; }

    public string RecipientName { get; set; } = null!;

    public string RecipientPhone { get; set; } = null!;

    public string AddressDetail { get; set; } = null!;

    public string ProvinceCity { get; set; } = null!;

    public string? District { get; set; }

    public string Ward { get; set; } = null!;

    public string? ProvinceCode { get; set; }

    public string? WardCode { get; set; }

    public string? AddressCatalogVersion { get; set; }

    public bool IsDefault { get; set; }

    public string Status { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
