using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SeeSight.SharedKernel.InternalAuth;

public static class InternalServiceTokenServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="InternalServiceTokenOptions"/> from the
    /// <c>Internal</c> configuration section and registers the fail-fast
    /// startup validator. Call <c>app.UseMiddleware&lt;InternalServiceTokenMiddleware&gt;()</c>
    /// separately in the request pipeline, early — before routing/authorization.
    /// </summary>
    public static IServiceCollection AddInternalServiceTokenValidation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<InternalServiceTokenOptions>, InternalServiceTokenOptionsValidator>();
        services.AddOptions<InternalServiceTokenOptions>()
            .Bind(configuration.GetSection(InternalServiceTokenOptions.SectionName))
            .ValidateOnStart();
        return services;
    }
}
