using Microsoft.IdentityModel.Tokens;

namespace SeeSight.Gateway.Authentication;

/// <summary>
/// Caches Identity Service's public signing keys, fetched once at startup and
/// refreshed periodically — so validating a token never requires a live call to
/// Identity Service on the request hot path, per docs/Authentication.md §8.
///
/// Registered as a singleton (see Program.cs) so every consumer — the JWT
/// validation pipeline, the health check, and the background refresher — shares
/// the same cached keys. Takes <see cref="IHttpClientFactory"/> rather than a
/// typed <c>HttpClient</c> deliberately: <c>AddHttpClient&lt;JwksCache&gt;</c>
/// would register JwksCache itself as transient (a new instance, and a new empty
/// cache, per resolution) — the wrong lifetime for a piece of shared state.
/// </summary>
public sealed class JwksCache(IHttpClientFactory httpClientFactory, ILogger<JwksCache> logger)
{
    public const string HttpClientName = "identity";

    private List<SecurityKey> _keys = [];

    public IReadOnlyList<SecurityKey> GetKeys() => _keys;

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        var jwks = await httpClient
            .GetFromJsonAsync<JsonWebKeySet>("/.well-known/jwks.json", cancellationToken)
            .ConfigureAwait(false);

        if (jwks is not null && jwks.Keys.Count > 0)
        {
            _keys = jwks.Keys.Cast<SecurityKey>().ToList();
            JwksCacheLog.Refreshed(logger, _keys.Count);
        }
        else
        {
            JwksCacheLog.EmptyResponse(logger);
        }
    }
}

internal static partial class JwksCacheLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "JWKS cache refreshed with {KeyCount} key(s).")]
    public static partial void Refreshed(ILogger logger, int keyCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "JWKS endpoint returned no keys — keeping the previous cache.")]
    public static partial void EmptyResponse(ILogger logger);
}
