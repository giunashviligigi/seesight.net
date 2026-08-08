using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace SeeSight.Gateway.RateLimiting;

/// <summary>
/// Fixed-window, per-client-IP rate limiting on /auth/login and /auth/register,
/// backed directly by StackExchange.Redis (INCR + EXPIRE) — see ADR 0008.
/// Fails open on any Redis error: the request is allowed through, logged, and
/// counted via <see cref="RateLimitMetrics.RedisUnavailableCounter"/>, per
/// ADR 0007. <see cref="IConnectionMultiplexer"/> is injected directly (an
/// interface, trivially fakeable in tests) rather than behind a bespoke
/// wrapper — no abstraction earns its keep here that the library doesn't
/// already provide.
/// </summary>
public sealed class RedisRateLimitMiddleware(
    RequestDelegate next,
    IConnectionMultiplexer redis,
    IOptions<RateLimitOptions> options,
    ILogger<RedisRateLimitMiddleware> logger)
{
    private static readonly HashSet<string> LimitedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/auth/login",
        "/auth/register",
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsLimited(context))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var rateLimitOptions = options.Value;
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var redisKey = $"ratelimit:auth:{context.Request.Path.Value}:{clientIp}";

        try
        {
            var db = redis.GetDatabase();
            var count = await db.StringIncrementAsync(redisKey).ConfigureAwait(false);
            if (count == 1)
            {
                await db.KeyExpireAsync(redisKey, TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds)).ConfigureAwait(false);
            }

            if (count > rateLimitOptions.RequestsPerWindow)
            {
                await WriteTooManyRequestsAsync(context, rateLimitOptions.WindowSeconds).ConfigureAwait(false);
                return;
            }
        }
        catch (RedisException ex)
        {
            FailOpen(ex);
        }
        catch (TimeoutException ex)
        {
            FailOpen(ex);
        }

        await next(context).ConfigureAwait(false);

        void FailOpen(Exception ex)
        {
            RateLimitMetrics.RedisUnavailableCounter.Add(1);
            RedisRateLimitMiddlewareLog.FailedOpen(logger, ex);
        }
    }

    private static bool IsLimited(HttpContext context) =>
        HttpMethods.IsPost(context.Request.Method) && LimitedPaths.Contains(context.Request.Path.Value ?? string.Empty);

    private static async Task WriteTooManyRequestsAsync(HttpContext context, int windowSeconds)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = windowSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await context.Response.WriteAsJsonAsync(
            new
            {
                status = StatusCodes.Status429TooManyRequests,
                title = "Too Many Requests",
                detail = "Rate limit exceeded. Try again later.",
            },
            options: null,
            contentType: "application/problem+json").ConfigureAwait(false);
    }
}

internal static partial class RedisRateLimitMiddlewareLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Rate limiter could not reach Redis — failing open (request allowed).")]
    public static partial void FailedOpen(ILogger logger, Exception exception);
}
