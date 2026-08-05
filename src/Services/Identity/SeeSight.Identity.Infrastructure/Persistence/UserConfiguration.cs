using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeeSight.Identity.Domain;

namespace SeeSight.Identity.Infrastructure.Persistence;

/// <summary>Fluent configuration only — no data-annotation attributes on the Domain entity (docs/DatabaseDesign.md §9).</summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(320);
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.FirstName).HasMaxLength(200);
        builder.Property(u => u.LastName).HasMaxLength(200);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.HasIndex(u => u.Role);

        builder.Property(u => u.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.HasIndex(u => u.Status);

        builder.Property(u => u.MustChangePassword).IsRequired();

        builder.Property(u => u.CompanyId);
        builder.HasIndex(u => u.CompanyId);

        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UpdatedAt).IsRequired();
    }
}
