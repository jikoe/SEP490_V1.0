namespace AgriSustain.Domain.Entities;

public partial class Product
{
    public void SubmitForApproval()
    {
        if (BaseRetailPrice <= 0 || ProductImages.Count == 0
            || ProductImages.Count(image => image.IsPrimary) != 1)
        {
            throw new InvalidOperationException("Invalid listing invariant.");
        }

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

