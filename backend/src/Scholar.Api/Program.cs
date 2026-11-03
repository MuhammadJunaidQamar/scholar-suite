using System.Text.Json.Serialization;
using Scholar.Api.Auth;
using Scholar.Api.Endpoints;
using Scholar.Api.Infrastructure;
using Scholar.Application;
using Scholar.Application.Abstractions;
using Scholar.Infrastructure;
using Scholar.Infrastructure.Identity;
using Scholar.Infrastructure.Persistence;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Scholar.Api"));

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        };
    });
    builder.Services.AddExceptionHandler<ApiExceptionHandler>();
    builder.Services.AddOpenApi();

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddScholarIdentity();
    builder.Services.AddScholarAuthentication(builder.Configuration);
    builder.Services.AddPermissionAuthorization();
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

    var app = builder.Build();

    if (await DatabaseCommands.TryRunAsync(app, args))
    {
        return;
    }

    // Request logging wraps the exception handler so it records the status actually sent (a 422 or 404 from a
    // handled exception is not logged as a 500).
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0} ms";
        options.EnrichDiagnosticContext = (diagnostics, http) =>
        {
            diagnostics.Set("UserId", http.User.FindFirst(ScholarClaims.UserId)?.Value ?? "-");
            diagnostics.Set("SchoolId", http.User.FindFirst(ScholarClaims.SchoolId)?.Value ?? "-");
        };
    });
    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseSecurityHeaders();

    if (!app.Environment.IsProduction())
    {
        app.MapOpenApi().AllowAnonymous();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Scholar API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "Scholar API";
        });
    }

    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseMiddleware<CsrfMiddleware>();
    app.UsePasswordChangeGate();
    app.UseAuthorization();

    app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

    var api = app.MapGroup("/api").AddEndpointFilter<ValidationFilter>();
    api.MapGet("/meta", (IConfiguration configuration) => new
        {
            product = configuration["Platform:ProductName"] ?? "Scholar Suite",
            version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
        })
        .WithTags("Meta")
        .AllowAnonymous();
    api.MapAuthEndpoints();
    api.MapAccessEndpoints();
    api.MapOrganizationEndpoints();
    api.MapStudentEndpoints();
    api.MapAdmissionEndpoints();
    api.MapStaffEndpoints();
    api.MapPlatformEndpoints();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException && ex.GetType().Name != "StopTheHostException")
{
    Log.Fatal(ex, "API host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
