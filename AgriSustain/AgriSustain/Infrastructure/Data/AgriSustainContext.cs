using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Infrastructure.Data;

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

    public virtual DbSet<BusinessPolicy> BusinessPolicies { get; set; }

    public virtual DbSet<Cart> Carts { get; set; }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<CommissionPolicy> CommissionPolicies { get; set; }

    public virtual DbSet<ContractDepositAllocation> ContractDepositAllocations { get; set; }

    public virtual DbSet<ContractLine> ContractLines { get; set; }

    public virtual DbSet<ContractMilestone> ContractMilestones { get; set; }

    public virtual DbSet<ContractSignature> ContractSignatures { get; set; }

    public virtual DbSet<ContractTerminationRequest> ContractTerminationRequests { get; set; }

    public virtual DbSet<ContractVersion> ContractVersions { get; set; }

    public virtual DbSet<CropSeason> CropSeasons { get; set; }

    public virtual DbSet<CultivationLog> CultivationLogs { get; set; }

    public virtual DbSet<DeliveryAddress> DeliveryAddresses { get; set; }

    public virtual DbSet<DisputeEvidence> DisputeEvidences { get; set; }

    public virtual DbSet<DisputeEvidenceRequest> DisputeEvidenceRequests { get; set; }

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

    public virtual DbSet<PaymentGatewayEvent> PaymentGatewayEvents { get; set; }

    public virtual DbSet<PaymentInvoiceAllocation> PaymentInvoiceAllocations { get; set; }

    public virtual DbSet<PaymentTransaction> PaymentTransactions { get; set; }

    public virtual DbSet<PayoutItem> PayoutItems { get; set; }

    public virtual DbSet<PreOrderCampaign> PreOrderCampaigns { get; set; }

    public virtual DbSet<PreOrderReservation> PreOrderReservations { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductImage> ProductImages { get; set; }

    public virtual DbSet<ProductReview> ProductReviews { get; set; }

    public virtual DbSet<RefundTransaction> RefundTransactions { get; set; }

    public virtual DbSet<RegionalPriceBenchmark> RegionalPriceBenchmarks { get; set; }

    public virtual DbSet<RetailCheckoutQuote> RetailCheckoutQuotes { get; set; }

    public virtual DbSet<RewardPointEntry> RewardPointEntries { get; set; }

    public virtual DbSet<SellerOrder> SellerOrders { get; set; }

    public virtual DbSet<Shipment> Shipments { get; set; }

    public virtual DbSet<ShipmentItem> ShipmentItems { get; set; }

    public virtual DbSet<TraceabilityQrcode> TraceabilityQrcodes { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<VDisputeEvidenceRequest> VDisputeEvidenceRequests { get; set; }

    public virtual DbSet<VInvoiceFund> VInvoiceFunds { get; set; }

    public virtual DbSet<VRewardPointBalance> VRewardPointBalances { get; set; }

    public virtual DbSet<Voucher> Vouchers { get; set; }

    public virtual DbSet<VoucherRedemption> VoucherRedemptions { get; set; }

    public virtual DbSet<WholesaleBuyerProfile> WholesaleBuyerProfiles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) { }
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        //=> optionsBuilder.UseSqlServer("Server=localhost;Database=AgriSustain;UId=sa;pwd=123;TrustServerCertificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("PK__AuditLog__EB5F6CBDEBB68346");

            entity.ToTable("AuditLog", tb => tb.HasTrigger("TR_AuditLog_AppendOnly"));

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
            entity.HasKey(e => e.B2bcontractId).HasName("PK__B2BContr__59105F1E7BBB5CF4");

            entity.ToTable("B2BContract", tb => tb.HasTrigger("TR_Contract_Write"));

            entity.HasIndex(e => e.ContractNumber, "UQ__B2BContr__C51D43DA3D1BBBD9").IsUnique();

            entity.Property(e => e.B2bcontractId).HasColumnName("B2BContractId");
            entity.Property(e => e.ContractNumber)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
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
            entity.HasKey(e => e.B2bsubscriptionId).HasName("PK__B2BSubsc__EE8E09CC4C9F4139");

            entity.ToTable("B2BSubscription", tb => tb.HasTrigger("TR_Subscription_Write"));

            entity.HasIndex(e => e.WholesaleBuyerProfileId, "UX_Subscription_Active")
                .IsUnique()
                .HasFilter("([SubscriptionStatus]='Active')");

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

            entity.HasOne(d => d.WholesaleBuyerProfile).WithOne(p => p.B2bsubscription)
                .HasForeignKey<B2bsubscription>(d => d.WholesaleBuyerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_B2BSubscription_WholesaleBuyer");
        });

        modelBuilder.Entity<B2bsubscriptionPackage>(entity =>
        {
            entity.HasKey(e => e.PackageId).HasName("PK__B2BSubsc__322035CC29DFA4C4");

            entity.ToTable("B2BSubscriptionPackage", tb => tb.HasTrigger("TR_B2BSubscriptionPackage_Validate"));

            entity.HasIndex(e => e.PackageName, "UQ_B2BSubscriptionPackage_PackageName").IsUnique();

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PackageDescription).HasMaxLength(1000);
            entity.Property(e => e.PackageName).HasMaxLength(150);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<BusinessPolicy>(entity =>
        {
            entity.HasKey(e => e.PolicyId);

            entity.ToTable("BusinessPolicy", tb => tb.HasTrigger("TR_BusinessPolicy_Validate"));

            entity.Property(e => e.PayoutDelayHours).HasDefaultValue(48);
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.CartId).HasName("PK__Cart__51BCD7B7477C1058");

            entity.ToTable("Cart", tb => tb.HasTrigger("TR_Cart_Validate"));

            entity.HasIndex(e => e.UserId, "UQ__Cart__1788CC4D31B80BAE").IsUnique();

            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.User).WithOne(p => p.Cart)
                .HasForeignKey<Cart>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Cart_User");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.CartItemId).HasName("PK__CartItem__488B0B0A9A476B47");

            entity.ToTable("CartItem", tb => tb.HasTrigger("TR_CartItem_Validate"));

            entity.HasIndex(e => new { e.CartId, e.ProductId, e.HarvestBatchId }, "UX_CartItem_Cart_Product_Batch")
                .IsUnique()
                .HasFilter("([HarvestBatchId] IS NOT NULL)");

            entity.HasIndex(e => new { e.CartId, e.ProductId, e.PreOrderCampaignId }, "UX_Cart_PreOrder")
                .IsUnique()
                .HasFilter("([HarvestBatchId] IS NULL AND [PreOrderCampaignId] IS NOT NULL)");

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

            entity.HasOne(d => d.HarvestBatch).WithMany(p => p.CartItems)
                .HasPrincipalKey(p => new { p.ProductId, p.HarvestBatchId })
                .HasForeignKey(d => new { d.ProductId, d.HarvestBatchId })
                .HasConstraintName("FK_CartItem_Product_HarvestBatch");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__Category__19093A0B9F2880E0");

            entity.ToTable("Category", tb => tb.HasTrigger("TR_Category_Validate"));

            entity.Property(e => e.CategoryName).HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.ParentCategory).WithMany(p => p.InverseParentCategory)
                .HasForeignKey(d => d.ParentCategoryId)
                .HasConstraintName("FK_Category_ParentCategory");
        });

        modelBuilder.Entity<CommissionPolicy>(entity =>
        {
            entity.HasKey(e => e.CommissionPolicyId).HasName("PK__Commissi__ACB43F57B11EE7AE");

            entity.ToTable("CommissionPolicy", tb => tb.HasTrigger("TR_CommissionPolicy_Validate"));

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

        modelBuilder.Entity<ContractDepositAllocation>(entity =>
        {
            entity.HasKey(e => e.ContractDepositAllocationId).HasName("PK__Contract__07935F1B509A7C51");

            entity.ToTable("ContractDepositAllocation", tb => tb.HasTrigger("TR_DepositAllocation_AppendOnly"));

            entity.HasIndex(e => new { e.ContractDepositAllocationId, e.PaymentTransactionId }, "UQ_DepositAllocation_Payment").IsUnique();

            entity.HasIndex(e => e.RequestKey, "UQ__Contract__F6AC419D33161667").IsUnique();

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.RequestKey)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.ContractMilestone).WithMany(p => p.ContractDepositAllocations)
                .HasForeignKey(d => d.ContractMilestoneId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ContractD__Contr__5772F790");

            entity.HasOne(d => d.PaymentTransaction).WithMany(p => p.ContractDepositAllocations)
                .HasForeignKey(d => d.PaymentTransactionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ContractD__Payme__567ED357");
        });

        modelBuilder.Entity<ContractLine>(entity =>
        {
            entity.HasKey(e => e.ContractLineId).HasName("PK__Contract__905F6E87DE77B4DB");

            entity.ToTable("ContractLine", tb => tb.HasTrigger("TR_ContractLine_Write"));

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

            entity.HasOne(d => d.ContractVersion).WithMany(p => p.ContractLines)
                .HasForeignKey(d => d.ContractVersionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContractLine_Version");

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
            entity.HasKey(e => e.ContractMilestoneId).HasName("PK__Contract__9CD091DC5D46733A");

            entity.ToTable("ContractMilestone", tb => tb.HasTrigger("TR_ContractMilestone_Write"));

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

            entity.HasOne(d => d.ContractVersion).WithMany(p => p.ContractMilestones)
                .HasForeignKey(d => d.ContractVersionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Milestone_Version");
        });

        modelBuilder.Entity<ContractSignature>(entity =>
        {
            entity.HasKey(e => e.ContractSignatureId).HasName("PK__Contract__6A8CC220260D5F11");

            entity.ToTable("ContractSignature", tb => tb.HasTrigger("TR_ContractSignature_Write"));

            entity.HasIndex(e => new { e.ContractVersionId, e.PartyRole }, "UX_Signature_Party").IsUnique();

            entity.HasIndex(e => new { e.ContractVersionId, e.SignedByUserId }, "UX_Signature_User").IsUnique();

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
            entity.HasKey(e => e.ContractTerminationRequestId).HasName("PK__Contract__B98B7390DB0B51CD");

            entity.ToTable("ContractTerminationRequest", tb => tb.HasTrigger("TR_ContractTerminationRequest_Validate"));

            entity.HasIndex(e => e.B2bcontractId, "UX_Termination_Pending")
                .IsUnique()
                .HasFilter("([RequestStatus]='Pending')");

            entity.Property(e => e.B2bcontractId).HasColumnName("B2BContractId");
            entity.Property(e => e.EvidenceUrl).HasMaxLength(500);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.RequestStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending");
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.SettlementAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.B2bcontract).WithOne(p => p.ContractTerminationRequest)
                .HasForeignKey<ContractTerminationRequest>(d => d.B2bcontractId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TerminationRequest_B2BContract");

            entity.HasOne(d => d.RequestedByUser).WithMany(p => p.ContractTerminationRequests)
                .HasForeignKey(d => d.RequestedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TerminationRequest_RequestedByUser");
        });

        modelBuilder.Entity<ContractVersion>(entity =>
        {
            entity.HasKey(e => e.ContractVersionId).HasName("PK__Contract__54F2943327C0B491");

            entity.ToTable("ContractVersion", tb => tb.HasTrigger("TR_ContractVersion_Write"));

            entity.HasIndex(e => new { e.B2bcontractId, e.VersionNo }, "UQ_ContractVersion_Contract_VersionNo").IsUnique();

            entity.Property(e => e.B2bcontractId).HasColumnName("B2BContractId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
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
            entity.HasKey(e => e.CropSeasonId).HasName("PK__CropSeas__675AF5007DF309C1");

            entity.ToTable("CropSeason", tb => tb.HasTrigger("TR_CropSeason_Validate"));

            entity.Property(e => e.ActualYield).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.CropVariety).HasMaxLength(150);
            entity.Property(e => e.CultivatedArea).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.EstimatedYield).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.SeasonName).HasMaxLength(200);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Planning");
            entity.Property(e => e.YieldUnit).HasMaxLength(50);

            entity.HasOne(d => d.FarmerProfile).WithMany(p => p.CropSeasons)
                .HasForeignKey(d => d.FarmerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CropSeason_FarmerProfile");

            entity.HasOne(d => d.Product).WithMany(p => p.CropSeasons)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CropSeason_Product");
        });

        modelBuilder.Entity<CultivationLog>(entity =>
        {
            entity.HasKey(e => e.CultivationLogId).HasName("PK__Cultivat__60C48ADFB5E91593");

            entity.ToTable("CultivationLog", tb => tb.HasTrigger("TR_CultivationLog_AppendOnly"));

            entity.Property(e => e.ActionType).HasMaxLength(100);
            entity.Property(e => e.CorrectionReason).HasMaxLength(500);
            entity.Property(e => e.DetailDescription).HasMaxLength(1000);
            entity.Property(e => e.Dosage).HasMaxLength(150);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.InputMaterial).HasMaxLength(255);
            entity.Property(e => e.PerformedBy).HasMaxLength(150);
            entity.Property(e => e.RecordedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.CorrectsLog).WithMany(p => p.InverseCorrectsLog)
                .HasForeignKey(d => d.CorrectsLogId)
                .HasConstraintName("FK_CultivationLog_Correction");

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
            entity.HasKey(e => e.DeliveryAddressId).HasName("PK__Delivery__4EBD54FCAEA037AF");

            entity.ToTable("DeliveryAddress", tb => tb.HasTrigger("TR_DeliveryAddress_Validate"));

            entity.HasIndex(e => e.UserId, "UX_DeliveryAddress_OneDefaultPerUser")
                .IsUnique()
                .HasFilter("([IsDefault]=(1))");

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

            entity.HasOne(d => d.User).WithOne(p => p.DeliveryAddress)
                .HasForeignKey<DeliveryAddress>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DeliveryAddress_User");
        });

        modelBuilder.Entity<DisputeEvidence>(entity =>
        {
            entity.HasKey(e => e.DisputeEvidenceId).HasName("PK__DisputeE__B2461231997E7DB9");

            entity.ToTable("DisputeEvidence", tb => tb.HasTrigger("TR_Evidence_Write"));

            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.SubmittedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.DisputeTicket).WithMany(p => p.DisputeEvidences)
                .HasForeignKey(d => d.DisputeTicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DisputeEvidence_DisputeTicket");

            entity.HasOne(d => d.EvidenceRequest).WithMany(p => p.DisputeEvidences)
                .HasForeignKey(d => d.EvidenceRequestId)
                .HasConstraintName("FK_Evidence_Request");

            entity.HasOne(d => d.SubmittedByUser).WithMany(p => p.DisputeEvidences)
                .HasForeignKey(d => d.SubmittedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DisputeEvidence_SubmittedByUser");
        });

        modelBuilder.Entity<DisputeEvidenceRequest>(entity =>
        {
            entity.HasKey(e => e.EvidenceRequestId).HasName("PK__DisputeE__86E44B409DCF502C");

            entity.ToTable("DisputeEvidenceRequest", tb => tb.HasTrigger("TR_EvidenceRequest_Write"));

            entity.Property(e => e.DueAt).HasComputedColumnSql("(dateadd(hour,(48),[RequestedAt]))", true);
            entity.Property(e => e.Instructions).HasMaxLength(1000);
            entity.Property(e => e.RequestStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Pending");
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.DisputeTicket).WithMany(p => p.DisputeEvidenceRequests)
                .HasForeignKey(d => d.DisputeTicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DisputeEv__Dispu__76EBA2E9");

            entity.HasOne(d => d.RequestedByUser).WithMany(p => p.DisputeEvidenceRequestRequestedByUsers)
                .HasForeignKey(d => d.RequestedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DisputeEv__Reque__78D3EB5B");

            entity.HasOne(d => d.RequestedPartyUser).WithMany(p => p.DisputeEvidenceRequestRequestedPartyUsers)
                .HasForeignKey(d => d.RequestedPartyUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DisputeEv__Reque__77DFC722");
        });

        modelBuilder.Entity<DisputeTicket>(entity =>
        {
            entity.HasKey(e => e.DisputeTicketId).HasName("PK__DisputeT__3C3F9CFEDBA79541");

            entity.ToTable("DisputeTicket", tb => tb.HasTrigger("TR_DisputeTicket_Validate"));

            entity.Property(e => e.B2bcontractId).HasColumnName("B2BContractId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
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
            entity.HasKey(e => e.FarmCertificateId).HasName("PK__FarmCert__2BDF0082B0EF348B");

            entity.ToTable("FarmCertificate", tb => tb.HasTrigger("TR_FarmCertificate_Validate"));

            entity.HasIndex(e => new { e.CertificateType, e.CertificateNumber }, "UQ_FarmCertificate_Type_Number").IsUnique();

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
            entity.HasKey(e => e.FarmerPayoutId).HasName("PK__FarmerPa__80AAF9C346B5F80F");

            entity.ToTable("FarmerPayout", tb => tb.HasTrigger("TR_FarmerPayout_Write"));

            entity.HasIndex(e => e.PayoutCode, "UQ__FarmerPa__3F2306E3E98F526C").IsUnique();

            entity.HasIndex(e => e.RequestKey, "UQ__FarmerPa__F6AC419D6087B004").IsUnique();

            entity.HasIndex(e => e.BankTransferRef, "UX_Payout_BankRef")
                .IsUnique()
                .HasFilter("([BankTransferRef] IS NOT NULL)");

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
            entity.Property(e => e.RequestKey)
                .HasMaxLength(100)
                .IsUnicode(false);

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
            entity.HasKey(e => e.FarmerProfileId).HasName("PK__FarmerPr__688AB36DFA9EF075");

            entity.ToTable("FarmerProfile", tb => tb.HasTrigger("TR_FarmerProfile_Validate"));

            entity.HasIndex(e => e.UserId, "UQ__FarmerPr__1788CC4D8B016AF1").IsUnique();

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
            entity.HasKey(e => e.HarvestBatchId).HasName("PK__HarvestB__619519FBF28D519C");

            entity.ToTable("HarvestBatch", tb => tb.HasTrigger("TR_HarvestBatch_Validate"));

            entity.HasIndex(e => new { e.ProductId, e.Status, e.ExpirationDate, e.HarvestBatchId }, "IX_HarvestBatch_Fefo");

            entity.HasIndex(e => new { e.ProductId, e.HarvestBatchId }, "UQ_HarvestBatch_Product_Batch").IsUnique();

            entity.HasIndex(e => e.BatchCode, "UQ__HarvestB__B22ADA8E8FADDF47").IsUnique();

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
                .HasDefaultValue("Planned");

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
            entity.HasKey(e => e.InventoryMovementId).HasName("PK__Inventor__DB9462B66F1AE9CB");

            entity.ToTable("InventoryMovement", tb => tb.HasTrigger("TR_InventoryMovement_Write"));

            entity.HasIndex(e => e.RequestKey, "UQ__Inventor__F6AC419DCEE7C1CE").IsUnique();

            entity.HasIndex(e => e.OrderItemBatchAllocationId, "UX_Movement_Dispatch")
                .IsUnique()
                .HasFilter("([MovementType]='Dispatch' AND [OrderItemBatchAllocationId] IS NOT NULL)");

            entity.Property(e => e.MovementType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.QuantityChange).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.RequestKey)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.HarvestBatch).WithMany(p => p.InventoryMovements)
                .HasForeignKey(d => d.HarvestBatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InventoryMovement_HarvestBatch");

            entity.HasOne(d => d.OrderItemBatchAllocation).WithOne(p => p.InventoryMovement)
                .HasForeignKey<InventoryMovement>(d => d.OrderItemBatchAllocationId)
                .HasConstraintName("FK_InventoryMovement_BatchAllocation");

            entity.HasOne(d => d.RecordedByUser).WithMany(p => p.InventoryMovements)
                .HasForeignKey(d => d.RecordedByUserId)
                .HasConstraintName("FK_InventoryMovement_RecordedByUser");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId).HasName("PK__Invoice__D796AAB526E64347");

            entity.ToTable("Invoice", tb => tb.HasTrigger("TR_Invoice_Validate"));

            entity.HasIndex(e => e.InvoiceNumber, "UQ__Invoice__D776E9815502D38A").IsUnique();

            entity.Property(e => e.B2bsubscriptionId).HasColumnName("B2BSubscriptionId");
            entity.Property(e => e.InvoiceNumber)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.InvoiceStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Draft");
            entity.Property(e => e.IssuedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.B2bsubscription).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.B2bsubscriptionId)
                .HasConstraintName("FK_Invoice_Subscription");

            entity.HasOne(d => d.ContractMilestone).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.ContractMilestoneId)
                .HasConstraintName("FK_Invoice_ContractMilestone");

            entity.HasOne(d => d.SellerOrder).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.SellerOrderId)
                .HasConstraintName("FK_Invoice_SellerOrder");
        });

        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            entity.HasKey(e => e.InvoiceLineId).HasName("PK__InvoiceL__0D760AD96EA72F75");

            entity.ToTable("InvoiceLine", tb => tb.HasTrigger("TR_InvoiceLine_Write"));

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
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E12BBC7A3FA");

            entity.ToTable("Notification", tb => tb.HasTrigger("TR_Notification_Validate"));

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
            entity.Property(e => e.SentAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Title).HasMaxLength(255);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notification_User");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Order__C3905BCFB2FB2F0F");

            entity.ToTable("Order", tb => tb.HasTrigger("TR_Order_Write"));

            entity.HasIndex(e => new { e.BuyerUserId, e.CheckoutRequestKey }, "UQ_Order_CheckoutRequest").IsUnique();

            entity.HasIndex(e => e.OrderCode, "UQ__Order__999B5229A8D719A9").IsUnique();

            entity.HasIndex(e => e.RetailQuoteId, "UX_Order_Quote")
                .IsUnique()
                .HasFilter("([RetailQuoteId] IS NOT NULL)");

            entity.Property(e => e.AddressCatalogVersionSnapshot)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.AddressDetailSnapshot).HasMaxLength(255);
            entity.Property(e => e.CheckoutRequestKey)
                .HasMaxLength(100)
                .IsUnicode(false);
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

            entity.HasOne(d => d.RetailCheckoutQuote).WithMany(p => p.Orders)
                .HasPrincipalKey(p => new { p.BuyerUserId, p.QuoteId })
                .HasForeignKey(d => new { d.BuyerUserId, d.RetailQuoteId })
                .HasConstraintName("FK_Order_RetailQuote_Buyer");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.OrderItemId).HasName("PK__OrderIte__57ED0681BE5BD3A4");

            entity.ToTable("OrderItem", tb => tb.HasTrigger("TR_OrderItem_Write"));

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

            entity.HasOne(d => d.HarvestBatch).WithMany(p => p.OrderItems)
                .HasPrincipalKey(p => new { p.ProductId, p.HarvestBatchId })
                .HasForeignKey(d => new { d.ProductId, d.HarvestBatchId })
                .HasConstraintName("FK_OrderItem_SelectedBatch");
        });

        modelBuilder.Entity<OrderItemBatchAllocation>(entity =>
        {
            entity.HasKey(e => e.OrderItemBatchAllocationId).HasName("PK__OrderIte__51B745BBDEE9E233");

            entity.ToTable("OrderItemBatchAllocation", tb => tb.HasTrigger("TR_Allocation_Write"));

            entity.HasIndex(e => new { e.OrderItemId, e.HarvestBatchId }, "UX_Allocation_Item_Batch").IsUnique();

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
            entity.HasKey(e => e.OrderStatusHistoryId).HasName("PK__OrderSta__D16EDBC3AF9F5DB0");

            entity.ToTable("OrderStatusHistory", tb => tb.HasTrigger("TR_OrderHistory_AppendOnly"));

            entity.Property(e => e.NewStatus)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("(sysutcdatetime())");
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

        modelBuilder.Entity<PaymentGatewayEvent>(entity =>
        {
            entity.HasKey(e => e.PaymentGatewayEventId).HasName("PK__PaymentG__A143F7694B7E708A");

            entity.ToTable("PaymentGatewayEvent", tb => tb.HasTrigger("TR_PaymentEvent_Write"));

            entity.HasIndex(e => new { e.GatewayProvider, e.ProviderEventId }, "UQ_GatewayEvent").IsUnique();

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.EventStatus)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.GatewayProvider)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PayloadHash)
                .HasMaxLength(64)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.ProviderEventId)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.ProviderTransactionReference)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.ReceivedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.PaymentTransaction).WithMany(p => p.PaymentGatewayEvents)
                .HasForeignKey(d => d.PaymentTransactionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PaymentGa__Payme__67A95F59");
        });

        modelBuilder.Entity<PaymentInvoiceAllocation>(entity =>
        {
            entity.HasKey(e => e.PaymentInvoiceAllocationId).HasName("PK__PaymentI__B20509AC543F2142");

            entity.ToTable("PaymentInvoiceAllocation", tb => tb.HasTrigger("TR_PaymentAllocation_AppendOnly"));

            entity.HasIndex(e => e.InvoiceId, "IX_PaymentAllocation_Invoice");

            entity.HasIndex(e => e.PaymentTransactionId, "IX_PaymentAllocation_Payment");

            entity.HasIndex(e => new { e.PaymentInvoiceAllocationId, e.PaymentTransactionId }, "UQ_PaymentAllocation_Payment").IsUnique();

            entity.HasIndex(e => e.RequestKey, "UQ__PaymentI__F6AC419D49E398A7").IsUnique();

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.RequestKey)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.ContractDepositAllocation).WithMany(p => p.PaymentInvoiceAllocations)
                .HasForeignKey(d => d.ContractDepositAllocationId)
                .HasConstraintName("FK__PaymentIn__Contr__60FC61CA");

            entity.HasOne(d => d.Invoice).WithMany(p => p.PaymentInvoiceAllocations)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PaymentIn__Invoi__5F141958");

            entity.HasOne(d => d.PaymentTransaction).WithMany(p => p.PaymentInvoiceAllocations)
                .HasForeignKey(d => d.PaymentTransactionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PaymentIn__Payme__5E1FF51F");
        });

        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.HasKey(e => e.PaymentTransactionId).HasName("PK__PaymentT__C22AEFE0CB95DD00");

            entity.ToTable("PaymentTransaction", tb => tb.HasTrigger("TR_PaymentTransaction_Write"));

            entity.HasIndex(e => new { e.GatewayProvider, e.RequestKey }, "UQ_Payment_Request").IsUnique();

            entity.HasIndex(e => e.TransactionCode, "UQ__PaymentT__D85E7026882D368C").IsUnique();

            entity.HasIndex(e => new { e.GatewayProvider, e.ProviderTransactionReference }, "UX_Payment_ProviderRef")
                .IsUnique()
                .HasFilter("([ProviderTransactionReference] IS NOT NULL)");

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
            entity.Property(e => e.ProviderTransactionReference)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.RequestKey)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("(sysutcdatetime())");
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
            entity.HasKey(e => e.PayoutItemId).HasName("PK__PayoutIt__3414EE9E7CA0AC22");

            entity.ToTable("PayoutItem", tb => tb.HasTrigger("TR_PayoutItem_Write"));

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

            entity.HasOne(d => d.InvoiceLine).WithMany(p => p.PayoutItems)
                .HasForeignKey(d => d.InvoiceLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PayoutItem_InvoiceLine");

            entity.HasOne(d => d.OrderItem).WithMany(p => p.PayoutItems)
                .HasForeignKey(d => d.OrderItemId)
                .HasConstraintName("FK_PayoutItem_OrderItem");
        });

        modelBuilder.Entity<PreOrderCampaign>(entity =>
        {
            entity.HasKey(e => e.PreOrderCampaignId).HasName("PK__PreOrder__718DDA50E6D25429");

            entity.ToTable("PreOrderCampaign", tb => tb.HasTrigger("TR_PreOrderCampaign_Validate"));

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
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PreOrderCampaign_CropSeason");

            entity.HasOne(d => d.Product).WithMany(p => p.PreOrderCampaigns)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PreOrderCampaign_Product");
        });

        modelBuilder.Entity<PreOrderReservation>(entity =>
        {
            entity.HasKey(e => e.PreOrderReservationId).HasName("PK__PreOrder__941AE6E1E1B8D689");

            entity.ToTable("PreOrderReservation", tb => tb.HasTrigger("TR_PreOrderReservation_Write"));

            entity.HasIndex(e => new { e.PreOrderCampaignId, e.ReservationStatus }, "IX_PreOrder_Capacity");

            entity.HasIndex(e => e.OrderItemId, "UQ__PreOrder__57ED068071EAEEA3").IsUnique();

            entity.HasIndex(e => e.RequestKey, "UQ__PreOrder__F6AC419D6B0D8361").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DepositPercentageSnapshot).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.PriceSnapshot).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.RequestKey)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ReservationStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Reserved");
            entity.Property(e => e.UnitSnapshot).HasMaxLength(50);

            entity.HasOne(d => d.OrderItem).WithOne(p => p.PreOrderReservation)
                .HasForeignKey<PreOrderReservation>(d => d.OrderItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PreOrderR__Order__4A18FC72");

            entity.HasOne(d => d.PreOrderCampaign).WithMany(p => p.PreOrderReservations)
                .HasForeignKey(d => d.PreOrderCampaignId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PreOrderR__PreOr__4B0D20AB");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PK__Product__B40CC6CD4E506CFE");

            entity.ToTable("Product", tb => tb.HasTrigger("TR_Product_Validate"));

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
            entity.HasKey(e => e.ProductImageId).HasName("PK__ProductI__07B2B1B88DF54735");

            entity.ToTable("ProductImage", tb => tb.HasTrigger("TR_ProductImage_Validate"));

            entity.HasIndex(e => e.ProductId, "UX_ProductImage_OnePrimaryPerProduct")
                .IsUnique()
                .HasFilter("([IsPrimary]=(1))");

            entity.Property(e => e.AltText).HasMaxLength(255);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductImage_Product");
        });

        modelBuilder.Entity<ProductReview>(entity =>
        {
            entity.HasKey(e => e.ProductReviewId).HasName("PK__ProductR__39631880A597FAE7");

            entity.ToTable("ProductReview", tb => tb.HasTrigger("TR_ProductReview_Validate"));

            entity.HasIndex(e => e.OrderItemId, "UQ__ProductR__57ED0680AE799D83").IsUnique();

            entity.Property(e => e.Comment).HasMaxLength(1000);
            entity.Property(e => e.IsPurchasedVerified).HasDefaultValue(true);
            entity.Property(e => e.ModerationReason).HasMaxLength(500);
            entity.Property(e => e.ModerationStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Approved");
            entity.Property(e => e.ReviewDate).HasDefaultValueSql("(sysutcdatetime())");

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
            entity.HasKey(e => e.RefundTransactionId).HasName("PK__RefundTr__4745915E127673BE");

            entity.ToTable("RefundTransaction", tb => tb.HasTrigger("TR_RefundTransaction_Write"));

            entity.HasIndex(e => e.RefundCode, "UQ__RefundTr__7DFE7F497899A6E4").IsUnique();

            entity.HasIndex(e => e.RequestKey, "UQ__RefundTr__F6AC419D4C5118D2").IsUnique();

            entity.HasIndex(e => e.GatewayReference, "UX_Refund_ProviderRef")
                .IsUnique()
                .HasFilter("([GatewayReference] IS NOT NULL)");

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
            entity.Property(e => e.RequestKey)
                .HasMaxLength(100)
                .IsUnicode(false);

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

            entity.HasOne(d => d.ContractDepositAllocation).WithMany(p => p.RefundTransactions)
                .HasPrincipalKey(p => new { p.ContractDepositAllocationId, p.PaymentTransactionId })
                .HasForeignKey(d => new { d.ContractDepositAllocationId, d.PaymentTransactionId })
                .HasConstraintName("FK_Refund_DepositPayment");

            entity.HasOne(d => d.PaymentInvoiceAllocation).WithMany(p => p.RefundTransactions)
                .HasPrincipalKey(p => new { p.PaymentInvoiceAllocationId, p.PaymentTransactionId })
                .HasForeignKey(d => new { d.PaymentInvoiceAllocationId, d.PaymentTransactionId })
                .HasConstraintName("FK_Refund_AllocatedPayment");
        });

        modelBuilder.Entity<RegionalPriceBenchmark>(entity =>
        {
            entity.HasKey(e => e.BenchmarkId).HasName("PK__Regional__2CE9029CE70A5E0A");

            entity.ToTable("RegionalPriceBenchmark", tb => tb.HasTrigger("TR_RegionalPriceBenchmark_Validate"));

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
            entity.HasKey(e => e.QuoteId).HasName("PK__RetailCh__AF9688C15E30EAE0");

            entity.ToTable("RetailCheckoutQuote", tb => tb.HasTrigger("TR_Quote_AppendOnly"));

            entity.HasIndex(e => new { e.BuyerUserId, e.ExpiresAt }, "IX_RetailQuote_Buyer_Expiry");

            entity.HasIndex(e => new { e.BuyerUserId, e.QuoteId }, "UQ_RetailCheckoutQuote_Buyer_Quote").IsUnique();

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

        modelBuilder.Entity<RewardPointEntry>(entity =>
        {
            entity.HasKey(e => e.RewardPointEntryId).HasName("PK__RewardPo__3B7DF33DB11A3529");

            entity.ToTable("RewardPointEntry", tb => tb.HasTrigger("TR_Reward_AppendOnly"));

            entity.HasIndex(e => new { e.OrderId, e.EntryType }, "IX_Reward_Order");

            entity.HasIndex(e => e.UserId, "IX_Reward_User");

            entity.HasIndex(e => e.RequestKey, "UQ__RewardPo__F6AC419D35FB1DC3").IsUnique();

            entity.Property(e => e.AvailableDelta).HasComputedColumnSql("(CONVERT([bigint],case when [EntryType]='Refund' OR [EntryType]='Release' OR [EntryType]='Earn' then [Points] when [EntryType]='Hold' then  -CONVERT([bigint],[Points]) else (0) end))", true);
            entity.Property(e => e.EntryType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.HeldDelta).HasComputedColumnSql("(CONVERT([bigint],case when [EntryType]='Hold' then [Points] when [EntryType]='Release' OR [EntryType]='Consume' then  -CONVERT([bigint],[Points]) else (0) end))", true);
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.RequestKey)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.Order).WithMany(p => p.RewardPointEntries)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK__RewardPoi__Order__6F4A8121");

            entity.HasOne(d => d.User).WithMany(p => p.RewardPointEntries)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RewardPoi__UserI__6E565CE8");
        });

        modelBuilder.Entity<SellerOrder>(entity =>
        {
            entity.HasKey(e => e.SellerOrderId).HasName("PK__SellerOr__EE1F8C1422E642FE");

            entity.ToTable("SellerOrder", tb => tb.HasTrigger("TR_SellerOrder_Write"));

            entity.HasIndex(e => new { e.OrderId, e.FarmerProfileId }, "UQ_SellerOrder_Order_Farmer").IsUnique();

            entity.HasIndex(e => e.SellerOrderCode, "UQ__SellerOr__784ABE9CCA75C310").IsUnique();

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
            entity.HasKey(e => e.ShipmentId).HasName("PK__Shipment__5CAD37ED58E69320");

            entity.ToTable("Shipment", tb => tb.HasTrigger("TR_Shipment_Validate"));

            entity.HasIndex(e => e.TrackingNumber, "UX_Shipment_TrackingNumber_NotNull")
                .IsUnique()
                .HasFilter("([TrackingNumber] IS NOT NULL)");

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
            entity.HasKey(e => e.ShipmentItemId).HasName("PK__Shipment__E46A9F6E289320EF");

            entity.ToTable("ShipmentItem", tb => tb.HasTrigger("TR_ShipmentItem_Validate"));

            entity.HasIndex(e => new { e.ShipmentId, e.OrderItemBatchAllocationId }, "UX_Shipment_Allocation").IsUnique();

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
            entity.HasKey(e => e.TraceabilityQrcodeId).HasName("PK__Traceabi__EC0CD854820BDC60");

            entity.ToTable("TraceabilityQRCode", tb => tb.HasTrigger("TR_TraceabilityQRCode_Validate"));

            entity.HasIndex(e => e.QrcontentString, "UQ__Traceabi__1DCFB785E9DC0DAD").IsUnique();

            entity.HasIndex(e => e.HarvestBatchId, "UQ__Traceabi__619519FAC08107BD").IsUnique();

            entity.Property(e => e.TraceabilityQrcodeId).HasColumnName("TraceabilityQRCodeId");
            entity.Property(e => e.GeneratedAt).HasDefaultValueSql("(sysutcdatetime())");
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
            entity.HasKey(e => e.UserId).HasName("PK__User__1788CC4C1EE96F0D");

            entity.ToTable("User", tb => tb.HasTrigger("TR_User_Validate"));

            entity.HasIndex(e => e.Username, "UQ__User__536C85E46FD72E95").IsUnique();

            entity.HasIndex(e => e.Email, "UQ__User__A9D10534209BB63E").IsUnique();

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

        modelBuilder.Entity<VDisputeEvidenceRequest>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vDisputeEvidenceRequest");

            entity.Property(e => e.EffectiveStatus)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.EvidenceRequestId).ValueGeneratedOnAdd();
            entity.Property(e => e.Instructions).HasMaxLength(1000);
        });

        modelBuilder.Entity<VInvoiceFund>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vInvoiceFunds");

            entity.Property(e => e.CollectedAmount).HasColumnType("decimal(38, 2)");
            entity.Property(e => e.InvoiceId).ValueGeneratedOnAdd();
            entity.Property(e => e.PayoutReservedAmount).HasColumnType("decimal(38, 2)");
            entity.Property(e => e.RefundReservedAmount).HasColumnType("decimal(38, 2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<VRewardPointBalance>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vRewardPointBalance");
        });

        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.VoucherId).HasName("PK__Voucher__3AEE7921FD2C713E");

            entity.ToTable("Voucher", tb => tb.HasTrigger("TR_Voucher_Validate"));

            entity.HasIndex(e => e.VoucherCode, "UQ__Voucher__7F0ABCA9ACEF6638").IsUnique();

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
            entity.HasKey(e => e.VoucherRedemptionId).HasName("PK__VoucherR__140B18225B5923B2");

            entity.ToTable("VoucherRedemption", tb => tb.HasTrigger("TR_VoucherRedemption_Write"));

            entity.HasIndex(e => new { e.VoucherId, e.Status, e.ExpiresAt }, "IX_VoucherRedemption_Quota");

            entity.HasIndex(e => new { e.VoucherId, e.OrderId, e.SellerOrderId }, "UX_Voucher_Order").IsUnique();

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
            entity.HasKey(e => e.WholesaleBuyerProfileId).HasName("PK__Wholesal__5F36EBC606ECC49B");

            entity.ToTable("WholesaleBuyerProfile", tb => tb.HasTrigger("TR_WholesaleBuyerProfile_Validate"));

            entity.HasIndex(e => e.TaxCode, "UQ__Wholesal__12945A2895F1085B").IsUnique();

            entity.HasIndex(e => e.UserId, "UQ__Wholesal__1788CC4D5A8DFD28").IsUnique();

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

