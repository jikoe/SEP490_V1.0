namespace AgriSustain.Application.DTOs.Administration;

/// <summary>
/// ProductCount counts products of every status; ActiveProductCount counts published ones, which
/// MSG-CAT-03 and MSG-CAT-05 report. Version is sent back with a save or status change.
/// </summary>
public sealed record CategoryDto(long Id, string Name, string? Description, bool IsActive,
    int ProductCount, bool IsArchived, int ActiveProductCount = 0, string Version = "");

/// <summary>Outcome is activated, deactivated, deleted or archived; Message is shown as is.</summary>
public sealed record CategoryChangeResultDto(string Outcome, string Message);
