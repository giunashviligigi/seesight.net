using Microsoft.EntityFrameworkCore;
using SeeSight.SharedKernel.Tenancy;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Domain;

namespace SeeSight.Tenant.Infrastructure.Persistence;

/// <summary>
/// The tenant + soft-delete filters are computed once per instance (i.e. once
/// per scoped request) from the injected <see cref="ITenantContext"/> and
/// referenced as instance fields inside each <c>HasQueryFilter</c> lambda —
/// EF Core's documented pattern for per-request global filters. Capturing the
/// values as *local variables inside <see cref="OnModelCreating"/>* instead
/// would be wrong: the built model is cached and shared across every
/// <see cref="TenantDbContext"/> instance, so a local-variable snapshot would
/// bake in the *first* request's tenant for every request afterward.
/// </summary>
public sealed class TenantDbContext : DbContext, ITenantDbContext
{
    private readonly bool _isSuperAdmin;
    private readonly Guid? _tenantCompanyId;

    public TenantDbContext(DbContextOptions<TenantDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _isSuperAdmin = tenantContext.IsSuperAdmin;
        _tenantCompanyId = tenantContext.CompanyId?.Value;
    }

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenantDbContext).Assembly);

        // Company is the tenant root, not tenant-scoped data — only the
        // soft-delete filter applies (docs/adr/0009-hand-rolled-tenant-context.md).
        modelBuilder.Entity<Company>().HasQueryFilter(c => c.DeletedAt == null);

        modelBuilder.Entity<Department>().HasQueryFilter(d =>
            d.DeletedAt == null && (_isSuperAdmin || d.CompanyId == _tenantCompanyId));

        modelBuilder.Entity<Employee>().HasQueryFilter(e =>
            e.DeletedAt == null && (_isSuperAdmin || e.CompanyId == _tenantCompanyId));
    }
}
