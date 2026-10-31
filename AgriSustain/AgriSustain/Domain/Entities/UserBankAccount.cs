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

    /// <summary>Verified or Unverified: result of the bank account lookup (BR-BA-06).</summary>
    public string VerificationStatus { get; set; } = "Unverified";

    /// <summary>QR image of the saved account, added after the account is saved.</summary>
    public string? QrImageUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual SupportedBank BankCodeNavigation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
