using System.Text.Json;
using Microsoft.Extensions.Options;
using SeeSight.Gateway.Authentication;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace SeeSight.Gateway.Proxy;

/// <summary>
/// Sets the httpOnly session cookie from Identity Service's register/login
/// response body — the cookie is set/read exclusively at the Gateway, per
/// docs/Authentication.md §3. Identity Service itself never sets a cookie; it
/// just returns the access token in the JSON body, same as it would for any
/// non-browser client.
/// </summary>
public sealed class SetAuthCookieTransformProvider(IOptions<AuthCookieOptions> cookieOptions) : ITransformProvider
{
    private static readonly HashSet<string> CookieSettingRouteIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "identity-register",
        "identity-login",
    };

    public void ValidateRoute(TransformRouteValidationContext context)
    {
    }

    public void ValidateCluster(TransformClusterValidationContext context)
    {
    }

    public void Apply(TransformBuilderContext context)
    {
        if (!CookieSettingRouteIds.Contains(context.Route.RouteId))
        {
            return;
        }

        var cookieName = cookieOptions.Value.Name;

        context.AddResponseTransform(async transformContext =>
        {
            var proxyResponse = transformContext.ProxyResponse;
            if (proxyResponse is null || !proxyResponse.IsSuccessStatusCode)
            {
                return;
            }

            var body = await proxyResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            using (var document = JsonDocument.Parse(body))
            {
                if (document.RootElement.TryGetProperty("accessToken", out var tokenElement) &&
                    document.RootElement.TryGetProperty("expiresAt", out var expiresElement) &&
                    tokenElement.GetString() is { Length: > 0 } token)
                {
                    var httpContext = transformContext.HttpContext;
                    var isLocalhost = httpContext.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

                    httpContext.Response.Cookies.Append(cookieName, token, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = !isLocalhost,
                        SameSite = SameSiteMode.Lax,
                        Path = "/",
                        Expires = expiresElement.GetDateTimeOffset(),
                    });
                }
            }

            // We consumed the original stream above — suppress YARP's default copy
            // and write the (unmodified) body back ourselves.
            transformContext.SuppressResponseBody = true;
            transformContext.HttpContext.Response.ContentType =
                proxyResponse.Content.Headers.ContentType?.ToString() ?? "application/json";
            await transformContext.HttpContext.Response.WriteAsync(body).ConfigureAwait(false);
        });
    }
}
