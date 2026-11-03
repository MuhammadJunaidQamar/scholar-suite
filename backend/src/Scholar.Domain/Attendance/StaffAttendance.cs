using Scholar.Domain.Common;

namespace Scholar.Domain.Attendance;

/// <summary>
/// A staff member's day. On duty (board exam invigilation, training) counts as attended; half day counts as half.
/// </summary>
public enum StaffAttendanceStatus { Present, Late, HalfDay, Absent, OnLeave, OnDuty }

public sealed class StaffAttendanceRecord : TenantEntity, IAuditChangesOnly, IConcurrencyVersioned
{
    private StaffAttendanceRecord() { }

    public StaffAttendanceRecord(Guid schoolId, Guid staffId, Guid campusId, DateOnly date, StaffAttendanceStatus status)
    {
        SchoolId = schoolId;
        StaffId = staffId;
        CampusId = campusId;
        Date = date;
        Status = status;
    }

    public Guid StaffId { get; private set; }
    public Guid CampusId { get; private set; }
    public DateOnly Date { get; private set; }
    public StaffAttendanceStatus Status { get; private set; }
    public TimeOnly? CheckIn { get; private set; }
    public TimeOnly? CheckOut { get; private set; }
    public string? Remark { get; private set; }
    public Guid? LeaveRequestId { get; private set; }
    public uint Version { get; set; }

    public void Set(StaffAttendanceStatus status, TimeOnly? checkIn, TimeOnly? checkOut, string? remark, Guid? leaveRequestId = null)
    {
        var attended = status is not (StaffAttendanceStatus.Absent or StaffAttendanceStatus.OnLeave);
        Guard.Against(checkIn is not null && checkOut is not null && checkOut <= checkIn, "staff_attendance.times",
            "Check-out must be after check-in.");
        Status = status;
        CheckIn = attended ? checkIn : null;
        CheckOut = attended ? checkOut : null;
        Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();
        LeaveRequestId = status is StaffAttendanceStatus.OnLeave or StaffAttendanceStatus.HalfDay ? leaveRequestId : null;
    }
}

/// <summary>
/// A requested change to a locked staff day, raised by the staff member ("I was on exam duty") or by whoever keeps
/// the staff register. Decided by someone holding the staff attendance approval permission.
/// </summary>
public sealed class StaffAttendanceCorrection : TenantEntity, IHasAuditTrail
{
    private StaffAttendanceCorrection() { }

    public StaffAttendanceCorrection(Guid schoolId, Guid staffId, DateOnly date, StaffAttendanceStatus? from, StaffAttendanceStatus to,
        TimeOnly? checkIn, TimeOnly? checkOut, string reason, bool requestedBySelf)
    {
        Guard.Against(string.IsNullOrWhiteSpace(reason), "correction.reason_required", "Explain why the attendance needs correcting.");
        SchoolId = schoolId;
        StaffId = staffId;
        Date = date;
        FromStatus = from;
        ToStatus = to;
        CheckIn = checkIn;
        CheckOut = checkOut;
        Reason = reason.Trim();
        RequestedBySelf = requestedBySelf;
    }

    public Guid StaffId { get; private set; }
    public DateOnly Date { get; private set; }
    public StaffAttendanceStatus? FromStatus { get; private set; }
    public StaffAttendanceStatus ToStatus { get; private set; }
    public TimeOnly? CheckIn { get; private set; }
    public TimeOnly? CheckOut { get; private set; }
    public string Reason { get; private set; } = default!;
    public bool RequestedBySelf { get; private set; }
    public CorrectionStatus Status { get; private set; } = CorrectionStatus.Pending;
    public Guid? DecidedBy { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public string? DecisionNote { get; private set; }

    public void Approve(Guid by, DateTimeOffset at, string? note) => Decide(CorrectionStatus.Approved, by, at, note);

    public void Reject(Guid by, DateTimeOffset at, string? note)
    {
        Guard.Against(string.IsNullOrWhiteSpace(note), "correction.note_required", "Say why the correction is rejected.");
        Decide(CorrectionStatus.Rejected, by, at, note);
    }

    public void Withdraw(Guid by, DateTimeOffset at) => Decide(CorrectionStatus.Withdrawn, by, at, null);

    private void Decide(CorrectionStatus to, Guid by, DateTimeOffset at, string? note)
    {
        Guard.Against(Status != CorrectionStatus.Pending, "correction.not_pending", "This correction has already been decided.");
        Status = to;
        DecidedBy = by;
        DecidedAt = at;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}

/// <summary>A kind of staff leave with its yearly allowance (null = no limit, e.g. unpaid leave).</summary>
public sealed class LeaveType : TenantEntity, IHasAuditTrail
{
    private LeaveType() { }

    public LeaveType(Guid schoolId, string code, string name, decimal? annualDays, bool isPaid)
    {
        SchoolId = schoolId;
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        AnnualDays = annualDays;
        IsPaid = isPaid;
    }

    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public decimal? AnnualDays { get; set; }
    public bool IsPaid { get; set; }
    public bool RequiresDocument { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>
/// Staff leave. <see cref="Days"/> counts working days (holidays and weekly offs inside the range are free) and is
/// fixed when the request is made, so later calendar edits never change an approved balance.
/// </summary>
public sealed class StaffLeaveRequest : TenantEntity, IHasAuditTrail
{
    private StaffLeaveRequest() { }

    public StaffLeaveRequest(Guid schoolId, Guid staffId, Guid leaveTypeId, DateOnly fromDate, DateOnly toDate, bool halfDay, decimal days,
        string reason, bool requestedBySelf)
    {
        Guard.Against(toDate < fromDate, "leave.dates", "The leave must end on or after the day it starts.");
        Guard.Against(halfDay && toDate != fromDate, "leave.half_day_range", "A half day covers a single date.");
        Guard.Against(days <= 0, "leave.no_working_days", "The dates you chose are all holidays or days off.");
        Guard.Against(string.IsNullOrWhiteSpace(reason), "leave.reason_required", "Give the reason for the leave.");
        SchoolId = schoolId;
        StaffId = staffId;
        LeaveTypeId = leaveTypeId;
        FromDate = fromDate;
        ToDate = toDate;
        HalfDay = halfDay;
        Days = days;
        Reason = reason.Trim();
        RequestedBySelf = requestedBySelf;
    }

    public Guid StaffId { get; private set; }
    public Guid LeaveTypeId { get; private set; }
    public DateOnly FromDate { get; private set; }
    public DateOnly ToDate { get; private set; }
    public bool HalfDay { get; private set; }
    public decimal Days { get; private set; }
    public string Reason { get; private set; } = default!;
    public bool RequestedBySelf { get; private set; }
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
