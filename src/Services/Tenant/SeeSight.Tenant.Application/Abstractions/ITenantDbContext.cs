using Microsoft.EntityFrameworkCore;
using SeeSight.Tenant.Domain;

namespace SeeSight.Tenant.Application.Abstractions;

/// <summary>
/// The persistence contract Application depends on; implemented by the real EF
/// Core <c>TenantDbContext</c> in Infrastructure — same Dependency Inversion
/// pattern as Identity Service's <c>IIdentityDbContext</c>.
/// </summary>
public interface ITenantDbContext
{
    DbSet<Company> Companies { get; }

    DbSet<Department> Departments { get; }

    DbSet<Employee> Employees { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
