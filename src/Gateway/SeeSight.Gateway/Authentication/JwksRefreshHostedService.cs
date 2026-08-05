using Microsoft.Extensions.Options;

namespace SeeSight.Gateway.Authentication;

/// <summary>
/// Fetches the JWKS at startup (retrying with backoff so the Gateway tolerates
/// Identity Service not being up yet during docker-compose startup ordering)
/// and then refreshes it periodically.
/// </summary>
public sealed class JwksRefreshHostedService(
    JwksCache cache,
    IOptions<IdentityServiceOptions> options,
    ILogger<JwksRefreshHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan[] StartupRetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await FetchWithRetryAsync(stoppingToken).ConfigureAwait(false);

        var interval = TimeSpan.FromMinutes(options.Value.JwksRefreshIntervalMinutes);
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await cache.RefreshAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                JwksRefreshHostedServiceLog.PeriodicRefreshFailed(logger, ex);
            }
        }
    }

    private async Task FetchWithRetryAsync(CancellationToken cancellationToken)
    {
        foreach (var delay in StartupRetryDelays)
        {
            try
            {
                await cache.RefreshAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                JwksRefreshHostedServiceLog.StartupFetchFailed(logger, ex, delay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        // Final attempt — let this one throw and fail startup if Identity Service
        // is still unreachable, per ServiceDependencyMatrix.md ("Identity Service
        // JWKS reachable (hard)" startup dependency for the Gateway).
        await cache.RefreshAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal static partial class JwksRefreshHostedServiceLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "JWKS startup fetch failed, retrying in {Delay}.")]
    public static partial void StartupFetchFailed(ILogger logger, Exception exception, TimeSpan delay);

    [LoggerMessage(Level = LogLevel.Error, Message = "Periodic JWKS refresh failed — keeping the previous cache.")]
    public static partial void PeriodicRefreshFailed(ILogger logger, Exception exception);
}
