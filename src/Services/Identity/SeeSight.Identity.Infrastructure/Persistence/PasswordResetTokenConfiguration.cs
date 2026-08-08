using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeeSight.Identity.Domain;

namespace SeeSight.Identity.Infrastructure.Persistence;

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId).IsRequired();
        builder.HasIndex(t => t.UserId);

        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(200);
        builder.HasIndex(t => t.TokenHash).IsUnique();

        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.UsedAt);
        builder.Property(t => t.CreatedAt).IsRequired();
    }
}
