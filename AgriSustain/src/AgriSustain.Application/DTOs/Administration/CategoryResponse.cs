namespace AgriSustain.Application.DTOs.Administration;

public sealed record CategoryResponse(
    long Id, string Name, long? ParentId, string? ParentName, string? Description,
    int Priority, bool IsActive, int ProductCount);
