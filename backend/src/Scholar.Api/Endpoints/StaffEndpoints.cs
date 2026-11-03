using Scholar.Api.Auth;
using Scholar.Application.Common;
using Scholar.Application.Staffing;
using Scholar.Domain.Access;

namespace Scholar.Api.Endpoints;

internal static class StaffEndpoints
{
    public static void MapStaffEndpoints(this IEndpointRouteBuilder api)
    {
        var staff = api.MapGroup("/staff").WithTags("Staff");
        staff.MapGet("/", ([AsParameters] StaffQuery q, StaffService s, CancellationToken ct) => s.ListAsync(q, ct))
            .RequirePermission(Permissions.Staff.View);
        staff.MapGet("/options", (Guid? campusId, StaffService s, CancellationToken ct) => s.OptionsAsync(campusId, ct))
            .RequirePermission(Permissions.Staff.View, Permissions.Academics.Manage, Permissions.Staff.RecordAttendance, Permissions.Hr.ApproveLeave)
            .WithSummary("Current staff for teacher pickers, teaching staff first.");
        staff.MapGet("/export", async ([AsParameters] StaffQuery q, string? format, StaffService s, CancellationToken ct) =>
        {
            var file = await s.ExportAsync(q, string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase) ? ExportFormat.Xlsx : ExportFormat.Csv, ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        }).RequirePermission(Permissions.Staff.Export);
        staff.MapGet("/{id:guid}", (Guid id, StaffService s, CancellationToken ct) => s.GetAsync(id, ct)).RequirePermission(Permissions.Staff.View);
        staff.MapPost("/", async (SaveStaffRequest r, StaffService s, CancellationToken ct) =>
        {
            var created = await s.CreateAsync(r, ct);
            return Results.Created($"/api/staff/{created.Id}", created);
        }).RequirePermission(Permissions.Staff.Create);
        staff.MapPut("/{id:guid}", (Guid id, SaveStaffRequest r, StaffService s, CancellationToken ct) => s.UpdateAsync(id, r, ct))
            .RequirePermission(Permissions.Staff.Update);
        staff.MapPost("/{id:guid}/status", (Guid id, ChangeStaffStatusRequest r, StaffService s, CancellationToken ct) =>
            s.ChangeStatusAsync(id, r, ct)).RequirePermission(Permissions.Staff.Update);
        staff.MapDelete("/{id:guid}", async (Guid id, StaffService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.Staff.Delete);

        staff.MapPost("/{id:guid}/account", (Guid id, GrantStaffAccessRequest r, StaffService s, CancellationToken ct) =>
                s.GrantAccessAsync(id, r, ct))
            .RequirePermission(Permissions.Staff.Update)
            .WithSummary("Give the staff member a sign-in account (their email) with the chosen roles.");
        staff.MapPut("/{id:guid}/account", (Guid id, LinkStaffAccountRequest r, StaffService s, CancellationToken ct) =>
                s.LinkAccountAsync(id, r, ct))
            .RequirePermission(Permissions.Staff.Update).WithSummary("Link an existing school account to this staff record.");
        staff.MapDelete("/{id:guid}/account", (Guid id, StaffService s, CancellationToken ct) => s.UnlinkAccountAsync(id, ct))
            .RequirePermission(Permissions.Staff.Update).WithSummary("Unlink the sign-in account (the account itself is kept).");

        var teaching = api.MapGroup("/org/teaching").WithTags("School configuration");
        teaching.MapGet("/", (Guid? academicYearId, Guid? campusId, TeachingAssignmentService s, CancellationToken ct) =>
                s.ListAsync(academicYearId, campusId, ct))
            .RequirePermission(Permissions.Academics.View, Permissions.Staff.View)
            .WithSummary("Class teachers and subject teachers for every section of a year.");
        teaching.MapPut("/sections/{sectionId:guid}", (Guid sectionId, SaveSectionTeachersRequest r, TeachingAssignmentService s,
            CancellationToken ct) => s.SaveAsync(sectionId, r, ct)).RequirePermission(Permissions.Academics.Manage);
    }
}
