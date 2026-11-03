using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Scholar.Application.Identity;

namespace Scholar.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.AddScoped<AuthService>();
        return services;
    }
}
