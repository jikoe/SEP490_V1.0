using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Domain.Entities;

public partial class AgriSustainContext : DbContext
{
    public AgriSustainContext()
    {
    }

    public AgriSustainContext(DbContextOptions<AgriSustainContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<B2bcontract> B2bcontracts { get; set; }

    public virtual DbSet<B2bsubscription> B2bsubscriptions { get; set; }

    public virtual DbSet<B2bsubscriptionPackage> B2bsubscriptionPackages { get; set; }

    public virtual DbSet<Cart> Carts { get; set; }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<CommissionPolicy> CommissionPolicies { get; set; }

    public virtual DbSet<ContractLine> ContractLines { get; set; }

    public virtual DbSet<ContractMilestone> ContractMilestones { get; set; }

    public virtual DbSet<ContractSignature> ContractSignatures { get; set; }

    public virtual DbSet<ContractTerminationRequest> ContractTerminationRequests { get; set; }

    public virtual DbSet<ContractVersion> ContractVersions { get; set; }

    public virtual DbSet<CropSeason> CropSeasons { get; set; }

    public virtual DbSet<CultivationLog> CultivationLogs { get; set; }

    public virtual DbSet<DeliveryAddress> DeliveryAddresses { get; set; }

    public virtual DbSet<DisputeEvidence> DisputeEvidences { get; set; }

    public virtual DbSet<DisputeTicket> DisputeTickets { get; set; }

    public virtual DbSet<FarmCertificate> FarmCertificates { get; set; }

    public virtual DbSet<FarmerPayout> FarmerPayouts { get; set; }

    public virtual DbSet<FarmerProfile> FarmerProfiles { get; set; }

    public virtual DbSet<HarvestBatch> HarvestBatches { get; set; }

    public virtual DbSet<InventoryMovement> InventoryMovements { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<InvoiceLine> InvoiceLines { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<OrderItemBatchAllocation> OrderItemBatchAllocations { get; set; }

    public virtual DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }

    public virtual DbSet<PaymentTransaction> PaymentTransactions { get; set; }

    public virtual DbSet<PayoutItem> PayoutItems { get; set; }

    public virtual DbSet<PreOrderCampaign> PreOrderCampaigns { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductImage> ProductImages { get; set; }

    public virtual DbSet<ProductReview> ProductReviews { get; set; }

    public virtual DbSet<RefundTransaction> RefundTransactions { get; set; }

    public virtual DbSet<RegionalPriceBenchmark> RegionalPriceBenchmarks { get; set; }

    public virtual DbSet<RetailCheckoutQuote> RetailCheckoutQuotes { get; set; }

    public virtual DbSet<SellerOrder> SellerOrders { get; set; }

    public virtual DbSet<Shipment> Shipments { get; set; }

    public virtual DbSet<ShipmentItem> ShipmentItems { get; set; }

    public virtual DbSet<TraceabilityQrcode> TraceabilityQrcodes { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Voucher> Vouchers { get; set; }

    public virtual DbSet<VoucherRedemption> VoucherRedemptions { get; set; }

    public virtual DbSet<WholesaleBuyerProfile> WholesaleBuyerProfiles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
            optionsBuilder.UseSqlServer("Server=localhost;Database=AgriSustain;UId=sa;pwd=123;TrustServerCertificate=True");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("PK__AuditLog__EB5F6CBD8BD0034F");

            entity.ToTable("AuditLog");

            entity.Property(e => e.Action)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.EntityType)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.TraceId).HasMaxLength(64);

            entity.HasOne(d => d.PerformedByUser).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.PerformedByUserId)
                .HasConstraintName("FK_AuditLog_PerformedByUser");
        });

        modelBuilder.Entity<B2bcontract>(entity =>
        {
            entity.HasKey(e => e.B2bcontractId).HasName("PK__B2BContr__59105F1E4BBBA9C1");

            entity.ToTable("B2BContract");

            entity.HasIndex(e => e.ContractNumber, "UQ__B2BContr__C51D43DA5377F604").IsUnique();

            entity.Property(e => e.B2bcontractId).HasColumnName("B2BContractId");
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

            entity.HasOne(d => d.FarmerProfile).WithMany(p => p.B2bcontracts)
                .HasForeignKey(d => d.FarmerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_B2BContract_FarmerProfile");

            entity.HasOne(d => d.WholesaleBuyerProfile).WithMany(p => p.B2bcontracts)
                .HasForeignKey(d => d.WholesaleBuyerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_B2BContract_WholesaleBuyer");
        });

        modelBuilder.Entity<B2bsubscription>(entity =>
        {
            entity.HasKey(e => e.B2bsubscriptionId).HasName("PK__B2BSubsc__EE8E09CC2393C66C");

            entity.ToTable("B2BSubscription");

            entity.Property(e => e.B2bsubscriptionId).HasColumnName("B2BSubscriptionId");
            entity.Property(e => e.PriceSnapshot).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SubscriptionStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Active");

            entity.HasOne(d => d.Package).WithMany(p => p.B2bsubscriptions)
                .HasForeignKey(d => d.PackageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_B2BSubscription_Package");

            entity.HasOne(d => d.WholesaleBuyerProfile).WithMany(p => p.B2bsubscriptions)
                .HasForeignKey(d => d.WholesaleBuyerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_B2BSubscription_WholesaleBuyer");
        });

        modelBuilder.Entity<B2bsubscriptionPackage>(entity =>
        {
            entity.HasKey(e => e.PackageId).HasName("PK__B2BSubsc__322035CC22F8BB1A");

            entity.ToTable("B2BSubscriptionPackage");

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PackageDescription).HasMaxLength(1000);
            entity.Property(e => e.PackageName).HasMaxLength(150);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.CartId).HasName("PK__Cart__51BCD7B71E7F6657");

            entity.ToTable("Cart");

            entity.HasIndex(e => e.UserId, "UQ__Cart__1788CC4D6CB8AA20").IsUnique();

            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.User).WithOne(p => p.Cart)
                .HasForeignKey<Cart>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Cart_User");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.CartItemId).HasName("PK__CartItem__488B0B0A19CB5BB5");

            entity.ToTable("CartItem");

            entity.Property(e => e.AddedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsSelected).HasDefaultValue(true);
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");

            entity.HasOne(d => d.Cart).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.CartId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CartItem_Cart");

            entity.HasOne(d => d.PreOrderCampaign).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.PreOrderCampaignId)
                .HasConstraintName("FK_CartItem_PreOrderCampaign");

            entity.HasOne(d => d.Product).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CartItem_Product");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__Category__19093A0B8C92A3EB");

            entity.ToTable("Category");

            entity.Property(e => e.CategoryName).HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.ParentCategory).WithMany(p => p.InverseParentCategory)
                .HasForeignKey(d => d.ParentCategoryId)
                .HasConstraintName("FK_Category_ParentCategory");
        });

        modelBuilder.Entity<CommissionPolicy>(entity =>
        {
            entity.HasKey(e => e.CommissionPolicyId).HasName("PK__Commissi__ACB43F57FF666026");

            entity.ToTable("CommissionPolicy");

            entity.Property(e => e.ApplicableOrderType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CommissionRate).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Note).HasMaxLength(500);

            entity.HasOne(d => d.Category).WithMany(p => p.CommissionPolicies)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CommissionPolicy_Category");
        });

        modelBuilder.Entity<ContractLine>(entity =>
        {
            entity.HasKey(e => e.ContractLineId).HasName("PK__Contract__905F6E87F50C768E");

            entity.ToTable("ContractLine");

            entity.Property(e => e.AgreedPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.B2bcontractId).HasColumnName("B2BContractId");
            entity.Property(e => e.CommittedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.MaxPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MinPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PriceFormula).HasMaxLength(255);
            entity.Property(e => e.PriceType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.QualitySpecification).HasMaxLength(500);
            entity.Property(e => e.UnitSnapshot).HasMaxLength(50);

            entity.HasOne(d => d.B2bcontract).WithMany(p => p.ContractLines)
                .HasForeignKey(d => d.B2bcontractId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContractLine_B2BContract");

            entity.HasOne(d => d.CropSeason).WithMany(p => p.ContractLines)
                .HasForeignKey(d => d.CropSeasonId)
                .HasConstraintName("FK_ContractLine_CropSeason");

            entity.HasOne(d => d.Product).WithMany(p => p.ContractLines)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContractLine_Product");
        });

        modelBuilder.Entity<ContractMilestone>(entity =>
        {
            entity.HasKey(e => e.ContractMilestoneId).HasName("PK__Contract__9CD091DCA8BFA982");

            entity.ToTable("ContractMilestone");

            entity.Property(e => e.AcceptedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.B2bcontractId).HasColumnName("B2BContractId");
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

            entity.HasOne(d => d.B2bcontract).WithMany(p => p.ContractMilestones)
                .HasForeignKey(d => d.B2bcontractId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContractMilestone_B2BContract");

            entity.HasOne(d => d.ContractLine).WithMany(p => p.ContractMilestones)
                .HasForeignKey(d => d.ContractLineId)
                .HasConstraintName("FK_ContractMilestone_ContractLine");
        });

        modelBuilder.Entity<ContractSignature>(entity =>
        {
            entity.HasKey(e => e.ContractSignatureId).HasName("PK__Contract__6A8CC2207ECC4B88");

            entity.ToTable("ContractSignature");

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

            entity.HasOne(d => d.ContractVersion).WithMany(p => p.ContractSignatures)
                .HasForeignKey(d => d.ContractVersionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContractSignature_ContractVersion");

            entity.HasOne(d => d.SignedByUser).WithMany(p => p.ContractSignatures)
                .HasForeignKey(d => d.SignedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContractSignature_SignedByUser");
        });

        modelBuilder.Entity<ContractTerminationRequest>(entity =>
        {
            entity.HasKey(e => e.ContractTerminationRequestId).HasName("PK__Contract__B98B73906C812F01");

            entity.ToTable("ContractTerminationRequest");

            entity.Property(e => e.B2bcontractId).HasColumnName("B2BContractId");
            entity.Property(e => e.EvidenceUrl).HasMaxLength(500);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.RequestStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending");
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.SettlementAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.B2bcontract).WithMany(p => p.ContractTerminationRequests)
                .HasForeignKey(d => d.B2bcontractId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TerminationRequest_B2BContract");

            entity.HasOne(d => d.RequestedByUser).WithMany(p => p.ContractTerminationRequests)
                .HasForeignKey(d => d.RequestedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TerminationRequest_RequestedByUser");
        });

        modelBuilder.Entity<ContractVersion>(entity =>
        {
            entity.HasKey(e => e.ContractVersionId).HasName("PK__Contract__54F29433394275C2");

            entity.ToTable("ContractVersion");

            entity.Property(e => e.B2bcontractId).HasColumnName("B2BContractId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.DocumentHash)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.FeedbackReason).HasMaxLength(500);
            entity.Property(e => e.VersionStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.B2bcontract).WithMany(p => p.ContractVersions)
                .HasForeignKey(d => d.B2bcontractId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContractVersion_B2BContract");

            entity.HasOne(d => d.ProposedByUser).WithMany(p => p.ContractVersions)
                .HasForeignKey(d => d.ProposedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContractVersion_ProposedByUser");
        });

        modelBuilder.Entity<CropSeason>(entity =>
        {
            entity.HasKey(e => e.CropSeasonId).HasName("PK__CropSeas__675AF50015CC71F6");

            entity.ToTable("CropSeason");

            entity.Property(e => e.ActualYield).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.CropVariety).HasMaxLength(150);
            entity.Property(e => e.CultivatedArea).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.EstimatedYield).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.SeasonName).HasMaxLength(200);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Planning");

            entity.HasOne(d => d.FarmerProfile).WithMany(p => p.CropSeasons)
                .HasForeignKey(d => d.FarmerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CropSeason_FarmerProfile");
        });

        modelBuilder.Entity<CultivationLog>(entity =>
        {
            entity.HasKey(e => e.CultivationLogId).HasName("PK__Cultivat__60C48ADF8973D2C7");

            entity.ToTable("CultivationLog");

            entity.Property(e => e.ActionType).HasMaxLength(100);
            entity.Property(e => e.DetailDescription).HasMaxLength(1000);
            entity.Property(e => e.Dosage).HasMaxLength(150);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.InputMaterial).HasMaxLength(255);
            entity.Property(e => e.PerformedBy).HasMaxLength(150);
            entity.Property(e => e.RecordedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.CropSeason).WithMany(p => p.CultivationLogs)
                .HasForeignKey(d => d.CropSeasonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CultivationLog_CropSeason");

            entity.HasOne(d => d.RecordedByUser).WithMany(p => p.CultivationLogs)
                .HasForeignKey(d => d.RecordedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CultivationLog_RecordedByUser");
        });

        modelBuilder.Entity<DeliveryAddress>(entity =>
        {
            entity.HasKey(e => e.DeliveryAddressId).HasName("PK__Delivery__4EBD54FCA0BED63E");

            entity.ToTable("DeliveryAddress");

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

            entity.HasOne(d => d.User).WithMany(p => p.DeliveryAddresses)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DeliveryAddress_User");
        });

        modelBuilder.Entity<DisputeEvidence>(entity =>
        {
            entity.HasKey(e => e.DisputeEvidenceId).HasName("PK__DisputeE__B2461231F40C16AB");

            entity.ToTable("DisputeEvidence");

            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.SubmittedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.DisputeTicket).WithMany(p => p.DisputeEvidences)
                .HasForeignKey(d => d.DisputeTicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DisputeEvidence_DisputeTicket");

            entity.HasOne(d => d.SubmittedByUser).WithMany(p => p.DisputeEvidences)
                .HasForeignKey(d => d.SubmittedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DisputeEvidence_SubmittedByUser");
        });

        modelBuilder.Entity<DisputeTicket>(entity =>
        {
            entity.HasKey(e => e.DisputeTicketId).HasName("PK__DisputeT__3C3F9CFE14A83482");

            entity.ToTable("DisputeTicket");

            entity.Property(e => e.B2bcontractId).HasColumnName("B2BContractId");
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

            entity.HasOne(d => d.AssignedStaffUser).WithMany(p => p.DisputeTicketAssignedStaffUsers)
                .HasForeignKey(d => d.AssignedStaffUserId)
                .HasConstraintName("FK_DisputeTicket_AssignedStaffUser");

            entity.HasOne(d => d.B2bcontract).WithMany(p => p.DisputeTickets)
                .HasForeignKey(d => d.B2bcontractId)
                .HasConstraintName("FK_DisputeTicket_B2BContract");

            entity.HasOne(d => d.SellerOrder).WithMany(p => p.DisputeTickets)
                .HasForeignKey(d => d.SellerOrderId)
                .HasConstraintName("FK_DisputeTicket_SellerOrder");

            entity.HasOne(d => d.SubmittedByUser).WithMany(p => p.DisputeTicketSubmittedByUsers)
                .HasForeignKey(d => d.SubmittedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DisputeTicket_SubmittedByUser");
        });

        modelBuilder.Entity<FarmCertificate>(entity =>
        {
            entity.HasKey(e => e.FarmCertificateId).HasName("PK__FarmCert__2BDF0082310CCD77");

            entity.ToTable("FarmCertificate");

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

            entity.HasOne(d => d.FarmerProfile).WithMany(p => p.FarmCertificates)
                .HasForeignKey(d => d.FarmerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FarmCertificate_FarmerProfile");

            entity.HasOne(d => d.ReviewedByUser).WithMany(p => p.FarmCertificates)
                .HasForeignKey(d => d.ReviewedByUserId)
                .HasConstraintName("FK_FarmCertificate_ReviewedByUser");
        });

        modelBuilder.Entity<FarmerPayout>(entity =>
        {
            entity.HasKey(e => e.FarmerPayoutId).HasName("PK__FarmerPa__80AAF9C35CCB3E34");

            entity.ToTable("FarmerPayout");

            entity.HasIndex(e => e.PayoutCode, "UQ__FarmerPa__3F2306E373F23B72").IsUnique();

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

            entity.HasOne(d => d.ApprovedByUser).WithMany(p => p.FarmerPayouts)
                .HasForeignKey(d => d.ApprovedByUserId)
                .HasConstraintName("FK_FarmerPayout_ApprovedByUser");

            entity.HasOne(d => d.FarmerProfile).WithMany(p => p.FarmerPayouts)
                .HasForeignKey(d => d.FarmerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FarmerPayout_FarmerProfile");
        });

        modelBuilder.Entity<FarmerProfile>(entity =>
        {
            entity.HasKey(e => e.FarmerProfileId).HasName("PK__FarmerPr__688AB36DC3043191");

            entity.ToTable("FarmerProfile");

            entity.HasIndex(e => e.UserId, "UQ__FarmerPr__1788CC4D5D063A52").IsUnique();

            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.BusinessLicenseNumber)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.CultivationZone).HasMaxLength(200);
            entity.Property(e => e.FarmName).HasMaxLength(200);
            entity.Property(e => e.Gpscoordinates)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("GPSCoordinates");
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
            entity.HasKey(e => e.HarvestBatchId).HasName("PK__HarvestB__619519FB6D9C7913");

            entity.ToTable("HarvestBatch");

            entity.HasIndex(e => new { e.ProductId, e.Status, e.ExpirationDate, e.HarvestBatchId }, "IX_HarvestBatch_Fefo");

            entity.HasIndex(e => e.BatchCode, "UQ__HarvestB__B22ADA8ED8093A93").IsUnique();

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

            entity.HasOne(d => d.CropSeason).WithMany(p => p.HarvestBatches)
                .HasForeignKey(d => d.CropSeasonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HarvestBatch_CropSeason");

            entity.HasOne(d => d.PreOrderCampaign).WithMany(p => p.HarvestBatches)
                .HasForeignKey(d => d.PreOrderCampaignId)
                .HasConstraintName("FK_HarvestBatch_PreOrderCampaign");

            entity.HasOne(d => d.Product).WithMany(p => p.HarvestBatches)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HarvestBatch_Product");
        });

        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.HasKey(e => e.InventoryMovementId).HasName("PK__Inventor__DB9462B6EAC307C2");

            entity.ToTable("InventoryMovement");

            entity.Property(e => e.MovementType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.QuantityChange).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Reason).HasMaxLength(500);

            entity.HasOne(d => d.HarvestBatch).WithMany(p => p.InventoryMovements)
                .HasForeignKey(d => d.HarvestBatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InventoryMovement_HarvestBatch");

            entity.HasOne(d => d.OrderItemBatchAllocation).WithMany(p => p.InventoryMovements)
                .HasForeignKey(d => d.OrderItemBatchAllocationId)
                .HasConstraintName("FK_InventoryMovement_BatchAllocation");

            entity.HasOne(d => d.RecordedByUser).WithMany(p => p.InventoryMovements)
                .HasForeignKey(d => d.RecordedByUserId)
                .HasConstraintName("FK_InventoryMovement_RecordedByUser");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId).HasName("PK__Invoice__D796AAB5783A60C5");

            entity.ToTable("Invoice");

            entity.HasIndex(e => e.InvoiceNumber, "UQ__Invoice__D776E981B27DF0FE").IsUnique();

            entity.Property(e => e.InvoiceNumber)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.InvoiceStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Issued");
            entity.Property(e => e.IssuedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.ContractMilestone).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.ContractMilestoneId)
                .HasConstraintName("FK_Invoice_ContractMilestone");

            entity.HasOne(d => d.SellerOrder).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.SellerOrderId)
                .HasConstraintName("FK_Invoice_SellerOrder");
        });

        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            entity.HasKey(e => e.InvoiceLineId).HasName("PK__InvoiceL__0D760AD97EFD97FD");

            entity.ToTable("InvoiceLine");

            entity.Property(e => e.LineAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.UnitPriceSnapshot).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceLines)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InvoiceLine_Invoice");

            entity.HasOne(d => d.OrderItem).WithMany(p => p.InvoiceLines)
                .HasForeignKey(d => d.OrderItemId)
                .HasConstraintName("FK_InvoiceLine_OrderItem");

            entity.HasOne(d => d.ShipmentItem).WithMany(p => p.InvoiceLines)
                .HasForeignKey(d => d.ShipmentItemId)
                .HasConstraintName("FK_InvoiceLine_ShipmentItem");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E12055F4FE4");

            entity.ToTable("Notification");

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

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notification_User");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Order__C3905BCFF37CB9E9");

            entity.ToTable("Order");

            entity.HasIndex(e => e.OrderCode, "UQ__Order__999B52292D426FB2").IsUnique();

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

            entity.HasOne(d => d.BuyerUser).WithMany(p => p.Orders)
                .HasForeignKey(d => d.BuyerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Order_BuyerUser");

            entity.HasOne(d => d.RetailQuote).WithMany(p => p.Orders)
                .HasForeignKey(d => d.RetailQuoteId)
                .HasConstraintName("FK_Order_RetailQuote");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.OrderItemId).HasName("PK__OrderIte__57ED0681687028DF");

            entity.ToTable("OrderItem");

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

            entity.HasOne(d => d.ContractLine).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ContractLineId)
                .HasConstraintName("FK_OrderItem_ContractLine");

            entity.HasOne(d => d.PreOrderCampaign).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.PreOrderCampaignId)
                .HasConstraintName("FK_OrderItem_PreOrderCampaign");

            entity.HasOne(d => d.Product).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderItem_Product");

            entity.HasOne(d => d.SellerOrder).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.SellerOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderItem_SellerOrder");
        });

        modelBuilder.Entity<OrderItemBatchAllocation>(entity =>
        {
            entity.HasKey(e => e.OrderItemBatchAllocationId).HasName("PK__OrderIte__51B745BBDA84E7AB");

            entity.ToTable("OrderItemBatchAllocation");

            entity.Property(e => e.AllocatedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.AllocationSequence).HasDefaultValue(1);
            entity.Property(e => e.AllocationStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Reserved");
            entity.Property(e => e.ReservedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.HarvestBatch).WithMany(p => p.OrderItemBatchAllocations)
                .HasForeignKey(d => d.HarvestBatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BatchAllocation_HarvestBatch");

            entity.HasOne(d => d.OrderItem).WithMany(p => p.OrderItemBatchAllocations)
                .HasForeignKey(d => d.OrderItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BatchAllocation_OrderItem");
        });

        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.HasKey(e => e.OrderStatusHistoryId).HasName("PK__OrderSta__D16EDBC3519EE2F3");

            entity.ToTable("OrderStatusHistory");

            entity.Property(e => e.NewStatus)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.OldStatus)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Reason).HasMaxLength(500);

            entity.HasOne(d => d.ChangedByUser).WithMany(p => p.OrderStatusHistories)
                .HasForeignKey(d => d.ChangedByUserId)
                .HasConstraintName("FK_OrderStatusHistory_ChangedByUser");

            entity.HasOne(d => d.SellerOrder).WithMany(p => p.OrderStatusHistories)
                .HasForeignKey(d => d.SellerOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderStatusHistory_SellerOrder");
        });

        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.HasKey(e => e.PaymentTransactionId).HasName("PK__PaymentT__C22AEFE0480805E2");

            entity.ToTable("PaymentTransaction");

            entity.HasIndex(e => e.TransactionCode, "UQ__PaymentT__D85E70260EFCF31C").IsUnique();

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.B2bcontractId).HasColumnName("B2BContractId");
            entity.Property(e => e.B2bsubscriptionId).HasColumnName("B2BSubscriptionId");
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

            entity.HasOne(d => d.B2bcontract).WithMany(p => p.PaymentTransactions)
                .HasForeignKey(d => d.B2bcontractId)
                .HasConstraintName("FK_PaymentTransaction_B2BContract");

            entity.HasOne(d => d.B2bsubscription).WithMany(p => p.PaymentTransactions)
                .HasForeignKey(d => d.B2bsubscriptionId)
                .HasConstraintName("FK_PaymentTransaction_B2BSubscription");

            entity.HasOne(d => d.ContractMilestone).WithMany(p => p.PaymentTransactions)
                .HasForeignKey(d => d.ContractMilestoneId)
                .HasConstraintName("FK_PaymentTransaction_ContractMilestone");

            entity.HasOne(d => d.Order).WithMany(p => p.PaymentTransactions)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_PaymentTransaction_Order");
        });

        modelBuilder.Entity<PayoutItem>(entity =>
        {
            entity.HasKey(e => e.PayoutItemId).HasName("PK__PayoutIt__3414EE9E0FAA72A1");

            entity.ToTable("PayoutItem");

            entity.Property(e => e.CommissionAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CommissionRateSnapshot).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.GrossAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.NetAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SourceAmountSettled).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.CommissionPolicy).WithMany(p => p.PayoutItems)
                .HasForeignKey(d => d.CommissionPolicyId)
                .HasConstraintName("FK_PayoutItem_CommissionPolicy");

            entity.HasOne(d => d.ContractMilestone).WithMany(p => p.PayoutItems)
                .HasForeignKey(d => d.ContractMilestoneId)
                .HasConstraintName("FK_PayoutItem_ContractMilestone");

            entity.HasOne(d => d.FarmerPayout).WithMany(p => p.PayoutItems)
                .HasForeignKey(d => d.FarmerPayoutId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PayoutItem_FarmerPayout");

            entity.HasOne(d => d.OrderItem).WithMany(p => p.PayoutItems)
                .HasForeignKey(d => d.OrderItemId)
                .HasConstraintName("FK_PayoutItem_OrderItem");
        });

        modelBuilder.Entity<PreOrderCampaign>(entity =>
        {
            entity.HasKey(e => e.PreOrderCampaignId).HasName("PK__PreOrder__718DDA50E35F70A7");

            entity.ToTable("PreOrderCampaign");

            entity.Property(e => e.CampaignName).HasMaxLength(255);
            entity.Property(e => e.CommittedYield).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.DepositPercentage).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.PreOrderPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Draft");
            entity.Property(e => e.TargetYield).HasColumnType("decimal(18, 3)");

            entity.HasOne(d => d.CropSeason).WithMany(p => p.PreOrderCampaigns)
                .HasForeignKey(d => d.CropSeasonId)
                .HasConstraintName("FK_PreOrderCampaign_CropSeason");

            entity.HasOne(d => d.Product).WithMany(p => p.PreOrderCampaigns)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PreOrderCampaign_Product");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PK__Product__B40CC6CDDB809E86");

            entity.ToTable("Product");

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

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Product_Category");

            entity.HasOne(d => d.FarmerProfile).WithMany(p => p.Products)
                .HasForeignKey(d => d.FarmerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Product_FarmerProfile");
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.ProductImageId).HasName("PK__ProductI__07B2B1B8F714B192");

            entity.ToTable("ProductImage");

            entity.Property(e => e.AltText).HasMaxLength(255);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductImage_Product");
        });

        modelBuilder.Entity<ProductReview>(entity =>
        {
            entity.HasKey(e => e.ProductReviewId).HasName("PK__ProductR__39631880C55ECE35");

            entity.ToTable("ProductReview");

            entity.HasIndex(e => e.OrderItemId, "UQ__ProductR__57ED06801794715F").IsUnique();

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

            entity.HasOne(d => d.User).WithMany(p => p.ProductReviews)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductReview_User");
        });

        modelBuilder.Entity<RefundTransaction>(entity =>
        {
            entity.HasKey(e => e.RefundTransactionId).HasName("PK__RefundTr__4745915EA13F3A70");

            entity.ToTable("RefundTransaction");

            entity.HasIndex(e => e.RefundCode, "UQ__RefundTr__7DFE7F49692A6492").IsUnique();

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

            entity.HasOne(d => d.ContractTerminationRequest).WithMany(p => p.RefundTransactions)
                .HasForeignKey(d => d.ContractTerminationRequestId)
                .HasConstraintName("FK_RefundTransaction_TerminationRequest");

            entity.HasOne(d => d.DisputeTicket).WithMany(p => p.RefundTransactions)
                .HasForeignKey(d => d.DisputeTicketId)
                .HasConstraintName("FK_RefundTransaction_DisputeTicket");

            entity.HasOne(d => d.PaymentTransaction).WithMany(p => p.RefundTransactions)
                .HasForeignKey(d => d.PaymentTransactionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RefundTransaction_PaymentTransaction");

            entity.HasOne(d => d.ProcessedByUser).WithMany(p => p.RefundTransactions)
                .HasForeignKey(d => d.ProcessedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RefundTransaction_ProcessedByUser");
        });

        modelBuilder.Entity<RegionalPriceBenchmark>(entity =>
        {
            entity.HasKey(e => e.BenchmarkId).HasName("PK__Regional__2CE9029C0C2D33FA");

            entity.ToTable("RegionalPriceBenchmark");

            entity.Property(e => e.AveragePrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CommodityName).HasMaxLength(150);
            entity.Property(e => e.MaxPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MinPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ProvinceName).HasMaxLength(100);
            entity.Property(e => e.RegionName).HasMaxLength(100);

            entity.HasOne(d => d.Category).WithMany(p => p.RegionalPriceBenchmarks)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RegionalPriceBenchmark_Category");
        });

        modelBuilder.Entity<RetailCheckoutQuote>(entity =>
        {
            entity.HasKey(e => e.QuoteId);

            entity.ToTable("RetailCheckoutQuote");

            entity.HasIndex(e => new { e.BuyerUserId, e.ExpiresAt }, "IX_RetailQuote_Buyer_Expiry");

            entity.Property(e => e.QuoteId).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Fingerprint)
                .HasMaxLength(64)
                .IsUnicode(false)
                .IsFixedLength();

            entity.HasOne(d => d.BuyerUser).WithMany(p => p.RetailCheckoutQuotes)
                .HasForeignKey(d => d.BuyerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RetailQuote_Buyer");
        });

        modelBuilder.Entity<SellerOrder>(entity =>
        {
            entity.HasKey(e => e.SellerOrderId).HasName("PK__SellerOr__EE1F8C147E2BFC9F");

            entity.ToTable("SellerOrder");

            entity.HasIndex(e => e.SellerOrderCode, "UQ__SellerOr__784ABE9CCC261470").IsUnique();

            entity.Property(e => e.B2bcontractId).HasColumnName("B2BContractId");
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

            entity.HasOne(d => d.B2bcontract).WithMany(p => p.SellerOrders)
                .HasForeignKey(d => d.B2bcontractId)
                .HasConstraintName("FK_SellerOrder_B2BContract");

            entity.HasOne(d => d.FarmerProfile).WithMany(p => p.SellerOrders)
                .HasForeignKey(d => d.FarmerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SellerOrder_FarmerProfile");

            entity.HasOne(d => d.Order).WithMany(p => p.SellerOrders)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SellerOrder_Order");
        });

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasKey(e => e.ShipmentId).HasName("PK__Shipment__5CAD37ED74E30F3F");

            entity.ToTable("Shipment");

            entity.HasIndex(e => e.TrackingNumber, "UQ__Shipment__784DB3D9071F7526").IsUnique();

            entity.Property(e => e.ShipmentStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Preparing");
            entity.Property(e => e.TrackingNumber)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.ContractMilestone).WithMany(p => p.Shipments)
                .HasForeignKey(d => d.ContractMilestoneId)
                .HasConstraintName("FK_Shipment_ContractMilestone");

            entity.HasOne(d => d.SellerOrder).WithMany(p => p.Shipments)
                .HasForeignKey(d => d.SellerOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Shipment_SellerOrder");
        });

        modelBuilder.Entity<ShipmentItem>(entity =>
        {
            entity.HasKey(e => e.ShipmentItemId).HasName("PK__Shipment__E46A9F6ECF3319C9");

            entity.ToTable("ShipmentItem");

            entity.Property(e => e.AcceptedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.QualityResult).HasMaxLength(255);
            entity.Property(e => e.ReceivedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.ReconciliationStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending");
            entity.Property(e => e.ShippedQuantity).HasColumnType("decimal(18, 3)");

            entity.HasOne(d => d.OrderItemBatchAllocation).WithMany(p => p.ShipmentItems)
                .HasForeignKey(d => d.OrderItemBatchAllocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShipmentItem_BatchAllocation");

            entity.HasOne(d => d.Shipment).WithMany(p => p.ShipmentItems)
                .HasForeignKey(d => d.ShipmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShipmentItem_Shipment");
        });

        modelBuilder.Entity<TraceabilityQrcode>(entity =>
        {
            entity.HasKey(e => e.TraceabilityQrcodeId).HasName("PK__Traceabi__EC0CD854B99BCA2A");

            entity.ToTable("TraceabilityQRCode");

            entity.HasIndex(e => e.QrcontentString, "UQ__Traceabi__1DCFB7858C03C9D3").IsUnique();

            entity.HasIndex(e => e.HarvestBatchId, "UQ__Traceabi__619519FA99423A4C").IsUnique();

            entity.Property(e => e.TraceabilityQrcodeId).HasColumnName("TraceabilityQRCodeId");
            entity.Property(e => e.GeneratedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.QrcontentString)
                .HasMaxLength(500)
                .HasColumnName("QRContentString");
            entity.Property(e => e.TargetUrl).HasMaxLength(500);

            entity.HasOne(d => d.HarvestBatch).WithOne(p => p.TraceabilityQrcode)
                .HasForeignKey<TraceabilityQrcode>(d => d.HarvestBatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TraceabilityQRCode_HarvestBatch");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__User__1788CC4C6D4D2615");

            entity.ToTable("User");

            entity.HasIndex(e => e.Username, "UQ__User__536C85E4CFD3292D").IsUnique();

            entity.HasIndex(e => e.Email, "UQ__User__A9D105340CD5E19B").IsUnique();

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
            entity.HasKey(e => e.VoucherId).HasName("PK__Voucher__3AEE7921EDD229D3");

            entity.ToTable("Voucher");

            entity.HasIndex(e => e.VoucherCode, "UQ__Voucher__7F0ABCA9BF2EC8AB").IsUnique();

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

            entity.HasOne(d => d.FarmerProfile).WithMany(p => p.Vouchers)
                .HasForeignKey(d => d.FarmerProfileId)
                .HasConstraintName("FK_Voucher_FarmerProfile");
        });

        modelBuilder.Entity<VoucherRedemption>(entity =>
        {
            entity.HasKey(e => e.VoucherRedemptionId).HasName("PK__VoucherR__140B1822F831B421");

            entity.ToTable("VoucherRedemption");

            entity.HasIndex(e => new { e.VoucherId, e.Status, e.ExpiresAt }, "IX_VoucherRedemption_Quota");

            entity.Property(e => e.DiscountSnapshot).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ReservedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Reserved");

            entity.HasOne(d => d.Order).WithMany(p => p.VoucherRedemptions)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VoucherRedemption_Order");

            entity.HasOne(d => d.SellerOrder).WithMany(p => p.VoucherRedemptions)
                .HasForeignKey(d => d.SellerOrderId)
                .HasConstraintName("FK_VoucherRedemption_SellerOrder");

            entity.HasOne(d => d.Voucher).WithMany(p => p.VoucherRedemptions)
                .HasForeignKey(d => d.VoucherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VoucherRedemption_Voucher");
        });

        modelBuilder.Entity<WholesaleBuyerProfile>(entity =>
        {
            entity.HasKey(e => e.WholesaleBuyerProfileId).HasName("PK__Wholesal__5F36EBC6E8F5D762");

            entity.ToTable("WholesaleBuyerProfile");

            entity.HasIndex(e => e.TaxCode, "UQ__Wholesal__12945A2881299EF0").IsUnique();

            entity.HasIndex(e => e.UserId, "UQ__Wholesal__1788CC4D652E5AA9").IsUnique();

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

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
