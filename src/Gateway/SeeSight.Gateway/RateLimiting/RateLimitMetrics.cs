using System.Diagnostics.Metrics;

namespace SeeSight.Gateway.RateLimiting;

/// <summary>The fail-open signal ADR 0007 requires be observable — see docs/adr/0007-redis-dependent-features-fail-open.md.</summary>
internal static class RateLimitMetrics
{
    public const string MeterName = "SeeSight.Gateway.RateLimiting";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> RedisUnavailableCounter =
        Meter.CreateCounter<long>("rate_limiter_redis_unavailable_total");
}
