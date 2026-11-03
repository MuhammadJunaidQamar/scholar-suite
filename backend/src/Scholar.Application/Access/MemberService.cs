using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Application.Identity;
using Scholar.Domain.Access;
using Scholar.Domain.Common;
using Scholar.Domain.Identity;

namespace Scholar.Application.Access;

/// <summary>People who can sign in to the school: invitations, role assignments, suspension.</summary>
public sealed class MemberService(
    IAppDbContext db,
    IAccessContext access,
    RoleService roles,
    UserManager<User> users,
    AuthService auth,
    AccountOnboarding onboarding,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public async Task<PagedResult<MemberListItemDto>> ListAsync(MemberQuery query, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Users.View, ct);
        var memberships = db.SchoolMemberships.AsQueryable()
            .WhereIf(query.Status is not null, m => m.Status == query.Status)
            .WhereIf(query.RoleId is not null, m => db.UserRoles.Any(ur => ur.UserId == m.UserId && ur.RoleId == query.RoleId));

        if (query.Term is { } term)
        {
            var pattern = $"%{term}%";
            memberships = memberships.Where(m =>
                EF.Functions.ILike(m.User.FirstName + " " + m.User.LastName, pattern)
                || EF.Functions.ILike(m.User.Email ?? "", pattern)
                || EF.Functions.ILike(m.User.UserName ?? "", pattern));
        }

        var sorted = memberships.ApplySort(query, new SortMap<SchoolMembership>
        {
            ["name"] = m => m.User.FirstName + " " + m.User.LastName,
            ["email"] = m => m.User.Email,
            ["lastLogin"] = m => m.User.LastLoginAt,
        }, "name");

        var page = await sorted.ToPagedAsync(query, m => new
        {
            m.UserId,
            m.User.FirstName,
            m.User.LastName,
            m.User.Email,
            m.User.UserName,
            m.User.Status,
            MembershipStatus = m.Status,
            m.User.LastLoginAt,
        }, ct);

        var ids = page.Items.Select(i => i.UserId).ToList();
        var assignments = await LoadAssignmentsAsync(ids, ct);
        return new PagedResult<MemberListItemDto>(
            page.Items.Select(i => new MemberListItemDto(i.UserId, $"{i.FirstName} {i.LastName}", i.Email, i.UserName!,
                i.Status, i.MembershipStatus, i.LastLoginAt, assignments.GetValueOrDefault(i.UserId, []))).ToList(),
            page.Page, page.PageSize, page.Total);
    }

    public async Task<InviteMemberResult> InviteAsync(InviteMemberRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Users.Manage, ct);
        var current = await access.GetAsync(ct);
        var schoolId = current.SchoolId;
        var targetRoles = await ValidateAssignmentsAsync(request.Roles, ct);

        var email = request.Email.Trim();
        var normalizedEmail = users.NormalizeEmail(email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
        var isNew = user is null;
        if (user is null)
        {
            user = new User(email, request.FirstName, request.LastName, email) { Status = UserStatus.Invited };
            var created = await users.CreateAsync(user);
            if (!created.Succeeded)
            {
                throw new DomainExceptionFromIdentity(created);
            }
        }

        var membership = await db.SchoolMemberships.SingleOrDefaultAsync(m => m.UserId == user.Id, ct);
        if (membership is null)
        {
            db.SchoolMemberships.Add(new SchoolMembership(schoolId, user.Id));
        }
        else
        {
            Guard.Against(membership.Status == MembershipStatus.Active && await db.UserRoles.AnyAsync(ur => ur.UserId == user.Id, ct),
                "member.exists", "This person already has access to the school. Edit their roles instead.");
            membership.Status = MembershipStatus.Active;
        }

        await ReplaceAssignmentsAsync(user.Id, schoolId, targetRoles, ct);
        await db.SaveChangesAsync(ct);

        // People who already have an account (e.g. at another school) simply sign in; new accounts are handed over.
        AccountHandover? handover = null;
        if (isNew || user.PasswordHash is null)
        {
            var schoolName = await db.Schools.Where(s => s.Id == schoolId).Select(s => s.Name).SingleAsync(ct);
            handover = await onboarding.HandOverAsync(user, schoolName, ct);
        }

        access.Invalidate(user.Id);
        return new InviteMemberResult(await GetAsync(user.Id, ct), handover);
    }

    public async Task<MemberListItemDto> GetAsync(Guid userId, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Users.View, ct);
        var member = await db.SchoolMemberships.Where(m => m.UserId == userId)
            .Select(m => new
            {
                m.UserId, m.User.FirstName, m.User.LastName, m.User.Email, m.User.UserName,
                UserStatus = m.User.Status, MembershipStatus = m.Status, m.User.LastLoginAt,
            })
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Member", userId);
        var assignments = await LoadAssignmentsAsync([userId], ct);
        return new MemberListItemDto(member.UserId, $"{member.FirstName} {member.LastName}", member.Email, member.UserName!,
            member.UserStatus, member.MembershipStatus, member.LastLoginAt, assignments.GetValueOrDefault(userId, []));
    }

    public async Task<MemberListItemDto> UpdateRolesAsync(Guid userId, UpdateMemberRolesRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Users.Manage, ct);
        var current = await access.GetAsync(ct);
        _ = await db.SchoolMemberships.SingleOrDefaultAsync(m => m.UserId == userId, ct) ?? throw new NotFoundException("Member", userId);
        var targetRoles = await ValidateAssignmentsAsync(request.Roles, ct);

        // Removing roles also requires holding them: an administrator cannot strip an owner.
        var existing = await db.UserRoles.Where(ur => ur.UserId == userId).Include(ur => ur.Role).ThenInclude(r => r.Permissions).ToListAsync(ct);
        foreach (var removed in existing.Where(e => targetRoles.All(t => t.Role.Id != e.RoleId || t.CampusId != e.CampusId)))
        {
            await EnsureCanGrantRoleAsync(removed.Role, ct);
        }

        await EnsureOwnerRemainsAsync(userId, targetRoles.Select(t => t.Role), ct);
        await ReplaceAssignmentsAsync(userId, current.SchoolId, targetRoles, ct);
        await db.SaveChangesAsync(ct);
        access.Invalidate(userId);
        return await GetAsync(userId, ct);
    }

    public async Task SetSuspendedAsync(Guid userId, bool suspended, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Users.Manage, ct);
        Guard.Against(userId == currentUser.UserId, "member.self", "You cannot suspend your own access.");
        var membership = await db.SchoolMemberships.SingleOrDefaultAsync(m => m.UserId == userId, ct)
            ?? throw new NotFoundException("Member", userId);

        var targetRoles = (await db.UserRoles.Where(ur => ur.UserId == userId)
                .Include(ur => ur.Role).ThenInclude(r => r.Permissions).ToListAsync(ct))
            .Select(ur => ur.Role).DistinctBy(r => r.Id);
        foreach (var role in targetRoles)
        {
            await EnsureCanGrantRoleAsync(role, ct);
        }

        if (suspended)
        {
            await EnsureOwnerRemainsAsync(userId, [], ct);
        }

        membership.Status = suspended ? MembershipStatus.Suspended : MembershipStatus.Active;
        await db.SaveChangesAsync(ct);
        if (suspended)
        {
            await auth.RevokeAllSessionsAsync(userId, null, "membership_suspended", ct);
        }

        access.Invalidate(userId);
    }

    /// <summary>
    /// Issues a one-time password for a member who can't sign in (no e-mail delivery, forgotten password) and ends
    /// their sessions. Refused for your own account, for platform administrators, for accounts that also belong to
    /// another school (one school must not take over a person's access elsewhere), and for anyone holding a role the
    /// actor could not grant (no taking over a more privileged account).
    /// </summary>
    public async Task<AccountHandover> ResetPasswordAsync(Guid userId, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Users.Manage, ct);
        Guard.Against(userId == currentUser.UserId, "member.self", "Change your own password from your account settings.");
        var membership = await db.SchoolMemberships.Include(m => m.User).SingleOrDefaultAsync(m => m.UserId == userId, ct)
            ?? throw new NotFoundException("Member", userId);
        var user = membership.User;
        Guard.Against(user.IsPlatformAdmin, "member.platform_admin", "Platform administrators manage their own sign-in.");
        var shared = await db.SchoolMemberships.IgnoreQueryFilters()
            .AnyAsync(m => m.UserId == userId && m.SchoolId != membership.SchoolId, ct);
        Guard.Against(shared, "member.shared_account",
            "This person also signs in to another school, so only they can reset their password (from the sign-in page).");

        var targetRoles = (await db.UserRoles.Where(ur => ur.UserId == userId)
                .Include(ur => ur.Role).ThenInclude(r => r.Permissions).ToListAsync(ct))
            .Select(ur => ur.Role).DistinctBy(r => r.Id);
        foreach (var role in targetRoles)
        {
            await EnsureCanGrantRoleAsync(role, ct);
        }

        var handover = await onboarding.ResetToTemporaryPasswordAsync(user, ct);
        await auth.RevokeAllSessionsAsync(userId, null, "password_reset_by_admin", ct);
        db.AuditLogs.Add(new Domain.Auditing.AuditLog(membership.SchoolId, currentUser.UserId, "member.password_reset", "User", userId.ToString(),
            null, clock.GetUtcNow(), currentUser.IpAddress, currentUser.UserAgent));
        await db.SaveChangesAsync(ct);
        access.Invalidate(userId);
        return handover;
    }

    private async Task<List<(Role Role, Guid? CampusId)>> ValidateAssignmentsAsync(IReadOnlyList<RoleAssignmentInput> inputs,
        CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var roleIds = inputs.Select(i => i.RoleId).Distinct().ToList();
        var found = await db.Roles.Include(r => r.Permissions).Where(r => roleIds.Contains(r.Id)).ToListAsync(ct);
        if (found.Count != roleIds.Count)
        {
            throw new NotFoundException("Role");
        }

        var campusIds = inputs.Where(i => i.CampusId is not null).Select(i => i.CampusId!.Value).Distinct().ToList();
        if (campusIds.Count > 0 && await db.Campuses.CountAsync(c => campusIds.Contains(c.Id), ct) != campusIds.Count)
        {
            throw new NotFoundException("Campus");
        }

        // A campus-scoped administrator can only hand out access to their own campuses.
        foreach (var input in inputs)
        {
            Guard.Against(input.CampusId is null && !current.AllCampuses, "member.campus_scope",
                "You can only assign roles limited to your own campuses.");
            Guard.Against(input.CampusId is { } c && !current.CanAccessCampus(c), "member.campus_scope",
                "You can only assign roles for your own campuses.");
        }

        var result = inputs.Distinct().Select(i => (found.Single(r => r.Id == i.RoleId), i.CampusId)).ToList();
        foreach (var role in result.Select(r => r.Item1).Distinct())
        {
            await EnsureCanGrantRoleAsync(role, ct);
        }

        return result;
    }

    private async Task EnsureCanGrantRoleAsync(Role role, CancellationToken ct)
    {
        if (role.IsLocked)
        {
            var current = await access.GetAsync(ct);
            if (!current.RoleKeys.Contains(SystemRoles.Owner) && !currentUser.IsPlatformAdmin)
            {
                throw new ForbiddenException("Only a school owner can grant or remove the owner role.");
            }

            return;
        }

        await roles.EnsureCanGrantAsync(role.Permissions.Select(p => p.Permission), ct);
    }

    private async Task EnsureOwnerRemainsAsync(Guid userId, IEnumerable<Role> futureRoles, CancellationToken ct)
    {
        if (futureRoles.Any(r => r.Key == SystemRoles.Owner))
        {
            return;
        }

        var otherOwners = await db.UserRoles.CountAsync(ur => ur.UserId != userId && ur.Role.Key == SystemRoles.Owner
            && db.SchoolMemberships.Any(m => m.UserId == ur.UserId && m.Status == MembershipStatus.Active), ct);
        var isOwner = await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.Role.Key == SystemRoles.Owner, ct);
        Guard.Against(isOwner && otherOwners == 0, "member.last_owner", "The school must keep at least one active owner.");
    }

    private async Task ReplaceAssignmentsAsync(Guid userId, Guid schoolId, IReadOnlyList<(Role Role, Guid? CampusId)> target,
        CancellationToken ct)
    {
        var existing = await db.UserRoles.Where(ur => ur.UserId == userId).ToListAsync(ct);
        db.UserRoles.RemoveRange(existing.Where(e => target.All(t => t.Role.Id != e.RoleId || t.CampusId != e.CampusId)));
        foreach (var (role, campusId) in target.Where(t => existing.All(e => e.RoleId != t.Role.Id || e.CampusId != t.CampusId)))
        {
            db.UserRoles.Add(new UserRole(schoolId, userId, role.Id, campusId));
        }
    }

    private async Task<Dictionary<Guid, IReadOnlyList<RoleAssignmentDto>>> LoadAssignmentsAsync(IReadOnlyCollection<Guid> userIds,
        CancellationToken ct)
    {
        var rows = await db.UserRoles.Where(ur => userIds.Contains(ur.UserId))
            .Select(ur => new
            {
                ur.UserId, ur.RoleId, ur.Role.Name, ur.Role.Key, ur.CampusId,
                CampusName = db.Campuses.Where(c => c.Id == ur.CampusId).Select(c => c.Name).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.UserId).ToDictionary(g => g.Key,
            g => (IReadOnlyList<RoleAssignmentDto>)g.Select(r => new RoleAssignmentDto(r.RoleId, r.Name, r.Key, r.CampusId, r.CampusName))
                .OrderBy(r => r.RoleName).ToList());
    }
}

