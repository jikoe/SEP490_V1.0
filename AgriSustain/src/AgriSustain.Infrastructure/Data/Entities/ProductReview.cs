using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class ProductReview
{
    public long ProductReviewId { get; set; }

    public long OrderItemId { get; set; }

    public long UserId { get; set; }

    public byte RatingStars { get; set; }

    public string? Comment { get; set; }

    public DateTime ReviewDate { get; set; }

    public bool IsPurchasedVerified { get; set; }

    public string ModerationStatus { get; set; } = null!;

    public string? ModerationReason { get; set; }

    public virtual OrderItem OrderItem { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
