namespace AgriSustain.Application.DTOs.Administration;

public sealed record CategoryRequest(string Name, long? ParentId, string? Description,
    int Priority, bool IsActive, bool Confirmed, string? ImageBase64 = null,
    string? ImageContentType = null, bool RemoveImage = false);
