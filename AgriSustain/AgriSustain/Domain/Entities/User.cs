using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class User
{
    public long UserId { get; set; }

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string? AvatarUrl { get; set; }

    public string Role { get; set; } = null!;

    public string Status { get; set; } = null!;

    /// <summary>'Male' | 'Female' | 'Other' (UC-AUTH-03).</summary>
    public string? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public bool IsEmailVerified { get; set; }

    public DateTime CreatedAt { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTime? LockedUntil { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual Cart? Cart { get; set; }

    public virtual ICollection<ContractSignature> ContractSignatures { get; set; } = new List<ContractSignature>();

    public virtual ICollection<ContractTerminationRequest> ContractTerminationRequests { get; set; } = new List<ContractTerminationRequest>();

    public virtual ICollection<ContractVersion> ContractVersions { get; set; } = new List<ContractVersion>();

    public virtual ICollection<CultivationLog> CultivationLogs { get; set; } = new List<CultivationLog>();

    public virtual DeliveryAddress? DeliveryAddress { get; set; }

    public virtual ICollection<DisputeEvidenceRequest> DisputeEvidenceRequestRequestedByUsers { get; set; } = new List<DisputeEvidenceRequest>();

    public virtual ICollection<DisputeEvidenceRequest> DisputeEvidenceRequestRequestedPartyUsers { get; set; } = new List<DisputeEvidenceRequest>();

    public virtual ICollection<DisputeEvidence> DisputeEvidences { get; set; } = new List<DisputeEvidence>();

    public virtual ICollection<DisputeTicket> DisputeTicketAssignedStaffUsers { get; set; } = new List<DisputeTicket>();

    public virtual ICollection<DisputeTicket> DisputeTicketSubmittedByUsers { get; set; } = new List<DisputeTicket>();

    public virtual ICollection<FarmCertificate> FarmCertificates { get; set; } = new List<FarmCertificate>();

    public virtual ICollection<FarmerPayout> FarmerPayouts { get; set; } = new List<FarmerPayout>();

    public virtual FarmerProfile? FarmerProfile { get; set; }

    public virtual ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<OrderStatusHistory> OrderStatusHistories { get; set; } = new List<OrderStatusHistory>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();

    public virtual ICollection<RefundTransaction> RefundTransactions { get; set; } = new List<RefundTransaction>();

    public virtual ICollection<RetailCheckoutQuote> RetailCheckoutQuotes { get; set; } = new List<RetailCheckoutQuote>();

    public virtual ICollection<RewardPointEntry> RewardPointEntries { get; set; } = new List<RewardPointEntry>();

    public virtual WholesaleBuyerProfile? WholesaleBuyerProfile { get; set; }
}
