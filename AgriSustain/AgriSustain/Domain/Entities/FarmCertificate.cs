using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class FarmCertificate
{
    public long FarmCertificateId { get; set; }

    public long FarmerProfileId { get; set; }

    public long? ReviewedByUserId { get; set; }

    public string CertificateType { get; set; } = null!;

    public string CertificateNumber { get; set; } = null!;

    public string IssuedBy { get; set; } = null!;

    public DateOnly IssueDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public string DocumentUrl { get; set; } = null!;

    public string VerificationStatus { get; set; } = null!;

    public string? RevocationReason { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public virtual FarmerProfile FarmerProfile { get; set; } = null!;

    public virtual User? ReviewedByUser { get; set; }
}
