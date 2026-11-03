using Scholar.Domain.Access;

namespace Scholar.Application.Abstractions;

/// <summary>Who is calling. Populated from the authenticated session; empty for anonymous/background work.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid? SessionId { get; }
    /// <summary>The school the session is currently working in.</summary>
    Guid? SchoolId { get; }
    bool IsPlatformAdmin { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}

/// <summary>
/// The tenant used for data isolation. Defaults to the caller's school; background jobs and public endpoints
/// enter a school explicitly. Every tenant-owned query and write is bound to this value.
/// </summary>
public interface ITenantContext
{
    Guid? SchoolId { get; }

    /// <summary>Temporarily works within <paramref name="schoolId"/>; dispose to restore the previous tenant.</summary>
    IDisposable Enter(Guid schoolId);
}

/// <summary>The caller's effective rights in the active school, resolved server-side for every request.</summary>
public sealed record EffectiveAccess(
    Guid UserId,
    Guid SchoolId,
    IReadOnlySet<string> Permissions,
    IReadOnlySet<Guid>? CampusIds,
    IReadOnlySet<Portal> Portals,
    IReadOnlyList<string> RoleKeys)
{
    public bool Has(string permission) => Permissions.Contains(permission);
    public bool HasAny(params string[] permissions) => permissions.Any(Permissions.Contains);

    /// <summary>True when the user is not limited to particular campuses.</summary>
    public bool AllCampuses => CampusIds is null;
    public bool CanAccessCampus(Guid campusId) => CampusIds is null || CampusIds.Contains(campusId);
}

public interface IAccessContext
{
    /// <summary>Effective access of the current user in the current school; throws when there is none.</summary>
    Task<EffectiveAccess> GetAsync(CancellationToken ct = default);

    /// <summary>Throws <see cref="Common.ForbiddenException"/> unless the user holds the permission.</summary>
    Task RequireAsync(string permission, CancellationToken ct = default);

    /// <summary>Drops cached access for a user (after role changes).</summary>
    void Invalidate(Guid userId);
}

/// <summary>Allocates human-readable document numbers (gap-free within the surrounding transaction).</summary>
public interface ISequenceGenerator
{
    Task<string> NextAsync(string key, DateOnly date, CancellationToken ct = default);
}
