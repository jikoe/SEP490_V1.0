namespace AgriSustain.Application.DTOs.Administration;

public sealed record UserSearch(string? Keyword = null, string? Role = null, string? Status = null,
    int Page = 1, int PageSize = 10);
