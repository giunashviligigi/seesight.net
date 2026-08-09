using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using SeeSight.SharedKernel.Http;
using SeeSight.SharedKernel.InternalAuth;
using SeeSight.SharedKernel.Tenancy;
using SeeSight.Shared.Observability;
using SeeSight.Tenant.Api.HealthChecks;
using SeeSight.Tenant.Api.Middleware;
using SeeSight.Tenant.Application;
using SeeSight.Tenant.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddSeeSightObservability("tenant");

builder.Services.AddTenantApplication();
builder.Services.AddTenantInfrastructure(builder.Configuration);
builder.Services.AddCurrentUserContext();
builder.Services.AddTenantContext();
builder.Services.AddInternalServiceTokenValidation(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Reject unknown request properties — docs/CodingStandards.md §4,
        // mirroring Identity.Api's convention exactly.
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    });

builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("tenant-db", tags: ["ready"]);

var app = builder.Build();

app.UseSeeSightCorrelationId();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<InternalServiceTokenMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

// Accessible to SeeSight.Tenant.IntegrationTests via WebApplicationFactory<Program>.
#pragma warning disable CA1050 // Declare types in namespaces - required for WebApplicationFactory<T>
public partial class Program;
#pragma warning restore CA1050
