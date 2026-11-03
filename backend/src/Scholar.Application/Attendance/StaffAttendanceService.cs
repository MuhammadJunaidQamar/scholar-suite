using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Domain.Access;
using Scholar.Domain.Attendance;
using Scholar.Domain.Common;
using Scholar.Domain.Staffing;

namespace Scholar.Application.Attendance;

/// <summary>
/// The daily staff register (per campus), its corrections, monthly sheets and each person's own view. Locking follows
/// the student registers. Staff can ask for corrections to their own days; nobody approves their own.
/// </summary>
public sealed class StaffAttendanceService(
    IAppDbContext db,
    IAccessContext access,
    AttendanceCalendar calendar,
    RegisterService registers,
    StaffLeaveService leave,
    ITabularExporter exporter)
{
    private sealed record Person(Guid Id, string FullName, string EmployeeNumber, string? Designation, StaffType StaffType, Guid? PhotoFileId,
        StaffStatus Status, Guid? UserId);

    // ───────────────────────────── Register ─────────────────────────────

    public async Task<StaffRegisterDto> RegisterAsync(Guid campusId, DateOnly? date, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        if (!current.HasAny(Permissions.Staff.ViewAttendance, Permissions.Staff.RecordAttendance)) throw new ForbiddenException();
        var campus = await CampusAsync(campusId, current, ct);
        var day = date ?? await calendar.TodayAsync(ct);
        var status = await calendar.DescribeAsync(day, campusId, ct);
        var locksAt = await calendar.LocksAtAsync(day, ct);
        var locked = calendar.Now >= locksAt;
        var people = await PeopleAsync(campusId, day, day, ct);
        var ids = people.Select(p => p.Id).ToList();
        var records = await db.StaffAttendanceRecords.Where(r => r.Date == day && ids.Contains(r.StaffId)).ToDictionaryAsync(r => r.StaffId, ct);
        var onLeave = await ApprovedLeaveAsync(ids, day, ct);

        var rows = people.Select(p =>
        {
            RegisterHintDto? hint = onLeave.TryGetValue(p.Id, out var l)
                ? new RegisterHintDto("leave", l.HalfDay ? $"Half day leave ({l.Code})" : $"On {l.Name}", l.Id)
                : p.Status == StaffStatus.Suspended ? new RegisterHintDto("suspended", "Suspended", null) : null;
            if (records.TryGetValue(p.Id, out var r))
            {
                return new StaffRegisterRowDto(p.Id, p.FullName, p.EmployeeNumber, p.Designation, p.StaffType, p.PhotoFileId, r.Status, r.CheckIn,
                    r.CheckOut, r.Remark, true, hint);
            }

            var suggested = l is not null ? (l.HalfDay ? StaffAttendanceStatus.HalfDay : StaffAttendanceStatus.OnLeave)
                : p.Status == StaffStatus.Suspended ? StaffAttendanceStatus.Absent : StaffAttendanceStatus.Present;
            return new StaffRegisterRowDto(p.Id, p.FullName, p.EmployeeNumber, p.Designation, p.StaffType, p.PhotoFileId, suggested, null, null, null,
                false, hint);
        }).ToList();

        var saved = records.Values.OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt).FirstOrDefault();
        var names = await registers.UserNamesAsync([saved?.UpdatedBy ?? saved?.CreatedBy], ct);
        var canRecord = current.Has(Permissions.Staff.RecordAttendance);
        return new StaffRegisterDto(day, campusId, campus, status, canRecord && !locked && !status.IsFuture, locked && records.Count > 0, locksAt,
            current.Has(Permissions.Staff.ApproveAttendance), canRecord, records.Count > 0, saved?.UpdatedAt ?? saved?.CreatedAt,
            RegisterService.Name(names, saved?.UpdatedBy ?? saved?.CreatedBy), rows, Count(rows.Select(r => (StaffAttendanceStatus?)r.Status), people.Count));
    }

    public async Task<StaffRegisterDto> SaveAsync(SaveStaffRegisterRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Staff.RecordAttendance, ct);
        var current = await access.GetAsync(ct);
        await CampusAsync(request.CampusId, current, ct);
        var today = await calendar.TodayAsync(ct);
        Guard.Against(request.Date > today, "attendance.future", "Attendance cannot be recorded for days that have not happened yet.");
        var day = await calendar.DescribeAsync(request.Date, request.CampusId, ct);
        Guard.Against(!day.IsSchoolDay && !request.OverrideClosedDay, "attendance.closed_day",
            $"{day.ClosedReason}. Choose \"Record anyway\" if staff worked that day.");
        if (await calendar.IsLockedAsync(request.Date, ct))
        {
            throw new ConflictException("attendance.locked", $"Staff attendance for {request.Date:d MMM} has locked. Ask for a correction instead.");
        }

        var people = (await PeopleAsync(request.CampusId, request.Date, request.Date, ct)).ToDictionary(p => p.Id);
        Guard.Against(request.Marks.Any(m => !people.ContainsKey(m.StaffId)), "attendance.not_on_roll",
            "Some of the people marked do not work at this campus on that date. Reload the register.");
        var ids = request.Marks.Select(m => m.StaffId).ToList();
        var onLeave = await ApprovedLeaveAsync(ids, request.Date, ct);
        await db.InTransactionAsync(async innerCt =>
        {
            var records = await db.StaffAttendanceRecords.Where(r => r.Date == request.Date && ids.Contains(r.StaffId)).ToDictionaryAsync(r => r.StaffId, innerCt);
            foreach (var mark in request.Marks)
            {
                if (!records.TryGetValue(mark.StaffId, out var record))
                {
                    record = new StaffAttendanceRecord(current.SchoolId, mark.StaffId, request.CampusId, request.Date, mark.Status);
                    db.StaffAttendanceRecords.Add(record);
                }

                var leaveId = mark.Status is StaffAttendanceStatus.OnLeave or StaffAttendanceStatus.HalfDay ? onLeave.GetValueOrDefault(mark.StaffId)?.Id : null;
                record.Set(mark.Status, mark.CheckIn, mark.CheckOut, mark.Remark, leaveId);
            }

            await db.SaveChangesAsync(innerCt);
            return true;
        }, ct: ct);
        return await RegisterAsync(request.CampusId, request.Date, ct);
    }

    // ───────────────────────────── Corrections ─────────────────────────────

    public async Task<StaffCorrectionDto> RequestCorrectionAsync(RequestStaffCorrectionRequest request, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        StaffMember staff;
        if (request.StaffId is { } staffId)
        {
            staff = await db.Staff.SingleOrDefaultAsync(s => s.Id == staffId, ct) ?? throw new NotFoundException("Staff member", staffId);
            if (staff.UserId != current.UserId)
            {
                await access.RequireAsync(Permissions.Staff.RecordAttendance, ct);
                if (!current.CanAccessCampus(staff.CampusId)) throw new NotFoundException("Staff member", staffId);
            }
        }
        else
        {
            staff = await db.Staff.SingleOrDefaultAsync(s => s.UserId == current.UserId, ct)
                    ?? throw new DomainException("attendance.no_staff_record", "Your account is not linked to a staff record.");
        }

        var self = staff.UserId == current.UserId;
        var today = await calendar.TodayAsync(ct);
        Guard.Against(request.Date > today, "attendance.future", "That day has not happened yet.");
        Guard.Against(request.Date < today.AddDays(-90), "correction.too_old", "Corrections can go back at most 90 days.");
        Guard.Against(!self && !await calendar.IsLockedAsync(request.Date, ct), "correction.not_locked",
            "This day is still open. Edit the staff register directly.");
        if (await db.StaffAttendanceCorrections.AnyAsync(c => c.StaffId == staff.Id && c.Date == request.Date && c.Status == CorrectionStatus.Pending, ct))
        {
            throw new ConflictException("correction.pending_exists", "A correction for this day is already waiting for approval.");
        }

        var record = await db.StaffAttendanceRecords.SingleOrDefaultAsync(r => r.StaffId == staff.Id && r.Date == request.Date, ct);
        Guard.Against(record?.Status == request.Status && record.CheckIn == request.CheckIn && record.CheckOut == request.CheckOut,
            "correction.no_change", "That is already what the register says.");
        var correction = new StaffAttendanceCorrection(current.SchoolId, staff.Id, request.Date, record?.Status, request.Status, request.CheckIn,
            request.CheckOut, request.Reason, self);
        await db.InTransactionAsync(async innerCt =>
        {
            db.StaffAttendanceCorrections.Add(correction);
            if (!self && current.Has(Permissions.Staff.ApproveAttendance))
            {
                correction.Approve(current.UserId, calendar.Now, "Applied directly by an approver.");
                await ApplyAsync(correction, staff, record, innerCt);
            }

            await db.SaveChangesAsync(innerCt);
            return true;
        }, ct: ct);
        return (await CorrectionDtosAsync(db.StaffAttendanceCorrections.Where(c => c.Id == correction.Id), ct)).Single();
    }

    public async Task<StaffCorrectionDto> DecideCorrectionAsync(Guid id, DecideRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Staff.ApproveAttendance, ct);
        var current = await access.GetAsync(ct);
        var correction = await db.StaffAttendanceCorrections.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Correction", id);
        var staff = await db.Staff.SingleAsync(s => s.Id == correction.StaffId, ct);
        if (!current.CanAccessCampus(staff.CampusId)) throw new NotFoundException("Correction", id);
        Guard.Against(staff.UserId == current.UserId, "correction.own", "Someone else has to decide corrections to your own attendance.");
        await db.InTransactionAsync(async innerCt =>
        {
            if (request.Approve)
            {
                var record = await db.StaffAttendanceRecords.SingleOrDefaultAsync(r => r.StaffId == staff.Id && r.Date == correction.Date, innerCt);
                if (record?.Status != correction.FromStatus)
                {
                    throw new ConflictException("correction.stale", "The register has changed since this correction was requested. Reject it and ask for a new one.");
                }

                correction.Approve(current.UserId, calendar.Now, request.Note);
                await ApplyAsync(correction, staff, record, innerCt);
            }
            else
            {
                correction.Reject(current.UserId, calendar.Now, request.Note);
            }

            await db.SaveChangesAsync(innerCt);
            return true;
        }, ct: ct);
        return (await CorrectionDtosAsync(db.StaffAttendanceCorrections.Where(c => c.Id == id), ct)).Single();
    }

    public async Task<StaffCorrectionDto> WithdrawCorrectionAsync(Guid id, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var correction = await db.StaffAttendanceCorrections.SingleOrDefaultAsync(c => c.Id == id && c.CreatedBy == current.UserId, ct)
                         ?? throw new NotFoundException("Correction", id);
        correction.Withdraw(current.UserId, calendar.Now);
        await db.SaveChangesAsync(ct);
        return (await CorrectionDtosAsync(db.StaffAttendanceCorrections.Where(c => c.Id == id), ct)).Single();
    }

    public async Task<PagedResult<StaffCorrectionDto>> ListCorrectionsAsync(StaffCorrectionQuery query, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var corrections = db.StaffAttendanceCorrections.AsQueryable();
        if (!current.HasAny(Permissions.Staff.ApproveAttendance, Permissions.Staff.ViewAttendance))
        {
            corrections = corrections.Where(c => c.CreatedBy == current.UserId || db.Staff.Any(s => s.Id == c.StaffId && s.UserId == current.UserId));
        }
        else if (!current.AllCampuses)
        {
            corrections = corrections.Where(c => db.Staff.Any(s => s.Id == c.StaffId && current.CampusIds!.Contains(s.CampusId)));
        }

        corrections = corrections
            .WhereIf(query.Status is not null, c => c.Status == query.Status)
            .WhereIf(query.CampusId is not null, c => db.Staff.Any(s => s.Id == c.StaffId && s.CampusId == query.CampusId));
        var total = await corrections.CountAsync(ct);
        var page = corrections.OrderBy(c => c.Status == CorrectionStatus.Pending ? 0 : 1).ThenByDescending(c => c.CreatedAt).ThenBy(c => c.Id)
            .Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize);
        return new PagedResult<StaffCorrectionDto>(await CorrectionDtosAsync(page, ct), query.SafePage, query.SafePageSize, total);
    }

    private async Task ApplyAsync(StaffAttendanceCorrection correction, StaffMember staff, StaffAttendanceRecord? record, CancellationToken ct)
    {
        if (record is null)
        {
            record = new StaffAttendanceRecord(staff.SchoolId, staff.Id, staff.CampusId, correction.Date, correction.ToStatus);
            db.StaffAttendanceRecords.Add(record);
        }

        var leaveId = (await ApprovedLeaveAsync([staff.Id], correction.Date, ct)).GetValueOrDefault(staff.Id)?.Id;
        record.Set(correction.ToStatus, correction.CheckIn, correction.CheckOut, $"Corrected: {correction.Reason}", leaveId);
    }

    // ───────────────────────────── Month & self-service ─────────────────────────────

    public async Task<StaffMonthDto> MonthAsync(Guid campusId, int year, int month, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        if (!current.HasAny(Permissions.Staff.ViewAttendance, Permissions.Staff.RecordAttendance)) throw new ForbiddenException();
        Guard.Against(month is < 1 or > 12 || year is < 2000 or > 2100, "attendance.range", "Choose a valid month.");
        var campus = await CampusAsync(campusId, current, ct);
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var days = await MonthDaysAsync(first, last, campusId, ct);
        var people = await PeopleAsync(campusId, first, last, ct);
        var ids = people.Select(p => p.Id).ToList();
        var marks = await db.StaffAttendanceRecords.Where(r => ids.Contains(r.StaffId) && r.Date >= first && r.Date <= last)
            .Select(r => new { r.StaffId, r.Date, r.Status }).ToListAsync(ct);
        var rows = people.Select(p =>
        {
            var mine = marks.Where(m => m.StaffId == p.Id).ToDictionary(m => m.Date, m => m.Status);
            var row = days.Select(d => mine.TryGetValue(d.Date, out var s) ? s : (StaffAttendanceStatus?)null).ToList();
            return new StaffMonthRowDto(p.Id, p.FullName, p.EmployeeNumber, p.Designation, row, Totals(row));
        }).ToList();
        return new StaffMonthDto(campusId, campus, year, month, days, rows);
    }

    public async Task<ExportFile> ExportMonthAsync(Guid campusId, int year, int month, ExportFormat format, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Staff.ViewAttendance, ct);
        var sheet = await MonthAsync(campusId, year, month, ct);
        var headers = new List<string> { "Employee no.", "Name", "Designation" };
        headers.AddRange(sheet.Days.Select(d => d.Date.Day.ToString()));
        headers.AddRange(["Present", "Late", "Half day", "Absent", "On leave", "On duty", "Attendance %"]);
        return exporter.Write($"Staff attendance {sheet.CampusName} {year}-{month:00}", headers, sheet.Staff.Select(s => (IReadOnlyList<object?>)
        [
            s.EmployeeNumber, s.FullName, s.DesignationName,
            .. s.Marks.Select((m, i) => (object?)(m is { } st ? Code(st) : sheet.Days[i].IsSchoolDay ? "" : "H")),
            s.Totals.Present, s.Totals.Late, s.Totals.HalfDay, s.Totals.Absent, s.Totals.OnLeave, s.Totals.OnDuty, s.Totals.Percentage,
        ]), format);
    }

    public async Task<MyAttendanceDto> MineAsync(int? year, int? month, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var staff = await db.Staff.SingleOrDefaultAsync(s => s.UserId == current.UserId, ct)
                    ?? throw new DomainException("attendance.no_staff_record", "Your account is not linked to a staff record. Ask the office to link it.");
        var today = await calendar.TodayAsync(ct);
        var first = new DateOnly(year ?? today.Year, month ?? today.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var days = await MonthDaysAsync(first, last, staff.CampusId, ct);
        var marks = await db.StaffAttendanceRecords.Where(r => r.StaffId == staff.Id && r.Date >= first && r.Date <= last)
            .ToDictionaryAsync(r => r.Date, r => r.Status, ct);
        var row = days.Select(d => marks.TryGetValue(d.Date, out var s) ? s : (StaffAttendanceStatus?)null).ToList();
        var me = await MeAsync(staff, ct);
        var leaveList = await leave.DtosAsync(db.StaffLeaveRequests.Where(l => l.StaffId == staff.Id).OrderByDescending(l => l.FromDate).Take(20), ct);
        var corrections = await CorrectionDtosAsync(db.StaffAttendanceCorrections.Where(c => c.StaffId == staff.Id).OrderByDescending(c => c.CreatedAt).Take(20), ct);
        return new MyAttendanceDto(me, first.Year, first.Month, days, row, Totals(row), await leave.BalancesCoreAsync(staff.Id, today, ct), leaveList,
            corrections);
    }

    internal async Task<MyStaffDto> MeAsync(StaffMember staff, CancellationToken ct) => new(staff.Id, staff.FullName, staff.EmployeeNumber,
        await db.Designations.Where(d => d.Id == staff.DesignationId).Select(d => d.Name).FirstOrDefaultAsync(ct), staff.CampusId,
        await db.Campuses.Where(c => c.Id == staff.CampusId).Select(c => c.Name).SingleAsync(ct));

    // ───────────────────────────── Helpers ─────────────────────────────

    public static string Code(StaffAttendanceStatus status) => status switch
    {
        StaffAttendanceStatus.Present => "P",
        StaffAttendanceStatus.Late => "LT",
        StaffAttendanceStatus.HalfDay => "HD",
        StaffAttendanceStatus.Absent => "A",
        StaffAttendanceStatus.OnLeave => "L",
        _ => "OD",
    };

    internal static StaffAttendanceTotalsDto Totals(IEnumerable<StaffAttendanceStatus?> marks)
    {
        var list = marks.Where(m => m is not null).Select(m => m!.Value).ToList();
        int C(StaffAttendanceStatus s) => list.Count(m => m == s);
        var worked = C(StaffAttendanceStatus.Present) + C(StaffAttendanceStatus.Late) + C(StaffAttendanceStatus.OnDuty) + 0.5m * C(StaffAttendanceStatus.HalfDay);
        // Approved leave is left out of the percentage: it is neither attendance nor an unexplained absence.
        var counted = list.Count - C(StaffAttendanceStatus.OnLeave);
        return new StaffAttendanceTotalsDto(list.Count, C(StaffAttendanceStatus.Present), C(StaffAttendanceStatus.Late), C(StaffAttendanceStatus.HalfDay),
            C(StaffAttendanceStatus.Absent), C(StaffAttendanceStatus.OnLeave), C(StaffAttendanceStatus.OnDuty),
            counted == 0 ? null : Math.Round(100m * worked / counted, 1));
    }

    private static StaffDayTotalsDto Count(IEnumerable<StaffAttendanceStatus?> marks, int staff)
    {
        var list = marks.Where(m => m is not null).Select(m => m!.Value).ToList();
        int C(StaffAttendanceStatus s) => list.Count(m => m == s);
        return new StaffDayTotalsDto(staff, list.Count, C(StaffAttendanceStatus.Present), C(StaffAttendanceStatus.Late), C(StaffAttendanceStatus.HalfDay),
            C(StaffAttendanceStatus.Absent), C(StaffAttendanceStatus.OnLeave), C(StaffAttendanceStatus.OnDuty));
    }

    private async Task<List<MonthDayDto>> MonthDaysAsync(DateOnly first, DateOnly last, Guid campusId, CancellationToken ct)
    {
        var closed = await calendar.ClosedReasonsAsync(first, last, ct);
        var days = new List<MonthDayDto>();
        for (var d = first; d <= last; d = d.AddDays(1))
        {
            var reason = closed(d, campusId);
            days.Add(new MonthDayDto(d, reason is null, reason, false));
        }

        return days;
    }

    /// <summary>Staff employed at the campus at some point in the range (leavers stay on the dates they still worked).</summary>
    private async Task<List<Person>> PeopleAsync(Guid campusId, DateOnly from, DateOnly to, CancellationToken ct) =>
        await db.Staff.Where(s => s.CampusId == campusId && s.JoinDate <= to && (s.ExitDate == null || s.ExitDate >= from))
            .OrderBy(s => s.StaffType).ThenBy(s => s.FullName)
            .Select(s => new Person(s.Id, s.FullName, s.EmployeeNumber, db.Designations.Where(d => d.Id == s.DesignationId).Select(d => d.Name).FirstOrDefault(),
                s.StaffType, s.PhotoFileId, s.Status, s.UserId))
            .ToListAsync(ct);

    private sealed record LeaveInfo(Guid Id, bool HalfDay, string Code, string Name);

    private async Task<Dictionary<Guid, LeaveInfo>> ApprovedLeaveAsync(List<Guid> staffIds, DateOnly day, CancellationToken ct) =>
        (await db.StaffLeaveRequests.Where(l => staffIds.Contains(l.StaffId) && l.Status == LeaveStatus.Approved && l.FromDate <= day && l.ToDate >= day)
            .Select(l => new { l.StaffId, Info = new LeaveInfo(l.Id, l.HalfDay, db.LeaveTypes.Where(t => t.Id == l.LeaveTypeId).Select(t => t.Code).First(),
                db.LeaveTypes.Where(t => t.Id == l.LeaveTypeId).Select(t => t.Name).First()) })
            .ToListAsync(ct))
        .GroupBy(l => l.StaffId).ToDictionary(g => g.Key, g => g.First().Info);

    private async Task<string> CampusAsync(Guid campusId, EffectiveAccess current, CancellationToken ct)
    {
        var name = current.CanAccessCampus(campusId) ? await db.Campuses.Where(c => c.Id == campusId).Select(c => c.Name).SingleOrDefaultAsync(ct) : null;
        return name ?? throw new NotFoundException("Campus", campusId);
    }

    private async Task<List<StaffCorrectionDto>> CorrectionDtosAsync(IQueryable<StaffAttendanceCorrection> query, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var rows = await query.Select(c => new
        {
            c.Id, c.StaffId, c.Date, c.FromStatus, c.ToStatus, c.CheckIn, c.CheckOut, c.Reason, c.RequestedBySelf, c.Status, c.CreatedBy, c.CreatedAt,
            c.DecidedBy, c.DecidedAt, c.DecisionNote,
            Staff = db.Staff.Where(s => s.Id == c.StaffId).Select(s => new { s.FullName, s.EmployeeNumber, s.UserId }).First(),
        }).ToListAsync(ct);
        var names = await registers.UserNamesAsync(rows.SelectMany(r => new[] { r.CreatedBy, r.DecidedBy }), ct);
        var approver = current.Has(Permissions.Staff.ApproveAttendance);
        return rows.Select(r => new StaffCorrectionDto(r.Id, r.StaffId, r.Staff.FullName, r.Staff.EmployeeNumber, r.Date, r.FromStatus, r.ToStatus,
            r.CheckIn, r.CheckOut, r.Reason, r.RequestedBySelf, r.Status, RegisterService.Name(names, r.CreatedBy), r.CreatedAt,
            RegisterService.Name(names, r.DecidedBy), r.DecidedAt, r.DecisionNote,
            approver && r.Status == CorrectionStatus.Pending && r.Staff.UserId != current.UserId,
            r.Status == CorrectionStatus.Pending && r.CreatedBy == current.UserId)).ToList();
    }
}
