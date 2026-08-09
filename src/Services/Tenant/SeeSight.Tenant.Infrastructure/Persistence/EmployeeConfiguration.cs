using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeeSight.Tenant.Domain;

namespace SeeSight.Tenant.Infrastructure.Persistence;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.CompanyId).IsRequired();
        builder.Property(e => e.DepartmentId);
        builder.Property(e => e.UserId);

        // Real FK — same-database reference, not cross-service (see the
        // identical rationale in DepartmentConfiguration). DepartmentId stays
        // a plain nullable column with no FK: an employee's department can be
        // cleared independently (Department.Delete unassigns members) and a
        // cross-tenant DepartmentId is already prevented at the Application
        // layer, not the database.
        builder.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.Email).IsRequired().HasMaxLength(320);
        // Unique(CompanyId, Email) — the same email may exist under a
        // different tenant, per docs/DatabaseDesign.md §4.
        builder.HasIndex(e => new { e.CompanyId, e.Email }).IsUnique();

        builder.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.LastName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.JobTitle).HasMaxLength(200);
        builder.Property(e => e.Phone).HasMaxLength(50);
        builder.Property(e => e.Nationality).HasMaxLength(2);
        builder.Property(e => e.PassportNumber).HasMaxLength(50);
        builder.Property(e => e.PreferredAirport).HasMaxLength(3);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.DepartmentId);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.Property(e => e.DeletedAt);
    }
}
