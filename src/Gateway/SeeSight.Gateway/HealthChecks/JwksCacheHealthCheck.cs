using Microsoft.Extensions.Diagnostics.HealthChecks;
using SeeSight.Gateway.Authentication;

namespace SeeSight.Gateway.HealthChecks;

/// <summary>Matches docs/ServiceDependencyMatrix.md: "cached JWKS valid" as a readiness signal.</summary>
public sealed class JwksCacheHealthCheck(JwksCache cache) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var result = cache.GetKeys().Count > 0
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("No JWKS keys cached yet.");

        return Task.FromResult(result);
    }
}
