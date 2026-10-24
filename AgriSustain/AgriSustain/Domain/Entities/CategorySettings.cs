namespace AgriSustain.Domain.Entities;

/// <summary>Optional category media and archive state without changing legacy columns.</summary>
public sealed class CategorySettings
{
    public long CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public bool IsArchived { get; private set; }

    public byte[]? ImageBytes { get; private set; }

    public string? ImageContentType { get; private set; }

    public void Archive()
    {
        IsArchived = true;
    }

    public void SetImage(byte[]? bytes, string? contentType)
    {
        ImageBytes = bytes;
        ImageContentType = contentType;
    }
}
