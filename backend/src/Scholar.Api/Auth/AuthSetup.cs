using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scholar.Api.Infrastructure;
using Scholar.Application.Identity;

namespace Scholar.Api.Auth;

internal static class AuthSetup
{
    public const string AuthRateLimit = "auth";
    public const string PublicRateLimit = "public";
    private const string SessionItem = "auth.session";

    public static IServiceCollection AddScholarAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.Section))
            .Validate(o => o.JwtSigningKey.Length >= 32, "Auth:JwtSigningKey must be at least 32 characters.")
            .ValidateOnStart();
        services.AddSingleton<Application.Identity.IAccessTokenIssuer, JwtAccessTokenIssuer>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>>((jwt, authOptions) =>
            {
                var settings = authOptions.Value;
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = settings.Issuer,
                    ValidAudience = settings.Audience,
                    IssuerSigningKey = JwtAccessTokenIssuer.SigningKey(settings),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                };
                jwt.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrEmpty(context.Request.Headers.Authorization)
                            && context.Request.Cookies.TryGetValue(AuthCookies.AccessName(settings), out var token))
                        {
                            context.Token = token;
                            context.HttpContext.Items[AuthCookies.ViaCookieItem] = true;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var sid = context.Principal?.FindFirst(ScholarClaims.SessionId)?.Value;
                        var validator = context.HttpContext.RequestServices.GetRequiredService<ISessionValidator>();
                        var state = Guid.TryParse(sid, out var sessionId)
                            ? await validator.GetAsync(sessionId, context.HttpContext.RequestAborted)
                            : null;
                        if (state is not { IsActive: true })
                        {
                            context.Fail("Session is no longer active.");
                            return;
                        }

                        context.HttpContext.Items[SessionItem] = state;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        var expired = context.AuthenticateFailure is SecurityTokenExpiredException;
                        await WriteProblemAsync(context.Response, 401, "Sign-in required",
                            expired ? "Your session token has expired." : "Please sign in to continue.",
                            expired ? "auth.token_expired" : "auth.required");
                    },
                    OnForbidden = context => WriteProblemAsync(context.Response, 403, "Forbidden",
                        "You do not have permission to perform this action.", "access.forbidden"),
                };
            });

        services.AddAuthorization(options =>
        {
            // Secure by default: every endpoint requires an authenticated user unless it opts out explicitly.
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });

        var authPerMinute = configuration.GetValue("RateLimits:AuthPerMinute", 10);
        var publicPerMinute = configuration.GetValue("RateLimits:PublicPerMinute", 20);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) => new ValueTask(WriteProblemAsync(context.HttpContext.Response, 429,
                "Too many requests", "Please wait a moment and try again.", "rate_limited"));
            options.AddPolicy(AuthRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = authPerMinute, Window = TimeSpan.FromMinutes(1) }));
            options.AddPolicy(PublicRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = publicPerMinute, Window = TimeSpan.FromMinutes(1) }));
        });

        return services;
    }

    /// <summary>
    /// Accounts provisioned with a temporary password may only use the auth endpoints until they choose their own.
    /// </summary>
    public static IApplicationBuilder UsePasswordChangeGate(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (context.Items.TryGetValue(SessionItem, out var value) && value is SessionState { MustChangePassword: true }
            && context.Request.Path.StartsWithSegments("/api")
            && !context.Request.Path.StartsWithSegments("/api/auth")
            && !context.Request.Path.StartsWithSegments("/api/meta"))
        {
            await WriteProblemAsync(context.Response, 403, "Password change required",
                "Choose a new password to continue.", "auth.password_change_required");
            return;
        }

        await next(context);
    });

    public static async Task WriteProblemAsync(HttpResponse response, int status, string title, string detail, string code)
    {
        if (response.HasStarted)
        {
            return;
        }

        response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;
        await response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
    }
}
