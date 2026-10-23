using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class ContractSignature
{
    public long ContractSignatureId { get; set; }

    public long ContractVersionId { get; set; }

    public long SignedByUserId { get; set; }

    public string PartyRole { get; set; } = null!;

    public string ConfirmationMethod { get; set; } = null!;

    public DateTime? ConfirmedAt { get; set; }

    public string ConfirmationStatus { get; set; } = null!;

    public virtual ContractVersion ContractVersion { get; set; } = null!;

    public virtual User SignedByUser { get; set; } = null!;
}
