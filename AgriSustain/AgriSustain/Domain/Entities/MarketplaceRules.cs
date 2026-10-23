using System.Linq.Expressions;

namespace AgriSustain.Domain.Entities;

public static class MarketplaceRules
{
    public static readonly Expression<Func<Product, bool>> PublicProduct = product =>
        product.ApprovalStatus == "Approved"
        && product.IsPublished
        && product.Category.IsActive
        && product.FarmerProfile.VerificationStatus == "Verified"
        && product.FarmerProfile.User.Status == "Active";

    public static Expression<Func<HarvestBatch, bool>> AvailableBatch(DateOnly today) => batch =>
        batch.Status == "Available"
        && batch.AvailableQuantity > 0
        && (batch.ExpirationDate == null || batch.ExpirationDate >= today);

    public static Expression<Func<FarmCertificate, bool>> ValidCertificate(DateOnly today) =>
        certificate => certificate.VerificationStatus == "Verified"
            && certificate.IssueDate <= today
            && (certificate.ExpiryDate == null || certificate.ExpiryDate >= today);

    public static readonly Expression<Func<ProductReview, bool>> PublicReview = review =>
        review.ModerationStatus == "Approved" && review.IsPurchasedVerified;
}
