using System.Security.Cryptography;
using System.Text;

namespace AgriSustain.Domain.Entities;

public partial class Category
{
    /// <summary>BR-CAT-02 maximum name length.</summary>
    public const int NameMaxLength = 100;

    /// <summary>
    /// Categories are flat: hierarchy and display priority are no longer managed.
    /// Sets the trimmed name, clears the parent and stores an empty description as null.
    /// </summary>
    /// <param name="name">Name, 1–100 characters after trimming.</param>
    /// <param name="description">Optional description.</param>
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

    /// <summary>
    /// Active categories are offered to Farmers and shown on the marketplace.
    /// </summary>
    /// <param name="isActive">New flag.</param>
    public void SetAvailability(bool isActive)
    {
        IsActive = isActive;
    }

    /// <summary>
    /// Version of the values shown to the Administrator. A save or status change sent with an
    /// older version was made against a record someone else has since changed (MSG122).
    /// </summary>
    /// <param name="name">Name.</param>
    /// <param name="description">Description.</param>
    /// <param name="isActive">Active flag.</param>
    /// <param name="isArchived">Archived flag.</param>
    /// <returns>First 16 hex characters of the SHA-256 of the values.</returns>
    public static string VersionOf(string name, string? description, bool isActive, bool isArchived)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{name}\u001f{description}\u001f{isActive}\u001f{isArchived}"));
        return Convert.ToHexString(hash)[..16];
    }
}
