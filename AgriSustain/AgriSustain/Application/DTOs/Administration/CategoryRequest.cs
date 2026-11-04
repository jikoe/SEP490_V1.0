namespace AgriSustain.Application.DTOs.Administration;

/// <summary>Version is the CategoryDto.Version the form was opened with; required when editing.</summary>
public sealed record CategoryRequest(string Name, string? Description, bool IsActive, bool Confirmed,
    string? Version = null);
