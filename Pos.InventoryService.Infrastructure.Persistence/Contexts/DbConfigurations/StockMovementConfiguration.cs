using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Infrastructure.Persistence.Contexts.DbConfigurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements", "inventory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QuantityDelta).HasPrecision(18, 3);
        builder.Property(x => x.BeforeQty).HasPrecision(18, 3);
        builder.Property(x => x.AfterQty).HasPrecision(18, 3);
        builder.Property(x => x.LowStockThreshold).HasPrecision(18, 3);
        builder.Property(x => x.MovementType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ReferenceType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.IdempotencyKey)
            .HasColumnType("uniqueidentifier")
            .IsRequired(false);

        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.ProductId, x.CreatedAt });

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.IdempotencyKey,
            x.ProductId,
            x.ProductVariantId
        })
        .IsUnique()
        .HasDatabaseName("UX_StockMovements_IdempotencyItem")
        .HasFilter("[IdempotencyKey] IS NOT NULL");
    }
}
