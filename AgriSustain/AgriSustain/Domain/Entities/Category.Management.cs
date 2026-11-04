using System.Security.Cryptography;
using System.Text;

namespace AgriSustain.Domain.Entities;

public partial class Category
{
    /// <summary>BR-CAT-02 maximum name length.</summary>
    public const int NameMaxLength = 100;

    /// <summary>Categories are flat: hierarchy and display priority are no longer managed.</summary>
    public void UpdateDetails(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > NameMaxLength)
        {
            throw new ArgumentException("Invalid category details.");
        }
        CategoryName = name.Trim();
        ParentCategoryId = null;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void SetAvailability(bool isActive)
    {
        IsActive = isActive;
    }

    /// <summary>
    /// Version of the values shown to the Administrator. A save or status change sent with an
    /// older version was made against a record someone else has since changed (MSG122).
    /// </summary>
    public static string VersionOf(string name, string? description, bool isActive, bool isArchived)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{name}\u001f{description}\u001f{isActive}\u001f{isArchived}"));
        return Convert.ToHexString(hash)[..16];
    }
}
