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
        var connectionString = configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException("Connection string 'IdentityDb' is required.");

        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());

        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<RsaSigningKeyProvider>();
        services.AddSingleton<IJwtIssuer, RsaJwtIssuer>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        return services;
    }
}
