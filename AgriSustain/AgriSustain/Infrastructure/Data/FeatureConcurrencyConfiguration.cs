using AgriSustain.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Data;

public partial class AgriSustainContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SellerOrder>().Property(x => x.Status).IsConcurrencyToken();
        modelBuilder.Entity<HarvestBatch>().Property(x => x.AvailableQuantity).IsConcurrencyToken();
        modelBuilder.Entity<HarvestBatch>().Property(x => x.ReservedQuantity).IsConcurrencyToken();
        modelBuilder.Entity<Product>().Property(x => x.ApprovalStatus).IsConcurrencyToken();
        modelBuilder.Entity<Product>().Property(x => x.IsPublished).IsConcurrencyToken();
        modelBuilder.Entity<Cart>().Property(x => x.UpdatedAt).IsConcurrencyToken();
        modelBuilder.Entity<DisputeEvidenceRequest>().Property(x => x.RequestStatus).IsConcurrencyToken();
    }
}
