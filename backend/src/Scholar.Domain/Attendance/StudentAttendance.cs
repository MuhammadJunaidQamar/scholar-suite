using Scholar.Domain.Common;

namespace Scholar.Domain.Attendance;

/// <summary>
/// How a student was marked. Late counts as attended; Leave is an approved or excused absence and, depending on the
/// school's policy, either counts as attended or is left out of percentages altogether.
/// </summary>
public enum AttendanceStatus { Present, Late, Absent, Leave }

/// <summary>
/// One register: a section on a date, either the daily roll call (<see cref="Period"/> is null, taken by the class
/// teacher) or one teaching period (taken by the subject teacher in period-wise mode). Records are kept for every
/// student who was on the roll that day.
/// </summary>
public sealed class AttendanceRegister : TenantEntity, IHasAuditTrail, IConcurrencyVersioned
{
    private AttendanceRegister() { }

    /// <remarks>Who took the register and when are the audit stamps (<c>CreatedBy</c>/<c>CreatedAt</c>); later edits
    /// are <c>UpdatedBy</c>/<c>UpdatedAt</c> plus the field-level audit trail.</remarks>
    public AttendanceRegister(Guid schoolId, Guid sectionId, Guid academicYearId, Guid campusId, DateOnly date, int? period,
        Guid? subjectId, Guid? takenByStaffId)
    {
        Guard.Against(period is < 1 or > 16, "attendance.period_invalid", "Choose a period between 1 and 16.");
        SchoolId = schoolId;
        SectionId = sectionId;
        AcademicYearId = academicYearId;
        CampusId = campusId;
        Date = date;
        Period = period;
        SubjectId = period is null ? null : subjectId;
        TakenByStaffId = takenByStaffId;
    }

    public Guid SectionId { get; private set; }
    public Guid AcademicYearId { get; private set; }
    public Guid CampusId { get; private set; }
    public DateOnly Date { get; private set; }
    /// <summary>Teaching period number (1-based); null for the daily roll call.</summary>
    public int? Period { get; private set; }
    public Guid? SubjectId { get; set; }
    /// <summary>The teacher who took the register, when the person signed in has a staff record.</summary>
    public Guid? TakenByStaffId { get; private set; }
    public string? Note { get; set; }
    public uint Version { get; set; }

    public ICollection<AttendanceRecord> Records { get; } = new List<AttendanceRecord>();

    public bool IsDaily => Period is null;

    /// <summary>Sets (or adds) one student's mark. Returns the previous status, null for a new record.</summary>
    public AttendanceStatus? Mark(Guid studentId, AttendanceStatus status, string? remark, TimeOnly? arrivedAt, Guid? leaveRequestId = null)
    {
        var record = Records.SingleOrDefault(r => r.StudentId == studentId);
        var previous = record?.Status;
        if (record is null)
        {
            record = new AttendanceRecord(SchoolId, Id, studentId, status);
            Records.Add(record);
        }

        record.Status = status;
        record.Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();
        record.ArrivedAt = status == AttendanceStatus.Late ? arrivedAt : null;
        record.LeaveRequestId = status == AttendanceStatus.Leave ? leaveRequestId ?? record.LeaveRequestId : null;
        return previous;
    }
}

public sealed class AttendanceRecord : TenantEntity, IAuditChangesOnly
{
    private AttendanceRecord() { }

    internal AttendanceRecord(Guid schoolId, Guid registerId, Guid studentId, AttendanceStatus status)
    {
        SchoolId = schoolId;
        RegisterId = registerId;
        StudentId = studentId;
        Status = status;
    }

    public Guid RegisterId { get; private set; }
    public AttendanceRegister Register { get; private set; } = default!;
    public Guid StudentId { get; private set; }
    public AttendanceStatus Status { get; set; }
    public string? Remark { get; set; }
    /// <summary>Arrival time for late students.</summary>
    public TimeOnly? ArrivedAt { get; set; }
    /// <summary>The approved leave that excused this day, when the mark came from one.</summary>
    public Guid? LeaveRequestId { get; set; }
}

public enum CorrectionStatus { Pending, Approved, Rejected, Withdrawn }

/// <summary>
/// A requested change to a register that has locked. Approvers' own corrections apply at once (still recorded);
/// everyone else's wait for someone holding the approval permission.
/// </summary>
public sealed class AttendanceCorrection : TenantEntity, IHasAuditTrail
{
    private AttendanceCorrection() { }

    public AttendanceCorrection(Guid schoolId, Guid registerId, string reason)
    {
        Guard.Against(string.IsNullOrWhiteSpace(reason), "correction.reason_required", "Explain why the register needs correcting.");
        SchoolId = schoolId;
        RegisterId = registerId;
        Reason = reason.Trim();
    }

    public Guid RegisterId { get; private set; }
    public string Reason { get; private set; } = default!;
    public CorrectionStatus Status { get; private set; } = CorrectionStatus.Pending;
    public Guid? DecidedBy { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public string? DecisionNote { get; private set; }

    public ICollection<AttendanceCorrectionItem> Items { get; } = new List<AttendanceCorrectionItem>();

    public void AddItem(Guid studentId, AttendanceStatus? from, AttendanceStatus to, string? remark)
    {
        Guard.Against(Items.Any(i => i.StudentId == studentId), "correction.duplicate_student", "Each student can appear once.");
        Items.Add(new AttendanceCorrectionItem(SchoolId, Id, studentId, from, to, remark));
    }

    public void Approve(Guid by, DateTimeOffset at, string? note)
    {
        Guard.Against(Status != CorrectionStatus.Pending, "correction.not_pending", "This correction has already been decided.");
        Status = CorrectionStatus.Approved;
        Decide(by, at, note);
    }

    public void Reject(Guid by, DateTimeOffset at, string? note)
    {
        Guard.Against(Status != CorrectionStatus.Pending, "correction.not_pending", "This correction has already been decided.");
        Guard.Against(string.IsNullOrWhiteSpace(note), "correction.note_required", "Say why the correction is rejected.");
        Status = CorrectionStatus.Rejected;
        Decide(by, at, note);
    }

    public void Withdraw(Guid by, DateTimeOffset at)
    {
        Guard.Against(Status != CorrectionStatus.Pending, "correction.not_pending", "Only a pending correction can be withdrawn.");
        Status = CorrectionStatus.Withdrawn;
        Decide(by, at, null);
    }

    private void Decide(Guid by, DateTimeOffset at, string? note)
    {
        DecidedBy = by;
        DecidedAt = at;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}

public sealed class AttendanceCorrectionItem : TenantEntity
{
    private AttendanceCorrectionItem() { }

    internal AttendanceCorrectionItem(Guid schoolId, Guid correctionId, Guid studentId, AttendanceStatus? from, AttendanceStatus to, string? remark)
    {
        Guard.Against(from == to, "correction.no_change", "A correction must change the student's mark.");
        SchoolId = schoolId;
        CorrectionId = correctionId;
        StudentId = studentId;
        FromStatus = from;
        ToStatus = to;
        Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();
    }

    public Guid CorrectionId { get; private set; }
    public Guid StudentId { get; private set; }
    /// <summary>The mark when the correction was requested; null when the student was missing from the register.</summary>
    public AttendanceStatus? FromStatus { get; private set; }
    public AttendanceStatus ToStatus { get; private set; }
    public string? Remark { get; private set; }
}

public enum LeaveStatus { Pending, Approved, Rejected, Cancelled }

public enum StudentLeaveKind { Sick, Family, Travel, Religious, Other }

/// <summary>
/// A leave application for a student (from a parent's note, call or, later, the family portal). Approval marks the
/// covered days as Leave: registers already taken are corrected, and registers taken later start from Leave.
/// </summary>
public sealed class StudentLeaveRequest : TenantEntity, IHasAuditTrail
{
    private StudentLeaveRequest() { }

    public StudentLeaveRequest(Guid schoolId, Guid studentId, DateOnly fromDate, DateOnly toDate, StudentLeaveKind kind, string reason,
        string? requestedBy)
    {
        Guard.Against(toDate < fromDate, "leave.dates", "The leave must end on or after the day it starts.");
        Guard.Against(toDate.DayNumber - fromDate.DayNumber > 120, "leave.too_long", "A single leave can cover at most 120 days.");
        Guard.Against(string.IsNullOrWhiteSpace(reason), "leave.reason_required", "Give the reason for the leave.");
        SchoolId = schoolId;
        StudentId = studentId;
        FromDate = fromDate;
        ToDate = toDate;
        Kind = kind;
        Reason = reason.Trim();
        RequestedBy = string.IsNullOrWhiteSpace(requestedBy) ? null : requestedBy.Trim();
    }

    public Guid StudentId { get; private set; }
    public DateOnly FromDate { get; private set; }
    public DateOnly ToDate { get; private set; }
    public StudentLeaveKind Kind { get; private set; }
    public string Reason { get; private set; } = default!;
    /// <summary>Who applied, as told to the office ("Father, by phone").</summary>
    public string? RequestedBy { get; private set; }
    public Guid? DocumentFileId { get; set; }
    public LeaveStatus Status { get; private set; } = LeaveStatus.Pending;
    public Guid? DecidedBy { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public string? DecisionNote { get; private set; }

    public bool Covers(DateOnly date) => date >= FromDate && date <= ToDate;

    public void Approve(Guid by, DateTimeOffset at, string? note) => Decide(LeaveStatus.Approved, by, at, note);

    public void Reject(Guid by, DateTimeOffset at, string? note)
    {
        Guard.Against(string.IsNullOrWhiteSpace(note), "leave.note_required", "Say why the leave is rejected.");
        Decide(LeaveStatus.Rejected, by, at, note);
    }

    public void Cancel(Guid by, DateTimeOffset at, string? note)
    {
        Guard.Against(Status is LeaveStatus.Rejected or LeaveStatus.Cancelled, "leave.closed", "This leave is already closed.");
        Status = LeaveStatus.Cancelled;
        DecidedBy = by;
        DecidedAt = at;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? DecisionNote : note.Trim();
    }

    private void Decide(LeaveStatus to, Guid by, DateTimeOffset at, string? note)
    {
        Guard.Against(Status != LeaveStatus.Pending, "leave.not_pending", "This leave has already been decided.");
        Status = to;
        DecidedBy = by;
        DecidedAt = at;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}

/// <summary>A campus's school day: teaching periods (numbered) and breaks, used to label period registers.</summary>
public sealed class BellSchedule : TenantEntity, IHasAuditTrail
{
    private BellSchedule() { }

    public BellSchedule(Guid schoolId, Guid campusId, string name)
    {
        SchoolId = schoolId;
        CampusId = campusId;
        Name = name.Trim();
    }

    public Guid CampusId { get; private set; }
    public string Name { get; set; } = default!;
    public ICollection<BellPeriod> Periods { get; } = new List<BellPeriod>();

    /// <summary>Replaces the day's slots. Teaching periods are numbered 1..n in time order; breaks are not numbered.</summary>
    public void ReplacePeriods(IEnumerable<(string Name, TimeOnly Start, TimeOnly End, bool IsBreak)> slots)
    {
        var ordered = slots.OrderBy(s => s.Start).ToList();
        Guard.Against(ordered.Count == 0, "bell.empty", "Add at least one period.");
        Guard.Against(ordered.Count > 20, "bell.too_many", "A day can have at most 20 slots.");
        for (var i = 0; i < ordered.Count; i++)
        {
            var label = string.IsNullOrWhiteSpace(ordered[i].Name) ? $"Slot {i + 1}" : ordered[i].Name.Trim();
            if (ordered[i].End <= ordered[i].Start)
            {
                throw new DomainException("bell.times", $"{label} must end after it starts.");
            }

            if (i > 0 && ordered[i].Start < ordered[i - 1].End)
            {
                throw new DomainException("bell.overlap", $"{label} starts before the slot before it ends.");
            }
        }

        Guard.Against(ordered.All(s => s.IsBreak), "bell.no_teaching", "Add at least one teaching period.");
        Periods.Clear();
        var number = 0;
        for (var i = 0; i < ordered.Count; i++)
        {
            var slot = ordered[i];
            Periods.Add(new BellPeriod(SchoolId, Id, i + 1, slot.IsBreak ? null : ++number,
                string.IsNullOrWhiteSpace(slot.Name) ? (slot.IsBreak ? "Break" : $"Period {number}") : slot.Name.Trim(),
                slot.Start, slot.End));
        }
    }
}

public sealed class BellPeriod : TenantEntity
{
    private BellPeriod() { }

    internal BellPeriod(Guid schoolId, Guid scheduleId, int sequence, int? number, string name, TimeOnly start, TimeOnly end)
    {
        SchoolId = schoolId;
        ScheduleId = scheduleId;
        Sequence = sequence;
        Number = number;
        Name = name;
        StartTime = start;
        EndTime = end;
    }

    public Guid ScheduleId { get; private set; }
    public int Sequence { get; private set; }
    /// <summary>Teaching period number; null for breaks and assembly.</summary>
    public int? Number { get; private set; }
    public string Name { get; private set; } = default!;
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }

    public bool IsBreak => Number is null;
}
