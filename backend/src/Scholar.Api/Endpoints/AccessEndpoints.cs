using Scholar.Api.Auth;
using Scholar.Application.Access;
using Scholar.Application.Platform;
using Scholar.Domain.Access;

namespace Scholar.Api.Endpoints;

internal static class AccessEndpoints
{
    public static void MapAccessEndpoints(this IEndpointRouteBuilder api)
    {
        var access = api.MapGroup("/access").WithTags("Users & access");

        access.MapGet("/permissions", () => PermissionCatalog.Groups)
            .RequirePermission(Permissions.Roles.View, Permissions.Users.View)
            .WithSummary("The permission catalogue, grouped for the role editor.");

        access.MapGet("/roles", (RoleService roles, CancellationToken ct) => roles.ListAsync(ct))
            .RequirePermission(Permissions.Roles.View);
        access.MapGet("/roles/{id:guid}", (Guid id, RoleService roles, CancellationToken ct) => roles.GetAsync(id, ct))
            .RequirePermission(Permissions.Roles.View);
        access.MapPost("/roles", async (SaveRoleRequest request, RoleService roles, CancellationToken ct) =>
            {
                var role = await roles.CreateAsync(request, ct);
                return Results.Created($"/api/access/roles/{role.Id}", role);
            })
            .RequirePermission(Permissions.Roles.Manage);
        access.MapPut("/roles/{id:guid}", (Guid id, SaveRoleRequest request, RoleService roles, CancellationToken ct) =>
                roles.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.Roles.Manage);
        access.MapDelete("/roles/{id:guid}", async (Guid id, RoleService roles, CancellationToken ct) =>
            {
                await roles.DeleteAsync(id, ct);
                return Results.NoContent();
            })
            .RequirePermission(Permissions.Roles.Manage);

        access.MapGet("/audit", ([AsParameters] AuditQuery query, AuditService audit, CancellationToken ct) => audit.ListAsync(query, ct))
            .RequirePermission(Permissions.Audit.View)
            .WithSummary("The school's audit trail, newest first.");

        access.MapGet("/members", ([AsParameters] MemberQuery query, MemberService members, CancellationToken ct) =>
                members.ListAsync(query, ct))
            .RequirePermission(Permissions.Users.View);
        access.MapGet("/members/{userId:guid}", (Guid userId, MemberService members, CancellationToken ct) =>
                members.GetAsync(userId, ct))
            .RequirePermission(Permissions.Users.View);
        access.MapPost("/members", async (InviteMemberRequest request, MemberService members, CancellationToken ct) =>
            {
                var member = await members.InviteAsync(request, ct);
                return Results.Created($"/api/access/members/{member.UserId}", member);
            })
            .RequirePermission(Permissions.Users.Manage);
        access.MapPut("/members/{userId:guid}/roles", (Guid userId, UpdateMemberRolesRequest request, MemberService members,
                CancellationToken ct) => members.UpdateRolesAsync(userId, request, ct))
            .RequirePermission(Permissions.Users.Manage);
        access.MapPost("/members/{userId:guid}/suspend", async (Guid userId, MemberService members, CancellationToken ct) =>
            {
                await members.SetSuspendedAsync(userId, true, ct);
                return Results.NoContent();
            })
            .RequirePermission(Permissions.Users.Manage);
        access.MapPost("/members/{userId:guid}/reactivate", async (Guid userId, MemberService members, CancellationToken ct) =>
            {
                await members.SetSuspendedAsync(userId, false, ct);
                return Results.NoContent();
            })
            .RequirePermission(Permissions.Users.Manage);
    }

    public static void MapPlatformEndpoints(this IEndpointRouteBuilder api)
    {
        var platform = api.MapGroup("/platform").WithTags("Platform").RequirePlatformAdmin();

        platform.MapGet("/schools", (SchoolProvisioningService service, CancellationToken ct) => service.ListAsync(ct));
        platform.MapPost("/schools", async (ProvisionSchoolRequest request, SchoolProvisioningService service, CancellationToken ct) =>
        {
            var school = await service.ProvisionAsync(request, ct);
            return Results.Created($"/api/platform/schools/{school.Id}", school);
        }).WithSummary("Provision a new school with its main campus, default roles and owner account.");
        platform.MapPost("/schools/{id:guid}/suspend", async (Guid id, SchoolProvisioningService service, CancellationToken ct) =>
        {
            await service.SetStatusAsync(id, active: false, ct);
            return Results.NoContent();
        });
        platform.MapPost("/schools/{id:guid}/activate", async (Guid id, SchoolProvisioningService service, CancellationToken ct) =>
        {
            await service.SetStatusAsync(id, active: true, ct);
            return Results.NoContent();
        });
    }
}
