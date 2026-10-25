namespace AgriSustain.Application.DTOs.Administration;

public sealed record CategoryRequest(string Name, string? Description, bool IsActive, bool Confirmed);