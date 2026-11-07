using AgriSustain.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Data;

public partial class AgriSustainContext
{
    public DbSet<CategorySettings> CategorySettings => Set<CategorySettings>();
    public DbSet<FeeType> FeeTypes => Set<FeeType>();
    public DbSet<FeePolicy> FeePolicies => Set<FeePolicy>();
    public DbSet<FeePolicyCategory> FeePolicyCategories => Set<FeePolicyCategory>();
    public DbSet<OrderItemFee> OrderItemFees => Set<OrderItemFee>();

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // The deployed schema links order items to batches through allocations only.
        modelBuilder.Entity<OrderItem>().Ignore(x => x.HarvestBatch);
        modelBuilder.Entity<HarvestBatch>().Ignore(x => x.OrderItems);
        modelBuilder.Entity<OrderItem>().Ignore(x => x.HarvestBatchId);
        // Legacy orders have no checkout request key.
        modelBuilder.Entity<Order>().Property(x => x.CheckoutRequestKey).IsRequired(false);
        modelBuilder.Entity<Order>()
            .HasIndex(x => new { x.BuyerUserId, x.CheckoutRequestKey }, "UQ_Order_CheckoutRequest")
            .IsUnique().HasDatabaseName("UQ_Order_CheckoutRequest")
            .HasFilter("[CheckoutRequestKey] IS NOT NULL");
        modelBuilder.Entity<CategorySettings>(entity =>
        {
            entity.ToTable("CategorySettings");
            entity.HasKey(x => x.CategoryId);
            entity.Property(x => x.CategoryId).ValueGeneratedNever();
            entity.HasOne(x => x.Category).WithOne()
                .HasForeignKey<CategorySettings>(x => x.CategoryId);
        });
        // Categories are flat (ParentCategoryId is always NULL), so this enforces global name uniqueness.
        modelBuilder.Entity<Category>().HasIndex(x => new { x.ParentCategoryId, x.CategoryName })
            .IsUnique().HasDatabaseName("UX_Category_Parent_Name").HasFilter(null);

        // Optimistic concurrency for administration, checkout and commerce writes.
        modelBuilder.Entity<User>().Property(x => x.Status).IsConcurrencyToken();
        modelBuilder.Entity<User>().Property(x => x.LockedUntil).IsConcurrencyToken();
        modelBuilder.Entity<User>().Property(x => x.FailedLoginCount).IsConcurrencyToken();
        modelBuilder.Entity<Category>().Property(x => x.CategoryName).IsConcurrencyToken();
        modelBuilder.Entity<Category>().Property(x => x.IsActive).IsConcurrencyToken();
        modelBuilder.Entity<HarvestBatch>().Property(x => x.AvailableQuantity).IsConcurrencyToken();
        modelBuilder.Entity<HarvestBatch>().Property(x => x.ReservedQuantity).IsConcurrencyToken();
        modelBuilder.Entity<Order>().Property(x => x.OrderStatus).IsConcurrencyToken();
        modelBuilder.Entity<SellerOrder>().Property(x => x.Status).IsConcurrencyToken();
        modelBuilder.Entity<Product>().Property(x => x.ApprovalStatus).IsConcurrencyToken();
        modelBuilder.Entity<Product>().Property(x => x.IsPublished).IsConcurrencyToken();
        modelBuilder.Entity<Cart>().Property(x => x.UpdatedAt).IsConcurrencyToken();
        modelBuilder.Entity<DisputeEvidenceRequest>()
            .Property(x => x.RequestStatus)
            .IsConcurrencyToken();
        modelBuilder.Entity<DisputeTicket>().Property(x => x.TicketStatus).IsConcurrencyToken();
        modelBuilder.Entity<RefundTransaction>().Property(x => x.RefundStatus).IsConcurrencyToken();
        modelBuilder.Entity<RefundTransaction>().Property(x => x.RefundMethod)
            .HasMaxLength(50)
            .IsUnicode(false);
        ConfigurePlatformFees(modelBuilder);
    }

    // UC-FIN-05 tables (Database/Update_FIN05_PlatformFees.sql).
    private static void ConfigurePlatformFees(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FeeType>(entity =>
        {
            entity.ToTable("FeeType");
            entity.HasKey(x => x.FeeTypeId);
            entity.HasIndex(x => x.FeeCode, "UQ_FeeType_FeeCode").IsUnique();
            entity.Property(x => x.FeeCode).HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.FeeName).HasMaxLength(100);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.FeeGroup).HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.RefundMode).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
        });
        modelBuilder.Entity<FeePolicy>(entity =>
        {
            entity.ToTable("FeePolicy");
            entity.HasKey(x => x.FeePolicyId);
            entity.HasIndex(x => x.PolicyCode, "UQ_FeePolicy_PolicyCode").IsUnique();
            entity.HasIndex(x => x.RequestKey, "UQ_FeePolicy_RequestKey").IsUnique();
            entity.HasIndex(x => new { x.FeeTypeId, x.OrderType, x.ApprovalStatus }, "IX_FeePolicy_Lookup");
            entity.Property(x => x.PolicyCode).HasMaxLength(40).IsUnicode(false);
            entity.Property(x => x.Method).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.CalculationBase).HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.Rate).HasColumnType("decimal(7, 4)");
            entity.Property(x => x.FixedAmount).HasColumnType("decimal(18, 2)");
            entity.Property(x => x.MinAmount).HasColumnType("decimal(18, 2)");
            entity.Property(x => x.MaxAmount).HasColumnType("decimal(18, 2)");
            entity.Property(x => x.OrderType).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.ApprovalStatus).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.Property(x => x.ReviewNote).HasMaxLength(500);
            entity.Property(x => x.RequestKey).HasMaxLength(100).IsUnicode(false);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasOne(x => x.FeeType).WithMany(x => x.Policies).HasForeignKey(x => x.FeeTypeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.ApprovedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<FeePolicyCategory>(entity =>
        {
            entity.ToTable("FeePolicyCategory");
            entity.HasKey(x => new { x.FeePolicyId, x.CategoryId });
            entity.HasOne(x => x.FeePolicy).WithMany(x => x.Categories).HasForeignKey(x => x.FeePolicyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<OrderItemFee>(entity =>
        {
            entity.ToTable("OrderItemFee");
            entity.HasKey(x => x.OrderItemFeeId);
            entity.HasIndex(x => x.OrderItemId, "IX_OrderItemFee_OrderItem");
            entity.Property(x => x.FeeCode).HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.FeeGroup).HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.RefundMode).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.RateSnapshot).HasColumnType("decimal(7, 4)");
            entity.Property(x => x.BaseAmount).HasColumnType("decimal(18, 2)");
            entity.Property(x => x.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(x => x.ReversedAmount).HasColumnType("decimal(18, 2)");
            entity.Ignore(x => x.NetAmount);
            entity.HasOne(x => x.OrderItem).WithMany(x => x.Fees).HasForeignKey(x => x.OrderItemId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<FeeType>().WithMany().HasForeignKey(x => x.FeeTypeId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<FeePolicy>().WithMany().HasForeignKey(x => x.FeePolicyId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
