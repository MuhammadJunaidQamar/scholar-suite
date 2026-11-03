using FluentValidation;
using Scholar.Application.Common;
using Scholar.Domain.Attendance;
using Scholar.Domain.Communication;
using Scholar.Domain.Platform;
using Scholar.Domain.Staffing;
using Scholar.Domain.Students;

namespace Scholar.Application.Attendance;

// ───────────────────────────── School days ─────────────────────────────

/// <param name="IsSchoolDay">A working day that is not a holiday.</param>
/// <param name="ClosedReason">Why no register is expected ("Sunday is not a working day", "Holiday: Iqbal Day").</param>
/// <param name="IsFuture">After today in the school's timezone; registers cannot be taken ahead.</param>
public sealed record DayStatusDto(DateOnly Date, bool IsSchoolDay, string? ClosedReason, bool IsFuture, bool IsToday);

// ───────────────────────────── Student registers ─────────────────────────────

public sealed record RegisterInfoDto(Guid Id, DateTimeOffset TakenAt, string? TakenByName, DateTimeOffset? UpdatedAt, string? UpdatedByName,
    string? Note, uint Version);

/// <param name="Kind">"leave" (approved leave covers the day), "pendingLeave", "suspended", "joined" (placed after the
/// register was taken) or "left" (no longer in the section on this date).</param>
public sealed record RegisterHintDto(string Kind, string Text, Guid? LeaveRequestId);

/// <param name="Saved">The mark is stored; false when it is the suggested starting mark of a register not yet taken.</param>
/// <param name="RecentAbsences">Absences in the 30 days before this date (shown so patterns are noticed early).</param>
public sealed record RegisterRowDto(Guid StudentId, string FullName, string AdmissionNumber, string? RollNumber, Gender Gender,
    Guid? PhotoFileId, AttendanceStatus Status, string? Remark, TimeOnly? ArrivedAt, bool Saved, RegisterHintDto? Hint,
    string? MedicalAlert, int RecentAbsences);

public sealed record RegisterCountsDto(int Total, int Present, int Late, int Absent, int Leave);

public sealed record MessageCountsDto(int Total, int Pending, int NotSent, int Queued, int Sent, int Delivered, int Failed, int Cancelled,
    string? NotSentReason);

/// <param name="Register">Null until the register has been taken.</param>
/// <param name="CanTake">May take (or, before it locks, edit) this register.</param>
/// <param name="CanCorrect">May ask for a correction once the register has locked.</param>
/// <param name="CanOverrideClosedDay">May take a register on a holiday or day off (make-up days).</param>
public sealed record RegisterSheetDto(
    Guid SectionId, string ClassName, Guid CampusId, string CampusName, DateOnly Date, int? Period, string? PeriodLabel,
    Guid? SubjectId, string? SubjectName, string? ClassTeacherName, RegisterInfoDto? Register, DayStatusDto Day, bool CanTake,
    bool IsLocked, DateTimeOffset? LocksAt, bool CanCorrect, bool CanApprove, bool CanOverrideClosedDay,
    IReadOnlyList<RegisterRowDto> Students, RegisterCountsDto Counts, CorrectionDto? PendingCorrection, MessageCountsDto? Alerts);

public sealed record RegisterMarkInput(Guid StudentId, AttendanceStatus Status, string? Remark = null, TimeOnly? ArrivedAt = null);

/// <param name="Version">The register's version when it was loaded (omit for a new register).</param>
/// <param name="OverrideClosedDay">Take a register on a holiday or day off (needs "take attendance (any class)").</param>
public sealed record SaveRegisterRequest(Guid SectionId, DateOnly Date, int? Period, Guid? SubjectId, IReadOnlyList<RegisterMarkInput> Marks,
    string? Note = null, uint? Version = null, bool OverrideClosedDay = false);

// ───────────────────────────── Corrections ─────────────────────────────

public sealed record CorrectionItemDto(Guid StudentId, string StudentName, string AdmissionNumber, AttendanceStatus? FromStatus,
    AttendanceStatus ToStatus, string? Remark, AttendanceStatus? CurrentStatus);

public sealed record CorrectionDto(Guid Id, Guid RegisterId, Guid SectionId, string ClassName, DateOnly Date, int? Period, string Reason,
    CorrectionStatus Status, string? RequestedByName, DateTimeOffset RequestedAt, string? DecidedByName, DateTimeOffset? DecidedAt,
    string? DecisionNote, IReadOnlyList<CorrectionItemDto> Items, bool CanDecide, bool CanWithdraw);

public sealed record RequestCorrectionRequest(IReadOnlyList<RegisterMarkInput> Changes, string Reason);

public sealed record DecideRequest(bool Approve, string? Note);

public sealed record CorrectionQuery : PageQuery
{
    public CorrectionStatus? Status { get; init; }
    public Guid? CampusId { get; init; }
    /// <summary>Only corrections the caller asked for.</summary>
    public bool? Mine { get; init; }
}

// ───────────────────────────── Student leave ─────────────────────────────

public sealed record StudentLeaveDto(Guid Id, Guid StudentId, string StudentName, string AdmissionNumber, string? ClassName,
    Guid? PhotoFileId, DateOnly FromDate, DateOnly ToDate, int SchoolDays, StudentLeaveKind Kind, string Reason, string? RequestedBy,
    LeaveStatus Status, DateTimeOffset RecordedAt, string? RecordedByName, string? DecidedByName, DateTimeOffset? DecidedAt,
    string? DecisionNote, bool CanDecide);

/// <param name="Approve">Record the leave as approved straight away (needs "manage student leave").</param>
public sealed record CreateStudentLeaveRequest(Guid StudentId, DateOnly FromDate, DateOnly ToDate, StudentLeaveKind Kind, string Reason,
    string? RequestedBy, bool Approve = false);

/// <param name="MarksChanged">Absences already recorded inside the leave that became Leave (on approval) or
/// Leave marks that went back to Absent (on cancelling an approved leave).</param>
public sealed record LeaveDecisionResult<T>(T Leave, int MarksChanged);

public sealed record StudentLeaveQuery : PageQuery
{
    public LeaveStatus? Status { get; init; }
    public Guid? StudentId { get; init; }
    public Guid? CampusId { get; init; }
    public Guid? SectionId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

// ───────────────────────────── Reports ─────────────────────────────

/// <param name="Percentage">Attended ÷ counted days. Leave either counts as attended or is left out, per the school's policy.</param>
public sealed record AttendanceTotalsDto(int Days, int Present, int Late, int Absent, int Leave, decimal? Percentage);

public sealed record SectionDayDto(Guid SectionId, string ClassName, int GradeOrder, Guid CampusId, string CampusName,
    string? ClassTeacherName, int OnRoll, Guid? RegisterId, DateTimeOffset? TakenAt, string? TakenByName, int Present, int Late,
    int Absent, int Leave);

public sealed record OverviewTotalsDto(int Sections, int Taken, int OnRoll, int Marked, int Present, int Late, int Absent, int Leave,
    decimal? AttendanceRate);

public sealed record StaffDayTotalsDto(int Staff, int Marked, int Present, int Late, int HalfDay, int Absent, int OnLeave, int OnDuty);

public sealed record AttendanceOverviewDto(DateOnly Date, DayStatusDto Day, AttendanceMode Mode, OverviewTotalsDto Totals,
    IReadOnlyList<SectionDayDto> Sections, int PendingCorrections, int PendingLeave, MessageCountsDto Alerts, StaffDayTotalsDto? Staff);

public sealed record TrendPointDto(DateOnly Date, int Marked, int Present, int Late, int Absent, int Leave, decimal? Rate);

public sealed record MessageStatusDto(Guid Id, MessageChannel Channel, string Recipient, MessageStatus Status, string? Detail);

/// <param name="ConsecutiveAbsences">School days in a row the student has been absent, ending on this date.</param>
public sealed record AbsenteeDto(Guid StudentId, string FullName, string AdmissionNumber, Guid? PhotoFileId, Guid SectionId, string ClassName,
    AttendanceStatus Status, TimeOnly? ArrivedAt, string? Remark, string? GuardianName, string? GuardianPhone, int ConsecutiveAbsences,
    IReadOnlyList<MessageStatusDto> Alerts);

public sealed record MonthDayDto(DateOnly Date, bool IsSchoolDay, string? ClosedReason, bool Taken);

/// <param name="Marks">One entry per day of the month (null: no mark).</param>
public sealed record MonthStudentRowDto(Guid StudentId, string FullName, string AdmissionNumber, string? RollNumber,
    IReadOnlyList<AttendanceStatus?> Marks, AttendanceTotalsDto Totals);

public sealed record MonthRegisterDto(Guid SectionId, string ClassName, int Year, int Month, IReadOnlyList<MonthDayDto> Days,
    IReadOnlyList<MonthStudentRowDto> Students, AttendanceTotalsDto Totals);

public sealed record MonthAttendanceDto(int Year, int Month, AttendanceTotalsDto Totals);

public sealed record DayMarkDto(DateOnly Date, AttendanceStatus Status, string? Remark, TimeOnly? ArrivedAt, string ClassName);

public sealed record SubjectAttendanceDto(Guid SubjectId, string SubjectName, int Periods, int Attended, decimal? Percentage);

public sealed record StudentAttendanceSummaryDto(Guid StudentId, string FullName, Guid AcademicYearId, string AcademicYearName, DateOnly From,
    DateOnly To, AttendanceTotalsDto Totals, IReadOnlyList<MonthAttendanceDto> Months, IReadOnlyList<DayMarkDto> Days,
    IReadOnlyList<SubjectAttendanceDto> Subjects, IReadOnlyList<StudentLeaveDto> Leave, decimal MinimumPercentage,
    bool LeaveCountsAsPresent);

public sealed record LowAttendanceQuery : PageQuery
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    /// <summary>Percentage threshold; defaults to the school's minimum attendance.</summary>
    public decimal? Below { get; init; }
    public Guid? CampusId { get; init; }
    public Guid? GradeId { get; init; }
    public Guid? SectionId { get; init; }
}

public sealed record LowAttendanceRowDto(Guid StudentId, string FullName, string AdmissionNumber, Guid? PhotoFileId, string? ClassName,
    AttendanceTotalsDto Totals, DateOnly? LastAbsent, string? GuardianName, string? GuardianPhone);

public sealed record LowAttendanceDto(DateOnly From, DateOnly To, decimal Below, PagedResult<LowAttendanceRowDto> Students);

// ───────────────────────────── Bell schedules ─────────────────────────────

public sealed record BellPeriodDto(int Sequence, int? Number, string Name, TimeOnly StartTime, TimeOnly EndTime, bool IsBreak);

public sealed record BellScheduleDto(Guid? Id, Guid CampusId, string CampusName, string Name, IReadOnlyList<BellPeriodDto> Periods);

public sealed record BellSlotInput(string Name, TimeOnly StartTime, TimeOnly EndTime, bool IsBreak);

public sealed record SaveBellScheduleRequest(string Name, IReadOnlyList<BellSlotInput> Slots);

// ───────────────────────────── Staff attendance ─────────────────────────────

public sealed record StaffRegisterRowDto(Guid StaffId, string FullName, string EmployeeNumber, string? DesignationName, StaffType StaffType,
    Guid? PhotoFileId, StaffAttendanceStatus Status, TimeOnly? CheckIn, TimeOnly? CheckOut, string? Remark, bool Saved,
    RegisterHintDto? Hint);

public sealed record StaffRegisterDto(DateOnly Date, Guid CampusId, string CampusName, DayStatusDto Day, bool CanRecord, bool IsLocked,
    DateTimeOffset? LocksAt, bool CanApprove, bool CanOverrideClosedDay, bool Taken, DateTimeOffset? LastSavedAt, string? LastSavedByName,
    IReadOnlyList<StaffRegisterRowDto> Staff, StaffDayTotalsDto Counts);

public sealed record StaffMarkInput(Guid StaffId, StaffAttendanceStatus Status, TimeOnly? CheckIn = null, TimeOnly? CheckOut = null,
    string? Remark = null);

public sealed record SaveStaffRegisterRequest(Guid CampusId, DateOnly Date, IReadOnlyList<StaffMarkInput> Marks, bool OverrideClosedDay = false);

public sealed record StaffCorrectionDto(Guid Id, Guid StaffId, string StaffName, string EmployeeNumber, DateOnly Date,
    StaffAttendanceStatus? FromStatus, StaffAttendanceStatus ToStatus, TimeOnly? CheckIn, TimeOnly? CheckOut, string Reason,
    bool RequestedBySelf, CorrectionStatus Status, string? RequestedByName, DateTimeOffset RequestedAt, string? DecidedByName,
    DateTimeOffset? DecidedAt, string? DecisionNote, bool CanDecide, bool CanWithdraw);

/// <param name="StaffId">Null: a correction to the caller's own attendance.</param>
public sealed record RequestStaffCorrectionRequest(Guid? StaffId, DateOnly Date, StaffAttendanceStatus Status, TimeOnly? CheckIn,
    TimeOnly? CheckOut, string Reason);

public sealed record StaffCorrectionQuery : PageQuery
{
    public CorrectionStatus? Status { get; init; }
    public Guid? CampusId { get; init; }
}

public sealed record StaffAttendanceTotalsDto(int Days, int Present, int Late, int HalfDay, int Absent, int OnLeave, int OnDuty,
    decimal? Percentage);

public sealed record StaffMonthRowDto(Guid StaffId, string FullName, string EmployeeNumber, string? DesignationName,
    IReadOnlyList<StaffAttendanceStatus?> Marks, StaffAttendanceTotalsDto Totals);

public sealed record StaffMonthDto(Guid CampusId, string CampusName, int Year, int Month, IReadOnlyList<MonthDayDto> Days,
    IReadOnlyList<StaffMonthRowDto> Staff);

// ───────────────────────────── Staff leave ─────────────────────────────

public sealed record LeaveTypeDto(Guid Id, string Code, string Name, decimal? AnnualDays, bool IsPaid, bool RequiresDocument, bool IsActive,
    int SortOrder);

public sealed record SaveLeaveTypeRequest(string Code, string Name, decimal? AnnualDays, bool IsPaid, bool RequiresDocument, bool IsActive,
    int SortOrder);

/// <param name="Remaining">Null for types without a yearly limit.</param>
public sealed record LeaveBalanceDto(Guid LeaveTypeId, string Code, string Name, decimal? Allowance, decimal Taken, decimal Pending,
    decimal? Remaining);

public sealed record StaffLeaveDto(Guid Id, Guid StaffId, string StaffName, string EmployeeNumber, string? DesignationName,
    Guid LeaveTypeId, string LeaveTypeCode, string LeaveTypeName, DateOnly FromDate, DateOnly ToDate, bool HalfDay, decimal Days,
    string Reason, LeaveStatus Status, bool RequestedBySelf, DateTimeOffset RequestedAt, string? RequestedByName, string? DecidedByName,
    DateTimeOffset? DecidedAt, string? DecisionNote, bool CanDecide, bool CanCancel);

/// <param name="StaffId">Null: leave for the caller's own staff record.</param>
/// <param name="Approve">Record as approved straight away (needs "approve staff leave").</param>
public sealed record CreateStaffLeaveRequest(Guid? StaffId, Guid LeaveTypeId, DateOnly FromDate, DateOnly ToDate, bool HalfDay, string Reason,
    bool Approve = false);

public sealed record StaffLeaveQuery : PageQuery
{
    public LeaveStatus? Status { get; init; }
    public Guid? StaffId { get; init; }
    public Guid? CampusId { get; init; }
    public Guid? LeaveTypeId { get; init; }
}

// ───────────────────────────── Teacher home & self-service ─────────────────────────────

/// <param name="Kind">"daily" (class teacher roll call) or "period" (a subject the teacher teaches the section).</param>
public sealed record TeacherRegisterTaskDto(Guid SectionId, string ClassName, string Kind, Guid? SubjectId, string? SubjectName,
    int? Period, bool Taken, int? OnRoll, int? Absent, DateTimeOffset? TakenAt);

public sealed record MyStaffDto(Guid StaffId, string FullName, string EmployeeNumber, string? DesignationName, Guid CampusId,
    string CampusName);

public sealed record MyDayDto(StaffAttendanceStatus? Status, TimeOnly? CheckIn, TimeOnly? CheckOut);

/// <param name="Staff">Null when the signed-in account is not linked to a staff record.</param>
public sealed record TeacherTodayDto(DateOnly Date, DayStatusDto Day, AttendanceMode Mode, MyStaffDto? Staff,
    IReadOnlyList<TeacherRegisterTaskDto> Registers, IReadOnlyList<BellPeriodDto> Periods, MyDayDto? MyAttendance, int PendingLeave,
    int PendingCorrections);

public sealed record MyAttendanceDto(MyStaffDto Staff, int Year, int Month, IReadOnlyList<MonthDayDto> Days,
    IReadOnlyList<StaffAttendanceStatus?> Marks, StaffAttendanceTotalsDto Totals, IReadOnlyList<LeaveBalanceDto> Balances,
    IReadOnlyList<StaffLeaveDto> Leave, IReadOnlyList<StaffCorrectionDto> Corrections);

// ───────────────────────────── Validators ─────────────────────────────

public sealed class SaveRegisterValidator : AbstractValidator<SaveRegisterRequest>
{
    public SaveRegisterValidator()
    {
        RuleFor(x => x.SectionId).NotEmpty();
        RuleFor(x => x.Period).InclusiveBetween(1, 16).When(x => x.Period is not null);
        RuleFor(x => x.Marks).NotNull().Must(m => m.Select(i => i.StudentId).Distinct().Count() == m.Count)
            .WithMessage("Each student can be marked once.");
        RuleForEach(x => x.Marks).ChildRules(m => m.RuleFor(i => i.Remark).MaximumLength(300));
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class RequestCorrectionValidator : AbstractValidator<RequestCorrectionRequest>
{
    public RequestCorrectionValidator()
    {
        RuleFor(x => x.Changes).NotEmpty().WithMessage("Change at least one mark.")
            .Must(m => m.Select(i => i.StudentId).Distinct().Count() == m.Count).WithMessage("Each student can appear once.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class CreateStudentLeaveValidator : AbstractValidator<CreateStudentLeaveRequest>
{
    public CreateStudentLeaveValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate).WithMessage("The leave must end on or after the day it starts.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.RequestedBy).MaximumLength(150);
    }
}

public sealed class SaveBellScheduleValidator : AbstractValidator<SaveBellScheduleRequest>
{
    public SaveBellScheduleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Slots).NotEmpty();
        RuleForEach(x => x.Slots).ChildRules(s => s.RuleFor(i => i.Name).MaximumLength(60));
    }
}

public sealed class SaveStaffRegisterValidator : AbstractValidator<SaveStaffRegisterRequest>
{
    public SaveStaffRegisterValidator()
    {
        RuleFor(x => x.CampusId).NotEmpty();
        RuleFor(x => x.Marks).NotEmpty().Must(m => m.Select(i => i.StaffId).Distinct().Count() == m.Count)
            .WithMessage("Each person can be marked once.");
        RuleForEach(x => x.Marks).ChildRules(m => m.RuleFor(i => i.Remark).MaximumLength(300));
    }
}

public sealed class RequestStaffCorrectionValidator : AbstractValidator<RequestStaffCorrectionRequest>
{
    public RequestStaffCorrectionValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
}

public sealed class SaveLeaveTypeValidator : AbstractValidator<SaveLeaveTypeRequest>
{
    public SaveLeaveTypeValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(10).Matches("^[A-Za-z0-9]+$").WithMessage("Use letters and digits only.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
        RuleFor(x => x.AnnualDays).InclusiveBetween(0, 365).When(x => x.AnnualDays is not null);
    }
}

public sealed class CreateStaffLeaveValidator : AbstractValidator<CreateStaffLeaveRequest>
{
    public CreateStaffLeaveValidator()
    {
        RuleFor(x => x.LeaveTypeId).NotEmpty().WithName("Leave type");
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate).WithMessage("The leave must end on or after the day it starts.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
