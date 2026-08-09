using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Infrastructure.Persistence;
using SeeSight.Identity.Infrastructure.Security;

namespace SeeSight.Identity.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Resolved lazily, from IConfiguration in the *built* container, not
        // captured eagerly here — WebApplicationFactory-based integration
        // tests add their Testcontainers connection string via a deferred
        // configuration source that isn't merged into `configuration` until
        // the host finishes building, which happens after this method runs.
        // Eagerly reading (and closing over) the connection string here would
        // silently bake in the pre-override value.
        services.AddDbContext<IdentityDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("IdentityDb")
                ?? throw new InvalidOperationException("Connection string 'IdentityDb' is required.");
            options.UseNpgsql(connectionString);
        });
        services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());

        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<RsaSigningKeyProvider>();
        services.AddSingleton<IJwtIssuer, RsaJwtIssuer>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenHasher, Sha256TokenHasher>();
        services.AddSingleton<IOpaqueTokenGenerator, SecureOpaqueTokenGenerator>();

        return services;
    }
}
