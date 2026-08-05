using Microsoft.Extensions.Diagnostics.HealthChecks;
using SeeSight.Identity.Infrastructure.Persistence;

namespace SeeSight.Identity.Api.HealthChecks;

/// <summary>Matches docs/Observability.md §3 exactly: DbContext.Database.CanConnectAsync().</summary>
public sealed class DatabaseHealthCheck(IdentityDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
        return canConnect
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Cannot connect to the Identity database.");
    }
}
