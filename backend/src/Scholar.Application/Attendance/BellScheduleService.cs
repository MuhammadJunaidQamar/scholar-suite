using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Application.Platform;
using Scholar.Domain.Access;
using Scholar.Domain.Attendance;
using Scholar.Domain.Platform;

namespace Scholar.Application.Attendance;

/// <summary>Each campus's school day (periods and breaks), used to label period registers and, later, timetables.</summary>
public sealed class BellScheduleService(IAppDbContext db, IAccessContext access)
{
    public async Task<IReadOnlyList<BellScheduleDto>> ListAsync(CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var campuses = await db.Campuses.Where(c => c.IsActive && (current.CampusIds == null || current.CampusIds.Contains(c.Id)))
            .OrderByDescending(c => c.IsMain).ThenBy(c => c.Name).Select(c => new { c.Id, c.Name }).ToListAsync(ct);
        var ids = campuses.Select(c => c.Id).ToList();
        var schedules = await db.BellSchedules.Include(s => s.Periods).Where(s => ids.Contains(s.CampusId)).ToListAsync(ct);
        return campuses.Select(c =>
        {
            var schedule = schedules.FirstOrDefault(s => s.CampusId == c.Id);
            return schedule is null ? new BellScheduleDto(null, c.Id, c.Name, "School day", []) : ToDto(schedule, c.Name);
        }).ToList();
    }

    internal async Task<IReadOnlyList<BellPeriodDto>> PeriodsForAsync(Guid campusId, CancellationToken ct) =>
        await db.BellPeriods.Where(p => db.BellSchedules.Any(s => s.Id == p.ScheduleId && s.CampusId == campusId))
            .OrderBy(p => p.Sequence)
            .Select(p => new BellPeriodDto(p.Sequence, p.Number, p.Name, p.StartTime, p.EndTime, p.Number == null))
            .ToListAsync(ct);

    public async Task<BellScheduleDto> SaveAsync(Guid campusId, SaveBellScheduleRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Timetable.Manage, ct);
        var current = await access.GetAsync(ct);
        var campus = current.CanAccessCampus(campusId) ? await db.Campuses.SingleOrDefaultAsync(c => c.Id == campusId, ct) : null;
        if (campus is null) throw new NotFoundException("Campus", campusId);

        var schedule = await db.BellSchedules.Include(s => s.Periods).SingleOrDefaultAsync(s => s.CampusId == campusId, ct);
        if (schedule is null)
        {
            schedule = new BellSchedule(current.SchoolId, campusId, request.Name);
            db.BellSchedules.Add(schedule);
        }

        schedule.Name = request.Name.Trim();
        var old = schedule.Periods.ToList();
        schedule.ReplacePeriods(request.Slots.Select(s => (s.Name, s.StartTime, s.EndTime, s.IsBreak)));
        db.BellPeriods.RemoveRange(old);
        await db.SaveChangesAsync(ct);
        return ToDto(schedule, campus.Name);
    }

    private static BellScheduleDto ToDto(BellSchedule schedule, string campusName) => new(schedule.Id, schedule.CampusId, campusName, schedule.Name,
        schedule.Periods.OrderBy(p => p.Sequence).Select(p => new BellPeriodDto(p.Sequence, p.Number, p.Name, p.StartTime, p.EndTime, p.IsBreak)).ToList());
}

/// <summary>Default staff leave types for a new school (Pakistani private-school norms; every value is editable).</summary>
internal sealed class AttendanceDefaultsProvisioningStep(IAppDbContext db) : ISchoolProvisioningStep
{
    public int Order => 30;

    public static readonly (string Code, string Name, decimal? Days, bool Paid, bool Document)[] LeaveTypes =
    [
        ("CL", "Casual leave", 10, true, false),
        ("SL", "Sick leave", 8, true, false),
        ("ML", "Maternity leave", 90, true, true),
        ("HL", "Hajj / Umrah leave", 40, false, false),
        ("UL", "Unpaid leave", null, false, false),
    ];

    public Task ApplyAsync(School school, CancellationToken ct)
    {
        var order = 1;
        foreach (var (code, name, days, paid, document) in LeaveTypes)
        {
            db.LeaveTypes.Add(new LeaveType(school.Id, code, name, days, paid) { RequiresDocument = document, SortOrder = order++ });
        }

        return Task.CompletedTask;
    }
}
