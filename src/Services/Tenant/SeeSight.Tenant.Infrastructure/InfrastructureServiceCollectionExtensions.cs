using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SeeSight.SharedKernel.InternalAuth;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Infrastructure.Identity;
using SeeSight.Tenant.Infrastructure.Persistence;

namespace SeeSight.Tenant.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddTenantInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Resolved lazily, from IConfiguration in the *built* container, not
        // captured eagerly here — WebApplicationFactory-based integration
        // tests add their Testcontainers connection string via a deferred
        // configuration source that isn't merged into `configuration` until
        // the host finishes building, which happens after this method runs.
        // Eagerly reading (and closing over) the connection string here would
        // silently bake in the pre-override value.
        services.AddDbContext<TenantDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("TenantDb")
                ?? throw new InvalidOperationException("Connection string 'TenantDb' is required.");
            options.UseNpgsql(connectionString);
        });
        services.AddScoped<ITenantDbContext>(sp => sp.GetRequiredService<TenantDbContext>());

        services.AddOptions<IdentityServiceOptions>()
            .Bind(configuration.GetSection(IdentityServiceOptions.SectionName));

        services.AddHttpClient<IIdentityServiceClient, IdentityServiceClient>((serviceProvider, client) =>
        {
            var identityOptions = serviceProvider.GetRequiredService<IOptions<IdentityServiceOptions>>().Value;
            var internalTokenOptions = serviceProvider.GetRequiredService<IOptions<InternalServiceTokenOptions>>().Value;

            client.BaseAddress = new Uri(identityOptions.BaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Add(InternalServiceTokenHeaders.ServiceToken, internalTokenOptions.ServiceToken);
        });

        return services;
    }
}
