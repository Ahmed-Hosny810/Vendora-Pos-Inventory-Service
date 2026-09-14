using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Infrastructure.Persistence.Contexts.DbConfigurations;

public class StockAdjustmentItemConfiguration : IEntityTypeConfiguration<StockAdjustmentItem>
{
    public void Configure(EntityTypeBuilder<StockAdjustmentItem> builder)
    {
        builder.ToTable("StockAdjustmentItems", "inventory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OldQuantity).HasPrecision(18, 3);
        builder.Property(x => x.NewQuantity).HasPrecision(18, 3);
        builder.Property(x => x.QuantityDelta).HasPrecision(18, 3);
        builder.HasOne(x => x.Adjustment).WithMany(x => x.Items)
            .HasForeignKey(x => new { x.TenantId, x.AdjustmentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
