using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeeSight.Tenant.Domain;

namespace SeeSight.Tenant.Infrastructure.Persistence;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("departments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.CompanyId).IsRequired();

        // Real FK — Company/Department share the same Tenant Service database,
        // so this is not a cross-service reference (docs/DatabaseDesign.md §4,
        // §"Tenant Service" ERD marks CompanyId as FK). Restrict, not Cascade:
        // Company is only ever soft-deleted by the application (see
        // DeleteCompanyCommandHandler), so a hard delete reaching this
        // constraint would indicate a bug, not a normal lifecycle event.
        builder.HasOne<Company>().WithMany().HasForeignKey(d => d.CompanyId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(d => d.Name).IsRequired().HasMaxLength(200);
        builder.Property(d => d.Code).HasMaxLength(50);

        // Unique(CompanyId, Name) — docs/DatabaseDesign.md §4.
        builder.HasIndex(d => new { d.CompanyId, d.Name }).IsUnique();

        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.UpdatedAt).IsRequired();
        builder.Property(d => d.DeletedAt);
    }
}
