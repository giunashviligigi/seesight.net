using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeeSight.Identity.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SeeSight.Identity.IntegrationTests;

/// <summary>
/// Boots the real Identity.Api host against a real, ephemeral, Testcontainers-managed
/// Postgres instance — no mocks below the HTTP boundary, per docs/CodingStandards.md §5.
/// </summary>
public sealed class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestInternalServiceToken = "integration-test-internal-service-token";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("identity")
        .WithUsername("seesight")
        .WithPassword("seesight")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
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
                ["ConnectionStrings:IdentityDb"] = _postgres.GetConnectionString(),
                ["Internal:ServiceToken"] = TestInternalServiceToken,
            });
        });
    }
}
