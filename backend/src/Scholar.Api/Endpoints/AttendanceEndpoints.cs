using Microsoft.AspNetCore.Mvc;
using Scholar.Api.Auth;
using Scholar.Application.Attendance;
using Scholar.Application.Common;
using Scholar.Domain.Access;

namespace Scholar.Api.Endpoints;

/// <summary>
/// Student and staff attendance, leave and the teacher's day. Endpoints without a permission are self-service
/// (signed-in staff acting on their own record); the services enforce the rest, including campus scope.
/// </summary>
internal static class AttendanceEndpoints
{
    private static ExportFormat Format(string? format) =>
        string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase) ? ExportFormat.Xlsx : ExportFormat.Csv;

    private static IResult Download(ExportFile file) => Results.File(file.Content, file.ContentType, file.FileName);

    public sealed record CancelRequest(string? Note);

    public static void MapAttendanceEndpoints(this IEndpointRouteBuilder api)
    {
        var attendance = api.MapGroup("/attendance").WithTags("Attendance");
        string[] view = [Permissions.Attendance.View, Permissions.Attendance.Manage];
        string[] take = [Permissions.Attendance.Record, Permissions.Attendance.Manage];

        // Registers
        attendance.MapGet("/registers/sheet", (Guid sectionId, DateOnly date, int? period, Guid? subjectId, RegisterService s, CancellationToken ct) =>
                s.SheetAsync(sectionId, date, period, subjectId, ct))
            .RequirePermission([.. view, Permissions.Attendance.Record])
            .WithSummary("The register to mark: saved marks, or suggested ones (leave, suspensions) when it has not been taken.");
        attendance.MapPut("/registers", (SaveRegisterRequest r, RegisterService s, CancellationToken ct) => s.SaveAsync(r, ct))
            .RequirePermission(take).WithSummary("Take or update a register until it locks. Same-day absences alert guardians.");
        attendance.MapPost("/registers/{id:guid}/corrections", (Guid id, RequestCorrectionRequest r, RegisterService s, CancellationToken ct) =>
                s.RequestCorrectionAsync(id, r, ct))
            .RequirePermission(take).WithSummary("Ask to change a locked register (applied at once for approvers).");
        attendance.MapGet("/corrections", ([AsParameters] CorrectionQuery q, RegisterService s, CancellationToken ct) => s.ListCorrectionsAsync(q, ct))
            .RequirePermission([.. view, Permissions.Attendance.Record, Permissions.Attendance.Approve]);
        attendance.MapPost("/corrections/{id:guid}/decision", (Guid id, DecideRequest r, RegisterService s, CancellationToken ct) =>
            s.DecideCorrectionAsync(id, r, ct)).RequirePermission(Permissions.Attendance.Approve);
        attendance.MapPost("/corrections/{id:guid}/withdraw", (Guid id, RegisterService s, CancellationToken ct) => s.WithdrawCorrectionAsync(id, ct))
            .RequirePermission([.. take, Permissions.Attendance.View]);

        // Reports
        attendance.MapGet("/overview", (DateOnly? date, Guid? campusId, AttendanceReportService s, CancellationToken ct) =>
            s.OverviewAsync(date, campusId, ct)).RequirePermission(view);
        attendance.MapGet("/trend", (DateOnly? from, DateOnly? to, Guid? campusId, AttendanceReportService s, CancellationToken ct) =>
            s.TrendAsync(from, to, campusId, ct)).RequirePermission(view);
        attendance.MapGet("/absentees", (DateOnly? date, Guid? campusId, Guid? sectionId, AttendanceReportService s, CancellationToken ct) =>
            s.AbsenteesAsync(date, campusId, sectionId, ct)).RequirePermission(view)
            .WithSummary("Absent and late students for a day with guardian contacts and alert status (the office call list).");
        attendance.MapGet("/sections/{id:guid}/month", (Guid id, int year, int month, AttendanceReportService s, CancellationToken ct) =>
            s.MonthAsync(id, year, month, ct)).RequirePermission(view);
        attendance.MapGet("/sections/{id:guid}/month/export", async (Guid id, int year, int month, string? format, AttendanceReportService s,
            CancellationToken ct) => Download(await s.ExportMonthAsync(id, year, month, Format(format), ct))).RequirePermission(Permissions.Attendance.Export);
        attendance.MapGet("/students/{id:guid}", (Guid id, Guid? academicYearId, AttendanceReportService s, CancellationToken ct) =>
            s.StudentAsync(id, academicYearId, ct)).RequirePermission(view);
        attendance.MapGet("/low", ([AsParameters] LowAttendanceQuery q, AttendanceReportService s, CancellationToken ct) => s.LowAttendanceAsync(q, ct))
            .RequirePermission(view).WithSummary("Students below the attendance threshold (default: the school's minimum).");
        attendance.MapGet("/low/export", async ([AsParameters] LowAttendanceQuery q, string? format, AttendanceReportService s, CancellationToken ct) =>
            Download(await s.ExportLowAttendanceAsync(q, Format(format), ct))).RequirePermission(Permissions.Attendance.Export);

        // Student leave
        var leave = attendance.MapGroup("/leave");
        leave.MapGet("/", ([AsParameters] StudentLeaveQuery q, StudentLeaveService s, CancellationToken ct) => s.ListAsync(q, ct))
            .RequirePermission(Permissions.Attendance.ManageLeave, Permissions.Attendance.View);
        leave.MapGet("/{id:guid}", (Guid id, StudentLeaveService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.Attendance.ManageLeave, Permissions.Attendance.View);
        leave.MapPost("/", (CreateStudentLeaveRequest r, StudentLeaveService s, CancellationToken ct) => s.CreateAsync(r, ct))
            .RequirePermission(Permissions.Attendance.ManageLeave);
        leave.MapPost("/{id:guid}/decision", (Guid id, DecideRequest r, StudentLeaveService s, CancellationToken ct) => s.DecideAsync(id, r, ct))
            .RequirePermission(Permissions.Attendance.ManageLeave);
        leave.MapPost("/{id:guid}/cancel", (Guid id, [FromBody] CancelRequest? r, StudentLeaveService s, CancellationToken ct) =>
            s.CancelAsync(id, r?.Note, ct)).RequirePermission(Permissions.Attendance.ManageLeave);

        // Bell schedules
        attendance.MapGet("/bell-schedules", (BellScheduleService s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission([.. view, Permissions.Attendance.Record, Permissions.Timetable.View, Permissions.Academics.View]);
        attendance.MapPut("/bell-schedules/{campusId:guid}", (Guid campusId, SaveBellScheduleRequest r, BellScheduleService s, CancellationToken ct) =>
            s.SaveAsync(campusId, r, ct)).RequirePermission(Permissions.Timetable.Manage);

        // Staff attendance
        var staff = api.MapGroup("/staff-attendance").WithTags("Staff attendance");
        staff.MapGet("/register", (Guid campusId, DateOnly? date, StaffAttendanceService s, CancellationToken ct) => s.RegisterAsync(campusId, date, ct))
            .RequirePermission(Permissions.Staff.ViewAttendance, Permissions.Staff.RecordAttendance);
        staff.MapPut("/register", (SaveStaffRegisterRequest r, StaffAttendanceService s, CancellationToken ct) => s.SaveAsync(r, ct))
            .RequirePermission(Permissions.Staff.RecordAttendance);
        staff.MapGet("/month", (Guid campusId, int year, int month, StaffAttendanceService s, CancellationToken ct) => s.MonthAsync(campusId, year, month, ct))
            .RequirePermission(Permissions.Staff.ViewAttendance, Permissions.Staff.RecordAttendance);
        staff.MapGet("/month/export", async (Guid campusId, int year, int month, string? format, StaffAttendanceService s, CancellationToken ct) =>
            Download(await s.ExportMonthAsync(campusId, year, month, Format(format), ct))).RequirePermission(Permissions.Staff.ViewAttendance);
        staff.MapPost("/corrections", (RequestStaffCorrectionRequest r, StaffAttendanceService s, CancellationToken ct) => s.RequestCorrectionAsync(r, ct))
            .WithSummary("Ask to correct a staff day: your own (any staff member) or anyone's once locked (staff register keepers).");
        staff.MapGet("/corrections", ([AsParameters] StaffCorrectionQuery q, StaffAttendanceService s, CancellationToken ct) => s.ListCorrectionsAsync(q, ct));
        staff.MapPost("/corrections/{id:guid}/decision", (Guid id, DecideRequest r, StaffAttendanceService s, CancellationToken ct) =>
            s.DecideCorrectionAsync(id, r, ct)).RequirePermission(Permissions.Staff.ApproveAttendance);
        staff.MapPost("/corrections/{id:guid}/withdraw", (Guid id, StaffAttendanceService s, CancellationToken ct) => s.WithdrawCorrectionAsync(id, ct));

        // Staff leave
        var staffLeave = api.MapGroup("/staff-leave").WithTags("Staff leave");
        staffLeave.MapGet("/types", (bool? includeInactive, StaffLeaveService s, CancellationToken ct) => s.ListTypesAsync(includeInactive == true, ct));
        staffLeave.MapPost("/types", (SaveLeaveTypeRequest r, StaffLeaveService s, CancellationToken ct) => s.SaveTypeAsync(null, r, ct))
            .RequirePermission(Permissions.Hr.Configure);
        staffLeave.MapPut("/types/{id:guid}", (Guid id, SaveLeaveTypeRequest r, StaffLeaveService s, CancellationToken ct) => s.SaveTypeAsync(id, r, ct))
            .RequirePermission(Permissions.Hr.Configure);
        staffLeave.MapDelete("/types/{id:guid}", async (Guid id, StaffLeaveService s, CancellationToken ct) =>
        {
            await s.DeleteTypeAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.Hr.Configure);
        staffLeave.MapGet("/", ([AsParameters] StaffLeaveQuery q, StaffLeaveService s, CancellationToken ct) => s.ListAsync(q, ct))
            .WithSummary("Leave requests: everyone's (in campus scope) with staff-leave access, otherwise your own.");
        staffLeave.MapGet("/{id:guid}", (Guid id, StaffLeaveService s, CancellationToken ct) => s.GetAsync(id, ct));
        staffLeave.MapPost("/", (CreateStaffLeaveRequest r, StaffLeaveService s, CancellationToken ct) => s.CreateAsync(r, ct))
            .WithSummary("Request leave for yourself, or record it for someone else (approvers).");
        staffLeave.MapPost("/{id:guid}/decision", (Guid id, DecideRequest r, StaffLeaveService s, CancellationToken ct) => s.DecideAsync(id, r, ct))
            .RequirePermission(Permissions.Hr.ApproveLeave);
        staffLeave.MapPost("/{id:guid}/cancel", (Guid id, [FromBody] CancelRequest? r, StaffLeaveService s, CancellationToken ct) =>
            s.CancelAsync(id, r?.Note, ct));
        staffLeave.MapGet("/balances", (Guid staffId, DateOnly? on, StaffLeaveService s, CancellationToken ct) => s.BalancesAsync(staffId, on, ct));

        // The teacher's day
        var teach = api.MapGroup("/teach").WithTags("Teacher");
        teach.MapGet("/today", (DateOnly? date, TeacherHomeService s, CancellationToken ct) => s.TodayAsync(date, ct));
        teach.MapGet("/my-attendance", (int? year, int? month, StaffAttendanceService s, CancellationToken ct) => s.MineAsync(year, month, ct));
    }
}
