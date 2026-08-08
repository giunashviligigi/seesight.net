namespace SeeSight.Gateway.RateLimiting;

public sealed class RateLimitOptions
{
    public const string SectionName = "Gateway:RateLimit";

    public string RedisConnectionString { get; set; } = "localhost:6379";

    public int RequestsPerWindow { get; set; } = 10;

    public int WindowSeconds { get; set; } = 60;
}
