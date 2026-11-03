using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Application.Communication;
using Scholar.Domain.Access;
using Scholar.Domain.Attendance;
using Scholar.Domain.Common;
using Scholar.Domain.Platform;
using Scholar.Domain.Students;

namespace Scholar.Application.Attendance;

/// <summary>
/// Student registers: the daily roll call and period registers, the sheet a teacher marks, saving it, and the
/// correction workflow for registers that have locked.
/// </summary>
/// <remarks>
/// Who may take a register: "take attendance (any class)" covers every section in the user's campus scope;
/// "take attendance (own classes)" covers the daily register of sections the user is class teacher of and the
/// period registers of sections they teach (or are class teacher of). Locked registers change only through
/// corrections, which approvers' own requests apply immediately and everyone else's wait for approval.
/// </remarks>
public sealed class RegisterService(
    IAppDbContext db,
    IAccessContext access,
    AttendanceCalendar calendar,
    AbsenceNotifier notifier,
    MessageDispatcher dispatcher)
{
    internal sealed record SectionInfo(Guid Id, Guid AcademicYearId, string YearName, DateOnly YearStart, DateOnly YearEnd, Guid CampusId,
        string CampusName, Guid GradeId, string ClassName, Guid? ClassTeacherId, string? ClassTeacherName);

    internal sealed record RosterStudent(Guid Id, string FullName, string AdmissionNumber, string? RollNumber, Gender Gender,
        Guid? PhotoFileId, StudentStatus Status, string? MedicalAlert, DateOnly StartDate);

    private sealed record Rights(bool CanView, bool CanTake, bool CanManage, bool CanApprove, Guid? StaffId);

    private Guid? _staffId;
    private bool _staffLoaded;

    // ───────────────────────────── Sheet ─────────────────────────────

    public async Task<RegisterSheetDto> SheetAsync(Guid sectionId, DateOnly date, int? period, Guid? subjectId, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var section = await LoadSectionAsync(sectionId, current, ct);
        var register = await db.AttendanceRegisters.AsNoTracking().Include(r => r.Records)
            .SingleOrDefaultAsync(r => r.SectionId == sectionId && r.Date == date && r.Period == period, ct);
        var rights = await RightsAsync(current, section, period, register?.SubjectId ?? subjectId, ct);
        if (!rights.CanView) throw new ForbiddenException("You can see registers only for the classes you teach.");
        return await BuildSheetAsync(section, date, period, register?.SubjectId ?? subjectId, register, rights, ct);
    }

    private async Task<RegisterSheetDto> BuildSheetAsync(SectionInfo section, DateOnly date, int? period, Guid? subjectId,
        AttendanceRegister? register, Rights rights, CancellationToken ct)
    {
        var settings = await calendar.SettingsAsync(ct);
        var day = await calendar.DescribeAsync(date, section.CampusId, ct);
        if (day.IsSchoolDay && (date < section.YearStart || date > section.YearEnd))
        {
            day = day with { IsSchoolDay = false, ClosedReason = $"Outside the {section.YearName} academic year" };
        }

        var locksAt = await calendar.LocksAtAsync(date, ct);
        var locked = calendar.Now >= locksAt;
        var roster = await RosterAsync(section.Id, date, ct);
        var rosterIds = roster.Select(r => r.Id).ToList();
        var records = register?.Records.ToDictionary(r => r.StudentId) ?? [];

        // Students no longer in the section who were marked when the register was taken stay visible.
        var formerIds = records.Keys.Where(id => !rosterIds.Contains(id)).ToList();
        var former = formerIds.Count == 0 ? [] : await db.Students.Where(s => formerIds.Contains(s.Id))
            .Select(s => new RosterStudent(s.Id, s.FullName, s.AdmissionNumber, null, s.Gender, s.PhotoFileId, s.Status, s.MedicalAlert, date))
            .ToListAsync(ct);
        var everyone = roster.Concat(former).Select(r => r.Id).ToList();

        var leave = await db.StudentLeaveRequests
            .Where(l => everyone.Contains(l.StudentId) && l.FromDate <= date && l.ToDate >= date
                        && (l.Status == LeaveStatus.Approved || l.Status == LeaveStatus.Pending))
            .Select(l => new { l.Id, l.StudentId, l.Status, l.Kind, l.ToDate })
            .ToListAsync(ct);
        var since = date.AddDays(-30);
        var recent = await db.AttendanceRecords
            .Where(r => everyone.Contains(r.StudentId) && r.Status == AttendanceStatus.Absent && r.Register.Period == null
                        && r.Register.Date >= since && r.Register.Date < date)
            .GroupBy(r => r.StudentId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);

        var rows = new List<RegisterRowDto>();
        foreach (var student in roster.Concat(former))
        {
            var approved = leave.FirstOrDefault(l => l.StudentId == student.Id && l.Status == LeaveStatus.Approved);
            var pending = leave.FirstOrDefault(l => l.StudentId == student.Id && l.Status == LeaveStatus.Pending);
            RegisterHintDto? hint = null;
            if (!rosterIds.Contains(student.Id)) hint = new RegisterHintDto("left", "No longer in this section", null);
            else if (approved is not null) hint = new RegisterHintDto("leave", $"Approved leave until {approved.ToDate:d MMM}", approved.Id);
            else if (student.Status == StudentStatus.Suspended) hint = new RegisterHintDto("suspended", "Suspended", null);
            else if (pending is not null) hint = new RegisterHintDto("pendingLeave", "Leave requested, awaiting approval", pending.Id);
            else if (register is not null && !records.ContainsKey(student.Id)) hint = new RegisterHintDto("joined", "Joined after this register was taken", null);

            if (records.TryGetValue(student.Id, out var record))
            {
                rows.Add(new RegisterRowDto(student.Id, student.FullName, student.AdmissionNumber, student.RollNumber, student.Gender,
                    student.PhotoFileId, record.Status, record.Remark, record.ArrivedAt, true, hint, student.MedicalAlert,
                    recent.GetValueOrDefault(student.Id)));
            }
            else
            {
                // Suggested starting mark: approved leave → Leave; suspended → Absent; everyone else → Present.
                var suggested = approved is not null ? AttendanceStatus.Leave
                    : student.Status == StudentStatus.Suspended ? AttendanceStatus.Absent : AttendanceStatus.Present;
                rows.Add(new RegisterRowDto(student.Id, student.FullName, student.AdmissionNumber, student.RollNumber, student.Gender,
                    student.PhotoFileId, suggested, suggested == AttendanceStatus.Absent ? "Suspended" : null, null, false, hint,
                    student.MedicalAlert, recent.GetValueOrDefault(student.Id)));
            }
        }

        rows = rows.OrderBy(r => r.Hint?.Kind == "left").ThenBy(r => RollOrder(r.RollNumber)).ThenBy(r => r.RollNumber).ThenBy(r => r.FullName).ToList();
        var counted = rows.Where(r => r.Hint?.Kind != "left").ToList();
        var counts = new RegisterCountsDto(counted.Count, counted.Count(r => r.Status == AttendanceStatus.Present),
            counted.Count(r => r.Status == AttendanceStatus.Late), counted.Count(r => r.Status == AttendanceStatus.Absent),
            counted.Count(r => r.Status == AttendanceStatus.Leave));

        RegisterInfoDto? info = null;
        CorrectionDto? pendingCorrection = null;
        MessageCountsDto? alerts = null;
        if (register is not null)
        {
            var names = await UserNamesAsync([register.CreatedBy, register.UpdatedBy], ct);
            info = new RegisterInfoDto(register.Id, register.CreatedAt, Name(names, register.CreatedBy), register.UpdatedAt,
                Name(names, register.UpdatedBy), register.Note, register.Version);
            var pendingId = await db.AttendanceCorrections.Where(c => c.RegisterId == register.Id && c.Status == CorrectionStatus.Pending)
                .Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
            if (pendingId is { } id) pendingCorrection = (await CorrectionDtosAsync(db.AttendanceCorrections.Where(c => c.Id == id), ct)).Single();
            if (register.IsDaily) alerts = await AbsenceNotifier.CountsAsync(db.OutboundMessages.Where(m => m.RelatedId == register.Id), dispatcher, ct);
        }

        string? periodLabel = null;
        if (period is { } number)
        {
            var slot = await db.BellPeriods
                .Where(p => p.Number == number && db.BellSchedules.Any(s => s.Id == p.ScheduleId && s.CampusId == section.CampusId))
                .Select(p => new { p.Name, p.StartTime, p.EndTime })
                .FirstOrDefaultAsync(ct);
            periodLabel = slot is null ? $"Period {number}" : $"{slot.Name} · {slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}";
        }

        var subjectName = subjectId is null ? null : await db.Subjects.Where(s => s.Id == subjectId).Select(s => s.Name).FirstOrDefaultAsync(ct);
        var canTakeNow = rights.CanTake && !day.IsFuture && (register is not null ? !locked : !locked || rights.CanManage)
                         && (day.IsSchoolDay || rights.CanManage) && (period is null || settings.Attendance.Mode == AttendanceMode.PeriodWise);
        return new RegisterSheetDto(section.Id, section.ClassName, section.CampusId, section.CampusName, date, period, periodLabel, subjectId,
            subjectName, section.ClassTeacherName, info, day, canTakeNow, register is not null && locked, locksAt,
            rights.CanTake && register is not null && locked, rights.CanApprove, rights.CanManage, rows, counts, pendingCorrection, alerts);
    }

    // ───────────────────────────── Save ─────────────────────────────

    public async Task<RegisterSheetDto> SaveAsync(SaveRegisterRequest request, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var section = await LoadSectionAsync(request.SectionId, current, ct);
        var settings = await calendar.SettingsAsync(ct);
        Guard.Against(request.Period is not null && settings.Attendance.Mode != AttendanceMode.PeriodWise, "attendance.periods_off",
            "Period-wise attendance is switched off for the school. Take the daily register instead.");

        var rights = await RightsAsync(current, section, request.Period, request.SubjectId, ct);
        if (!rights.CanTake)
        {
            throw new ForbiddenException(request.Period is null
                ? "Only the class teacher (or the office) can take this section's daily register."
                : "You can take period registers only for sections you teach.");
        }

        var subjectId = await ResolveSubjectAsync(section, request.Period, request.SubjectId, rights, ct);
        var today = await calendar.TodayAsync(ct);
        Guard.Against(request.Date > today, "attendance.future", "Registers cannot be taken for days that have not happened yet.");
        Guard.Against(request.Date < section.YearStart || request.Date > section.YearEnd, "attendance.outside_year",
            $"{request.Date:d MMM yyyy} is outside the {section.YearName} academic year.");
        var day = await calendar.DescribeAsync(request.Date, section.CampusId, ct);
        if (!day.IsSchoolDay && !(request.OverrideClosedDay && rights.CanManage))
        {
            throw new DomainException("attendance.closed_day", rights.CanManage
                ? $"{day.ClosedReason}. Choose \"Take anyway\" if the school opened for a make-up day."
                : $"{day.ClosedReason}, so no register is expected.");
        }

        var locksAt = await calendar.LocksAtAsync(request.Date, ct);
        var locked = calendar.Now >= locksAt;
        var outcome = await db.InTransactionAsync(async innerCt =>
        {
            var register = await db.AttendanceRegisters.Include(r => r.Records)
                .SingleOrDefaultAsync(r => r.SectionId == section.Id && r.Date == request.Date && r.Period == request.Period, innerCt);
            if (register is not null && locked)
            {
                throw new ConflictException("attendance.locked",
                    $"This register locked on {await calendar.DescribeMomentAsync(locksAt, innerCt)}. Ask for a correction instead.");
            }

            if (register is null && locked && !rights.CanManage)
            {
                throw new ConflictException("attendance.locked",
                    $"The register for {request.Date:d MMM} can no longer be taken by teachers. Ask the school office to record it.");
            }

            var roster = await RosterAsync(section.Id, request.Date, innerCt);
            var rosterIds = roster.Select(r => r.Id).ToHashSet();
            var unknown = request.Marks.Where(m => !rosterIds.Contains(m.StudentId) && register?.Records.Any(r => r.StudentId == m.StudentId) != true).ToList();
            Guard.Against(unknown.Count > 0, "attendance.not_on_roll",
                "Some of the students marked are not in this section on that date. Reload the register.");

            if (register is null)
            {
                var missing = rosterIds.Count(id => request.Marks.All(m => m.StudentId != id));
                Guard.Against(missing > 0, "attendance.incomplete",
                    $"{missing} student{(missing == 1 ? " has" : "s have")} no mark. Reload the register to see everyone.");
                register = new AttendanceRegister(current.SchoolId, section.Id, section.AcademicYearId, section.CampusId, request.Date,
                    request.Period, subjectId, rights.StaffId);
                db.AttendanceRegisters.Add(register);
            }
            else
            {
                if (request.Version is { } version) db.Entry(register).Property(r => r.Version).OriginalValue = version;
                if (request.Period is not null && subjectId is not null) register.SubjectId = subjectId;
                Touch(register);
            }

            register.Note = Clean(request.Note);
            var previous = register.Records.ToDictionary(r => r.StudentId, r => (AttendanceStatus?)r.Status);
            var leave = await ApprovedLeaveAsync(request.Marks.Where(m => m.Status == AttendanceStatus.Leave).Select(m => m.StudentId).ToList(),
                request.Date, innerCt);
            foreach (var mark in request.Marks)
            {
                register.Mark(mark.StudentId, mark.Status, mark.Remark, mark.ArrivedAt, LeaveFor(leave, mark.StudentId));
            }

            var messages = await notifier.PrepareAsync(register, previous, innerCt);
            await db.SaveChangesAsync(innerCt);
            return messages;
        }, ct: ct);

        await notifier.DispatchAsync(outcome, ct);
        return await SheetAsync(section.Id, request.Date, request.Period, subjectId, ct);
    }

    // ───────────────────────────── Corrections ─────────────────────────────

    public async Task<CorrectionDto> RequestCorrectionAsync(Guid registerId, RequestCorrectionRequest request, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var register = await db.AttendanceRegisters.Include(r => r.Records).SingleOrDefaultAsync(r => r.Id == registerId, ct)
            ?? throw new NotFoundException("Register", registerId);
        var section = await LoadSectionAsync(register.SectionId, current, ct);
        var rights = await RightsAsync(current, section, register.Period, register.SubjectId, ct);
        if (!rights.CanTake) throw new ForbiddenException("You can correct registers only for the classes you teach.");
        Guard.Against(!await calendar.IsLockedAsync(register.Date, ct), "correction.not_locked", "This register is still open. Edit it directly.");
        if (await db.AttendanceCorrections.AnyAsync(c => c.RegisterId == registerId && c.Status == CorrectionStatus.Pending, ct))
        {
            throw new ConflictException("correction.pending_exists", "A correction for this register is already waiting for approval.");
        }

        var roster = (await RosterAsync(section.Id, register.Date, ct)).Select(r => r.Id).ToHashSet();
        var correction = new AttendanceCorrection(current.SchoolId, registerId, request.Reason);
        foreach (var change in request.Changes)
        {
            var record = register.Records.SingleOrDefault(r => r.StudentId == change.StudentId);
            Guard.Against(record is null && !roster.Contains(change.StudentId), "attendance.not_on_roll",
                "That student was not in this section on the register's date.");
            correction.AddItem(change.StudentId, record?.Status, change.Status, change.Remark);
        }

        await db.InTransactionAsync(async innerCt =>
        {
            db.AttendanceCorrections.Add(correction);
            if (rights.CanApprove)
            {
                // An approver's own correction needs no second person; it is still recorded with its reason.
                correction.Approve(current.UserId, calendar.Now, "Applied directly by an approver.");
                await ApplyAsync(register, correction, innerCt);
            }

            await db.SaveChangesAsync(innerCt);
            return true;
        }, ct: ct);
        return (await CorrectionDtosAsync(db.AttendanceCorrections.Where(c => c.Id == correction.Id), ct)).Single();
    }

    public async Task<CorrectionDto> DecideCorrectionAsync(Guid correctionId, DecideRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Attendance.Approve, ct);
        var current = await access.GetAsync(ct);
        var correction = await db.AttendanceCorrections.Include(c => c.Items).SingleOrDefaultAsync(c => c.Id == correctionId, ct)
            ?? throw new NotFoundException("Correction", correctionId);
        var register = await db.AttendanceRegisters.Include(r => r.Records).SingleAsync(r => r.Id == correction.RegisterId, ct);
        await LoadSectionAsync(register.SectionId, current, ct); // campus scope

        await db.InTransactionAsync(async innerCt =>
        {
            if (request.Approve)
            {
                correction.Approve(current.UserId, calendar.Now, request.Note);
                await ApplyAsync(register, correction, innerCt);
            }
            else
            {
                correction.Reject(current.UserId, calendar.Now, request.Note);
            }

            await db.SaveChangesAsync(innerCt);
            return true;
        }, ct: ct);
        return (await CorrectionDtosAsync(db.AttendanceCorrections.Where(c => c.Id == correctionId), ct)).Single();
    }

    public async Task<CorrectionDto> WithdrawCorrectionAsync(Guid correctionId, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var correction = await db.AttendanceCorrections.SingleOrDefaultAsync(c => c.Id == correctionId && c.CreatedBy == current.UserId, ct)
            ?? throw new NotFoundException("Correction", correctionId);
        correction.Withdraw(current.UserId, calendar.Now);
        await db.SaveChangesAsync(ct);
        return (await CorrectionDtosAsync(db.AttendanceCorrections.Where(c => c.Id == correctionId), ct)).Single();
    }

    public async Task<PagedResult<CorrectionDto>> ListCorrectionsAsync(CorrectionQuery query, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        if (!current.HasAny(Permissions.Attendance.Approve, Permissions.Attendance.View, Permissions.Attendance.Record, Permissions.Attendance.Manage))
        {
            throw new ForbiddenException();
        }

        var corrections = db.AttendanceCorrections.AsQueryable();
        if (!current.Has(Permissions.Attendance.Approve) || query.Mine == true)
        {
            corrections = corrections.Where(c => c.CreatedBy == current.UserId);
        }

        if (!current.AllCampuses)
        {
            corrections = corrections.Where(c => db.AttendanceRegisters.Any(r => r.Id == c.RegisterId && current.CampusIds!.Contains(r.CampusId)));
        }

        corrections = corrections
            .WhereIf(query.Status is not null, c => c.Status == query.Status)
            .WhereIf(query.CampusId is not null, c => db.AttendanceRegisters.Any(r => r.Id == c.RegisterId && r.CampusId == query.CampusId));
        var total = await corrections.CountAsync(ct);
        var page = corrections.OrderBy(c => c.Status == CorrectionStatus.Pending ? 0 : 1).ThenByDescending(c => c.CreatedAt).ThenBy(c => c.Id)
            .Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize);
        return new PagedResult<CorrectionDto>(await CorrectionDtosAsync(page, ct), query.SafePage, query.SafePageSize, total);
    }

    private async Task ApplyAsync(AttendanceRegister register, AttendanceCorrection correction, CancellationToken ct)
    {
        var items = correction.Items.Count > 0 ? correction.Items.ToList()
            : await db.AttendanceCorrectionItems.Where(i => i.CorrectionId == correction.Id).ToListAsync(ct);
        var previous = register.Records.ToDictionary(r => r.StudentId, r => (AttendanceStatus?)r.Status);
        var leave = await ApprovedLeaveAsync(items.Where(i => i.ToStatus == AttendanceStatus.Leave).Select(i => i.StudentId).ToList(), register.Date, ct);
        foreach (var item in items)
        {
            var now = previous.GetValueOrDefault(item.StudentId);
            if (now != item.FromStatus)
            {
                var name = await db.Students.Where(s => s.Id == item.StudentId).Select(s => s.FullName).SingleAsync(ct);
                throw new ConflictException("correction.stale",
                    $"{name}'s mark has changed since this correction was requested. Reject it and ask for a new one.");
            }

            var existing = register.Records.SingleOrDefault(r => r.StudentId == item.StudentId);
            register.Mark(item.StudentId, item.ToStatus, item.Remark ?? existing?.Remark, existing?.ArrivedAt, LeaveFor(leave, item.StudentId));
        }

        Touch(register);
        await notifier.PrepareAsync(register, previous, ct);
    }

    // ───────────────────────────── Shared helpers ─────────────────────────────

    internal async Task<SectionInfo> LoadSectionAsync(Guid sectionId, EffectiveAccess current, CancellationToken ct)
    {
        var section = await db.Sections.Where(s => s.Id == sectionId)
            .Select(s => new SectionInfo(s.Id, s.AcademicYearId, s.AcademicYear.Name, s.AcademicYear.StartDate, s.AcademicYear.EndDate, s.CampusId,
                db.Campuses.Where(c => c.Id == s.CampusId).Select(c => c.Name).First(), s.GradeId, s.Grade.Name + " – " + s.Name,
                s.ClassTeacherId, db.Staff.Where(t => t.Id == s.ClassTeacherId).Select(t => t.FullName).FirstOrDefault()))
            .SingleOrDefaultAsync(ct);
        if (section is null || !current.CanAccessCampus(section.CampusId)) throw new NotFoundException("Section", sectionId);
        return section;
    }

    /// <summary>Students placed in the section on the date (from enrolment history, so past registers stay right).</summary>
    internal async Task<List<RosterStudent>> RosterAsync(Guid sectionId, DateOnly date, CancellationToken ct)
    {
        var rows = await db.Enrollments
            .Where(e => e.SectionId == sectionId && e.StartDate <= date && (e.EndDate == null || e.EndDate >= date))
            .Join(db.Students, e => e.StudentId, s => s.Id, (e, s) => new RosterStudent(s.Id, s.FullName, s.AdmissionNumber, e.RollNumber,
                s.Gender, s.PhotoFileId, s.Status, s.MedicalAlert, e.StartDate))
            .ToListAsync(ct);
        return rows.GroupBy(r => r.Id).Select(g => g.OrderByDescending(r => r.StartDate).First()).ToList();
    }

    internal async Task<Guid?> MyStaffIdAsync(Guid userId, CancellationToken ct)
    {
        if (_staffLoaded) return _staffId;
        _staffId = await db.Staff.Where(s => s.UserId == userId).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
        _staffLoaded = true;
        return _staffId;
    }

    private async Task<Rights> RightsAsync(EffectiveAccess current, SectionInfo section, int? period, Guid? subjectId, CancellationToken ct)
    {
        var manage = current.Has(Permissions.Attendance.Manage);
        var staffId = await MyStaffIdAsync(current.UserId, ct);
        var assigned = false;
        if (current.Has(Permissions.Attendance.Record) && staffId is { } me)
        {
            assigned = section.ClassTeacherId == me
                       || (period is not null && await db.SectionSubjectTeachers.AnyAsync(t => t.SectionId == section.Id && t.StaffId == me
                           && (subjectId == null || t.SubjectId == subjectId), ct));
        }

        var canTake = manage || assigned;
        return new Rights(canTake || current.Has(Permissions.Attendance.View), canTake, manage, current.Has(Permissions.Attendance.Approve), staffId);
    }

    private async Task<Guid?> ResolveSubjectAsync(SectionInfo section, int? period, Guid? subjectId, Rights rights, CancellationToken ct)
    {
        if (period is null) return null;
        if (subjectId is { } id)
        {
            if (!await db.Subjects.AnyAsync(s => s.Id == id, ct)) throw new NotFoundException("Subject", id);
            return id;
        }

        // A subject teacher with a single subject in the section does not need to choose it.
        if (rights.StaffId is { } me)
        {
            var mine = await db.SectionSubjectTeachers.Where(t => t.SectionId == section.Id && t.StaffId == me).Select(t => t.SubjectId).ToListAsync(ct);
            if (mine.Count == 1) return mine[0];
        }

        return null;
    }

    private async Task<Dictionary<Guid, Guid>> ApprovedLeaveAsync(List<Guid> studentIds, DateOnly date, CancellationToken ct) =>
        studentIds.Count == 0 ? []
            : (await db.StudentLeaveRequests.Where(l => studentIds.Contains(l.StudentId) && l.Status == LeaveStatus.Approved
                                                       && l.FromDate <= date && l.ToDate >= date)
                .Select(l => new { l.StudentId, l.Id }).ToListAsync(ct))
            .GroupBy(l => l.StudentId).ToDictionary(g => g.Key, g => g.First().Id);

    private static Guid? LeaveFor(Dictionary<Guid, Guid> leave, Guid studentId) => leave.TryGetValue(studentId, out var id) ? id : null;

    /// <summary>Marks the register itself as changed, so its version (and "last edited") move with its marks.</summary>
    private void Touch(AttendanceRegister register) => db.Entry(register).Property(r => r.Note).IsModified = true;

    internal async Task<List<CorrectionDto>> CorrectionDtosAsync(IQueryable<AttendanceCorrection> query, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var rows = await query
            .Select(c => new
            {
                c.Id, c.RegisterId, c.Reason, c.Status, c.CreatedBy, c.CreatedAt, c.DecidedBy, c.DecidedAt, c.DecisionNote,
                Register = db.AttendanceRegisters.Where(r => r.Id == c.RegisterId).Select(r => new
                {
                    r.SectionId, r.Date, r.Period,
                    ClassName = db.Sections.Where(s => s.Id == r.SectionId).Select(s => s.Grade.Name + " – " + s.Name).First(),
                }).First(),
            })
            .ToListAsync(ct);
        var ids = rows.Select(r => r.Id).ToList();
        var items = await db.AttendanceCorrectionItems.Where(i => ids.Contains(i.CorrectionId))
            .Join(db.Students, i => i.StudentId, s => s.Id, (i, s) => new { i.CorrectionId, i.StudentId, s.FullName, s.AdmissionNumber, i.FromStatus, i.ToStatus, i.Remark })
            .ToListAsync(ct);
        var registerIds = rows.Select(r => r.RegisterId).Distinct().ToList();
        var marks = await db.AttendanceRecords.Where(r => registerIds.Contains(r.RegisterId))
            .Select(r => new { r.RegisterId, r.StudentId, r.Status }).ToListAsync(ct);
        var names = await UserNamesAsync(rows.SelectMany(r => new[] { r.CreatedBy, r.DecidedBy }), ct);
        var approver = current.Has(Permissions.Attendance.Approve);
        return rows.Select(r => new CorrectionDto(r.Id, r.RegisterId, r.Register.SectionId, r.Register.ClassName, r.Register.Date, r.Register.Period,
            r.Reason, r.Status, Name(names, r.CreatedBy), r.CreatedAt, Name(names, r.DecidedBy), r.DecidedAt, r.DecisionNote,
            items.Where(i => i.CorrectionId == r.Id).OrderBy(i => i.FullName)
                .Select(i => new CorrectionItemDto(i.StudentId, i.FullName, i.AdmissionNumber, i.FromStatus, i.ToStatus, i.Remark,
                    marks.Where(m => m.RegisterId == r.RegisterId && m.StudentId == i.StudentId).Select(m => (AttendanceStatus?)m.Status).FirstOrDefault()))
                .ToList(),
            approver && r.Status == CorrectionStatus.Pending,
            r.Status == CorrectionStatus.Pending && r.CreatedBy == current.UserId)).ToList();
    }

    internal async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var wanted = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        return wanted.Count == 0 ? []
            : await db.Users.Where(u => wanted.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => (u.FirstName + " " + u.LastName).Trim(), ct);
    }

    internal static string? Name(Dictionary<Guid, string> names, Guid? id) => id is { } value ? names.GetValueOrDefault(value) : null;

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    /// <summary>Numeric roll numbers sort as numbers ("2" before "10"); others after, alphabetically.</summary>
    private static int RollOrder(string? roll) => int.TryParse(roll, out var n) ? n : int.MaxValue;
}
