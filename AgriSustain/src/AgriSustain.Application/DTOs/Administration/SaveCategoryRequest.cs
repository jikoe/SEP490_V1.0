namespace AgriSustain.Application.DTOs.Administration;

public sealed record SaveCategoryRequest(
    string Name, long? ParentId, string? Description, int Priority = 0,
    bool IsActive = true, bool Confirmed = false);
