namespace AgriSustain.Application.DTOs.Administration;

public sealed record CategoryDto(long Id, string Name, long? ParentId, string? ParentName,
    string? Description, int Priority, bool IsActive, int ProductCount, bool IsArchived,
    string? ImageBase64, string? ImageContentType);
