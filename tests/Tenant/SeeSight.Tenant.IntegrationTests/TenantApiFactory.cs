using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Infrastructure.Persistence;
using SeeSight.Tenant.IntegrationTests.TestSupport;
using Testcontainers.PostgreSql;

namespace SeeSight.Tenant.IntegrationTests;

/// <summary>
/// Boots the real Tenant.Api host against a real, ephemeral, Testcontainers-managed
/// Postgres instance — no mocks below the HTTP boundary except the one external
/// service call (Identity Service), which is faked per docs/CodingStandards.md §5.
/// </summary>
public sealed class TenantApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestInternalServiceToken = "integration-test-internal-service-token";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("tenant")
        .WithUsername("seesight")
        .WithPassword("seesight")
        .Build();

    public FakeIdentityServiceClient IdentityServiceClient { get; } = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TenantDb"] = _postgres.GetConnectionString(),
                ["Internal:ServiceToken"] = TestInternalServiceToken,
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IIdentityServiceClient>();
            services.AddSingleton<IIdentityServiceClient>(IdentityServiceClient);
        });
    }
}
