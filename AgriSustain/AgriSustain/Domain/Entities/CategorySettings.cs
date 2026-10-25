namespace AgriSustain.Domain.Entities;

/// <summary>Category archive state without changing legacy columns.</summary>
public sealed class CategorySettings
{
    public long CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public bool IsArchived { get; private set; }

    public void Archive()
    {
        IsArchived = true;
    }
}
