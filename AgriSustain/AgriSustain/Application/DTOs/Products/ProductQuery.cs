namespace AgriSustain.Application.DTOs.Products;

public sealed record ProductQuery(string? Keyword = null, long? CategoryId = null,
    string? ApprovalStatus = null, bool? IsPublished = null, int Page = 1, int PageSize = 10);

