using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace SeeSight.Shared.Observability;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Request-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing) && !string.IsNullOrWhiteSpace(existing)
            ? existing.ToString()
            : Guid.CreateVersion7().ToString();

        // Set on the *request* too, not just the response — a reverse-proxied
        // request (e.g. YARP forwarding Gateway -> Identity Service) carries this
        // header downstream so every service in the chain shares one id instead
        // of each minting its own (docs/Observability.md §4).
        context.Request.Headers[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
