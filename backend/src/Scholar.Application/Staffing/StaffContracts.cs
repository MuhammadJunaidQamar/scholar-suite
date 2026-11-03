using FluentValidation;
using Scholar.Application.Access;
using Scholar.Application.Common;
using Scholar.Application.Organization;
using Scholar.Domain.Identity;
using Scholar.Domain.Staffing;
using Scholar.Domain.Students;

namespace Scholar.Application.Staffing;

public sealed record StaffQuery : PageQuery
{
    public Guid? CampusId { get; init; }
    public Guid? DepartmentId { get; init; }
    public Guid? DesignationId { get; init; }
    public StaffType? StaffType { get; init; }
    public StaffStatus? Status { get; init; }
    /// <summary>"current" (default: active, on leave, suspended), "left" or "all".</summary>
    public string? Scope { get; init; }
}

/// <param name="ClassTeacherOf">Sections the person is class teacher of this year ("Class 5 – A, Class 6 – B").</param>
public sealed record StaffListItemDto(Guid Id, string EmployeeNumber, string FullName, Gender Gender, Guid? PhotoFileId,
    StaffType StaffType, StaffStatus Status, Guid CampusId, string CampusName, string? DepartmentName, string? DesignationName,
    string? Phone, string? Email, DateOnly JoinDate, bool HasLogin, string? ClassTeacherOf);

/// <summary>A lightweight choice for teacher pickers.</summary>
public sealed record StaffOptionDto(Guid Id, string FullName, string EmployeeNumber, Guid CampusId, string? DesignationName,
    StaffType StaffType, int ClassTeacherCount, int SubjectCount);

/// <param name="SubjectId">Null for a class-teacher assignment.</param>
public sealed record StaffAssignmentDto(Guid SectionId, string ClassName, int GradeOrder, Guid? SubjectId, string? SubjectName);

public sealed record StaffAccountDto(Guid UserId, string UserName, string? Email, UserStatus Status, bool AccessSuspended,
    IReadOnlyList<string> Roles, DateTimeOffset? LastLoginAt);

public sealed record StaffDetailDto(
    Guid Id, string EmployeeNumber, string FirstName, string LastName, string FullName, Gender Gender, DateOnly? DateOfBirth,
    string? NationalId, string? Phone, string? Email, AddressDto Address, Guid? PhotoFileId, Guid CampusId, string CampusName,
    Guid? DepartmentId, string? DepartmentName, Guid? DesignationId, string? DesignationName, StaffType StaffType,
    EmploymentType EmploymentType, DateOnly JoinDate, StaffStatus Status, DateOnly? ExitDate, string? ExitReason,
    StaffAccountDto? Account, IReadOnlyList<StaffAssignmentDto> ClassTeacherOf, IReadOnlyList<StaffAssignmentDto> Teaches, uint Version);

public sealed record SaveStaffRequest(
    string FirstName, string LastName, Gender Gender, DateOnly? DateOfBirth, string? NationalId, string? Phone, string? Email,
    AddressDto? Address, Guid CampusId, Guid? DepartmentId, Guid? DesignationId, StaffType StaffType, EmploymentType EmploymentType,
    DateOnly JoinDate, string? EmployeeNumber = null, uint? Version = null);

public sealed record ChangeStaffStatusRequest(StaffStatus Status, DateOnly EffectiveDate, string? Reason);

/// <param name="RemovedAssignments">Class-teacher and subject assignments released because the person left.</param>
public sealed record StaffStatusResult(StaffDetailDto Staff, int RemovedAssignments);

/// <summary>Creates (or reuses) the sign-in account for the staff member's email and links it to the record.</summary>
public sealed record GrantStaffAccessRequest(IReadOnlyList<RoleAssignmentInput> Roles);

public sealed record LinkStaffAccountRequest(Guid UserId);

public sealed record StaffAccessResult(StaffDetailDto Staff, Identity.AccountHandover? Handover);

// ───────────────────────────── Teaching assignments ─────────────────────────────

public sealed record SubjectTeacherDto(Guid SubjectId, string SubjectName, string SubjectCode, bool Offered, Guid? StaffId, string? StaffName);

/// <param name="Subjects">The grade's subjects for the year, plus any other subject someone is assigned to teach.</param>
public sealed record SectionTeachingDto(Guid SectionId, string ClassName, Guid CampusId, string CampusName, int GradeOrder,
    Guid? ClassTeacherId, string? ClassTeacherName, IReadOnlyList<SubjectTeacherDto> Subjects);

public sealed record SubjectTeacherInput(Guid SubjectId, Guid? StaffId);

/// <summary>Sets a section's class teacher and the teachers of the listed subjects (a null teacher clears one).
/// Subjects that are not listed keep their teacher.</summary>
public sealed record SaveSectionTeachersRequest(Guid? ClassTeacherId, IReadOnlyList<SubjectTeacherInput> Subjects);

public sealed class SaveStaffValidator : AbstractValidator<SaveStaffRequest>
{
    public SaveStaffValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).MaximumLength(100);
        RuleFor(x => x.CampusId).NotEmpty().WithName("Campus");
        RuleFor(x => x.NationalId).Must(v => string.IsNullOrWhiteSpace(v) || v.Count(char.IsDigit) == 13)
            .WithMessage("CNIC numbers have 13 digits.");
        RuleFor(x => x.Phone).Must(p => string.IsNullOrWhiteSpace(p) || p.Count(char.IsDigit) >= 10)
            .WithMessage("Enter a mobile number, e.g. 0300 1234567.");
        RuleFor(x => x.Email).EmailAddress().MaximumLength(254).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.DateOfBirth)
            .Must(d => d is null || d <= DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-16)).WithMessage("Staff must be at least 16 years old.")
            .Must(d => d is null || d >= DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-90)).WithMessage("Check the date of birth.");
        RuleFor(x => x.JoinDate).Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1))
            .WithMessage("The joining date is too far in the future.");
        RuleFor(x => x.EmployeeNumber).MaximumLength(40);
    }
}

public sealed class ChangeStaffStatusValidator : AbstractValidator<ChangeStaffStatusRequest>
{
    public ChangeStaffStatusValidator() => RuleFor(x => x.Reason).MaximumLength(500);
}

public sealed class GrantStaffAccessValidator : AbstractValidator<GrantStaffAccessRequest>
{
    public GrantStaffAccessValidator() => RuleFor(x => x.Roles).NotEmpty().WithMessage("Choose at least one role.");
}

public sealed class SaveSectionTeachersValidator : AbstractValidator<SaveSectionTeachersRequest>
{
    public SaveSectionTeachersValidator()
    {
        RuleFor(x => x.Subjects).NotNull();
        RuleFor(x => x.Subjects).Must(s => s.Select(i => i.SubjectId).Distinct().Count() == s.Count)
            .WithMessage("Each subject can have only one teacher per section.");
    }
}
