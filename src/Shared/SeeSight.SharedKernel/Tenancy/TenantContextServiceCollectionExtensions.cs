using Microsoft.Extensions.DependencyInjection;
using SeeSight.SharedKernel.Http;

namespace SeeSight.SharedKernel.Tenancy;

public static class TenantContextServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ITenantContext"/>, scoped per request, mapped from
    /// <see cref="ICurrentUserContext"/> — call <c>AddCurrentUserContext()</c>
    /// first (or alongside; both are scoped and order-independent at
    /// registration time, but <see cref="ICurrentUserContext"/> must resolve
    /// for this to have data).
    /// </summary>
    public static IServiceCollection AddTenantContext(this IServiceCollection services)
    {
        services.AddScoped<ITenantContext, CurrentUserTenantContext>();
        return services;
    }
}
