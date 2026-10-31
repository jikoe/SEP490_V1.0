using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class UserBankAccount
{
    public long UserBankAccountId { get; set; }

    public long UserId { get; set; }

    public string BankCode { get; set; } = null!;

    public string AccountNumber { get; set; } = null!;

    public string AccountHolder { get; set; } = null!;

    public bool IsDefault { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public DateTime? PayoutHoldUntil { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual SupportedBank BankCodeNavigation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
