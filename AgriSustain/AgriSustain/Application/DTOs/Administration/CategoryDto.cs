namespace AgriSustain.Application.DTOs.Administration;

public sealed record CategoryDto(long Id, string Name, string? Description, bool IsActive,
    int ProductCount, bool IsArchived);