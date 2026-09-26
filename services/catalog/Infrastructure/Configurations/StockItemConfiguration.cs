#nullable enable
using Catalog.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Infrastructure.Configurations;

public sealed class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("stock_items");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(s => s.ProductId).HasColumnName("product_id");
        builder.Property(s => s.Quantity).HasColumnName("quantity");
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>();
        builder.HasIndex(s => s.ProductId).IsUnique();

        // Postgres optimistic concurrency — the system xmin column as a row version.
        builder.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").IsRowVersion();
    }
}
