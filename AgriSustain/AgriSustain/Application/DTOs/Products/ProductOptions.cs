namespace AgriSustain.Application.DTOs.Products;

public sealed record ProductOptions(bool CanManage, string VerificationStatus,
    IReadOnlyList<CategoryOption> Categories);

