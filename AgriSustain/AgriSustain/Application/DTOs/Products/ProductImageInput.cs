namespace AgriSustain.Application.DTOs.Products;

public sealed record ProductImageInput(string ImageUrl, bool IsPrimary, string? AltText);

