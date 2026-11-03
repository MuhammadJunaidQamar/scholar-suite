using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scholar.Application.Abstractions;
using Scholar.Infrastructure.Persistence;

namespace Scholar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. Set it with user-secrets or the ConnectionStrings__Default variable.");

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history");
                npgsql.CommandTimeout(60);
            });
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<ISequenceGenerator, SequenceGenerator>();
        services.AddScoped<Seeding.DemoSeeder>();
        services.AddSingleton<Application.Documents.IFileStorage, Documents.LocalFileStorage>();
        services.AddSingleton<Application.Common.ITabularExporter, Exports.TabularExporter>();
        services.AddSingleton<Application.Common.ITabularReader, Exports.TabularReader>();

        return services;
    }
}
