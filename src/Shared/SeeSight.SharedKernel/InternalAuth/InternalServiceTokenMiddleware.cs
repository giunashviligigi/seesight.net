using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace SeeSight.SharedKernel.InternalAuth;

/// <summary>
/// Guards every <c>/internal/*</c> endpoint with the shared internal-service
/// token (constant-time comparison), per
/// docs/adr/0006-internal-service-to-service-authentication.md. Requests
/// outside <c>/internal</c> pass through untouched — this middleware is
/// registered once, globally, in each service that exposes internal
/// endpoints, the same self-contained-path-check pattern as
/// SeeSight.Gateway.Authentication.MustChangePasswordMiddleware.
/// </summary>
public sealed class InternalServiceTokenMiddleware(
    RequestDelegate next,
    IOptions<InternalServiceTokenOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/internal"))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var expected = options.Value.ServiceToken;
        var presented = context.Request.Headers[InternalServiceTokenHeaders.ServiceToken].ToString();

        if (string.IsNullOrEmpty(expected) || !IsMatch(expected, presented))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    status = StatusCodes.Status401Unauthorized,
                    title = "Unauthorized",
                    detail = "A valid internal-service token is required.",
                },
                options: null,
                contentType: "application/problem+json").ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool IsMatch(string expected, string presented)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var presentedBytes = Encoding.UTF8.GetBytes(presented);

        // FixedTimeEquals requires equal-length inputs; a length mismatch is
        // itself not sensitive (token lengths aren't secret), so short-circuit
        // is fine — the byte *content* comparison is what must stay constant-time.
        return expectedBytes.Length == presentedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, presentedBytes);
    }
}
