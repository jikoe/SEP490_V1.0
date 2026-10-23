using System;
using System.Collections.Generic;
using AgriSustain.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Data.Contexts;

/// <summary>Maps the existing AgriSustain SQL schema; regenerated with EF Database First.</summary>
public partial class AgriSustainDbContext : DbContext
{
    /// <summary>Receives scoped SQL Server options from dependency injection.</summary>
    public AgriSustainDbContext(DbContextOptions<AgriSustainDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditLog> AuditLog { get; set; }

    public virtual DbSet<B2BContract> B2BContract { get; set; }

    public virtual DbSet<B2BSubscription> B2BSubscription { get; set; }

    public virtual DbSet<B2BSubscriptionPackage> B2BSubscriptionPackage { get; set; }

    public virtual DbSet<Cart> Cart { get; set; }

    public virtual DbSet<CartItem> CartItem { get; set; }

    public virtual DbSet<Category> Category { get; set; }

    public virtual DbSet<CommissionPolicy> CommissionPolicy { get; set; }

    public virtual DbSet<ContractLine> ContractLine { get; set; }

    public virtual DbSet<ContractMilestone> ContractMilestone { get; set; }

    public virtual DbSet<ContractSignature> ContractSignature { get; set; }

    public virtual DbSet<ContractTerminationRequest> ContractTerminationRequest { get; set; }

    public virtual DbSet<ContractVersion> ContractVersion { get; set; }

    public virtual DbSet<CropSeason> CropSeason { get; set; }

    public virtual DbSet<CultivationLog> CultivationLog { get; set; }

    public virtual DbSet<DeliveryAddress> DeliveryAddress { get; set; }

    public virtual DbSet<DisputeEvidence> DisputeEvidence { get; set; }

    public virtual DbSet<DisputeTicket> DisputeTicket { get; set; }

    public virtual DbSet<FarmCertificate> FarmCertificate { get; set; }

    public virtual DbSet<FarmerPayout> FarmerPayout { get; set; }

    public virtual DbSet<FarmerProfile> FarmerProfile { get; set; }

    public virtual DbSet<HarvestBatch> HarvestBatch { get; set; }

    public virtual DbSet<InventoryMovement> InventoryMovement { get; set; }

    public virtual DbSet<Invoice> Invoice { get; set; }

    public virtual DbSet<InvoiceLine> InvoiceLine { get; set; }

    public virtual DbSet<Notification> Notification { get; set; }

    public virtual DbSet<Order> Order { get; set; }

    public virtual DbSet<OrderItem> OrderItem { get; set; }

    public virtual DbSet<OrderItemBatchAllocation> OrderItemBatchAllocation { get; set; }

    public virtual DbSet<OrderStatusHistory> OrderStatusHistory { get; set; }

    public virtual DbSet<PaymentTransaction> PaymentTransaction { get; set; }

    public virtual DbSet<PayoutItem> PayoutItem { get; set; }

    public virtual DbSet<PreOrderCampaign> PreOrderCampaign { get; set; }

    public virtual DbSet<Product> Product { get; set; }

    public virtual DbSet<ProductImage> ProductImage { get; set; }

    public virtual DbSet<ProductReview> ProductReview { get; set; }

    public virtual DbSet<RefundTransaction> RefundTransaction { get; set; }

    public virtual DbSet<RegionalPriceBenchmark> RegionalPriceBenchmark { get; set; }

    public virtual DbSet<RetailCheckoutQuote> RetailCheckoutQuote { get; set; }

    public virtual DbSet<SellerOrder> SellerOrder { get; set; }

    public virtual DbSet<Shipment> Shipment { get; set; }

    public virtual DbSet<ShipmentItem> ShipmentItem { get; set; }

    public virtual DbSet<TraceabilityQRCode> TraceabilityQRCode { get; set; }

    public virtual DbSet<User> User { get; set; }

    public virtual DbSet<Voucher> Voucher { get; set; }

    public virtual DbSet<VoucherRedemption> VoucherRedemption { get; set; }

    public virtual DbSet<WholesaleBuyerProfile> WholesaleBuyerProfile { get; set; }

    /// <summary>Configures the database's existing keys, defaults, indexes and relationships.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("PK__AuditLog__EB5F6CBD8ECA754D");

            entity.Property(e => e.Action)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.EntityType)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.TraceId).HasMaxLength(64);

            entity.HasOne(d => d.PerformedByUser).WithMany(p => p.AuditLog)
                .HasForeignKey(d => d.PerformedByUserId)
                .HasConstraintName("FK_AuditLog_PerformedByUser");
        });

        modelBuilder.Entity<B2BContract>(entity =>
        {
            entity.HasKey(e => e.B2BContractId).HasName("PK__B2BContr__59105F1E4C6BE8A7");

            entity.HasIndex(e => e.ContractNumber, "UQ__B2BContr__C51D43DA0289BDE2").IsUnique();

            entity.Property(e => e.ContractNumber)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.CurrentVersionNo).HasDefaultValue(1);
            entity.Property(e => e.DepositAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DigitalSignatureStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Unsigned");
            entity.Property(e => e.PaymentTerms).HasMaxLength(1000);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Draft");

            entity.HasOne(d => d.FarmerProfile).WithMany(p => p.B2BContract)
                .HasForeignKey(d => d.FarmerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_B2BContract_FarmerProfile");

            entity.HasOne(d => d.WholesaleBuyerProfile).WithMany(p => p.B2BContract)
                .HasForeignKey(d => d.WholesaleBuyerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_B2BContract_WholesaleBuyer");
        });

        modelBuilder.Entity<B2BSubscription>(entity =>
        {
            entity.HasKey(e => e.B2BSubscriptionId).HasName("PK__B2BSubsc__EE8E09CCECFF9185");

            entity.Property(e => e.PriceSnapshot).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SubscriptionStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Active");

            entity.HasOne(d => d.Package).WithMany(p => p.B2BSubscription)
                .HasForeignKey(d => d.PackageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_B2BSubscription_Package");

            entity.HasOne(d => d.WholesaleBuyerProfile).WithMany(p => p.B2BSubscription)
                .HasForeignKey(d => d.WholesaleBuyerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_B2BSubscription_WholesaleBuyer");
        });

        modelBuilder.Entity<B2BSubscriptionPackage>(entity =>
        {
            entity.HasKey(e => e.PackageId).HasName("PK__B2BSubsc__322035CCF7D51AFD");

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PackageDescription).HasMaxLength(1000);
            entity.Property(e => e.PackageName).HasMaxLength(150);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.CartId).HasName("PK__Cart__51BCD7B771F055E5");

            entity.HasIndex(e => e.UserId, "UQ__Cart__1788CC4DBEC3C7DD").IsUnique();

            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.User).WithOne(p => p.Cart)
                .HasForeignKey<Cart>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Cart_User");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.CartItemId).HasName("PK__CartItem__488B0B0A4C960CA5");

            entity.Property(e => e.AddedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsSelected).HasDefaultValue(true);
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");

            entity.HasOne(d => d.Cart).WithMany(p => p.CartItem)
                .HasForeignKey(d => d.CartId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CartItem_Cart");

            entity.HasOne(d => d.PreOrderCampaign).WithMany(p => p.CartItem)
                .HasForeignKey(d => d.PreOrderCampaignId)
                .HasConstraintName("FK_CartItem_PreOrderCampaign");

            entity.HasOne(d => d.Product).WithMany(p => p.CartItem)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CartItem_Product");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__Category__19093A0B12480F14");

            entity.Property(e => e.CategoryName).HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.ParentCategory).WithMany(p => p.InverseParentCategory)
                .HasForeignKey(d => d.ParentCategoryId)
                .HasConstraintName("FK_Category_ParentCategory");
        });

        modelBuilder.Entity<CommissionPolicy>(entity =>
        {
            entity.HasKey(e => e.CommissionPolicyId).HasName("PK__Commissi__ACB43F57E9190325");

            entity.Property(e => e.ApplicableOrderType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CommissionRate).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Note).HasMaxLength(500);

            entity.HasOne(d => d.Category).WithMany(p => p.CommissionPolicy)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CommissionPolicy_Category");
        });

        modelBuilder.Entity<ContractLine>(entity =>
        {
            entity.HasKey(e => e.ContractLineId).HasName("PK__Contract__905F6E87FFC2A6F8");

            entity.Property(e => e.AgreedPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CommittedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.MaxPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MinPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PriceFormula).HasMaxLength(255);
            entity.Property(e => e.PriceType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.QualitySpecification).HasMaxLength(500);
            entity.Property(e => e.UnitSnapshot).HasMaxLength(50);

            entity.HasOne(d => d.B2BContract).WithMany(p => p.ContractLine)
                .HasForeignKey(d => d.B2BContractId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContractLine_B2BContract");

            entity.HasOne(d => d.CropSeason).WithMany(p => p.ContractLine)
                .HasForeignKey(d => d.CropSeasonId)
                .HasConstraintName("FK_ContractLine_CropSeason");

            entity.HasOne(d => d.Product).WithMany(p => p.ContractLine)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContractLine_Product");
        });

        modelBuilder.Entity<ContractMilestone>(entity =>
        {
            entity.HasKey(e => e.ContractMilestoneId).HasName("PK__Contract__9CD091DC2DD058E3");

            entity.Property(e => e.AcceptedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DeliveredQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.DeliveryQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.FulfillmentStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending");
            entity.Property(e => e.MilestoneTitle).HasMaxLength(200);
            entity.Property(e => e.MilestoneType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PaidAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Unpaid");

            entity.HasOne(d => d.B2BContract).WithMany(p => p.ContractMilestone)
                .HasForeignKey(d => d.B2BContractId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContractMilestone_B2BContract");

            entity.HasOne(d => d.ContractLine).WithMany(p => p.ContractMilestone)
                .HasForeignKey(d => d.ContractLineId)
                .HasConstraintName("FK_ContractMilestone_ContractLine");
        });

        modelBuilder.Entity<ContractSignature>(entity =>
        {
            entity.HasKey(e => e.ContractSignatureId).HasName("PK__Contract__6A8CC2209CA1948A");

            entity.Property(e => e.ConfirmationMethod)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ConfirmationStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending");
            entity.Property(e => e.PartyRole)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.ContractVersion).WithMany(p => p.ContractSignature)
                .HasForeignKey(d => d.ContractVersionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContractSignature_ContractVersion");

            entity.HasOne(d => d.SignedByUser).WithMany(p => p.ContractSignature)
                .HasForeignKey(d => d.SignedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContractSignature_SignedByUser");
        });

        modelBuilder.Entity<ContractTerminationRequest>(entity =>
        {
            entity.HasKey(e => e.ContractTerminationRequestId).HasName("PK__Contract__B98B7390E6BEF34C");

            entity.Property(e => e.EvidenceUrl).HasMaxLength(500);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.RequestStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending");
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.SettlementAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.B2BContract).WithMany(p => p.ContractTerminationRequest)
                .HasForeignKey(d => d.B2BContractId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TerminationRequest_B2BContract");

            entity.HasOne(d => d.RequestedByUser).WithMany(p => p.ContractTerminationRequest)
                .HasForeignKey(d => d.RequestedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TerminationRequest_RequestedByUser");
        });

        modelBuilder.Entity<ContractVersion>(entity =>
        {
            entity.HasKey(e => e.ContractVersionId).HasName("PK__Contract__54F29433B70212B1");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.DocumentHash)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.FeedbackReason).HasMaxLength(500);
            entity.Property(e => e.VersionStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.B2BContract).WithMany(p => p.ContractVersion)
                .HasForeignKey(d => d.B2BContractId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContractVersion_B2BContract");

            entity.HasOne(d => d.ProposedByUser).WithMany(p => p.ContractVersion)
                .HasForeignKey(d => d.ProposedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContractVersion_ProposedByUser");
        });

        modelBuilder.Entity<CropSeason>(entity =>
        {
            entity.HasKey(e => e.CropSeasonId).HasName("PK__CropSeas__675AF5006CA4707F");

            entity.Property(e => e.ActualYield).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.CropVariety).HasMaxLength(150);
            entity.Property(e => e.CultivatedArea).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.EstimatedYield).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.SeasonName).HasMaxLength(200);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Planning");

            entity.HasOne(d => d.FarmerProfile).WithMany(p => p.CropSeason)
                .HasForeignKey(d => d.FarmerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CropSeason_FarmerProfile");
        });

        modelBuilder.Entity<CultivationLog>(entity =>
        {
            entity.HasKey(e => e.CultivationLogId).HasName("PK__Cultivat__60C48ADFB39FC15B");

            entity.Property(e => e.ActionType).HasMaxLength(100);
            entity.Property(e => e.DetailDescription).HasMaxLength(1000);
            entity.Property(e => e.Dosage).HasMaxLength(150);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.InputMaterial).HasMaxLength(255);
            entity.Property(e => e.PerformedBy).HasMaxLength(150);
            entity.Property(e => e.RecordedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.CropSeason).WithMany(p => p.CultivationLog)
                .HasForeignKey(d => d.CropSeasonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CultivationLog_CropSeason");

            entity.HasOne(d => d.RecordedByUser).WithMany(p => p.CultivationLog)
                .HasForeignKey(d => d.RecordedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CultivationLog_RecordedByUser");
        });

        modelBuilder.Entity<DeliveryAddress>(entity =>
        {
            entity.HasKey(e => e.DeliveryAddressId).HasName("PK__Delivery__4EBD54FC663EE899");

            entity.Property(e => e.AddressCatalogVersion)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.AddressDetail).HasMaxLength(500);
            entity.Property(e => e.District).HasMaxLength(100);
            entity.Property(e => e.ProvinceCity).HasMaxLength(100);
            entity.Property(e => e.ProvinceCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.RecipientName).HasMaxLength(150);
            entity.Property(e => e.RecipientPhone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Active");
            entity.Property(e => e.Ward).HasMaxLength(100);
            entity.Property(e => e.WardCode)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithMany(p => p.DeliveryAddress)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DeliveryAddress_User");
        });

        modelBuilder.Entity<DisputeEvidence>(entity =>
        {
            entity.HasKey(e => e.DisputeEvidenceId).HasName("PK__DisputeE__B2461231F5FE86D8");

            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.SubmittedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.DisputeTicket).WithMany(p => p.DisputeEvidence)
                .HasForeignKey(d => d.DisputeTicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DisputeEvidence_DisputeTicket");

            entity.HasOne(d => d.SubmittedByUser).WithMany(p => p.DisputeEvidence)
                .HasForeignKey(d => d.SubmittedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DisputeEvidence_SubmittedByUser");
        });

        modelBuilder.Entity<DisputeTicket>(entity =>
        {
            entity.HasKey(e => e.DisputeTicketId).HasName("PK__DisputeT__3C3F9CFE11E6AB28");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.DisputeReason).HasMaxLength(255);
            entity.Property(e => e.FaultParty)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RefundAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ResolutionNotes).HasMaxLength(1000);
            entity.Property(e => e.TicketStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Open");

            entity.HasOne(d => d.AssignedStaffUser).WithMany(p => p.DisputeTicketAssignedStaffUser)
                .HasForeignKey(d => d.AssignedStaffUserId)
                .HasConstraintName("FK_DisputeTicket_AssignedStaffUser");

            entity.HasOne(d => d.B2BContract).WithMany(p => p.DisputeTicket)
                .HasForeignKey(d => d.B2BContractId)
                .HasConstraintName("FK_DisputeTicket_B2BContract");

            entity.HasOne(d => d.SellerOrder).WithMany(p => p.DisputeTicket)
                .HasForeignKey(d => d.SellerOrderId)
                .HasConstraintName("FK_DisputeTicket_SellerOrder");

            entity.HasOne(d => d.SubmittedByUser).WithMany(p => p.DisputeTicketSubmittedByUser)
                .HasForeignKey(d => d.SubmittedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DisputeTicket_SubmittedByUser");
        });

        modelBuilder.Entity<FarmCertificate>(entity =>
        {
            entity.HasKey(e => e.FarmCertificateId).HasName("PK__FarmCert__2BDF00825A754964");

            entity.Property(e => e.CertificateNumber)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.CertificateType).HasMaxLength(100);
            entity.Property(e => e.DocumentUrl).HasMaxLength(500);
            entity.Property(e => e.IssuedBy).HasMaxLength(200);
            entity.Property(e => e.RevocationReason).HasMaxLength(500);
            entity.Property(e => e.VerificationStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.FarmerProfile).WithMany(p => p.FarmCertificate)
                .HasForeignKey(d => d.FarmerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FarmCertificate_FarmerProfile");

            entity.HasOne(d => d.ReviewedByUser).WithMany(p => p.FarmCertificate)
                .HasForeignKey(d => d.ReviewedByUserId)
                .HasConstraintName("FK_FarmCertificate_ReviewedByUser");
        });

        modelBuilder.Entity<FarmerPayout>(entity =>
        {
            entity.HasKey(e => e.FarmerPayoutId).HasName("PK__FarmerPa__80AAF9C30D10BBAB");

            entity.HasIndex(e => e.PayoutCode, "UQ__FarmerPa__3F2306E39741F000").IsUnique();

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.BankTransferRef)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.DeductedCommission).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.NetAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PayoutCode)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PayoutStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.ApprovedByUser).WithMany(p => p.FarmerPayout)
                .HasForeignKey(d => d.ApprovedByUserId)
                .HasConstraintName("FK_FarmerPayout_ApprovedByUser");

            entity.HasOne(d => d.FarmerProfile).WithMany(p => p.FarmerPayout)
                .HasForeignKey(d => d.FarmerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FarmerPayout_FarmerProfile");
        });

        modelBuilder.Entity<FarmerProfile>(entity =>
        {
            entity.HasKey(e => e.FarmerProfileId).HasName("PK__FarmerPr__688AB36D08140124");

            entity.HasIndex(e => e.UserId, "UQ__FarmerPr__1788CC4D330DB74C").IsUnique();

            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.BusinessLicenseNumber)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.CultivationZone).HasMaxLength(200);
            entity.Property(e => e.FarmName).HasMaxLength(200);
            entity.Property(e => e.GPSCoordinates)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ProductionScale).HasMaxLength(200);
            entity.Property(e => e.ProvinceCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.RejectReason).HasMaxLength(500);
            entity.Property(e => e.RepresentativeName).HasMaxLength(150);
            entity.Property(e => e.ReputationScore).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.VerificationStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.User).WithOne(p => p.FarmerProfile)
                .HasForeignKey<FarmerProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FarmerProfile_User");
        });

        modelBuilder.Entity<HarvestBatch>(entity =>
        {
            entity.HasKey(e => e.HarvestBatchId).HasName("PK__HarvestB__619519FB8DEEBD18");

            entity.HasIndex(e => new { e.ProductId, e.Status, e.ExpirationDate, e.HarvestBatchId }, "IX_HarvestBatch_Fefo");

            entity.HasIndex(e => e.BatchCode, "UQ__HarvestB__B22ADA8EDAEF1057").IsUnique();

            entity.Property(e => e.AvailableQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.BatchCode)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.HarvestYield).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.QualityNotes).HasMaxLength(500);
            entity.Property(e => e.ReservedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Available");

            entity.HasOne(d => d.CropSeason).WithMany(p => p.HarvestBatch)
                .HasForeignKey(d => d.CropSeasonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HarvestBatch_CropSeason");

            entity.HasOne(d => d.PreOrderCampaign).WithMany(p => p.HarvestBatch)
                .HasForeignKey(d => d.PreOrderCampaignId)
                .HasConstraintName("FK_HarvestBatch_PreOrderCampaign");

            entity.HasOne(d => d.Product).WithMany(p => p.HarvestBatch)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HarvestBatch_Product");
        });

        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.HasKey(e => e.InventoryMovementId).HasName("PK__Inventor__DB9462B68C93DEB0");

            entity.Property(e => e.MovementType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.QuantityChange).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Reason).HasMaxLength(500);

            entity.HasOne(d => d.HarvestBatch).WithMany(p => p.InventoryMovement)
                .HasForeignKey(d => d.HarvestBatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InventoryMovement_HarvestBatch");

            entity.HasOne(d => d.OrderItemBatchAllocation).WithMany(p => p.InventoryMovement)
                .HasForeignKey(d => d.OrderItemBatchAllocationId)
                .HasConstraintName("FK_InventoryMovement_BatchAllocation");

            entity.HasOne(d => d.RecordedByUser).WithMany(p => p.InventoryMovement)
                .HasForeignKey(d => d.RecordedByUserId)
                .HasConstraintName("FK_InventoryMovement_RecordedByUser");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId).HasName("PK__Invoice__D796AAB5DB6B9234");

            entity.HasIndex(e => e.InvoiceNumber, "UQ__Invoice__D776E98142E06912").IsUnique();

            entity.Property(e => e.InvoiceNumber)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.InvoiceStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Issued");
            entity.Property(e => e.IssuedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.ContractMilestone).WithMany(p => p.Invoice)
                .HasForeignKey(d => d.ContractMilestoneId)
                .HasConstraintName("FK_Invoice_ContractMilestone");

            entity.HasOne(d => d.SellerOrder).WithMany(p => p.Invoice)
                .HasForeignKey(d => d.SellerOrderId)
                .HasConstraintName("FK_Invoice_SellerOrder");
        });

        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            entity.HasKey(e => e.InvoiceLineId).HasName("PK__InvoiceL__0D760AD97F1AD7C2");

            entity.Property(e => e.LineAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.UnitPriceSnapshot).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceLine)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InvoiceLine_Invoice");

            entity.HasOne(d => d.OrderItem).WithMany(p => p.InvoiceLine)
                .HasForeignKey(d => d.OrderItemId)
                .HasConstraintName("FK_InvoiceLine_OrderItem");

            entity.HasOne(d => d.ShipmentItem).WithMany(p => p.InvoiceLine)
                .HasForeignKey(d => d.ShipmentItemId)
                .HasConstraintName("FK_InvoiceLine_ShipmentItem");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E1294ABA875");

            entity.Property(e => e.DeliveryChannel)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DeliveryStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Sent");
            entity.Property(e => e.NotificationType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RelatedRecordType)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.SentAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Title).HasMaxLength(255);

            entity.HasOne(d => d.User).WithMany(p => p.Notification)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notification_User");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Order__C3905BCF62DE406E");

            entity.HasIndex(e => e.OrderCode, "UQ__Order__999B5229FB4B9D32").IsUnique();

            entity.Property(e => e.AddressCatalogVersionSnapshot)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.AddressDetailSnapshot).HasMaxLength(255);
            entity.Property(e => e.DeliveryFee).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DeliveryMethod)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OrderCode)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.OrderDate).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.OrderNotes).HasMaxLength(300);
            entity.Property(e => e.OrderStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending");
            entity.Property(e => e.OrderType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PointDiscount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ProvinceCodeSnapshot)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ProvinceNameSnapshot).HasMaxLength(100);
            entity.Property(e => e.RecipientNameSnapshot).HasMaxLength(150);
            entity.Property(e => e.RecipientPhoneSnapshot)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ShippingAddress).HasMaxLength(500);
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.VoucherCodeSnapshot)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.VoucherDiscount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.WardCodeSnapshot)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.WardNameSnapshot).HasMaxLength(100);

            entity.HasOne(d => d.BuyerUser).WithMany(p => p.Order)
                .HasForeignKey(d => d.BuyerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Order_BuyerUser");

            entity.HasOne(d => d.RetailQuote).WithMany(p => p.Order)
                .HasForeignKey(d => d.RetailQuoteId)
                .HasConstraintName("FK_Order_RetailQuote");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.OrderItemId).HasName("PK__OrderIte__57ED06815FF28A0F");

            entity.Property(e => e.DiscountApplied).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ItemStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Normal");
            entity.Property(e => e.ProductNameSnapshot).HasMaxLength(255);
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UnitPriceSnapshot).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UnitSnapshot).HasMaxLength(50);

            entity.HasOne(d => d.ContractLine).WithMany(p => p.OrderItem)
                .HasForeignKey(d => d.ContractLineId)
                .HasConstraintName("FK_OrderItem_ContractLine");

            entity.HasOne(d => d.PreOrderCampaign).WithMany(p => p.OrderItem)
                .HasForeignKey(d => d.PreOrderCampaignId)
                .HasConstraintName("FK_OrderItem_PreOrderCampaign");

            entity.HasOne(d => d.Product).WithMany(p => p.OrderItem)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderItem_Product");

            entity.HasOne(d => d.SellerOrder).WithMany(p => p.OrderItem)
                .HasForeignKey(d => d.SellerOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderItem_SellerOrder");
        });

        modelBuilder.Entity<OrderItemBatchAllocation>(entity =>
        {
            entity.HasKey(e => e.OrderItemBatchAllocationId).HasName("PK__OrderIte__51B745BB031651E7");

            entity.Property(e => e.AllocatedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.AllocationSequence).HasDefaultValue(1);
            entity.Property(e => e.AllocationStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Reserved");
            entity.Property(e => e.ReservedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.HarvestBatch).WithMany(p => p.OrderItemBatchAllocation)
                .HasForeignKey(d => d.HarvestBatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BatchAllocation_HarvestBatch");

            entity.HasOne(d => d.OrderItem).WithMany(p => p.OrderItemBatchAllocation)
                .HasForeignKey(d => d.OrderItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BatchAllocation_OrderItem");
        });

        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.HasKey(e => e.OrderStatusHistoryId).HasName("PK__OrderSta__D16EDBC3E5EC205C");

            entity.Property(e => e.NewStatus)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.OldStatus)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Reason).HasMaxLength(500);

            entity.HasOne(d => d.ChangedByUser).WithMany(p => p.OrderStatusHistory)
                .HasForeignKey(d => d.ChangedByUserId)
                .HasConstraintName("FK_OrderStatusHistory_ChangedByUser");

            entity.HasOne(d => d.SellerOrder).WithMany(p => p.OrderStatusHistory)
                .HasForeignKey(d => d.SellerOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderStatusHistory_SellerOrder");
        });

        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.HasKey(e => e.PaymentTransactionId).HasName("PK__PaymentT__C22AEFE012A2645B");

            entity.HasIndex(e => e.TransactionCode, "UQ__PaymentT__D85E702691AB476A").IsUnique();

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.GatewayProvider)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PaymentPurpose)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending");
            entity.Property(e => e.PaymentType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.TransactionCode)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.B2BContract).WithMany(p => p.PaymentTransaction)
                .HasForeignKey(d => d.B2BContractId)
                .HasConstraintName("FK_PaymentTransaction_B2BContract");

            entity.HasOne(d => d.B2BSubscription).WithMany(p => p.PaymentTransaction)
                .HasForeignKey(d => d.B2BSubscriptionId)
                .HasConstraintName("FK_PaymentTransaction_B2BSubscription");

            entity.HasOne(d => d.ContractMilestone).WithMany(p => p.PaymentTransaction)
                .HasForeignKey(d => d.ContractMilestoneId)
                .HasConstraintName("FK_PaymentTransaction_ContractMilestone");

            entity.HasOne(d => d.Order).WithMany(p => p.PaymentTransaction)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_PaymentTransaction_Order");
        });

        modelBuilder.Entity<PayoutItem>(entity =>
        {
            entity.HasKey(e => e.PayoutItemId).HasName("PK__PayoutIt__3414EE9ECA83D870");

            entity.Property(e => e.CommissionAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CommissionRateSnapshot).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.GrossAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.NetAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SourceAmountSettled).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.CommissionPolicy).WithMany(p => p.PayoutItem)
                .HasForeignKey(d => d.CommissionPolicyId)
                .HasConstraintName("FK_PayoutItem_CommissionPolicy");

            entity.HasOne(d => d.ContractMilestone).WithMany(p => p.PayoutItem)
                .HasForeignKey(d => d.ContractMilestoneId)
                .HasConstraintName("FK_PayoutItem_ContractMilestone");

            entity.HasOne(d => d.FarmerPayout).WithMany(p => p.PayoutItem)
                .HasForeignKey(d => d.FarmerPayoutId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PayoutItem_FarmerPayout");

            entity.HasOne(d => d.OrderItem).WithMany(p => p.PayoutItem)
                .HasForeignKey(d => d.OrderItemId)
                .HasConstraintName("FK_PayoutItem_OrderItem");
        });

        modelBuilder.Entity<PreOrderCampaign>(entity =>
        {
            entity.HasKey(e => e.PreOrderCampaignId).HasName("PK__PreOrder__718DDA50B0CD8E33");

            entity.Property(e => e.CampaignName).HasMaxLength(255);
            entity.Property(e => e.CommittedYield).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.DepositPercentage).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.PreOrderPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Draft");
            entity.Property(e => e.TargetYield).HasColumnType("decimal(18, 3)");

            entity.HasOne(d => d.CropSeason).WithMany(p => p.PreOrderCampaign)
                .HasForeignKey(d => d.CropSeasonId)
                .HasConstraintName("FK_PreOrderCampaign_CropSeason");

            entity.HasOne(d => d.Product).WithMany(p => p.PreOrderCampaign)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PreOrderCampaign_Product");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PK__Product__B40CC6CD13718620");

            entity.Property(e => e.ApprovalStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending");
            entity.Property(e => e.BaseRetailPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.OriginProvince).HasMaxLength(100);
            entity.Property(e => e.ProductName).HasMaxLength(255);
            entity.Property(e => e.RejectionReason).HasMaxLength(500);
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.WholesaleMinQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.WholesalePrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Category).WithMany(p => p.Product)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Product_Category");

            entity.HasOne(d => d.FarmerProfile).WithMany(p => p.Product)
                .HasForeignKey(d => d.FarmerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Product_FarmerProfile");
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.ProductImageId).HasName("PK__ProductI__07B2B1B8763FCF5B");

            entity.Property(e => e.AltText).HasMaxLength(255);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImage)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductImage_Product");
        });

        modelBuilder.Entity<ProductReview>(entity =>
        {
            entity.HasKey(e => e.ProductReviewId).HasName("PK__ProductR__396318804AD063EE");

            entity.HasIndex(e => e.OrderItemId, "UQ__ProductR__57ED0680F40E636B").IsUnique();

            entity.Property(e => e.Comment).HasMaxLength(1000);
            entity.Property(e => e.IsPurchasedVerified).HasDefaultValue(true);
            entity.Property(e => e.ModerationReason).HasMaxLength(500);
            entity.Property(e => e.ModerationStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Approved");
            entity.Property(e => e.ReviewDate).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.OrderItem).WithOne(p => p.ProductReview)
                .HasForeignKey<ProductReview>(d => d.OrderItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductReview_OrderItem");

            entity.HasOne(d => d.User).WithMany(p => p.ProductReview)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductReview_User");
        });

        modelBuilder.Entity<RefundTransaction>(entity =>
        {
            entity.HasKey(e => e.RefundTransactionId).HasName("PK__RefundTr__4745915E3343AEE5");

            entity.HasIndex(e => e.RefundCode, "UQ__RefundTr__7DFE7F49BDD0617D").IsUnique();

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.GatewayReference)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.RefundCode)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.RefundStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.ContractTerminationRequest).WithMany(p => p.RefundTransaction)
                .HasForeignKey(d => d.ContractTerminationRequestId)
                .HasConstraintName("FK_RefundTransaction_TerminationRequest");

            entity.HasOne(d => d.DisputeTicket).WithMany(p => p.RefundTransaction)
                .HasForeignKey(d => d.DisputeTicketId)
                .HasConstraintName("FK_RefundTransaction_DisputeTicket");

            entity.HasOne(d => d.PaymentTransaction).WithMany(p => p.RefundTransaction)
                .HasForeignKey(d => d.PaymentTransactionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RefundTransaction_PaymentTransaction");

            entity.HasOne(d => d.ProcessedByUser).WithMany(p => p.RefundTransaction)
                .HasForeignKey(d => d.ProcessedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RefundTransaction_ProcessedByUser");
        });

        modelBuilder.Entity<RegionalPriceBenchmark>(entity =>
        {
            entity.HasKey(e => e.BenchmarkId).HasName("PK__Regional__2CE9029CA11B3564");

            entity.Property(e => e.AveragePrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CommodityName).HasMaxLength(150);
            entity.Property(e => e.MaxPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MinPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ProvinceName).HasMaxLength(100);
            entity.Property(e => e.RegionName).HasMaxLength(100);

            entity.HasOne(d => d.Category).WithMany(p => p.RegionalPriceBenchmark)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RegionalPriceBenchmark_Category");
        });

        modelBuilder.Entity<RetailCheckoutQuote>(entity =>
        {
            entity.HasKey(e => e.QuoteId);

            entity.HasIndex(e => new { e.BuyerUserId, e.ExpiresAt }, "IX_RetailQuote_Buyer_Expiry");

            entity.Property(e => e.QuoteId).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Fingerprint)
                .HasMaxLength(64)
                .IsUnicode(false)
                .IsFixedLength();

            entity.HasOne(d => d.BuyerUser).WithMany(p => p.RetailCheckoutQuote)
                .HasForeignKey(d => d.BuyerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RetailQuote_Buyer");
        });

        modelBuilder.Entity<SellerOrder>(entity =>
        {
            entity.HasKey(e => e.SellerOrderId).HasName("PK__SellerOr__EE1F8C143312FEE6");

            entity.HasIndex(e => e.SellerOrderCode, "UQ__SellerOr__784ABE9C7E3A5DE0").IsUnique();

            entity.Property(e => e.DeliveryFee).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DiscountShare).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SellerNameSnapshot).HasMaxLength(200);
            entity.Property(e => e.SellerOrderCode)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending");
            entity.Property(e => e.Subtotal).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.B2BContract).WithMany(p => p.SellerOrder)
                .HasForeignKey(d => d.B2BContractId)
                .HasConstraintName("FK_SellerOrder_B2BContract");

            entity.HasOne(d => d.FarmerProfile).WithMany(p => p.SellerOrder)
                .HasForeignKey(d => d.FarmerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SellerOrder_FarmerProfile");

            entity.HasOne(d => d.Order).WithMany(p => p.SellerOrder)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SellerOrder_Order");
        });

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasKey(e => e.ShipmentId).HasName("PK__Shipment__5CAD37EDDECC590F");

            entity.HasIndex(e => e.TrackingNumber, "UQ__Shipment__784DB3D95B84B7C9").IsUnique();

            entity.Property(e => e.ShipmentStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Preparing");
            entity.Property(e => e.TrackingNumber)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.ContractMilestone).WithMany(p => p.Shipment)
                .HasForeignKey(d => d.ContractMilestoneId)
                .HasConstraintName("FK_Shipment_ContractMilestone");

            entity.HasOne(d => d.SellerOrder).WithMany(p => p.Shipment)
                .HasForeignKey(d => d.SellerOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Shipment_SellerOrder");
        });

        modelBuilder.Entity<ShipmentItem>(entity =>
        {
            entity.HasKey(e => e.ShipmentItemId).HasName("PK__Shipment__E46A9F6E7E0C558C");

            entity.Property(e => e.AcceptedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.QualityResult).HasMaxLength(255);
            entity.Property(e => e.ReceivedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.ReconciliationStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending");
            entity.Property(e => e.ShippedQuantity).HasColumnType("decimal(18, 3)");

            entity.HasOne(d => d.OrderItemBatchAllocation).WithMany(p => p.ShipmentItem)
                .HasForeignKey(d => d.OrderItemBatchAllocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShipmentItem_BatchAllocation");

            entity.HasOne(d => d.Shipment).WithMany(p => p.ShipmentItem)
                .HasForeignKey(d => d.ShipmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShipmentItem_Shipment");
        });

        modelBuilder.Entity<TraceabilityQRCode>(entity =>
        {
            entity.HasKey(e => e.TraceabilityQRCodeId).HasName("PK__Traceabi__EC0CD854620D8708");

            entity.HasIndex(e => e.QRContentString, "UQ__Traceabi__1DCFB785075193F7").IsUnique();

            entity.HasIndex(e => e.HarvestBatchId, "UQ__Traceabi__619519FA85A154AC").IsUnique();

            entity.Property(e => e.GeneratedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.QRContentString).HasMaxLength(500);
            entity.Property(e => e.TargetUrl).HasMaxLength(500);

            entity.HasOne(d => d.HarvestBatch).WithOne(p => p.TraceabilityQRCode)
                .HasForeignKey<TraceabilityQRCode>(d => d.HarvestBatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TraceabilityQRCode_HarvestBatch");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__User__1788CC4C59F53976");

            entity.HasIndex(e => e.Username, "UQ__User__536C85E4D3E2F0DA").IsUnique();

            entity.HasIndex(e => e.Email, "UQ__User__A9D105343329DB89").IsUnique();

            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Role)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Active");
            entity.Property(e => e.Username).HasMaxLength(100);
        });

        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.VoucherId).HasName("PK__Voucher__3AEE7921D22220E7");

            entity.HasIndex(e => e.VoucherCode, "UQ__Voucher__7F0ABCA9910BA85A").IsUnique();

            entity.Property(e => e.DiscountType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DiscountValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MaxDiscountAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MinOrderAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.VoucherCode)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.FarmerProfile).WithMany(p => p.Voucher)
                .HasForeignKey(d => d.FarmerProfileId)
                .HasConstraintName("FK_Voucher_FarmerProfile");
        });

        modelBuilder.Entity<VoucherRedemption>(entity =>
        {
            entity.HasKey(e => e.VoucherRedemptionId).HasName("PK__VoucherR__140B182296B200E1");

            entity.HasIndex(e => new { e.VoucherId, e.Status, e.ExpiresAt }, "IX_VoucherRedemption_Quota");

            entity.Property(e => e.DiscountSnapshot).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ReservedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Reserved");

            entity.HasOne(d => d.Order).WithMany(p => p.VoucherRedemption)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VoucherRedemption_Order");

            entity.HasOne(d => d.SellerOrder).WithMany(p => p.VoucherRedemption)
                .HasForeignKey(d => d.SellerOrderId)
                .HasConstraintName("FK_VoucherRedemption_SellerOrder");

            entity.HasOne(d => d.Voucher).WithMany(p => p.VoucherRedemption)
                .HasForeignKey(d => d.VoucherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VoucherRedemption_Voucher");
        });

        modelBuilder.Entity<WholesaleBuyerProfile>(entity =>
        {
            entity.HasKey(e => e.WholesaleBuyerProfileId).HasName("PK__Wholesal__5F36EBC65A67F98E");

            entity.HasIndex(e => e.TaxCode, "UQ__Wholesal__12945A284F80E0E0").IsUnique();

            entity.HasIndex(e => e.UserId, "UQ__Wholesal__1788CC4D2A409D56").IsUnique();

            entity.Property(e => e.BusinessAddress).HasMaxLength(500);
            entity.Property(e => e.BusinessType).HasMaxLength(100);
            entity.Property(e => e.CompanyName).HasMaxLength(255);
            entity.Property(e => e.DocumentUrl).HasMaxLength(500);
            entity.Property(e => e.RejectReason).HasMaxLength(500);
            entity.Property(e => e.RepresentativeName).HasMaxLength(150);
            entity.Property(e => e.TaxCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.VerificationStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.User).WithOne(p => p.WholesaleBuyerProfile)
                .HasForeignKey<WholesaleBuyerProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WholesaleBuyerProfile_User");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    /// <summary>Provides a separate-file extension point without editing generated mappings.</summary>
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
