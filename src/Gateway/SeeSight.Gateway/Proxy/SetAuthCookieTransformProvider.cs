using System.Text.Json;
using Microsoft.Extensions.Options;
using SeeSight.Gateway.Authentication;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace SeeSight.Gateway.Proxy;

/// <summary>
/// Sets/clears the httpOnly session cookies from Identity Service's response
/// bodies — cookies are set/read exclusively at the Gateway, per
/// docs/Authentication.md §3. Identity Service itself never sets a cookie; it
/// just returns tokens in the JSON body, same as it would for any non-browser
/// client. The refresh cookie is scoped to <c>Path=/auth</c> — it's only ever
/// needed by the auth endpoints themselves, so it isn't sent on every request
/// the way the access token cookie is (a real exposure-surface reduction, not
/// just a formality).
/// </summary>
public sealed class SetAuthCookieTransformProvider(IOptions<AuthCookieOptions> cookieOptions) : ITransformProvider
{
    private static readonly HashSet<string> CookieSettingRouteIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "identity-register",
        "identity-login",
        "identity-refresh",
    };

    private const string CookieClearingRouteId = "identity-logout";

    public void ValidateRoute(TransformRouteValidationContext context)
    {
    }

    public void ValidateCluster(TransformClusterValidationContext context)
    {
    }

    public void Apply(TransformBuilderContext context)
    {
        if (CookieSettingRouteIds.Contains(context.Route.RouteId))
        {
            context.AddResponseTransform(transformContext => SetCookiesFromResponseBodyAsync(transformContext, cookieOptions.Value));
        }
        else if (string.Equals(context.Route.RouteId, CookieClearingRouteId, StringComparison.OrdinalIgnoreCase))
        {
            context.AddResponseTransform(transformContext => ClearCookiesAsync(transformContext, cookieOptions.Value));
        }
    }

    private static async ValueTask SetCookiesFromResponseBodyAsync(ResponseTransformContext transformContext, AuthCookieOptions options)
    {
        var proxyResponse = transformContext.ProxyResponse;
        if (proxyResponse is null || !proxyResponse.IsSuccessStatusCode)
        {
            return;
        }

        var body = await proxyResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        var httpContext = transformContext.HttpContext;
        var isLocalhost = httpContext.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

        using (var document = JsonDocument.Parse(body))
        {
            var root = document.RootElement;

            if (root.TryGetProperty("accessToken", out var accessTokenElement) &&
                root.TryGetProperty("accessTokenExpiresAt", out var accessExpiresElement) &&
                accessTokenElement.GetString() is { Length: > 0 } accessToken)
            {
                httpContext.Response.Cookies.Append(options.Name, accessToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = !isLocalhost,
                    SameSite = SameSiteMode.Lax,
                    Path = "/",
                    Expires = accessExpiresElement.GetDateTimeOffset(),
                });
            }

            if (root.TryGetProperty("refreshToken", out var refreshTokenElement) &&
                root.TryGetProperty("refreshTokenExpiresAt", out var refreshExpiresElement) &&
                refreshTokenElement.GetString() is { Length: > 0 } refreshToken)
            {
                httpContext.Response.Cookies.Append(options.RefreshTokenName, refreshToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = !isLocalhost,
                    SameSite = SameSiteMode.Lax,
                    Path = "/auth",
                    Expires = refreshExpiresElement.GetDateTimeOffset(),
                });
            }
        }

        // We consumed the original stream above — suppress YARP's default copy
        // and write the (unmodified) body back ourselves.
        transformContext.SuppressResponseBody = true;
        httpContext.Response.ContentType = proxyResponse.Content.Headers.ContentType?.ToString() ?? "application/json";
        await httpContext.Response.WriteAsync(body).ConfigureAwait(false);
    }

    private static ValueTask ClearCookiesAsync(ResponseTransformContext transformContext, AuthCookieOptions options)
    {
        var httpContext = transformContext.HttpContext;
        var isLocalhost = httpContext.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

        httpContext.Response.Cookies.Delete(options.Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = !isLocalhost,
            SameSite = SameSiteMode.Lax,
            Path = "/",
        });
        httpContext.Response.Cookies.Delete(options.RefreshTokenName, new CookieOptions
        {
            HttpOnly = true,
            Secure = !isLocalhost,
            SameSite = SameSiteMode.Lax,
            Path = "/auth",
        });

        return ValueTask.CompletedTask;
    }
}
