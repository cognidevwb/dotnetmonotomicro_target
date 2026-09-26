#nullable enable
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentEntity = Payments.Domain.Payment;

namespace Payments.Infrastructure.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<PaymentEntity>
{
    public void Configure(EntityTypeBuilder<PaymentEntity> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(p => p.OrderId).HasColumnName("order_id").IsRequired();

        builder.Property(p => p.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .IsRequired();

        // One payment per order — the database, not a read-check-then-write, is what
        // makes Charge idempotent under retry.
        builder.HasIndex(p => p.OrderId).IsUnique().HasDatabaseName("ix_payments_order_id");

        // Postgres optimistic concurrency via the xmin system column.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
