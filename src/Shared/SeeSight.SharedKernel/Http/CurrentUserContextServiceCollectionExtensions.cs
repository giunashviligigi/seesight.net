using Microsoft.Extensions.DependencyInjection;

namespace SeeSight.SharedKernel.Http;

public static class CurrentUserContextServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ICurrentUserContext"/>, scoped per request, populated
    /// from the Gateway-forwarded identity headers.
    /// </summary>
    public static IServiceCollection AddCurrentUserContext(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
        return services;
    }
}
