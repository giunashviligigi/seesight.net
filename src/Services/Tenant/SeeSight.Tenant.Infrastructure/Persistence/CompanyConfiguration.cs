using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeeSight.Tenant.Domain;

namespace SeeSight.Tenant.Infrastructure.Persistence;

/// <summary>Fluent configuration only — no data-annotation attributes on the Domain entity (docs/DatabaseDesign.md §9).</summary>
public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.LegalName).HasMaxLength(200);

        builder.Property(c => c.Slug).IsRequired().HasMaxLength(250);
        builder.HasIndex(c => c.Slug).IsUnique();

        builder.Property(c => c.Country).HasMaxLength(2);
        builder.Property(c => c.BillingEmail).HasMaxLength(320);
        builder.Property(c => c.Timezone).IsRequired().HasMaxLength(100);

        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(c => c.PolicyJson).HasColumnType("jsonb");

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();
        builder.Property(c => c.DeletedAt);
    }
}
