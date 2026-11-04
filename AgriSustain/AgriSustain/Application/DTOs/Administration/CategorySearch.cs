namespace AgriSustain.Application.DTOs.Administration;

/// <summary>
/// Status is Active, Inactive or Archived (BR-CAT-01); empty lists every category, archived ones
/// included. IsActive is the older filter and applies to non-archived categories only.
/// </summary>
public sealed record CategorySearch(string? Keyword = null, bool? IsActive = null, int Page = 1,
    int PageSize = 10, string? Status = null);
