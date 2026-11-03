using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scholar.Application.Identity;
using Scholar.Domain.Identity;
using Scholar.Infrastructure.Persistence;

namespace Scholar.Infrastructure.Identity;

public static class IdentityServices
{
    public static IServiceCollection AddScholarIdentity(this IServiceCollection services)
    {
        services.AddIdentityCore<User>(options =>
            {
                options.Password.RequiredLength = PasswordPolicy.MinimumLength;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 3;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                // Uniqueness of e-mail is enforced by a partial unique index (e-mail is optional for students).
                options.User.RequireUniqueEmail = false;
                options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyz0123456789-._@+";
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));
        services.AddDataProtection()
            .PersistKeysToDbContext<AppDbContext>()
            .SetApplicationName("scholar-suite");

        services.AddMemoryCache();
        services.AddScoped<ISessionValidator, SessionValidator>();
        services.AddScoped<IAccountNotifier, LoggingAccountNotifier>();
        return services;
    }
}

internal sealed class SessionValidator(AppDbContext db, IMemoryCache cache, TimeProvider clock) : ISessionValidator
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    public async Task<SessionState?> GetAsync(Guid sessionId, CancellationToken ct)
    {
        var key = Key(sessionId);
        if (cache.TryGetValue(key, out SessionState? cached))
        {
            return cached;
        }

        var now = clock.GetUtcNow();
        var state = await db.UserSessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new SessionState(s.Id, s.UserId,
                s.RevokedAt == null && s.ExpiresAt > now && s.User.Status != UserStatus.Disabled,
                s.User.MustChangePassword))
            .SingleOrDefaultAsync(ct);
        cache.Set(key, state, CacheFor);
        return state;
    }

    public void Invalidate(Guid sessionId) => cache.Remove(Key(sessionId));

    private static string Key(Guid sessionId) => $"session:{sessionId:N}";
}

/// <summary>
/// Development stand-in until the communication module delivers real e-mail: writes links to the log.
/// It never claims delivery.
/// </summary>
internal sealed class LoggingAccountNotifier(ILogger<LoggingAccountNotifier> logger) : IAccountNotifier
{
    public Task SendPasswordResetAsync(User user, string resetLink, CancellationToken ct)
    {
        logger.LogWarning("[dev-mail] Password reset for {UserName}: {Link}", user.UserName, resetLink);
        return Task.CompletedTask;
    }

    public Task SendInvitationAsync(User user, string schoolName, string setupLink, CancellationToken ct)
    {
        logger.LogWarning("[dev-mail] Invitation for {UserName} to {School}: {Link}", user.UserName, schoolName, setupLink);
        return Task.CompletedTask;
    }
}
