using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AgriSustain.Domain.Entities;

/// <summary>UC-ADM-09 rules of a subscription tier (BR-ST-01 to BR-ST-04).</summary>
public partial class B2bsubscriptionPackage
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    /// <summary>BR-ST-03: values are copied into new subscriptions; current ones keep their snapshot.</summary>
    public void Apply(string name, string? description, int durationDays, decimal price,
        bool directContact, bool contractTemplate, int? transactionLimit, bool isActive)
    {
        PackageName = name.Trim();
        PackageDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DurationDays = durationDays;
        Price = price;
        UnlocksFarmerDirectContact = directContact;
        CanDownloadContractTemplate = contractTemplate;
        TransactionLimit = transactionLimit;
        IsActive = isActive;
    }

    /// <summary>
    /// Version of the values shown to the Administrator. A save or status change sent with an
    /// older version was made against a tier someone else has since changed (MSG122).
    /// </summary>
    public string Version() => VersionOf(PackageName, PackageDescription, DurationDays, Price,
        UnlocksFarmerDirectContact, CanDownloadContractTemplate, TransactionLimit, IsActive);

    public static string VersionOf(string name, string? description, int durationDays, decimal price,
        bool directContact, bool contractTemplate, int? transactionLimit, bool isActive)
    {
        string text = string.Join('\u001f', name, description, durationDays,
            price.ToString("0.00", CultureInfo.InvariantCulture), directContact, contractTemplate,
            transactionLimit, isActive);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }
}
