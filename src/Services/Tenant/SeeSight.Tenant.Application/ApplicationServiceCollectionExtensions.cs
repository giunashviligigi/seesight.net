using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SeeSight.Tenant.Application.Behaviors;
using SeeSight.Tenant.Application.Common;

namespace SeeSight.Tenant.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddTenantApplication(this IServiceCollection services)
    {
        var assembly = typeof(ApplicationServiceCollectionExtensions).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ITenantResolver, TenantResolver>();

        return services;
    }
}
