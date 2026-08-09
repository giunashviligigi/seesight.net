using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SeeSight.Gateway.Authentication;
using SeeSight.Gateway.HealthChecks;
using SeeSight.Gateway.Proxy;
using SeeSight.Gateway.RateLimiting;
using SeeSight.Shared.Observability;
using StackExchange.Redis;
using Yarp.ReverseProxy.Transforms.Builder;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("yarp.config.json", optional: false, reloadOnChange: true);

// yarp.config.json's cluster destination is a build-time placeholder only.
// IdentityService:BaseUrl is the one canonical "where is Identity Service"
// setting (already environment-specific via appsettings.*.json / docker-compose
// env vars) — this layers it on top of the YARP config so the proxy destination
// and the JwksCache/health-check HttpClient can never drift apart, which is
// exactly the class of bug this fixes: the two were previously configured
// independently, and only the JwksCache one was ever overridden for Docker.
var identityBaseUrl = builder.Configuration[$"{IdentityServiceOptions.SectionName}:BaseUrl"] ?? "http://localhost:5075";
var tenantBaseUrl = builder.Configuration[$"{TenantServiceOptions.SectionName}:BaseUrl"] ?? "http://localhost:5076";
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ReverseProxy:Clusters:identity:Destinations:destination1:Address"] = identityBaseUrl,
    ["ReverseProxy:Clusters:tenant:Destinations:destination1:Address"] = tenantBaseUrl,
});

builder.AddSeeSightObservability("gateway", RateLimitMetrics.MeterName);

builder.Services.AddOptions<IdentityServiceOptions>()
    .Bind(builder.Configuration.GetSection(IdentityServiceOptions.SectionName));
builder.Services.AddOptions<TenantServiceOptions>()
    .Bind(builder.Configuration.GetSection(TenantServiceOptions.SectionName));
builder.Services.AddOptions<AuthCookieOptions>()
    .Bind(builder.Configuration.GetSection(AuthCookieOptions.SectionName));

builder.Services.AddHttpClient(JwksCache.HttpClientName, (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<IdentityServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});
builder.Services.AddSingleton<JwksCache>();
builder.Services.AddHostedService<JwksRefreshHostedService>();

builder.Services.AddOptions<RateLimitOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitOptions.SectionName));
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var rateLimitOptions = sp.GetRequiredService<IOptions<RateLimitOptions>>().Value;
    var redisOptions = ConfigurationOptions.Parse(rateLimitOptions.RedisConnectionString);
    // Per ADR 0007/0008: a Redis outage — even at startup — must not take the
    // Gateway down. AbortOnConnectFail = false means this call never throws
    // even if Redis is completely unreachable; the multiplexer retries in the
    // background and every rate-limit check fails open until it reconnects.
    redisOptions.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(redisOptions);
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// JwtBearerOptions needs the JwksCache singleton and the configured cookie name —
// Configure<TDep> resolves them from DI at options-binding time, which a plain
// AddJwtBearer(Action<JwtBearerOptions>) lambda can't do.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwksCache, IOptions<AuthCookieOptions>>((options, jwksCache, cookieOptions) =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://seesight.identity",
            ValidateAudience = true,
            ValidAudience = "seesight",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = (_, _, _, _) => jwksCache.GetKeys(),
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var cookieName = cookieOptions.Value.Name;
                if (context.Request.Cookies.TryGetValue(cookieName, out var cookieToken) &&
                    !string.IsNullOrEmpty(cookieToken))
                {
                    context.Token = cookieToken;
                }

                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddSingleton<ITransformProvider, ForwardedIdentityTransformProvider>();
builder.Services.AddSingleton<ITransformProvider, SetAuthCookieTransformProvider>();

builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Gateway:AllowedOrigins").Get<string[]>() ?? [];
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowCredentials();
        }

        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddHealthChecks()
    .AddCheck<JwksCacheHealthCheck>("jwks-cache", tags: ["ready"]);

var app = builder.Build();

app.UseSeeSightCorrelationId();
app.UseMiddleware<RedisRateLimitMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<MustChangePasswordMiddleware>();

app.MapReverseProxy();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

// Aggregated health — pure convenience fan-out, no business logic (docs/Microservices.md §1).
app.MapGet("/health", async (
    IHttpClientFactory httpClientFactory,
    IOptions<IdentityServiceOptions> identityOptions,
    IOptions<TenantServiceOptions> tenantOptions,
    CancellationToken cancellationToken) =>
{
    async Task<bool> IsHealthyAsync(string baseUrl)
    {
        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(baseUrl);
        try
        {
            var response = await client.GetAsync("/health/ready", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    var identityHealthy = await IsHealthyAsync(identityOptions.Value.BaseUrl).ConfigureAwait(false);
    var tenantHealthy = await IsHealthyAsync(tenantOptions.Value.BaseUrl).ConfigureAwait(false);
    var allHealthy = identityHealthy && tenantHealthy;

    return Results.Json(
        new
        {
            status = allHealthy ? "Healthy" : "Unhealthy",
            services = new
            {
                identity = identityHealthy ? "Healthy" : "Unhealthy",
                tenant = tenantHealthy ? "Healthy" : "Unhealthy",
            },
        },
        statusCode: allHealthy ? 200 : 503);
});

app.Run();

#pragma warning disable CA1050 // required for WebApplicationFactory<Program> in Gateway tests
public partial class Program;
#pragma warning restore CA1050
