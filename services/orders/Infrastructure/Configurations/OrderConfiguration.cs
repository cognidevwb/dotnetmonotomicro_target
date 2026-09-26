#nullable enable
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderEntity = Orders.Domain.Order;

namespace Orders.Infrastructure.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<OrderEntity>
{
    public void Configure(EntityTypeBuilder<OrderEntity> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.CustomerId).IsRequired();
        builder.Property(o => o.Status).HasMaxLength(32).IsRequired();
        builder.Property(o => o.Total).HasPrecision(18, 2);

        builder.HasMany(o => o.Lines)
            .WithOne()
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => o.CustomerId);

        // Postgres optimistic concurrency — the saga and the cancel compensation can
        // touch the same order, so a lost update must fail loudly.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
