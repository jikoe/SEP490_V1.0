namespace AgriSustain.Application.DTOs.Products;

public sealed record ProductOptions(bool CanManage, string VerificationStatus,
    IReadOnlyList<CategoryOption> Categories)
{
    public IReadOnlyList<ProductCertificateOption> Certificates { get; init; } = [];
}

public sealed record ProductCertificateOption(long FarmCertificateId, string CertificateType,
    string CertificateNumber, string IssuedBy, DateOnly IssueDate, DateOnly? ExpiryDate,
    string VerificationStatus, bool IsEligible);
