namespace AgriSustain.Domain.Entities;

/// <summary>Category archive state without changing legacy columns.</summary>
public sealed class CategorySettings
{
    public long CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public bool IsArchived { get; private set; }

    /// <summary>
    /// Marks the category archived for good; it stays visible as read-only history.
    /// </summary>
    public void Archive()
    {
        IsArchived = true;
    }
}
