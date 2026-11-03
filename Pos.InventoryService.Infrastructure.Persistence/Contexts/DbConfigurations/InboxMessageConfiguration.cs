using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Infrastructure.Persistence.Contexts.DbConfigurations;

public class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages", "inventory");
        builder.HasKey(x => new { x.ConsumerName, x.EventId });
        builder.Property(x => x.EventId).ValueGeneratedNever();
        builder.Property(x => x.ConsumerName).HasMaxLength(150).IsUnicode(false).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(150).IsUnicode(false).IsRequired();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.OccurredAt).IsRequired();
        builder.Property(x => x.ProcessedAt).IsRequired();
    }
}
