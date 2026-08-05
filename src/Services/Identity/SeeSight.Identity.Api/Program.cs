using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using SeeSight.Identity.Api.HealthChecks;
using SeeSight.Identity.Api.Middleware;
using SeeSight.Identity.Application;
using SeeSight.Identity.Infrastructure;
using SeeSight.Identity.Infrastructure.Security;
using SeeSight.SharedKernel.Http;
using SeeSight.Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.AddSeeSightObservability("identity");

builder.Services.AddIdentityApplication();
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddCurrentUserContext();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Reject unknown request properties — the direct successor to the original
        // system's ValidationPipe({ whitelist: true, forbidNonWhitelisted: true }),
        // which System.Text.Json does not do by default (docs/CodingStandards.md §4).
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    });

builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("identity-db", tags: ["ready"]);

var app = builder.Build();

app.UseSeeSightCorrelationId();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.MapGet("/.well-known/jwks.json", (RsaSigningKeyProvider keyProvider) => Results.Json(keyProvider.GetJsonWebKeySet()));

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

// Accessible to SeeSight.Identity.IntegrationTests via WebApplicationFactory<Program>.
#pragma warning disable CA1050 // Declare types in namespaces - required for WebApplicationFactory<T>
public partial class Program;
#pragma warning restore CA1050
