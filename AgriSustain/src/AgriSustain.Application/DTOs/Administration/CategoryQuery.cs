namespace AgriSustain.Application.DTOs.Administration;

public sealed record CategoryQuery(
    string? Keyword = null, bool? IsActive = null, long? ParentId = null,
    bool RootOnly = false, int Page = 1, int PageSize = 10, long? Id = null);
