using AgriSustain.Domain.Exceptions;

namespace AgriSustain.Domain.Entities.Catalog;

public sealed class Category
{
    public long Id
    {
        get; init;
    }
    public string Name { get; private set; } = string.Empty;
    public long? ParentId
    {
        get; private set;
    }
    public string? Description
    {
        get; private set;
    }
    public int Priority
    {
        get; private set;
    }
    public bool IsActive
    {
        get; private set;
    }

    /// <summary>
    /// Applies the approved name/description/priority invariants and replaces persisted
    /// classification fields. Does not add the declined duplicate-name/cycle/version rules.
    /// </summary>
    public void Update(
        string name, long? parentId, string? description, int priority, bool isActive)
    {
        name = name.Trim();
        if (name.Length is < 2 or > 100 || priority < 0 || description?.Length > 500)
        {
            throw new BusinessRuleException("Thông tin danh mục không hợp lệ.");
        }

        Name = name;
        ParentId = parentId;
        Description = description;
        Priority = priority;
        IsActive = isActive;
    }

    /// <summary>
    /// Toggles the existing IsActive flag for hiding/locking without deleting referenced products
    /// or introducing an archive state.
    /// </summary>
    public void SetActive(bool active) => IsActive = active;

    /// <summary>
    /// Returns a detached entity copy for the demo transaction snapshot; scalar field values
    /// remain identical.
    /// </summary>
    public Category Copy() => (Category)MemberwiseClone();
}
