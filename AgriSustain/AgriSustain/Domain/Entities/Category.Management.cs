namespace AgriSustain.Domain.Entities;

public partial class Category
{
    /// <summary>Categories are flat: hierarchy and display priority are no longer managed.</summary>
    public void UpdateDetails(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150)
        {
            throw new ArgumentException("Invalid category details.");
        }
        CategoryName = name.Trim();
        ParentCategoryId = null;
        Description = description;
    }

    public void SetAvailability(bool isActive)
    {
        IsActive = isActive;
    }
}
