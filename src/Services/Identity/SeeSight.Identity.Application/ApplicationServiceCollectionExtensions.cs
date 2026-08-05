using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SeeSight.Identity.Application.Behaviors;

namespace SeeSight.Identity.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        var assembly = typeof(ApplicationServiceCollectionExtensions).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
