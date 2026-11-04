namespace AgriSustain.Domain.Entities;

public partial class Product
{
    public void ValidatePricing() {
        if (BaseRetailPrice <= 0 || WholesalePrice.HasValue != WholesaleMinQuantity.HasValue
            || WholesalePrice is <= 0 || WholesalePrice > BaseRetailPrice || WholesaleMinQuantity is <= 1)
            throw new InvalidOperationException("Invalid product pricing.");
    }
    public void UpdatePrices(decimal retailPrice, decimal? minimumQuantity, decimal? wholesalePrice) {
        if (retailPrice <= 0 || wholesalePrice.HasValue != minimumQuantity.HasValue
            || wholesalePrice is <= 0 || wholesalePrice > retailPrice || minimumQuantity is <= 1)
            throw new InvalidOperationException("Invalid product pricing.");
        BaseRetailPrice = retailPrice;
        WholesaleMinQuantity = minimumQuantity;
        WholesalePrice = wholesalePrice;
    }
    public void SubmitForApproval()
    {
        if (BaseRetailPrice <= 0 || ProductImages.Count == 0
            || ProductImages.Count(image => image.IsPrimary) != 1)
        {
            throw new InvalidOperationException("Invalid listing invariant.");
        }

        ValidatePricing();
        ApprovalStatus = "Pending";
        IsPublished = false;
    }

    public void SetVisibility(bool published)
    {
        if (published && ApprovalStatus != "Approved")
        {
            throw new InvalidOperationException("Only approved products can be published.");
        }

        IsPublished = published;
    }
}

