#nullable enable
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CustomerEntity = Customers.Domain.Customer;

namespace Customers.Infrastructure.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<CustomerEntity>
{
    public void Configure(EntityTypeBuilder<CustomerEntity> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(c => c.Email)
            .HasColumnName("email")
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(c => c.Email).IsUnique();

        builder.Property(c => c.Active)
            .HasColumnName("active")
            .HasDefaultValue(true)
            .IsRequired();

        // Postgres optimistic concurrency via the system xmin column — no extra
        // rowversion column, and concurrent writers get a DbUpdateConcurrencyException.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
