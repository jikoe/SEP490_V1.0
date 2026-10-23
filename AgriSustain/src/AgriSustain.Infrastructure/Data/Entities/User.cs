using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

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

    public bool IsEmailVerified { get; set; }

    public DateTime CreatedAt { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTime? LockedUntil { get; set; }

    public virtual ICollection<AuditLog> AuditLog { get; set; } = new List<AuditLog>();

    public virtual Cart? Cart { get; set; }

    public virtual ICollection<ContractSignature> ContractSignature { get; set; } = new List<ContractSignature>();

    public virtual ICollection<ContractTerminationRequest> ContractTerminationRequest { get; set; } = new List<ContractTerminationRequest>();

    public virtual ICollection<ContractVersion> ContractVersion { get; set; } = new List<ContractVersion>();

    public virtual ICollection<CultivationLog> CultivationLog { get; set; } = new List<CultivationLog>();

    public virtual ICollection<DeliveryAddress> DeliveryAddress { get; set; } = new List<DeliveryAddress>();

    public virtual ICollection<DisputeEvidence> DisputeEvidence { get; set; } = new List<DisputeEvidence>();

    public virtual ICollection<DisputeTicket> DisputeTicketAssignedStaffUser { get; set; } = new List<DisputeTicket>();

    public virtual ICollection<DisputeTicket> DisputeTicketSubmittedByUser { get; set; } = new List<DisputeTicket>();

    public virtual ICollection<FarmCertificate> FarmCertificate { get; set; } = new List<FarmCertificate>();

    public virtual ICollection<FarmerPayout> FarmerPayout { get; set; } = new List<FarmerPayout>();

    public virtual FarmerProfile? FarmerProfile { get; set; }

    public virtual ICollection<InventoryMovement> InventoryMovement { get; set; } = new List<InventoryMovement>();

    public virtual ICollection<Notification> Notification { get; set; } = new List<Notification>();

    public virtual ICollection<Order> Order { get; set; } = new List<Order>();

    public virtual ICollection<OrderStatusHistory> OrderStatusHistory { get; set; } = new List<OrderStatusHistory>();

    public virtual ICollection<ProductReview> ProductReview { get; set; } = new List<ProductReview>();

    public virtual ICollection<RefundTransaction> RefundTransaction { get; set; } = new List<RefundTransaction>();

    public virtual ICollection<RetailCheckoutQuote> RetailCheckoutQuote { get; set; } = new List<RetailCheckoutQuote>();

    public virtual WholesaleBuyerProfile? WholesaleBuyerProfile { get; set; }
}
