using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Domain.Access;
using Scholar.Domain.Attendance;
using Scholar.Domain.Platform;

namespace Scholar.Application.Attendance;

/// <summary>
/// The teacher's day: registers they are responsible for today (class teacher roll calls; in period-wise mode, the
/// sections and subjects they teach), their own attendance, and what is waiting on them.
/// </summary>
public sealed class TeacherHomeService(IAppDbContext db, IAccessContext access, AttendanceCalendar calendar, BellScheduleService bells,
    StaffAttendanceService staffAttendance)
{
    public async Task<TeacherTodayDto> TodayAsync(DateOnly? date, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var day = date ?? await calendar.TodayAsync(ct);
        var settings = await calendar.SettingsAsync(ct);
        var staff = await db.Staff.SingleOrDefaultAsync(s => s.UserId == current.UserId, ct);
        if (staff is null)
        {
            var noStaffDay = await calendar.DescribeAsync(day, Guid.Empty, ct);
            return new TeacherTodayDto(day, noStaffDay, settings.Attendance.Mode, null, [], [], null, 0, 0);
        }

        var status = await calendar.DescribeAsync(day, staff.CampusId, ct);
        var yearId = await db.AcademicYears.Where(y => y.StartDate <= day && y.EndDate >= day).OrderByDescending(y => y.IsCurrent)
            .Select(y => (Guid?)y.Id).FirstOrDefaultAsync(ct);
        var mine = await db.Sections.Where(s => s.AcademicYearId == yearId && s.IsActive && s.ClassTeacherId == staff.Id)
            .OrderBy(s => s.Grade.LevelOrder).ThenBy(s => s.Name)
            .Select(s => new { s.Id, ClassName = s.Grade.Name + " – " + s.Name }).ToListAsync(ct);
        var teaching = settings.Attendance.Mode == AttendanceMode.PeriodWise
            ? await (from t in db.SectionSubjectTeachers
                    join s in db.Sections on t.SectionId equals s.Id
                    join subject in db.Subjects on t.SubjectId equals subject.Id
                    where t.StaffId == staff.Id && s.AcademicYearId == yearId && s.IsActive
                    orderby s.Grade.LevelOrder, s.Name, subject.Name
                    select new { SectionId = s.Id, ClassName = s.Grade.Name + " – " + s.Name, SubjectId = subject.Id, SubjectName = subject.Name })
                .ToListAsync(ct)
            : [];

        var sectionIds = mine.Select(m => m.Id).Concat(teaching.Select(t => t.SectionId)).Distinct().ToList();
        var registers = await db.AttendanceRegisters.Where(r => r.Date == day && sectionIds.Contains(r.SectionId))
            .Select(r => new
            {
                r.Id, r.SectionId, r.Period, r.SubjectId, r.CreatedAt,
                OnRoll = r.Records.Count,
                Absent = r.Records.Count(x => x.Status == AttendanceStatus.Absent),
            })
            .ToListAsync(ct);
        var onRoll = await db.Enrollments.Where(e => sectionIds.Contains(e.SectionId) && e.StartDate <= day && (e.EndDate == null || e.EndDate >= day))
            .GroupBy(e => e.SectionId).Select(g => new { g.Key, Count = g.Select(e => e.StudentId).Distinct().Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);

        var tasks = new List<TeacherRegisterTaskDto>();
        foreach (var section in mine)
        {
            var register = registers.FirstOrDefault(r => r.SectionId == section.Id && r.Period == null);
            tasks.Add(new TeacherRegisterTaskDto(section.Id, section.ClassName, "daily", null, null, null, register is not null,
                register?.OnRoll ?? onRoll.GetValueOrDefault(section.Id), register?.Absent, register?.CreatedAt));
        }

        foreach (var lesson in teaching)
        {
            var taken = registers.Where(r => r.SectionId == lesson.SectionId && r.Period != null && r.SubjectId == lesson.SubjectId)
                .OrderByDescending(r => r.Period).FirstOrDefault();
            tasks.Add(new TeacherRegisterTaskDto(lesson.SectionId, lesson.ClassName, "period", lesson.SubjectId, lesson.SubjectName, taken?.Period,
                taken is not null, taken?.OnRoll ?? onRoll.GetValueOrDefault(lesson.SectionId), taken?.Absent, taken?.CreatedAt));
        }

        var myDay = await db.StaffAttendanceRecords.Where(r => r.StaffId == staff.Id && r.Date == day)
            .Select(r => new MyDayDto(r.Status, r.CheckIn, r.CheckOut)).FirstOrDefaultAsync(ct);
        var pendingLeave = current.Has(Permissions.Attendance.ManageLeave) && mine.Count > 0
            ? await db.StudentLeaveRequests.CountAsync(l => l.Status == LeaveStatus.Pending
                                                           && db.Students.Any(s => s.Id == l.StudentId && s.CurrentSectionId != null && sectionIds.Contains(s.CurrentSectionId.Value)), ct)
            : 0;
        var pendingCorrections = await db.AttendanceCorrections.CountAsync(c => c.Status == CorrectionStatus.Pending && c.CreatedBy == current.UserId, ct)
                                 + await db.StaffAttendanceCorrections.CountAsync(c => c.Status == CorrectionStatus.Pending && c.StaffId == staff.Id, ct);
        return new TeacherTodayDto(day, status, settings.Attendance.Mode, await staffAttendance.MeAsync(staff, ct), tasks,
            await bells.PeriodsForAsync(staff.CampusId, ct), myDay, pendingLeave, pendingCorrections);
    }
}
