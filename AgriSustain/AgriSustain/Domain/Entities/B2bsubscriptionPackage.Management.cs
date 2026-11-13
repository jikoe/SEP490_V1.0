using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AgriSustain.Domain.Entities;

/// <summary>UC-ADM-09 rules of a subscription tier (BR-ST-01 to BR-ST-04).</summary>
public partial class B2bsubscriptionPackage
{
    /// <summary>
    /// BR-ST-01 maximum name length.
    /// </summary>
    public const int NameMaxLength = 100;
    /// <summary>
    /// Maximum description length.
    /// </summary>
    public const int DescriptionMaxLength = 500;

    /// <summary>
    /// BR-ST-03: values are copied into new subscriptions; current ones keep their snapshot.
    /// Trims the name and stores an empty description as null.
    /// </summary>
    /// <param name="name">Tier name.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="durationDays">Duration in days (1–3650).</param>
    /// <param name="price">Price in whole VND.</param>
    /// <param name="directContact">Unlocks direct contact with Farmers.</param>
    /// <param name="contractTemplate">Allows downloading the contract template.</param>
    /// <param name="transactionLimit">Transaction limit, or null for unlimited.</param>
    /// <param name="isActive">Whether the tier can be bought.</param>
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
    /// <returns>16 hex characters.</returns>
    public string Version() => VersionOf(PackageName, PackageDescription, DurationDays, Price,
        UnlocksFarmerDirectContact, CanDownloadContractTemplate, TransactionLimit, IsActive);

    /// <summary>
    /// First 16 hex characters of the SHA-256 of all displayed values (price with 2 decimals, invariant culture).
    /// </summary>
    /// <param name="name">Name.</param>
    /// <param name="description">Description.</param>
    /// <param name="durationDays">Duration.</param>
    /// <param name="price">Price.</param>
    /// <param name="directContact">Direct contact flag.</param>
    /// <param name="contractTemplate">Contract template flag.</param>
    /// <param name="transactionLimit">Transaction limit.</param>
    /// <param name="isActive">Active flag.</param>
    /// <returns>Version string.</returns>
    public static string VersionOf(string name, string? description, int durationDays, decimal price,
        bool directContact, bool contractTemplate, int? transactionLimit, bool isActive)
    {
        string text = string.Join('\u001f', name, description, durationDays,
            price.ToString("0.00", CultureInfo.InvariantCulture), directContact, contractTemplate,
            transactionLimit, isActive);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }
}
