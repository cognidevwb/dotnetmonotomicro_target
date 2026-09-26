#nullable enable
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderLineEntity = Orders.Domain.OrderLine;

namespace Orders.Infrastructure.Configurations;

public sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLineEntity>
{
    public void Configure(EntityTypeBuilder<OrderLineEntity> builder)
    {
        builder.ToTable("order_lines");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.OrderId).IsRequired();
        builder.Property(l => l.ProductId).IsRequired();
        builder.Property(l => l.Quantity).IsRequired();
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);

        builder.HasIndex(l => l.OrderId);

        // Postgres optimistic concurrency token.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
