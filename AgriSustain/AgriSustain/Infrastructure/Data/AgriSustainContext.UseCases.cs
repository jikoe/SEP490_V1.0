using AgriSustain.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Data;

public partial class AgriSustainContext
{
    public DbSet<CategorySettings> CategorySettings => Set<CategorySettings>();

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
    }
}
