using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class SupportedBank
{
    public string BankCode { get; set; } = null!;

    public string ShortName { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }

    public virtual ICollection<FarmerPayout> FarmerPayouts { get; set; } = new List<FarmerPayout>();

    public virtual ICollection<RefundTransaction> RefundTransactions { get; set; } = new List<RefundTransaction>();

    public virtual ICollection<UserBankAccount> UserBankAccounts { get; set; } = new List<UserBankAccount>();
}
