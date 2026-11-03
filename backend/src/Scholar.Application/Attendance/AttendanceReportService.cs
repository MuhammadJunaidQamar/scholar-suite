using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Application.Communication;
using Scholar.Domain.Access;
using Scholar.Domain.Attendance;
using Scholar.Domain.Communication;
using Scholar.Domain.Staffing;
using Scholar.Domain.Students;

namespace Scholar.Application.Attendance;

/// <summary>
/// Attendance reporting: today's picture, trends, the absentee call list, monthly registers, a student's record and
/// the low-attendance list. Every figure is computed from stored registers; percentages follow the school's
/// "leave counts as present" policy.
/// </summary>
public sealed class AttendanceReportService(
    IAppDbContext db,
    IAccessContext access,
    AttendanceCalendar calendar,
    RegisterService registers,
    StudentLeaveService leave,
    MessageDispatcher dispatcher,
    ITabularExporter exporter)
{
    private const int MaxRangeDays = 400;

    // ───────────────────────────── Daily overview ─────────────────────────────

    public async Task<AttendanceOverviewDto> OverviewAsync(DateOnly? date, Guid? campusId, CancellationToken ct)
    {
        var current = await RequireViewAsync(ct);
        var day = date ?? await calendar.TodayAsync(ct);
        var settings = await calendar.SettingsAsync(ct);
        var status = await calendar.DescribeAsync(day, campusId ?? Guid.Empty, ct);

        var yearId = await YearForAsync(day, ct);
        var sections = await ScopedSections(current, yearId, campusId)
            .OrderBy(s => s.Grade.LevelOrder).ThenBy(s => s.Name)
            .Select(s => new
            {
                s.Id, ClassName = s.Grade.Name + " – " + s.Name, s.Grade.LevelOrder, s.CampusId,
                Campus = db.Campuses.Where(c => c.Id == s.CampusId).Select(c => c.Name).First(),
                Teacher = db.Staff.Where(t => t.Id == s.ClassTeacherId).Select(t => t.FullName).FirstOrDefault(),
            })
            .ToListAsync(ct);
        var sectionIds = sections.Select(s => s.Id).ToList();
        var onRoll = await OnRollAsync(sectionIds, day, ct);
        var taken = await db.AttendanceRegisters.Where(r => r.Date == day && r.Period == null && sectionIds.Contains(r.SectionId))
            .Select(r => new { r.Id, r.SectionId, r.CreatedAt, r.CreatedBy }).ToListAsync(ct);
        var registerIds = taken.Select(r => r.Id).ToList();
        var counts = await db.AttendanceRecords.Where(r => registerIds.Contains(r.RegisterId))
            .GroupBy(r => new { r.RegisterId, r.Status }).Select(g => new { g.Key.RegisterId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);
        var names = await registers.UserNamesAsync(taken.Select(t => t.CreatedBy), ct);

        var rows = sections.Select(s =>
        {
            var register = taken.FirstOrDefault(t => t.SectionId == s.Id);
            int C(AttendanceStatus st) => register is null ? 0 : counts.Where(c => c.RegisterId == register.Id && c.Status == st).Sum(c => c.Count);
            return new SectionDayDto(s.Id, s.ClassName, s.LevelOrder, s.CampusId, s.Campus, s.Teacher, onRoll.GetValueOrDefault(s.Id),
                register?.Id, register?.CreatedAt, RegisterService.Name(names, register?.CreatedBy), C(AttendanceStatus.Present),
                C(AttendanceStatus.Late), C(AttendanceStatus.Absent), C(AttendanceStatus.Leave));
        }).ToList();

        int Sum(Func<SectionDayDto, int> pick) => rows.Sum(pick);
        var totals = new OverviewTotalsDto(rows.Count, rows.Count(r => r.RegisterId is not null), Sum(r => r.OnRoll),
            Sum(r => r.Present + r.Late + r.Absent + r.Leave), Sum(r => r.Present), Sum(r => r.Late), Sum(r => r.Absent), Sum(r => r.Leave),
            AttendanceCalendar.Percentage(Sum(r => r.Present), Sum(r => r.Late), Sum(r => r.Absent), Sum(r => r.Leave),
                settings.Attendance.ExcusedCountsAsPresent));

        var pendingCorrections = current.Has(Permissions.Attendance.Approve)
            ? await db.AttendanceCorrections.CountAsync(c => c.Status == CorrectionStatus.Pending
                                                             && db.AttendanceRegisters.Any(r => r.Id == c.RegisterId && (current.CampusIds == null || current.CampusIds.Contains(r.CampusId))), ct)
            : 0;
        var pendingLeave = await db.StudentLeaveRequests.CountAsync(l => l.Status == LeaveStatus.Pending
                                                                        && db.Students.Any(s => s.Id == l.StudentId && (current.CampusIds == null || current.CampusIds.Contains(s.CampusId))), ct);
        var alerts = await AbsenceNotifier.CountsAsync(db.OutboundMessages.Where(m => m.RelatedId != null && registerIds.Contains(m.RelatedId.Value)),
            dispatcher, ct);

        StaffDayTotalsDto? staff = null;
        if (current.Has(Permissions.Staff.ViewAttendance))
        {
            staff = await StaffDayTotalsAsync(current, day, campusId, ct);
        }

        return new AttendanceOverviewDto(day, status, settings.Attendance.Mode, totals, rows, pendingCorrections, pendingLeave, alerts, staff);
    }

    private async Task<StaffDayTotalsDto> StaffDayTotalsAsync(EffectiveAccess current, DateOnly day, Guid? campusId, CancellationToken ct)
    {
        var staff = db.Staff.Where(s => s.Status == StaffStatus.Active || s.Status == StaffStatus.OnLeave)
            .Where(s => s.JoinDate <= day)
            .WhereIf(campusId is not null, s => s.CampusId == campusId);
        if (!current.AllCampuses) staff = staff.Where(s => current.CampusIds!.Contains(s.CampusId));
        var total = await staff.CountAsync(ct);
        var marks = await db.StaffAttendanceRecords.Where(r => r.Date == day && staff.Any(s => s.Id == r.StaffId))
            .GroupBy(r => r.Status).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        int C(StaffAttendanceStatus s) => marks.GetValueOrDefault(s);
        return new StaffDayTotalsDto(total, marks.Values.Sum(), C(StaffAttendanceStatus.Present), C(StaffAttendanceStatus.Late),
            C(StaffAttendanceStatus.HalfDay), C(StaffAttendanceStatus.Absent), C(StaffAttendanceStatus.OnLeave), C(StaffAttendanceStatus.OnDuty));
    }

    // ───────────────────────────── Trend ─────────────────────────────

    public async Task<IReadOnlyList<TrendPointDto>> TrendAsync(DateOnly? from, DateOnly? to, Guid? campusId, CancellationToken ct)
    {
        var current = await RequireViewAsync(ct);
        var end = to ?? await calendar.TodayAsync(ct);
        var start = from ?? end.AddDays(-29);
        Guard(end < start || end.DayNumber - start.DayNumber > MaxRangeDays, "Choose a range of at most 400 days.");
        var settings = await calendar.SettingsAsync(ct);
        var records = DailyRecords(current, start, end, campusId);
        var rows = await records.GroupBy(r => new { r.Register.Date, r.Status })
            .Select(g => new { g.Key.Date, g.Key.Status, Count = g.Count() }).ToListAsync(ct);
        var closed = await calendar.ClosedReasonsAsync(start, end, ct);

        var points = new List<TrendPointDto>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            var dayRows = rows.Where(r => r.Date == d).ToList();
            if (dayRows.Count == 0 && closed(d, campusId ?? Guid.Empty) is not null) continue;
            int C(AttendanceStatus s) => dayRows.Where(r => r.Status == s).Sum(r => r.Count);
            points.Add(new TrendPointDto(d, dayRows.Sum(r => r.Count), C(AttendanceStatus.Present), C(AttendanceStatus.Late),
                C(AttendanceStatus.Absent), C(AttendanceStatus.Leave),
                AttendanceCalendar.Percentage(C(AttendanceStatus.Present), C(AttendanceStatus.Late), C(AttendanceStatus.Absent),
                    C(AttendanceStatus.Leave), settings.Attendance.ExcusedCountsAsPresent)));
        }

        return points;
    }

    // ───────────────────────────── Absentee call list ─────────────────────────────

    public async Task<IReadOnlyList<AbsenteeDto>> AbsenteesAsync(DateOnly? date, Guid? campusId, Guid? sectionId, CancellationToken ct)
    {
        var current = await RequireViewAsync(ct);
        var day = date ?? await calendar.TodayAsync(ct);
        var rows = await DailyRecords(current, day, day, campusId)
            .WhereIf(sectionId is not null, r => r.Register.SectionId == sectionId)
            .Where(r => r.Status == AttendanceStatus.Absent || r.Status == AttendanceStatus.Late)
            .Select(r => new
            {
                r.StudentId, r.Status, r.ArrivedAt, r.Remark, r.RegisterId, r.Register.SectionId,
                ClassName = db.Sections.Where(s => s.Id == r.Register.SectionId).Select(s => s.Grade.Name + " – " + s.Name).First(),
                GradeOrder = db.Sections.Where(s => s.Id == r.Register.SectionId).Select(s => s.Grade.LevelOrder).First(),
                Student = db.Students.Where(s => s.Id == r.StudentId).Select(s => new
                {
                    s.FullName, s.AdmissionNumber, s.PhotoFileId,
                    Guardian = s.Guardians.OrderByDescending(g => g.IsPrimary).Select(g => new { g.Guardian.FullName, g.Guardian.Phone }).FirstOrDefault(),
                }).First(),
            })
            .ToListAsync(ct);
        if (rows.Count == 0) return [];

        var studentIds = rows.Select(r => r.StudentId).ToList();
        var history = await db.AttendanceRecords
            .Where(r => studentIds.Contains(r.StudentId) && r.Register.Period == null && r.Register.Date < day && r.Register.Date >= day.AddDays(-60))
            .Select(r => new { r.StudentId, r.Register.Date, r.Status })
            .ToListAsync(ct);
        var registerIds = rows.Select(r => r.RegisterId).Distinct().ToList();
        var messages = await db.OutboundMessages.Where(m => m.RelatedId != null && registerIds.Contains(m.RelatedId.Value))
            .Select(m => new { m.Id, m.StudentId, m.Channel, m.Recipient, m.Status, m.StatusDetail })
            .ToListAsync(ct);

        return rows
            .OrderBy(r => r.Status).ThenBy(r => r.GradeOrder).ThenBy(r => r.ClassName).ThenBy(r => r.Student.FullName)
            .Select(r =>
            {
                var streak = r.Status == AttendanceStatus.Absent ? 1 : 0;
                if (streak == 1)
                {
                    foreach (var earlier in history.Where(h => h.StudentId == r.StudentId).OrderByDescending(h => h.Date))
                    {
                        if (earlier.Status != AttendanceStatus.Absent) break;
                        streak++;
                    }
                }

                return new AbsenteeDto(r.StudentId, r.Student.FullName, r.Student.AdmissionNumber, r.Student.PhotoFileId, r.SectionId, r.ClassName,
                    r.Status, r.ArrivedAt, r.Remark, r.Student.Guardian?.FullName, r.Student.Guardian?.Phone, streak,
                    messages.Where(m => m.StudentId == r.StudentId)
                        .Select(m => new MessageStatusDto(m.Id, m.Channel, m.Recipient, m.Status, m.StatusDetail)).ToList());
            })
            .ToList();
    }

    // ───────────────────────────── Monthly register ─────────────────────────────

    public async Task<MonthRegisterDto> MonthAsync(Guid sectionId, int year, int month, CancellationToken ct)
    {
        var current = await RequireViewAsync(ct);
        Guard(month is < 1 or > 12 || year is < 2000 or > 2100, "Choose a valid month.");
        var section = await registers.LoadSectionAsync(sectionId, current, ct);
        var settings = await calendar.SettingsAsync(ct);
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var closed = await calendar.ClosedReasonsAsync(first, last, ct);
        var taken = await db.AttendanceRegisters.Where(r => r.SectionId == sectionId && r.Period == null && r.Date >= first && r.Date <= last)
            .Select(r => new { r.Id, r.Date }).ToListAsync(ct);
        var days = new List<MonthDayDto>();
        for (var d = first; d <= last; d = d.AddDays(1))
        {
            var reason = d < section.YearStart || d > section.YearEnd ? "Outside the academic year" : closed(d, section.CampusId);
            days.Add(new MonthDayDto(d, reason is null, reason, taken.Any(t => t.Date == d)));
        }

        var students = await db.Enrollments
            .Where(e => e.SectionId == sectionId && e.StartDate <= last && (e.EndDate == null || e.EndDate >= first))
            .Join(db.Students, e => e.StudentId, s => s.Id, (e, s) => new { s.Id, s.FullName, s.AdmissionNumber, e.RollNumber, e.StartDate })
            .ToListAsync(ct);
        var registerIds = taken.Select(t => t.Id).ToList();
        var marks = await db.AttendanceRecords.Where(r => registerIds.Contains(r.RegisterId))
            .Select(r => new { r.StudentId, r.Register.Date, r.Status }).ToListAsync(ct);
        var markedIds = marks.Select(m => m.StudentId).Distinct().ToList();
        var extra = markedIds.Except(students.Select(s => s.Id)).ToList();
        if (extra.Count > 0)
        {
            students.AddRange(await db.Students.Where(s => extra.Contains(s.Id))
                .Select(s => new { s.Id, s.FullName, s.AdmissionNumber, RollNumber = (string?)null, StartDate = first }).ToListAsync(ct));
        }

        var leaveCounts = settings.Attendance.ExcusedCountsAsPresent;
        var rows = students.GroupBy(s => s.Id).Select(g => g.OrderByDescending(s => s.StartDate).First())
            .OrderBy(s => int.TryParse(s.RollNumber, out var n) ? n : int.MaxValue).ThenBy(s => s.FullName)
            .Select(s =>
            {
                var mine = marks.Where(m => m.StudentId == s.Id).ToDictionary(m => m.Date, m => m.Status);
                var row = days.Select(d => mine.TryGetValue(d.Date, out var st) ? st : (AttendanceStatus?)null).ToList();
                return new MonthStudentRowDto(s.Id, s.FullName, s.AdmissionNumber, s.RollNumber, row, Totals(row, leaveCounts));
            })
            .ToList();
        var all = rows.SelectMany(r => r.Marks).ToList();
        return new MonthRegisterDto(sectionId, section.ClassName, year, month, days, rows, Totals(all, leaveCounts));
    }

    public async Task<ExportFile> ExportMonthAsync(Guid sectionId, int year, int month, ExportFormat format, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Attendance.Export, ct);
        var register = await MonthAsync(sectionId, year, month, ct);
        var headers = new List<string> { "Roll no.", "Admission no.", "Name" };
        headers.AddRange(register.Days.Select(d => d.Date.Day.ToString()));
        headers.AddRange(["Present", "Late", "Absent", "Leave", "Attendance %"]);
        var rows = register.Students.Select(s => (IReadOnlyList<object?>)
        [
            s.RollNumber, s.AdmissionNumber, s.FullName,
            .. s.Marks.Select((m, i) => (object?)(m is { } st ? Code(st) : register.Days[i].IsSchoolDay ? "" : "H")),
            s.Totals.Present, s.Totals.Late, s.Totals.Absent, s.Totals.Leave, s.Totals.Percentage,
        ]);
        var file = exporter.Write($"Attendance {register.ClassName} {year}-{month:00}", headers, rows, format);
        return file;
    }

    // ───────────────────────────── A student's attendance ─────────────────────────────

    public async Task<StudentAttendanceSummaryDto> StudentAsync(Guid studentId, Guid? academicYearId, CancellationToken ct)
    {
        var current = await RequireViewAsync(ct);
        var student = await db.Students.Where(s => s.Id == studentId && (current.CampusIds == null || current.CampusIds.Contains(s.CampusId)))
            .Select(s => new { s.Id, s.FullName, s.CurrentAcademicYearId }).SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Student", studentId);
        var yearId = academicYearId ?? student.CurrentAcademicYearId ?? await YearForAsync(await calendar.TodayAsync(ct), ct);
        var year = await db.AcademicYears.Where(y => y.Id == yearId).Select(y => new { y.Id, y.Name, y.StartDate, y.EndDate }).SingleOrDefaultAsync(ct)
                   ?? throw new NotFoundException("Academic year", yearId ?? Guid.Empty);
        var settings = await calendar.SettingsAsync(ct);
        var today = await calendar.TodayAsync(ct);
        var to = year.EndDate < today ? year.EndDate : today;

        var marks = await db.AttendanceRecords
            .Where(r => r.StudentId == studentId && r.Register.Period == null && r.Register.Date >= year.StartDate && r.Register.Date <= to)
            .OrderBy(r => r.Register.Date)
            .Select(r => new DayMarkDto(r.Register.Date, r.Status, r.Remark, r.ArrivedAt,
                db.Sections.Where(s => s.Id == r.Register.SectionId).Select(s => s.Grade.Name + " – " + s.Name).First()))
            .ToListAsync(ct);
        var leaveCounts = settings.Attendance.ExcusedCountsAsPresent;
        var months = marks.GroupBy(m => (m.Date.Year, m.Date.Month)).OrderBy(g => g.Key)
            .Select(g => new MonthAttendanceDto(g.Key.Year, g.Key.Month, Totals(g.Select(m => (AttendanceStatus?)m.Status), leaveCounts)))
            .ToList();

        var subjects = await db.AttendanceRecords
            .Where(r => r.StudentId == studentId && r.Register.Period != null && r.Register.SubjectId != null
                        && r.Register.Date >= year.StartDate && r.Register.Date <= to)
            .GroupBy(r => new { r.Register.SubjectId, r.Status }).Select(g => new { g.Key.SubjectId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);
        var subjectNames = await db.Subjects.Where(s => subjects.Select(x => x.SubjectId).Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var bySubject = subjects.GroupBy(s => s.SubjectId!.Value).Select(g =>
        {
            int C(AttendanceStatus st) => g.Where(x => x.Status == st).Sum(x => x.Count);
            var attended = C(AttendanceStatus.Present) + C(AttendanceStatus.Late) + (leaveCounts ? C(AttendanceStatus.Leave) : 0);
            var counted = attended + C(AttendanceStatus.Absent);
            return new SubjectAttendanceDto(g.Key, subjectNames.GetValueOrDefault(g.Key) ?? "Subject", g.Sum(x => x.Count), attended,
                counted == 0 ? null : Math.Round(100m * attended / counted, 1));
        }).OrderBy(s => s.SubjectName).ToList();

        var leaveList = await leave.DtosAsync(db.StudentLeaveRequests.Where(l => l.StudentId == studentId && l.ToDate >= year.StartDate && l.FromDate <= year.EndDate)
            .OrderByDescending(l => l.FromDate), ct);
        return new StudentAttendanceSummaryDto(student.Id, student.FullName, year.Id, year.Name, year.StartDate, to,
            Totals(marks.Select(m => (AttendanceStatus?)m.Status), leaveCounts), months, marks, bySubject, leaveList,
            settings.Attendance.MinimumPercentage, leaveCounts);
    }

    // ───────────────────────────── Low attendance ─────────────────────────────

    public async Task<LowAttendanceDto> LowAttendanceAsync(LowAttendanceQuery query, CancellationToken ct)
    {
        var (from, to, below, rows) = await LowAttendanceRowsAsync(query, ct);
        var page = rows.Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize).ToList();
        return new LowAttendanceDto(from, to, below, new PagedResult<LowAttendanceRowDto>(page, query.SafePage, query.SafePageSize, rows.Count));
    }

    public async Task<ExportFile> ExportLowAttendanceAsync(LowAttendanceQuery query, ExportFormat format, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Attendance.Export, ct);
        var (from, to, below, rows) = await LowAttendanceRowsAsync(query, ct);
        string[] headers = ["Admission no.", "Name", "Class", "Days", "Present", "Late", "Absent", "Leave", "Attendance %", "Last absent", "Guardian", "Phone"];
        return exporter.Write($"Below {below:0.#}% attendance {from:yyyy-MM-dd} to {to:yyyy-MM-dd}", headers, rows.Select(r => (IReadOnlyList<object?>)
        [
            r.AdmissionNumber, r.FullName, r.ClassName, r.Totals.Days, r.Totals.Present, r.Totals.Late, r.Totals.Absent, r.Totals.Leave,
            r.Totals.Percentage, r.LastAbsent, r.GuardianName, r.GuardianPhone,
        ]), format);
    }

    private async Task<(DateOnly From, DateOnly To, decimal Below, List<LowAttendanceRowDto> Rows)> LowAttendanceRowsAsync(LowAttendanceQuery query,
        CancellationToken ct)
    {
        var current = await RequireViewAsync(ct);
        var settings = await calendar.SettingsAsync(ct);
        var today = await calendar.TodayAsync(ct);
        var yearId = await YearForAsync(today, ct);
        var yearStart = await db.AcademicYears.Where(y => y.Id == yearId).Select(y => (DateOnly?)y.StartDate).FirstOrDefaultAsync(ct) ?? today.AddDays(-90);
        var to = query.To ?? today;
        var from = query.From ?? (yearStart < to ? yearStart : to.AddDays(-30));
        Guard(to < from || to.DayNumber - from.DayNumber > MaxRangeDays, "Choose a range of at most 400 days.");
        var below = query.Below ?? settings.Attendance.MinimumPercentage;

        var students = db.Students.Where(s => s.Status == StudentStatus.Active || s.Status == StudentStatus.Suspended)
            .WhereIf(query.CampusId is not null, s => s.CampusId == query.CampusId)
            .WhereIf(query.SectionId is not null, s => s.CurrentSectionId == query.SectionId)
            .WhereIf(query.GradeId is not null, s => db.Sections.Any(x => x.Id == s.CurrentSectionId && x.GradeId == query.GradeId));
        if (!current.AllCampuses) students = students.Where(s => current.CampusIds!.Contains(s.CampusId));
        if (query.Term is { } term)
        {
            var pattern = $"%{term}%";
            students = students.Where(s => EF.Functions.ILike(s.FullName, pattern) || EF.Functions.ILike(s.AdmissionNumber, term + "%"));
        }

        var counts = await db.AttendanceRecords
            .Where(r => r.Register.Period == null && r.Register.Date >= from && r.Register.Date <= to && students.Any(s => s.Id == r.StudentId))
            .GroupBy(r => new { r.StudentId, r.Status })
            .Select(g => new { g.Key.StudentId, g.Key.Status, Count = g.Count(), Last = g.Max(r => r.Register.Date) })
            .ToListAsync(ct);
        var leaveCounts = settings.Attendance.ExcusedCountsAsPresent;
        var low = counts.GroupBy(c => c.StudentId).Select(g =>
        {
            int C(AttendanceStatus s) => g.Where(x => x.Status == s).Sum(x => x.Count);
            var totals = new AttendanceTotalsDto(g.Sum(x => x.Count), C(AttendanceStatus.Present), C(AttendanceStatus.Late), C(AttendanceStatus.Absent),
                C(AttendanceStatus.Leave), AttendanceCalendar.Percentage(C(AttendanceStatus.Present), C(AttendanceStatus.Late), C(AttendanceStatus.Absent),
                    C(AttendanceStatus.Leave), leaveCounts));
            var lastAbsent = g.Where(x => x.Status == AttendanceStatus.Absent).Select(x => (DateOnly?)x.Last).FirstOrDefault();
            return (StudentId: g.Key, Totals: totals, LastAbsent: lastAbsent);
        }).Where(x => x.Totals.Percentage is { } p && p < below).ToList();

        var ids = low.Select(l => l.StudentId).ToList();
        var details = await db.Students.Where(s => ids.Contains(s.Id)).Select(s => new
        {
            s.Id, s.FullName, s.AdmissionNumber, s.PhotoFileId,
            ClassName = db.Sections.Where(x => x.Id == s.CurrentSectionId).Select(x => x.Grade.Name + " – " + x.Name).FirstOrDefault(),
            Guardian = s.Guardians.OrderByDescending(g => g.IsPrimary).Select(g => new { g.Guardian.FullName, g.Guardian.Phone }).FirstOrDefault(),
        }).ToDictionaryAsync(s => s.Id, ct);
        var rows = low.Where(l => details.ContainsKey(l.StudentId))
            .Select(l =>
            {
                var d = details[l.StudentId];
                return new LowAttendanceRowDto(d.Id, d.FullName, d.AdmissionNumber, d.PhotoFileId, d.ClassName, l.Totals, l.LastAbsent,
                    d.Guardian?.FullName, d.Guardian?.Phone);
            })
            .OrderBy(r => r.Totals.Percentage).ThenByDescending(r => r.Totals.Absent).ThenBy(r => r.FullName)
            .ToList();
        return (from, to, below, rows);
    }

    // ───────────────────────────── Helpers ─────────────────────────────

    public static string Code(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Present => "P",
        AttendanceStatus.Late => "LT",
        AttendanceStatus.Absent => "A",
        _ => "L",
    };

    internal static AttendanceTotalsDto Totals(IEnumerable<AttendanceStatus?> marks, bool leaveCountsAsPresent)
    {
        int present = 0, late = 0, absent = 0, onLeave = 0;
        foreach (var mark in marks)
        {
            switch (mark)
            {
                case AttendanceStatus.Present: present++; break;
                case AttendanceStatus.Late: late++; break;
                case AttendanceStatus.Absent: absent++; break;
                case AttendanceStatus.Leave: onLeave++; break;
            }
        }

        return new AttendanceTotalsDto(present + late + absent + onLeave, present, late, absent, onLeave,
            AttendanceCalendar.Percentage(present, late, absent, onLeave, leaveCountsAsPresent));
    }

    private async Task<EffectiveAccess> RequireViewAsync(CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        if (!current.HasAny(Permissions.Attendance.View, Permissions.Attendance.Manage)) throw new ForbiddenException();
        return current;
    }

    private IQueryable<Domain.Organization.Section> ScopedSections(EffectiveAccess current, Guid? yearId, Guid? campusId)
    {
        var sections = db.Sections.Where(s => s.AcademicYearId == yearId && s.IsActive).WhereIf(campusId is not null, s => s.CampusId == campusId);
        return current.AllCampuses ? sections : sections.Where(s => current.CampusIds!.Contains(s.CampusId));
    }

    private IQueryable<AttendanceRecord> DailyRecords(EffectiveAccess current, DateOnly from, DateOnly to, Guid? campusId)
    {
        var records = db.AttendanceRecords.Where(r => r.Register.Period == null && r.Register.Date >= from && r.Register.Date <= to)
            .WhereIf(campusId is not null, r => r.Register.CampusId == campusId);
        return current.AllCampuses ? records : records.Where(r => current.CampusIds!.Contains(r.Register.CampusId));
    }

    private async Task<Dictionary<Guid, int>> OnRollAsync(List<Guid> sectionIds, DateOnly day, CancellationToken ct) =>
        await db.Enrollments.Where(e => sectionIds.Contains(e.SectionId) && e.StartDate <= day && (e.EndDate == null || e.EndDate >= day))
            .GroupBy(e => e.SectionId).Select(g => new { g.Key, Count = g.Select(e => e.StudentId).Distinct().Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);

    /// <summary>The academic year containing the date, else the current one.</summary>
    internal async Task<Guid?> YearForAsync(DateOnly day, CancellationToken ct) =>
        await db.AcademicYears.Where(y => y.StartDate <= day && y.EndDate >= day).OrderByDescending(y => y.IsCurrent).Select(y => (Guid?)y.Id)
            .FirstOrDefaultAsync(ct)
        ?? await db.AcademicYears.Where(y => y.IsCurrent).Select(y => (Guid?)y.Id).FirstOrDefaultAsync(ct);

    private static void Guard(bool condition, string message)
    {
        if (condition) throw new Domain.Common.DomainException("attendance.range", message);
    }
}
