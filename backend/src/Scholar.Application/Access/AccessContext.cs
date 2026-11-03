using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Application.Identity;
using Scholar.Domain.Access;
using Scholar.Domain.Identity;

namespace Scholar.Application.Access;

/// <summary>
/// Resolves the caller's effective permissions, campus scope and portals for the active school. Results are cached
/// briefly per user+school and invalidated whenever that user's roles change, so revocations apply immediately on
/// this node and within <see cref="CacheFor"/> elsewhere.
/// </summary>
internal sealed class AccessContext(IAppDbContext db, ICurrentUser currentUser, IMemoryCache cache) : IAccessContext
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);
    private EffectiveAccess? _resolved;

    public async Task<EffectiveAccess> GetAsync(CancellationToken ct = default)
    {
        if (_resolved is not null)
        {
            return _resolved;
        }

        var userId = currentUser.UserId ?? throw new UnauthorizedException("auth.required", "Please sign in.");
        var schoolId = currentUser.SchoolId ?? throw new ForbiddenException("Select a school to continue.");
        var key = $"access:{userId:N}:{schoolId:N}:{Version(userId)}";
        if (cache.TryGetValue(key, out EffectiveAccess? cached) && cached is not null)
        {
            return _resolved = cached;
        }

        var access = await ResolveAsync(userId, schoolId, ct);
        cache.Set(key, access, CacheFor);
        return _resolved = access;
    }

    public async Task RequireAsync(string permission, CancellationToken ct = default)
    {
        var access = await GetAsync(ct);
        if (!access.Has(permission))
        {
            throw new ForbiddenException();
        }
    }

    public void Invalidate(Guid userId)
    {
        cache.Set($"access-version:{userId:N}", Version(userId) + 1, TimeSpan.FromHours(1));
        _resolved = null;
    }

    private int Version(Guid userId) => cache.TryGetValue($"access-version:{userId:N}", out int v) ? v : 0;

    private async Task<EffectiveAccess> ResolveAsync(Guid userId, Guid schoolId, CancellationToken ct)
    {
        var empty = new EffectiveAccess(userId, schoolId, new HashSet<string>(), new HashSet<Guid>(), new HashSet<Portal>(), []);

        if (currentUser.IsPlatformAdmin)
        {
            // Platform administrators act with full rights in any school they enter (audited at switch time).
            return new EffectiveAccess(userId, schoolId, PermissionCatalog.Keys, null,
                new HashSet<Portal> { Portal.Admin }, ["platform_admin"]);
        }

        var membershipActive = await db.SchoolMemberships.IgnoreQueryFilters()
            .AnyAsync(m => m.UserId == userId && m.SchoolId == schoolId && m.Status == MembershipStatus.Active, ct);
        if (!membershipActive)
        {
            return empty;
        }

        var assignments = await db.UserRoles.IgnoreQueryFilters()
            .Where(ur => ur.UserId == userId && ur.SchoolId == schoolId)
            .Select(ur => new
            {
                ur.CampusId,
                ur.Role.Key,
                ur.Role.Portal,
                ur.Role.IsLocked,
                Permissions = ur.Role.Permissions.Select(p => p.Permission).ToList(),
            })
            .ToListAsync(ct);

        if (assignments.Count == 0)
        {
            return empty;
        }

        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assignment in assignments)
        {
            permissions.UnionWith(assignment.IsLocked ? PermissionCatalog.Keys : assignment.Permissions);
        }

        HashSet<Guid>? campuses = assignments.Any(a => a.CampusId is null)
            ? null
            : assignments.Select(a => a.CampusId!.Value).ToHashSet();

        var portals = assignments.Select(a => a.Portal).ToHashSet();
        if (permissions.Contains(Permissions.Portals.Teacher)) portals.Add(Portal.Teacher);
        if (permissions.Contains(Permissions.Portals.Family)) portals.Add(Portal.Family);
        if (permissions.Contains(Permissions.Portals.Student)) portals.Add(Portal.Student);

        var roleKeys = assignments.Where(a => a.Key is not null).Select(a => a.Key!).Distinct().ToList();
        return new EffectiveAccess(userId, schoolId, permissions, campuses, portals, roleKeys);
    }
}
