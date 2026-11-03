using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Domain.Access;
using Scholar.Domain.Common;

namespace Scholar.Application.Access;

/// <summary>
/// Role management. A user can never grant permissions they do not hold themselves, which prevents privilege
/// escalation through role editing or assignment.
/// </summary>
public sealed class RoleService(IAppDbContext db, IAccessContext access)
{
    public async Task<IReadOnlyList<RoleListItemDto>> ListAsync(CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Roles.View, ct);
        var roles = await db.Roles
            .OrderBy(r => r.Portal).ThenBy(r => r.IsSystem ? 0 : 1).ThenBy(r => r.Name)
            .Select(r => new
            {
                r.Id, r.Key, r.Name, r.Description, r.Portal, r.IsSystem, r.IsLocked,
                PermissionCount = r.Permissions.Count,
                MemberCount = db.UserRoles.Where(ur => ur.RoleId == r.Id).Select(ur => ur.UserId).Distinct().Count(),
            })
            .ToListAsync(ct);
        return roles.Select(r => new RoleListItemDto(r.Id, r.Key, r.Name, r.Description, r.Portal, r.IsSystem, r.IsLocked,
            r.IsLocked ? PermissionCatalog.Keys.Count : r.PermissionCount, r.MemberCount)).ToList();
    }

    public async Task<RoleDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Roles.View, ct);
        var role = await db.Roles.Include(r => r.Permissions).SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Role", id);
        return ToDetail(role);
    }

    public async Task<RoleDetailDto> CreateAsync(SaveRoleRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Roles.Manage, ct);
        await EnsureCanGrantAsync(request.Permissions, ct);
        await EnsureNameAvailableAsync(request.Name, null, ct);

        var current = await access.GetAsync(ct);
        var role = new Role(current.SchoolId, request.Name, request.Portal) { Description = request.Description };
        role.SetPermissions(request.Permissions);
        db.Roles.Add(role);
        await db.SaveChangesAsync(ct);
        return ToDetail(role);
    }

    public async Task<RoleDetailDto> UpdateAsync(Guid id, SaveRoleRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Roles.Manage, ct);
        var role = await db.Roles.Include(r => r.Permissions).SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Role", id);
        Guard.Against(role.IsLocked, "role.locked", "This role is locked and cannot be edited.");

        // Only the permissions being added need to be held by the editor.
        var added = request.Permissions.Except(role.Permissions.Select(p => p.Permission)).ToList();
        await EnsureCanGrantAsync(added, ct);
        await EnsureNameAvailableAsync(request.Name, role.Id, ct);

        role.Name = request.Name.Trim();
        role.Description = request.Description;
        if (!role.IsSystem)
        {
            role.Portal = request.Portal;
        }

        role.SetPermissions(request.Permissions);
        await db.SaveChangesAsync(ct);
        await InvalidateMembersAsync(role.Id, ct);
        return ToDetail(role);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Roles.Manage, ct);
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Role", id);
        Guard.Against(role.IsSystem, "role.system", "Built-in roles cannot be deleted. Remove their permissions instead.");
        var inUse = await db.UserRoles.AnyAsync(ur => ur.RoleId == id, ct);
        Guard.Against(inUse, "role.in_use", "Remove this role from all users before deleting it.");
        db.Roles.Remove(role);
        await db.SaveChangesAsync(ct);
    }

    internal async Task EnsureCanGrantAsync(IEnumerable<string> permissions, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        // Portal entitlements are not data privileges (portal data is scoped by ownership), so they never block grants.
        var missing = permissions.Where(p => !p.StartsWith("portal.", StringComparison.Ordinal) && !current.Has(p)).ToList();
        if (missing.Count > 0)
        {
            throw new ForbiddenException(
                $"You cannot grant permissions you do not have yourself ({string.Join(", ", missing.Take(5))}{(missing.Count > 5 ? "…" : "")}).");
        }
    }

    private async Task EnsureNameAvailableAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var normalized = name.Trim().ToLower();
        var taken = await db.Roles.AnyAsync(r => r.Name.ToLower() == normalized && r.Id != exceptId, ct);
        if (taken)
        {
            throw new ConflictException("role.name_taken", "A role with this name already exists.");
        }
    }

    private async Task InvalidateMembersAsync(Guid roleId, CancellationToken ct)
    {
        var userIds = await db.UserRoles.Where(ur => ur.RoleId == roleId).Select(ur => ur.UserId).Distinct().ToListAsync(ct);
        foreach (var userId in userIds)
        {
            access.Invalidate(userId);
        }
    }

    private static RoleDetailDto ToDetail(Role role) => new(role.Id, role.Key, role.Name, role.Description, role.Portal,
        role.IsSystem, role.IsLocked,
        role.IsLocked ? [.. PermissionCatalog.Keys.Order()] : role.Permissions.Select(p => p.Permission).Order().ToList());
}
