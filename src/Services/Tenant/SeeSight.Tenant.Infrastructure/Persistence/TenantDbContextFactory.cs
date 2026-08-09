using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SeeSight.SharedKernel.Tenancy;

namespace SeeSight.Tenant.Infrastructure.Persistence;

/// <summary>
/// Lets EF Core design-time tooling (<c>dotnet ef migrations add</c>) construct
/// <see cref="TenantDbContext"/> without a full DI container — it needs a real
/// <see cref="ITenantContext"/> instance only because of the constructor shape,
/// never actually evaluated at design time (no queries run during migration
/// generation, only model reflection).
/// </summary>
public sealed class TenantDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public TenantId? CompanyId => null;
        public bool IsSuperAdmin => false;
    }

    public TenantDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=tenant;Username=seesight;Password=seesight");

        return new TenantDbContext(optionsBuilder.Options, new DesignTimeTenantContext());
    }
}
