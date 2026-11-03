using FluentValidation;
using Scholar.Application.Common;
using Scholar.Application.Organization;
using Scholar.Domain.Students;

namespace Scholar.Application.Students;

public sealed record StudentQuery : PageQuery
{
    public Guid? CampusId { get; init; }
    public Guid? GradeId { get; init; }
    public Guid? SectionId { get; init; }
    public StudentStatus? Status { get; init; }
    public Gender? Gender { get; init; }
    /// <summary>"current" (default: active + suspended), "left" (withdrawn, transferred, graduated, alumni) or "all".</summary>
    public string? Scope { get; init; }
}

public sealed record StudentListItemDto(Guid Id, string AdmissionNumber, string FullName, Gender Gender, DateOnly DateOfBirth,
    Guid? PhotoFileId, StudentStatus Status, Guid CampusId, string CampusName, Guid? SectionId, string? ClassName, int? GradeOrder,
    string? GuardianName, string? GuardianPhone, DateOnly AdmissionDate, bool HasMedicalAlert);

public sealed record GuardianLinkDto(Guid LinkId, Guid GuardianId, string FullName, GuardianRelationship Relationship, string Phone,
    string? Email, string? Occupation, bool IsPrimary, bool IsEmergencyContact, bool LivesWithStudent, bool CanPickUp, bool IsFeePayer,
    bool PortalAccess, bool CanViewAttendance, bool CanViewResults, bool CanViewFees, bool ReceivesNotifications, bool HasLogin);

public sealed record EnrollmentDto(Guid Id, Guid AcademicYearId, string AcademicYearName, Guid SectionId, string ClassName,
    string CampusName, string? RollNumber, EnrollmentStatus Status, DateOnly StartDate, DateOnly? EndDate, string? Reason);

public sealed record StatusChangeDto(Guid Id, StudentStatus FromStatus, StudentStatus ToStatus, DateOnly EffectiveDate, string? Reason,
    string? DestinationSchool, string? ChangedByName, DateTimeOffset RecordedAt);

public sealed record EmergencyContactDto(Guid Id, string Name, string Relationship, string Phone, string? AlternatePhone, int Priority);

public sealed record SiblingDto(Guid Id, string FullName, string AdmissionNumber, string? ClassName, Guid? PhotoFileId, StudentStatus Status);

public sealed record StudentDetailDto(
    Guid Id, string AdmissionNumber, string? RegistrationNumber, string FirstName, string? MiddleName, string LastName, string FullName,
    Gender Gender, DateOnly DateOfBirth, string? PlaceOfBirth, string? NationalId, string? Nationality, string? Religion,
    string? BloodGroup, string? MotherTongue, Guid? PhotoFileId, string? Email, string? Phone, AddressDto Address,
    string? PreviousSchoolName, string? PreviousSchoolClass, DateOnly AdmissionDate, StudentStatus Status, DateOnly? StatusChangedOn,
    string? MedicalAlert, Guid CampusId, string CampusName, bool HasLogin, EnrollmentDto? CurrentEnrollment,
    IReadOnlyList<EnrollmentDto> Enrollments, IReadOnlyList<GuardianLinkDto> Guardians,
    IReadOnlyList<EmergencyContactDto> EmergencyContacts, IReadOnlyList<SiblingDto> Siblings,
    IReadOnlyList<StatusChangeDto> StatusHistory, int DocumentCount, uint Version);

public sealed record StudentDetailsInput(
    string FirstName, string? MiddleName, string LastName, Gender Gender, DateOnly DateOfBirth, string? PlaceOfBirth,
    string? NationalId, string? Nationality, string? Religion, string? BloodGroup, string? MotherTongue, string? Email, string? Phone,
    AddressDto? Address, string? PreviousSchoolName, string? PreviousSchoolClass, string? MedicalAlert, string? RegistrationNumber);

public sealed record PlacementInput(Guid SectionId, DateOnly AdmissionDate, string? RollNumber);

public sealed record GuardianInput(string FirstName, string LastName, string Phone, string? Email, string? NationalId,
    string? Occupation, string? Employer, string? AlternatePhone, AddressDto? Address);

public sealed record GuardianLinkInput(
    Guid? GuardianId, GuardianInput? NewGuardian, GuardianRelationship Relationship, bool IsPrimary, bool IsFeePayer,
    bool IsEmergencyContact = true, bool LivesWithStudent = true, bool CanPickUp = true, bool PortalAccess = true,
    bool CanViewAttendance = true, bool CanViewResults = true, bool CanViewFees = true, bool ReceivesNotifications = true);

public sealed record EmergencyContactInput(string Name, string Relationship, string Phone, string? AlternatePhone);

public sealed record CreateStudentRequest(StudentDetailsInput Student, PlacementInput Placement, IReadOnlyList<GuardianLinkInput> Guardians,
    IReadOnlyList<EmergencyContactInput>? EmergencyContacts, string? AdmissionNumber = null, bool OverrideCapacity = false);

public sealed record UpdateStudentRequest(StudentDetailsInput Student, uint Version);

public sealed record ChangeStudentStatusRequest(StudentStatus Status, DateOnly EffectiveDate, string? Reason, string? DestinationSchool);

public sealed record TransferSectionRequest(Guid SectionId, DateOnly EffectiveDate, string? Reason, bool OverrideCapacity = false);

public sealed record UpdateGuardianLinkRequest(
    GuardianRelationship Relationship, bool IsPrimary, bool IsFeePayer, bool IsEmergencyContact, bool LivesWithStudent, bool CanPickUp,
    bool PortalAccess, bool CanViewAttendance, bool CanViewResults, bool CanViewFees, bool ReceivesNotifications);

public sealed record MedicalProfileDto(string? Allergies, string? Conditions, string? Medications, string? SpecialNeeds,
    string? DoctorName, string? DoctorPhone, string? EmergencyInstructions, string? BloodGroup, string? MedicalAlert);

internal static class StudentRules
{
    public static IRuleBuilderOptions<T, string?> NationalId<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(v => string.IsNullOrWhiteSpace(v) || new string(v.Where(char.IsDigit).ToArray()).Length == 13)
            .WithMessage("B-Form and CNIC numbers have 13 digits.");
}

public sealed class StudentDetailsValidator : AbstractValidator<StudentDetailsInput>
{
    public StudentDetailsValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MiddleName).MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DateOfBirth)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-2)).WithMessage("The student must be at least 2 years old.")
            .Must(d => d >= DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30)).WithMessage("Check the date of birth.");
        RuleFor(x => x.NationalId).NationalId();
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.BloodGroup).Matches("^(A|B|AB|O)[+-]$").When(x => !string.IsNullOrWhiteSpace(x.BloodGroup))
            .WithMessage("Use a blood group such as B+ or O-.");
        RuleFor(x => x.MedicalAlert).MaximumLength(300);
        RuleFor(x => x.PreviousSchoolName).MaximumLength(200);
    }
}

public sealed class GuardianInputValidator : AbstractValidator<GuardianInput>
{
    public GuardianInputValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).MaximumLength(100);
        RuleFor(x => x.Phone).NotEmpty().Must(p => p.Count(char.IsDigit) >= 10).WithMessage("Enter a mobile number, e.g. 0300 1234567.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.NationalId).NationalId();
    }
}

public sealed class CreateStudentValidator : AbstractValidator<CreateStudentRequest>
{
    public CreateStudentValidator()
    {
        RuleFor(x => x.Student).NotNull().SetValidator(new StudentDetailsValidator());
        RuleFor(x => x.Placement.SectionId).NotEmpty().WithName("Section");
        RuleFor(x => x.Guardians).NotEmpty().WithMessage("Add at least one parent or guardian.");
        RuleFor(x => x.Guardians).Must(g => g.Count(l => l.IsPrimary) <= 1).WithMessage("Only one guardian can be the primary contact.");
        RuleForEach(x => x.Guardians).ChildRules(link =>
        {
            link.RuleFor(l => l).Must(l => l.GuardianId is not null || l.NewGuardian is not null)
                .WithMessage("Choose an existing guardian or enter a new one.");
            link.RuleFor(l => l.NewGuardian!).SetValidator(new GuardianInputValidator()).When(l => l.NewGuardian is not null);
        });
        RuleForEach(x => x.EmergencyContacts).ChildRules(c =>
        {
            c.RuleFor(e => e.Name).NotEmpty().MaximumLength(150);
            c.RuleFor(e => e.Relationship).NotEmpty().MaximumLength(60);
            c.RuleFor(e => e.Phone).NotEmpty().MaximumLength(40);
        });
        RuleFor(x => x.AdmissionNumber).MaximumLength(40);
    }
}

public sealed class UpdateStudentValidator : AbstractValidator<UpdateStudentRequest>
{
    public UpdateStudentValidator() => RuleFor(x => x.Student).NotNull().SetValidator(new StudentDetailsValidator());
}

public sealed class ChangeStudentStatusValidator : AbstractValidator<ChangeStudentStatusRequest>
{
    public ChangeStudentStatusValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.DestinationSchool).MaximumLength(200);
    }
}
