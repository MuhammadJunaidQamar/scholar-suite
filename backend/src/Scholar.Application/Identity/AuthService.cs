using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Domain.Auditing;
using Scholar.Domain.Identity;
using Scholar.Domain.Platform;

namespace Scholar.Application.Identity;

/// <summary>
/// Sign-in, token rotation, password lifecycle and session management. All failures that could reveal whether an
/// account exists use the same generic message.
/// </summary>
public sealed class AuthService(
    IAppDbContext db,
    UserManager<User> users,
    IAccessTokenIssuer tokenIssuer,
    ISessionValidator sessions,
    IAccountNotifier notifier,
    ICurrentUser currentUser,
    TimeProvider clock,
    IOptions<AuthOptions> options,
    ILogger<AuthService> logger)
{
    private const string InvalidCredentials = "The email/username or password is incorrect.";

    /// <summary>A just-rotated refresh token presented again within this window is treated as a benign race.</summary>
    private static readonly TimeSpan RotationGrace = TimeSpan.FromSeconds(20);

    private static readonly Lazy<string> DummyHash = new(() =>
        new PasswordHasher<User>().HashPassword(null!, "timing-equaliser-" + Guid.NewGuid()));

    private AuthOptions Options => options.Value;

    public async Task<AuthResult> LoginAsync(LoginRequest request, ClientType clientType, CancellationToken ct)
    {
        var user = await FindByIdentifierAsync(request.Identifier, ct);
        if (user is null)
        {
            // Spend comparable time to a real verification so response timing does not reveal unknown accounts.
            users.PasswordHasher.VerifyHashedPassword(null!, DummyHash.Value, request.Password);
            await AuditAsync(null, null, "auth.login_failed", "User", null, "unknown_identifier", ct);
            throw new UnauthorizedException("auth.invalid_credentials", InvalidCredentials);
        }

        if (await users.IsLockedOutAsync(user))
        {
            var minutes = user.LockoutEnd is { } end ? Math.Max(1, (int)Math.Ceiling((end - clock.GetUtcNow()).TotalMinutes)) : 15;
            throw new UnauthorizedException("auth.locked_out",
                $"Too many failed attempts. Try again in {minutes} minute{(minutes == 1 ? "" : "s")}.");
        }

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            await AuditAsync(user.LastSchoolId, user.Id, "auth.login_failed", "User", user.Id.ToString(), "bad_password", ct);
            throw new UnauthorizedException("auth.invalid_credentials", InvalidCredentials);
        }

        if (user.Status == UserStatus.Disabled)
        {
            throw new UnauthorizedException("auth.disabled", "This account has been disabled. Please contact the school office.");
        }

        await users.ResetAccessFailedCountAsync(user);

        var schoolId = await ResolveSchoolAsync(user, request.SchoolCode, ct);
        var now = clock.GetUtcNow();
        var session = new UserSession(user.Id, schoolId, clientType, now, TimeSpan.FromDays(Options.SessionMaxDays),
            currentUser.IpAddress, currentUser.UserAgent);
        db.UserSessions.Add(session);
        var (refreshToken, refreshEntity) = CreateRefreshToken(session, now);
        db.RefreshTokens.Add(refreshEntity);

        user.LastLoginAt = now;
        user.LastSchoolId = schoolId ?? user.LastSchoolId;
        if (user.Status == UserStatus.Invited)
        {
            user.Status = UserStatus.Active;
        }

        await TouchMembershipAsync(user.Id, schoolId, now, ct);
        db.AuditLogs.Add(new AuditLog(schoolId, user.Id, "auth.login", "UserSession", session.Id.ToString(),
            null, now, currentUser.IpAddress, currentUser.UserAgent));
        await db.SaveChangesAsync(ct);

        return BuildResult(user, session, refreshToken, refreshEntity.ExpiresAt);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedException("auth.refresh_missing", "Your session has ended. Please sign in again.");
        }

        var now = clock.GetUtcNow();
        var hash = Hash(refreshToken);
        var token = await db.RefreshTokens.Include(t => t.Session).ThenInclude(s => s.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is null)
        {
            throw new UnauthorizedException("auth.refresh_invalid", "Your session has ended. Please sign in again.");
        }

        var session = token.Session;
        if (token.ConsumedAt is { } consumedAt)
        {
            if (now - consumedAt <= RotationGrace)
            {
                // Two tabs refreshed at once: the other request already rotated the cookie. Not an attack.
                throw new UnauthorizedException("auth.refresh_in_progress", "Session refresh already in progress.");
            }

            session.Revoke("refresh_token_reuse", now);
            db.AuditLogs.Add(new AuditLog(session.SchoolId, session.UserId, "auth.refresh_reuse_detected", "UserSession",
                session.Id.ToString(), null, now, currentUser.IpAddress, currentUser.UserAgent));
            await db.SaveChangesAsync(ct);
            sessions.Invalidate(session.Id);
            logger.LogWarning("Refresh token reuse detected for session {SessionId}; session revoked", session.Id);
            throw new UnauthorizedException("auth.refresh_reused", "Your session has ended for security reasons. Please sign in again.");
        }

        if (token.ExpiresAt <= now || !session.IsActive(now) || session.User.Status == UserStatus.Disabled)
        {
            throw new UnauthorizedException("auth.session_expired", "Your session has ended. Please sign in again.");
        }

        var (newToken, newEntity) = CreateRefreshToken(session, now);
        await db.InTransactionAsync(async innerCt =>
        {
            // Conditional update: only one concurrent request can consume a given token.
            var consumed = await db.RefreshTokens
                .Where(t => t.Id == token.Id && t.ConsumedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumedAt, now).SetProperty(t => t.ReplacedById, newEntity.Id), innerCt);
            if (consumed == 0)
            {
                throw new UnauthorizedException("auth.refresh_in_progress", "Session refresh already in progress.");
            }

            db.RefreshTokens.Add(newEntity);
            session.LastSeenAt = now;
            session.IpAddress = currentUser.IpAddress ?? session.IpAddress;
            await db.SaveChangesAsync(innerCt);
            return true;
        }, ct: ct);
        return BuildResult(session.User, session, newToken, newEntity.ExpiresAt);
    }

    public async Task LogoutAsync(CancellationToken ct)
    {
        if (currentUser.SessionId is not { } sessionId)
        {
            return;
        }

        var session = await db.UserSessions.SingleOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
        {
            return;
        }

        session.Revoke("logout", clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        sessions.Invalidate(sessionId);
    }

    /// <summary>Logout when the access token has already expired: the refresh token identifies the session.</summary>
    public async Task LogoutWithRefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var session = await db.RefreshTokens.Where(t => t.TokenHash == hash).Select(t => t.Session).SingleOrDefaultAsync(ct);
        if (session is null)
        {
            return;
        }

        session.Revoke("logout", clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        sessions.Invalidate(session.Id);
    }

    public async Task<AuthResult> SwitchSchoolAsync(Guid schoolId, CancellationToken ct)
    {
        var (user, session) = await RequireSessionAsync(ct);
        var allowed = await db.SchoolMemberships.IgnoreQueryFilters()
            .AnyAsync(m => m.UserId == user.Id && m.SchoolId == schoolId && m.Status == MembershipStatus.Active, ct)
            || user.IsPlatformAdmin;
        var schoolActive = await db.Schools.AnyAsync(s => s.Id == schoolId && s.Status == SchoolStatus.Active, ct);
        if (!allowed || !schoolActive)
        {
            throw new ForbiddenException("You do not have access to that school.");
        }

        var now = clock.GetUtcNow();
        session.SchoolId = schoolId;
        user.LastSchoolId = schoolId;
        await TouchMembershipAsync(user.Id, schoolId, now, ct);
        db.AuditLogs.Add(new AuditLog(schoolId, user.Id, "auth.school_switched", "UserSession", session.Id.ToString(),
            null, now, currentUser.IpAddress, currentUser.UserAgent));

        // Rotate the refresh token too, so the new school context survives the next refresh.
        var (refreshToken, refreshEntity) = CreateRefreshToken(session, now);
        await db.InTransactionAsync(async innerCt =>
        {
            await db.RefreshTokens.Where(t => t.SessionId == session.Id && t.ConsumedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumedAt, now).SetProperty(t => t.ReplacedById, refreshEntity.Id), innerCt);
            db.RefreshTokens.Add(refreshEntity);
            await db.SaveChangesAsync(innerCt);
            return true;
        }, ct: ct);
        sessions.Invalidate(session.Id);
        return BuildResult(user, session, refreshToken, refreshEntity.ExpiresAt);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        var user = await FindByIdentifierAsync(request.Identifier, ct);

        // Same response whether or not the account exists.
        if (user is null || user.Status == UserStatus.Disabled || string.IsNullOrEmpty(user.Email))
        {
            logger.LogInformation("Password reset requested for unknown or ineligible identifier");
            return;
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        var link = $"{Options.PublicAppUrl.TrimEnd('/')}/reset-password?uid={user.Id}&token={Uri.EscapeDataString(token)}";
        await notifier.SendPasswordResetAsync(user, link, ct);
        await AuditAsync(user.LastSchoolId, user.Id, "auth.password_reset_requested", "User", user.Id.ToString(), null, ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == request.UserId, ct);
        if (user is null)
        {
            throw new UnauthorizedException("auth.reset_invalid", "This reset link is invalid or has expired.");
        }

        var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
            {
                throw new UnauthorizedException("auth.reset_invalid", "This reset link is invalid or has expired.");
            }

            throw new DomainExceptionFromIdentity(result);
        }

        user.MustChangePassword = false;
        await users.SetLockoutEndDateAsync(user, null);
        await RevokeAllSessionsAsync(user.Id, exceptSessionId: null, "password_reset", ct);
        await AuditAsync(user.LastSchoolId, user.Id, "auth.password_reset", "User", user.Id.ToString(), null, ct);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        var (user, session) = await RequireSessionAsync(ct);
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
            {
                throw new FluentValidation.ValidationException(
                    [new FluentValidation.Results.ValidationFailure("CurrentPassword", "The current password is incorrect.")]);
            }

            throw new DomainExceptionFromIdentity(result);
        }

        user.MustChangePassword = false;
        await users.UpdateAsync(user);
        await RevokeAllSessionsAsync(user.Id, session.Id, "password_changed", ct);
        sessions.Invalidate(session.Id);
        await AuditAsync(session.SchoolId, user.Id, "auth.password_changed", "User", user.Id.ToString(), null, ct);
    }

    public async Task<IReadOnlyList<SessionDto>> ListSessionsAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("auth.required", "Please sign in.");
        var now = clock.GetUtcNow();
        var current = currentUser.SessionId;
        return await db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderByDescending(s => s.LastSeenAt)
            .Select(s => new SessionDto(s.Id, s.ClientType, s.UserAgent, s.IpAddress, s.CreatedAt, s.LastSeenAt, s.ExpiresAt,
                s.Id == current))
            .ToListAsync(ct);
    }

    public async Task RevokeSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("auth.required", "Please sign in.");
        var session = await db.UserSessions.SingleOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct)
            ?? throw new NotFoundException("Session", sessionId);
        session.Revoke("revoked_by_user", clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        sessions.Invalidate(sessionId);
    }

    public async Task<AuthUserDto> GetUserAsync(CancellationToken ct)
    {
        var (user, session) = await RequireSessionAsync(ct);
        return ToDto(user, session.SchoolId);
    }

    public async Task<IReadOnlyList<SchoolSummaryDto>> ListMySchoolsAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("auth.required", "Please sign in.");
        return await db.SchoolMemberships.IgnoreQueryFilters()
            .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active)
            .Join(db.Schools.Where(s => s.Status == SchoolStatus.Active), m => m.SchoolId, s => s.Id, (m, s) => s)
            .OrderBy(s => s.Name)
            .Select(s => new SchoolSummaryDto(s.Id, s.Code, s.Name, s.ShortName, s.LogoFileId, s.BrandColor))
            .ToListAsync(ct);
    }

    /// <summary>Revokes all of a user's sessions (used when disabling accounts or after credential changes).</summary>
    public async Task RevokeAllSessionsAsync(Guid userId, Guid? exceptSessionId, string reason, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var active = await db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.Id != exceptSessionId)
            .ToListAsync(ct);
        foreach (var session in active)
        {
            session.Revoke(reason, now);
            sessions.Invalidate(session.Id);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<User?> FindByIdentifierAsync(string identifier, CancellationToken ct)
    {
        identifier = identifier.Trim();
        if (identifier.Contains('@', StringComparison.Ordinal))
        {
            var email = users.NormalizeEmail(identifier);
            return await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == email, ct);
        }

        var name = users.NormalizeName(identifier);
        return await db.Users.SingleOrDefaultAsync(u => u.NormalizedUserName == name, ct);
    }

    private async Task<(User User, UserSession Session)> RequireSessionAsync(CancellationToken ct)
    {
        if (currentUser.SessionId is not { } sessionId)
        {
            throw new UnauthorizedException("auth.required", "Please sign in.");
        }

        var session = await db.UserSessions.Include(s => s.User).SingleOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || !session.IsActive(clock.GetUtcNow()))
        {
            throw new UnauthorizedException("auth.session_expired", "Your session has ended. Please sign in again.");
        }

        return (session.User, session);
    }

    private async Task<Guid?> ResolveSchoolAsync(User user, string? schoolCode, CancellationToken ct)
    {
        var memberships = await db.SchoolMemberships.IgnoreQueryFilters()
            .Where(m => m.UserId == user.Id && m.Status == MembershipStatus.Active)
            .Join(db.Schools.Where(s => s.Status == SchoolStatus.Active), m => m.SchoolId, s => s.Id,
                (m, s) => new { s.Id, s.Code, m.LastActiveAt })
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(schoolCode))
        {
            var code = schoolCode.Trim().ToLowerInvariant();
            var match = memberships.FirstOrDefault(m => m.Code == code);
            if (match is not null)
            {
                return match.Id;
            }

            if (user.IsPlatformAdmin && await db.Schools.Where(s => s.Code == code).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct) is { } adminSchool)
            {
                return adminSchool;
            }

            throw new UnauthorizedException("auth.no_school_access", "This account does not have access to that school.");
        }

        if (memberships.Count == 0)
        {
            return user.IsPlatformAdmin
                ? null
                : throw new UnauthorizedException("auth.no_school_access", "This account is not linked to an active school.");
        }

        return memberships.FirstOrDefault(m => m.Id == user.LastSchoolId)?.Id
               ?? memberships.OrderByDescending(m => m.LastActiveAt).First().Id;
    }

    private async Task TouchMembershipAsync(Guid userId, Guid? schoolId, DateTimeOffset now, CancellationToken ct)
    {
        if (schoolId is null)
        {
            return;
        }

        var membership = await db.SchoolMemberships.IgnoreQueryFilters()
            .SingleOrDefaultAsync(m => m.UserId == userId && m.SchoolId == schoolId, ct);
        if (membership is not null)
        {
            membership.LastActiveAt = now;
        }
    }

    private (string Token, RefreshToken Entity) CreateRefreshToken(UserSession session, DateTimeOffset now)
    {
        var raw = Base64Url(RandomNumberGenerator.GetBytes(32));
        var expires = now.AddDays(Options.RefreshTokenDays);
        if (expires > session.ExpiresAt)
        {
            expires = session.ExpiresAt;
        }

        return (raw, new RefreshToken(session.Id, Hash(raw), now, expires));
    }

    private AuthResult BuildResult(User user, UserSession session, string refreshToken, DateTimeOffset refreshExpires)
    {
        var (accessToken, accessExpires) = tokenIssuer.Issue(user, session);
        return new AuthResult(new AuthTokens(accessToken, accessExpires, refreshToken, refreshExpires, session.Id),
            ToDto(user, session.SchoolId));
    }

    private static AuthUserDto ToDto(User user, Guid? schoolId) =>
        new(user.Id, user.FirstName, user.LastName, user.FullName, user.Email, user.UserName!, user.MustChangePassword,
            user.IsPlatformAdmin, schoolId);

    private async Task AuditAsync(Guid? schoolId, Guid? userId, string action, string entityType, string? entityId,
        string? detail, CancellationToken ct)
    {
        db.AuditLogs.Add(new AuditLog(schoolId, userId, action, entityType, entityId,
            detail is null ? null : $"{{\"detail\":\"{detail}\"}}", clock.GetUtcNow(), currentUser.IpAddress, currentUser.UserAgent));
        await db.SaveChangesAsync(ct);
    }

    internal static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>Identity rejected a password or user change; surfaces as a validation error on the password field.</summary>
public sealed class DomainExceptionFromIdentity(IdentityResult result) : FluentValidation.ValidationException(
    result.Errors.Select(e => new FluentValidation.Results.ValidationFailure("NewPassword", e.Description)));
