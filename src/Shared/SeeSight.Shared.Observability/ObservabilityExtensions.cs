using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;

namespace SeeSight.Shared.Observability;

/// <summary>
/// One-line OpenTelemetry (traces + metrics, OTLP export) and Serilog (structured
/// console logging, enriched with the active trace/span id) setup, reused by every
/// service — see docs/Observability.md §1-2. Deliberately scoped to what M1 needs
/// (basic tracing/logging + health checks); dashboards/alerts arrive in M13 per
/// docs/ImplementationRoadmap.md.
/// </summary>
public static class ObservabilityExtensions
{
    public static IHostApplicationBuilder AddSeeSightObservability(
        this IHostApplicationBuilder builder,
        string serviceName,
        params string[] additionalMeterNames)
    {
        var otlpEndpoint = builder.Configuration["Observability:OtlpEndpoint"] ?? "http://localhost:4317";

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));

                foreach (var meterName in additionalMeterNames)
                {
                    metrics.AddMeter(meterName);
                }
            });

        builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithSpan()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

        return builder;
    }

    /// <summary>
    /// Stamps/propagates the <see cref="CorrelationIdMiddleware.HeaderName"/> header
    /// and pushes it into every log line's context for the duration of the request.
    /// </summary>
    public static IApplicationBuilder UseSeeSightCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
