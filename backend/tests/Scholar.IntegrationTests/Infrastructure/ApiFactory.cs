using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Scholar.Infrastructure.Persistence;

namespace Scholar.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API against a dedicated PostgreSQL database (never an in-memory substitute). The schema is migrated
/// once per run; every test class starts from empty tables via Respawn.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SigningKey = "integration-tests-signing-key-0123456789abcdefghij";

    private Respawner? _respawner;

    public ApiFactory()
    {
        ConnectionString = ResolveConnectionString();
        // Environment variables are read by WebApplication.CreateBuilder, before Program registers services.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", ConnectionString);
        Environment.SetEnvironmentVariable("Auth__JwtSigningKey", SigningKey);
        Environment.SetEnvironmentVariable("Auth__SecureCookies", "false");
        Environment.SetEnvironmentVariable("RateLimits__AuthPerMinute", "100000");
        Environment.SetEnvironmentVariable("RateLimits__PublicPerMinute", "100000");
    }

    public string ConnectionString { get; }
    public TestClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    public async Task InitializeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        }

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Table("__ef_migrations_history")],
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);
        Clock.Reset();
    }

    /// <summary>A client whose cookies the test can inspect (needed to echo the CSRF token).</summary>
    public BrowserClient CreateBrowserClient()
    {
        var cookies = new CookieContainer();
        var client = CreateDefaultClient(new Uri("http://localhost"), new CookieContainerHandler(cookies));
        return new BrowserClient(client, cookies);
    }

    public new Task DisposeAsync() => base.DisposeAsync().AsTask();

    private static string ResolveConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        return configuration["SCHOLAR_TEST_CONNECTION"]
               ?? configuration.GetConnectionString("Test")
               ?? throw new InvalidOperationException(
                   "No test database configured. Set SCHOLAR_TEST_CONNECTION or the API user-secret ConnectionStrings:Test.");
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

/// <summary>Base for integration tests: shares the host, resets data before each test class.</summary>
[Collection(ApiCollection.Name)]
public abstract class IntegrationTest(ApiFactory factory) : IAsyncLifetime
{
    protected ApiFactory Factory { get; } = factory;

    public virtual Task InitializeAsync() => Factory.ResetDatabaseAsync();

    public virtual Task DisposeAsync() => Task.CompletedTask;
}

public sealed class TestClock : TimeProvider
{
    private TimeSpan _offset;

    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow + _offset;

    public void Advance(TimeSpan by) => _offset += by;

    public void Reset() => _offset = TimeSpan.Zero;
}
