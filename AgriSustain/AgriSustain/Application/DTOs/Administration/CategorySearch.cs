namespace AgriSustain.Application.DTOs.Administration;

public sealed record CategorySearch(string? Keyword = null, bool? IsActive = null, int Page = 1,
    int PageSize = 10);