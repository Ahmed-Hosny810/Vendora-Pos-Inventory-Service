using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.InventoryService.Infrastructure.Persistence.ReadModels;


namespace Pos.InventoryService.Infrastructure.Persistence.Contexts.DbConfigurations
{
    public class InventoryBranchReadModelConfiguration: IEntityTypeConfiguration<InventoryBranchReadModel>
    {
        public void Configure(EntityTypeBuilder<InventoryBranchReadModel> builder)
        {
            builder.HasNoKey();
            builder.ToView("Branches", "branch");
        }
    }

    public class InventoryProductReadModelConfiguration: IEntityTypeConfiguration<InventoryProductReadModel>
    {
        public void Configure(
            EntityTypeBuilder<InventoryProductReadModel> builder)
        {
            builder.HasNoKey();
            builder.ToView("Products", "Catalog");
        }
    }

    public class InventoryVariantReadModelConfiguration : IEntityTypeConfiguration<InventoryVariantReadModel>
    {
        public void Configure(
            EntityTypeBuilder<InventoryVariantReadModel> builder)
        {
            builder.HasNoKey();
            builder.ToView("ProductVariants", "Catalog");
        }
    }

    public class InventoryUnitReadModelConfiguration
        : IEntityTypeConfiguration<InventoryUnitReadModel>
    {
        public void Configure(EntityTypeBuilder<InventoryUnitReadModel> builder)
        {
            builder.HasNoKey();
            builder.ToView("Units", "Catalog");
        }
    }
}
