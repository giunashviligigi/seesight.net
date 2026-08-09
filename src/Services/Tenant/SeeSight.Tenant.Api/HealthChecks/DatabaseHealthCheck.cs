using Microsoft.Extensions.Diagnostics.HealthChecks;
using SeeSight.Tenant.Infrastructure.Persistence;

namespace SeeSight.Tenant.Api.HealthChecks;

/// <summary>Matches docs/Observability.md §3 exactly: DbContext.Database.CanConnectAsync().</summary>
public sealed class DatabaseHealthCheck(TenantDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
        return canConnect
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Cannot connect to the Tenant database.");
    }
}
