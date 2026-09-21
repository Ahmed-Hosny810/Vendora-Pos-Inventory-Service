using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Infrastructure.Persistence.Contexts.DbConfigurations;

public class LowStockAlertConfiguration : IEntityTypeConfiguration<LowStockAlert>
{
    public void Configure(EntityTypeBuilder<LowStockAlert> builder)
    {
        builder.ToTable("LowStockAlerts", "inventory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QuantityOnHand).HasPrecision(18, 3);
        builder.Property(x => x.Threshold).HasPrecision(18, 3);
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
        // Explicit filter includes null variants in active-alert uniqueness.
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.ProductId, x.ProductVariantId })
            .IsUnique()
            .HasDatabaseName("UX_LowStockAlerts_ActiveItem")
            .HasFilter("[Status] = N'Active'");
    }
}
