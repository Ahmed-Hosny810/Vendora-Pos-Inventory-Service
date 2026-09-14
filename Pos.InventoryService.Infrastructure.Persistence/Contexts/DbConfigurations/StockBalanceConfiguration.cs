using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Infrastructure.Persistence.Contexts.DbConfigurations;

public class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> builder)
    {
        builder.ToTable("StockBalances", "inventory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QuantityOnHand).HasPrecision(18, 3);
        builder.Property(x => x.QuantityReserved).HasPrecision(18, 3);
        builder.Property(x => x.LowStockThreshold).HasPrecision(18, 3);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Ignore(x => x.AvailableQuantity);
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.ProductId, x.ProductVariantId })
            .IsUnique().HasFilter(null);
    }
}
