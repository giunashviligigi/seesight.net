using SeeSight.SharedKernel.Http;

namespace SeeSight.Gateway.Authentication;

/// <summary>
/// Blocks every endpoint except a small allowlist for a user whose access token
/// carries <c>mustChangePassword=true</c> — per docs/Authentication.md §4.
/// Placed after <c>UseAuthorization()</c> in the pipeline, so it only ever runs
/// for requests that already satisfied whatever authorization policy their
/// route has; it only takes action when the request is authenticated at all
/// (public routes like /auth/login pass through untouched).
/// </summary>
public sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/auth/change-password",
        "/auth/me",
        "/auth/logout",
        "/auth/refresh",
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true && IsBlocked(context))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    status = StatusCodes.Status403Forbidden,
                    title = "Forbidden",
                    detail = "You must change your password before continuing.",
                },
                options: null,
                contentType: "application/problem+json").ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool IsBlocked(HttpContext context)
    {
        var mustChangePassword = context.User.FindFirst(SeeSightClaimTypes.MustChangePassword)?.Value;
        if (!string.Equals(mustChangePassword, "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !AllowedPaths.Contains(context.Request.Path.Value ?? string.Empty);
    }
}
