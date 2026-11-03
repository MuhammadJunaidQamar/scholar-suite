using Scholar.Domain.Common;
using Scholar.Domain.Identity;

namespace Scholar.Domain.Access;

/// <summary>Which experience a role lands in. A user with several roles can switch portals.</summary>
public enum Portal { Admin, Teacher, Family, Student }

/// <summary>A named bundle of permissions, owned by a school. System roles carry a stable <see cref="Key"/>.</summary>
public sealed class Role : TenantEntity, IHasAuditTrail
{
    private Role() { }

    public Role(Guid schoolId, string name, Portal portal, string? key = null, bool isSystem = false, bool isLocked = false)
    {
        SchoolId = schoolId;
        Name = name.Trim();
        Portal = portal;
        Key = key;
        IsSystem = isSystem;
        IsLocked = isLocked;
    }

    public string? Key { get; private set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Portal Portal { get; set; }
    public bool IsSystem { get; private set; }
    /// <summary>Locked roles (School Owner) always hold every permission and cannot be edited.</summary>
    public bool IsLocked { get; private set; }

    public ICollection<RolePermission> Permissions { get; } = new List<RolePermission>();

    public void SetPermissions(IEnumerable<string> permissions)
    {
        Guard.Against(IsLocked, "role.locked", "This role is locked and its permissions cannot be changed.");
        var wanted = permissions.Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var unknown = wanted.Where(p => !PermissionCatalog.Exists(p)).ToList();
        Guard.Against(unknown.Count > 0, "role.unknown_permission", $"Unknown permission(s): {string.Join(", ", unknown)}.");

        foreach (var existing in Permissions.Where(p => !wanted.Contains(p.Permission)).ToList())
        {
            Permissions.Remove(existing);
        }

        foreach (var permission in wanted.Where(p => Permissions.All(e => e.Permission != p)))
        {
            Permissions.Add(new RolePermission(Id, permission));
        }
    }

    /// <summary>Used by seeding/synchronisation to keep locked roles complete as the catalogue grows.</summary>
    public void GrantAllPermissions()
    {
        foreach (var permission in PermissionCatalog.Keys.Where(p => Permissions.All(e => e.Permission != p)))
        {
            Permissions.Add(new RolePermission(Id, permission));
        }
    }
}

public sealed class RolePermission
{
    private RolePermission() { }

    public RolePermission(Guid roleId, string permission)
    {
        RoleId = roleId;
        Permission = permission;
    }

    public Guid RoleId { get; private set; }
    public string Permission { get; private set; } = default!;
}

/// <summary>Assigns a role to a user within a school, optionally limited to one campus.</summary>
public sealed class UserRole : TenantEntity
{
    private UserRole() { }

    public UserRole(Guid schoolId, Guid userId, Guid roleId, Guid? campusId = null)
    {
        SchoolId = schoolId;
        UserId = userId;
        RoleId = roleId;
        CampusId = campusId;
    }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = default!;
    public Guid RoleId { get; private set; }
    public Role Role { get; private set; } = default!;
    /// <summary>Null = all campuses of the school.</summary>
    public Guid? CampusId { get; private set; }
}
