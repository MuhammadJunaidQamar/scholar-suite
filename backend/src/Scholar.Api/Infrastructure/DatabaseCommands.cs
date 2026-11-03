using Microsoft.EntityFrameworkCore;
using Scholar.Infrastructure.Persistence;

namespace Scholar.Api.Infrastructure;

/// <summary>
/// Command-line operations: <c>dotnet run -- migrate</c> applies migrations and exits. In Development the API also
/// migrates on start when <c>Database:MigrateOnStartup</c> is true.
/// </summary>
internal static class DatabaseCommands
{
    public static async Task<bool> TryRunAsync(WebApplication app, string[] args)
    {
        if (args.Contains("migrate", StringComparer.OrdinalIgnoreCase))
        {
            await MigrateAsync(app.Services);
            return true;
        }

        if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            await MigrateAsync(app.Services);
        }

        return false;
    }

    private static async Task MigrateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Database");
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Database is up to date");
            return;
        }

        logger.LogInformation("Applying {Count} migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
        await db.Database.MigrateAsync();
        logger.LogInformation("Migrations applied");
    }
}
