using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeeSight.Identity.Domain;

namespace SeeSight.Identity.Infrastructure.Persistence;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId).IsRequired();
        builder.HasIndex(t => t.UserId);

        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(200);
        builder.HasIndex(t => t.TokenHash).IsUnique();

        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.RevokedAt);
        builder.Property(t => t.ReplacedByTokenId);
        builder.Property(t => t.CreatedByIp).HasMaxLength(64);
        builder.Property(t => t.CreatedAt).IsRequired();
    }
}
