namespace AgriSustain.Domain.Entities;

public partial class Category
{
    public void UpdateDetails(string name, long? parentId, string? description, int priority)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150 || priority < 0)
        {
            throw new ArgumentException("Invalid category details.");
        }
        CategoryName = name.Trim();
        ParentCategoryId = parentId;
        Description = description;
        Priority = priority;
    }

    public void SetAvailability(bool isActive)
    {
        IsActive = isActive;
    }
}
