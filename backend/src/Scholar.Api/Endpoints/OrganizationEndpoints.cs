using Scholar.Api.Auth;
using Scholar.Application.Organization;
using Scholar.Domain.Access;
using Scholar.Domain.Platform;

namespace Scholar.Api.Endpoints;

internal static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this IEndpointRouteBuilder api)
    {
        var org = api.MapGroup("/org").WithTags("School configuration");

        org.MapGet("/overview", (OverviewService s, CancellationToken ct) => s.GetAsync(ct))
            .RequirePermission(Permissions.School.View)
            .WithSummary("Today's place in the academic calendar, structure counts, upcoming dates and setup progress.");
        org.MapGet("/campuses", (OverviewService s, CancellationToken ct) => s.ListCampusesAsync(ct))
            .WithSummary("Campuses of the school (lightweight list for pickers).");

        // School profile, modules, policies, numbering
        var school = org.MapGroup("/school");
        school.MapGet("/", (SchoolSettingsService s, CancellationToken ct) => s.GetAsync(ct)).RequirePermission(Permissions.School.View);
        school.MapPut("/profile", (UpdateSchoolProfileRequest r, SchoolSettingsService s, CancellationToken ct) => s.UpdateProfileAsync(r, ct))
            .RequirePermission(Permissions.School.Manage);
        school.MapPut("/policies", (SchoolSettings r, SchoolSettingsService s, CancellationToken ct) => s.UpdatePoliciesAsync(r, ct))
            .RequirePermission(Permissions.School.Manage);
        school.MapGet("/modules", (SchoolSettingsService s, CancellationToken ct) => s.ListModulesAsync(ct)).RequirePermission(Permissions.School.View);
        school.MapPut("/modules", (UpdateModulesRequest r, SchoolSettingsService s, CancellationToken ct) => s.UpdateModulesAsync(r, ct))
            .RequirePermission(Permissions.School.Manage);
        school.MapGet("/sequences", (SchoolSettingsService s, CancellationToken ct) => s.ListSequencesAsync(ct)).RequirePermission(Permissions.School.View);
        school.MapPut("/sequences/{key}", (string key, UpdateSequenceRequest r, SchoolSettingsService s, CancellationToken ct) =>
            s.UpdateSequenceAsync(key, r, ct)).RequirePermission(Permissions.School.Manage);

        // Campuses (full management)
        var campuses = org.MapGroup("/campus-management").RequirePermission(Permissions.School.View);
        campuses.MapGet("/", (CampusService s, CancellationToken ct) => s.ListAsync(ct));
        campuses.MapPost("/", (SaveCampusRequest r, CampusService s, CancellationToken ct) => s.CreateAsync(r, ct)).RequirePermission(Permissions.School.Manage);
        campuses.MapPut("/{id:guid}", (Guid id, SaveCampusRequest r, CampusService s, CancellationToken ct) => s.UpdateAsync(id, r, ct))
            .RequirePermission(Permissions.School.Manage);
        campuses.MapPost("/{id:guid}/make-main", (Guid id, CampusService s, CancellationToken ct) => s.MakeMainAsync(id, ct))
            .RequirePermission(Permissions.School.Manage);

        // Academic years & terms
        var years = org.MapGroup("/academic-years").RequirePermission(Permissions.Academics.View);
        years.MapGet("/", (AcademicYearService s, CancellationToken ct) => s.ListAsync(ct));
        years.MapPost("/", (SaveAcademicYearRequest r, AcademicYearService s, CancellationToken ct) => s.CreateAsync(r, ct))
            .RequirePermission(Permissions.Academics.Manage);
        years.MapPut("/{id:guid}", (Guid id, SaveAcademicYearRequest r, AcademicYearService s, CancellationToken ct) => s.UpdateAsync(id, r, ct))
            .RequirePermission(Permissions.Academics.Manage);
        years.MapDelete("/{id:guid}", async (Guid id, AcademicYearService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.Academics.Manage);
        years.MapPost("/{id:guid}/make-current", (Guid id, AcademicYearService s, CancellationToken ct) => s.MakeCurrentAsync(id, ct))
            .RequirePermission(Permissions.Academics.Manage);
        years.MapPost("/{id:guid}/close", (Guid id, AcademicYearService s, CancellationToken ct) => s.CloseAsync(id, ct))
            .RequirePermission(Permissions.Academics.Manage);
        years.MapPost("/{id:guid}/terms", (Guid id, TermInput r, AcademicYearService s, CancellationToken ct) => s.AddTermAsync(id, r, ct))
            .RequirePermission(Permissions.Academics.Manage);
        years.MapPut("/{id:guid}/terms/{termId:guid}", (Guid id, Guid termId, TermInput r, AcademicYearService s, CancellationToken ct) =>
            s.UpdateTermAsync(id, termId, r, ct)).RequirePermission(Permissions.Academics.Manage);
        years.MapDelete("/{id:guid}/terms/{termId:guid}", (Guid id, Guid termId, AcademicYearService s, CancellationToken ct) =>
            s.RemoveTermAsync(id, termId, ct)).RequirePermission(Permissions.Academics.Manage);

        // Grades & sections
        var grades = org.MapGroup("/grades").RequirePermission(Permissions.Academics.View);
        grades.MapGet("/", (GradeService s, CancellationToken ct) => s.ListAsync(ct));
        grades.MapPost("/", (SaveGradeRequest r, GradeService s, CancellationToken ct) => s.CreateAsync(r, ct)).RequirePermission(Permissions.Academics.Manage);
        grades.MapPut("/{id:guid}", (Guid id, SaveGradeRequest r, GradeService s, CancellationToken ct) => s.UpdateAsync(id, r, ct))
            .RequirePermission(Permissions.Academics.Manage);
        grades.MapPut("/order", (ReorderGradesRequest r, GradeService s, CancellationToken ct) => s.ReorderAsync(r, ct))
            .RequirePermission(Permissions.Academics.Manage);
        grades.MapDelete("/{id:guid}", async (Guid id, GradeService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.Academics.Manage);

        var sections = org.MapGroup("/sections");
        sections.MapGet("/", ([AsParameters] SectionQuery q, SectionService s, CancellationToken ct) => s.ListAsync(q, ct))
            .RequirePermission(Permissions.Academics.View, Permissions.Students.View, Permissions.Attendance.View);
        sections.MapPost("/", (SaveSectionRequest r, SectionService s, CancellationToken ct) => s.CreateAsync(r, ct))
            .RequirePermission(Permissions.Academics.Manage);
        sections.MapPut("/{id:guid}", (Guid id, SaveSectionRequest r, SectionService s, CancellationToken ct) => s.UpdateAsync(id, r, ct))
            .RequirePermission(Permissions.Academics.Manage);
        sections.MapDelete("/{id:guid}", async (Guid id, SectionService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.Academics.Manage);
        sections.MapPost("/copy", (CopySectionsRequest r, SectionService s, CancellationToken ct) => s.CopyAsync(r, ct))
            .RequirePermission(Permissions.Academics.Manage);

        // Subjects, groups, curriculum
        var subjects = org.MapGroup("/subjects").RequirePermission(Permissions.Academics.View);
        subjects.MapGet("/", (SubjectService s, CancellationToken ct) => s.ListAsync(ct));
        subjects.MapPost("/", (SaveSubjectRequest r, SubjectService s, CancellationToken ct) => s.SaveAsync(null, r, ct))
            .RequirePermission(Permissions.Academics.Manage);
        subjects.MapPut("/{id:guid}", (Guid id, SaveSubjectRequest r, SubjectService s, CancellationToken ct) => s.SaveAsync(id, r, ct))
            .RequirePermission(Permissions.Academics.Manage);
        subjects.MapDelete("/{id:guid}", async (Guid id, SubjectService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.Academics.Manage);
        subjects.MapGet("/groups", (SubjectService s, CancellationToken ct) => s.ListGroupsAsync(ct));
        subjects.MapPost("/groups", (SaveSubjectGroupRequest r, SubjectService s, CancellationToken ct) => s.SaveGroupAsync(null, r, ct))
            .RequirePermission(Permissions.Academics.Manage);
        subjects.MapPut("/groups/{id:guid}", (Guid id, SaveSubjectGroupRequest r, SubjectService s, CancellationToken ct) => s.SaveGroupAsync(id, r, ct))
            .RequirePermission(Permissions.Academics.Manage);

        var curriculum = org.MapGroup("/curriculum").RequirePermission(Permissions.Academics.View);
        curriculum.MapGet("/", (Guid? academicYearId, Guid? gradeId, SubjectService s, CancellationToken ct) =>
            s.GetCurriculumAsync(academicYearId, gradeId, ct));
        curriculum.MapPut("/", (SetCurriculumRequest r, SubjectService s, CancellationToken ct) => s.SetCurriculumAsync(r, ct))
            .RequirePermission(Permissions.Academics.Manage);
        curriculum.MapPost("/copy", (CopyCurriculumRequest r, SubjectService s, CancellationToken ct) => s.CopyCurriculumAsync(r, ct))
            .RequirePermission(Permissions.Academics.Manage);

        // Rooms
        var rooms = org.MapGroup("/rooms").RequirePermission(Permissions.Academics.View);
        rooms.MapGet("/", ([AsParameters] RoomQuery q, RoomService s, CancellationToken ct) => s.ListAsync(q, ct));
        rooms.MapPost("/", (SaveRoomRequest r, RoomService s, CancellationToken ct) => s.SaveAsync(null, r, ct)).RequirePermission(Permissions.Academics.Manage);
        rooms.MapPut("/{id:guid}", (Guid id, SaveRoomRequest r, RoomService s, CancellationToken ct) => s.SaveAsync(id, r, ct))
            .RequirePermission(Permissions.Academics.Manage);
        rooms.MapDelete("/{id:guid}", async (Guid id, RoomService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.Academics.Manage);

        // Calendar
        var calendar = org.MapGroup("/calendar").RequirePermission(Permissions.Academics.View, Permissions.Events.View);
        calendar.MapGet("/", ([AsParameters] CalendarQuery q, CalendarService s, CancellationToken ct) => s.ListAsync(q, ct));
        calendar.MapPost("/", (SaveCalendarEventRequest r, CalendarService s, CancellationToken ct) => s.SaveAsync(null, r, ct))
            .RequirePermission(Permissions.Academics.Manage, Permissions.Events.Manage);
        calendar.MapPut("/{id:guid}", (Guid id, SaveCalendarEventRequest r, CalendarService s, CancellationToken ct) => s.SaveAsync(id, r, ct))
            .RequirePermission(Permissions.Academics.Manage, Permissions.Events.Manage);
        calendar.MapDelete("/{id:guid}", async (Guid id, CalendarService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.Academics.Manage, Permissions.Events.Manage);

        // Grading scales
        var grading = org.MapGroup("/grading-scales").RequirePermission(Permissions.Academics.View);
        grading.MapGet("/", (GradingService s, CancellationToken ct) => s.ListAsync(ct));
        grading.MapPost("/", (SaveGradingScaleRequest r, GradingService s, CancellationToken ct) => s.SaveAsync(null, r, ct))
            .RequirePermission(Permissions.Academics.Manage);
        grading.MapPut("/{id:guid}", (Guid id, SaveGradingScaleRequest r, GradingService s, CancellationToken ct) => s.SaveAsync(id, r, ct))
            .RequirePermission(Permissions.Academics.Manage);
        grading.MapPost("/{id:guid}/make-default", (Guid id, GradingService s, CancellationToken ct) => s.MakeDefaultAsync(id, ct))
            .RequirePermission(Permissions.Academics.Manage);
        grading.MapDelete("/{id:guid}", async (Guid id, GradingService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.Academics.Manage);

        // Departments & designations
        var staffing = org.MapGroup("/").RequirePermission(Permissions.School.View);
        staffing.MapGet("/departments", (StaffStructureService s, CancellationToken ct) => s.ListDepartmentsAsync(ct));
        staffing.MapPost("/departments", (SaveDepartmentRequest r, StaffStructureService s, CancellationToken ct) => s.SaveDepartmentAsync(null, r, ct))
            .RequirePermission(Permissions.School.Manage);
        staffing.MapPut("/departments/{id:guid}", (Guid id, SaveDepartmentRequest r, StaffStructureService s, CancellationToken ct) =>
            s.SaveDepartmentAsync(id, r, ct)).RequirePermission(Permissions.School.Manage);
        staffing.MapDelete("/departments/{id:guid}", async (Guid id, StaffStructureService s, CancellationToken ct) =>
        {
            await s.DeleteDepartmentAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.School.Manage);
        staffing.MapGet("/designations", (StaffStructureService s, CancellationToken ct) => s.ListDesignationsAsync(ct));
        staffing.MapPost("/designations", (SaveDesignationRequest r, StaffStructureService s, CancellationToken ct) => s.SaveDesignationAsync(null, r, ct))
            .RequirePermission(Permissions.School.Manage);
        staffing.MapPut("/designations/{id:guid}", (Guid id, SaveDesignationRequest r, StaffStructureService s, CancellationToken ct) =>
            s.SaveDesignationAsync(id, r, ct)).RequirePermission(Permissions.School.Manage);
        staffing.MapDelete("/designations/{id:guid}", async (Guid id, StaffStructureService s, CancellationToken ct) =>
        {
            await s.DeleteDesignationAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.School.Manage);
    }
}
