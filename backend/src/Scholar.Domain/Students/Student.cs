using Scholar.Domain.Common;

namespace Scholar.Domain.Students;

public enum Gender { Female, Male, Other }

public enum StudentStatus { Active, Suspended, Inactive, Withdrawn, TransferredOut, Graduated, Alumni }

/// <summary>
/// A learner's permanent record. Placement in a class lives in <see cref="Enrollment"/> (one per academic year); the
/// student keeps a cached pointer to the current section for fast lists.
/// </summary>
public sealed class Student : TenantEntity, IHasAuditTrail, IConcurrencyVersioned
{
    private Student() { }

    public Student(Guid schoolId, Guid campusId, string admissionNumber, string firstName, string lastName, Gender gender,
        DateOnly dateOfBirth, DateOnly admissionDate)
    {
        SchoolId = schoolId;
        CampusId = campusId;
        AdmissionNumber = admissionNumber;
        Rename(firstName, null, lastName);
        Gender = gender;
        DateOfBirth = dateOfBirth;
        AdmissionDate = admissionDate;
    }

    public Guid CampusId { get; set; }
    public string AdmissionNumber { get; private set; } = default!;
    public string? RegistrationNumber { get; set; }
    public string FirstName { get; private set; } = default!;
    public string? MiddleName { get; private set; }
    public string LastName { get; private set; } = default!;
    /// <summary>Database-computed "first middle last", indexed for fuzzy search.</summary>
    public string FullName { get; private set; } = default!;
    public Gender Gender { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string? PlaceOfBirth { get; set; }
    /// <summary>B-Form (minors) or CNIC number, digits only.</summary>
    public string? NationalId { get; set; }
    public string? Nationality { get; set; } = "Pakistani";
    public string? Religion { get; set; }
    public string? BloodGroup { get; set; }
    public string? MotherTongue { get; set; }
    public Guid? PhotoFileId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public Address Address { get; set; } = new();
    public string? PreviousSchoolName { get; set; }
    public string? PreviousSchoolClass { get; set; }
    public DateOnly AdmissionDate { get; set; }
    public StudentStatus Status { get; private set; } = StudentStatus.Active;
    public DateOnly? StatusChangedOn { get; private set; }
    /// <summary>Short alert shown to every teacher (e.g. "Severe nut allergy — EpiPen in office"). Full details are restricted.</summary>
    public string? MedicalAlert { get; set; }
    public Guid? UserId { get; set; }
    public Guid? CurrentSectionId { get; set; }
    public Guid? CurrentAcademicYearId { get; set; }
    public uint Version { get; set; }

    public ICollection<Enrollment> Enrollments { get; } = new List<Enrollment>();
    public ICollection<StudentGuardian> Guardians { get; } = new List<StudentGuardian>();
    public ICollection<EmergencyContact> EmergencyContacts { get; } = new List<EmergencyContact>();

    public bool IsCurrent => Status is StudentStatus.Active or StudentStatus.Suspended;

    public void Rename(string firstName, string? middleName, string lastName)
    {
        Guard.Against(string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName), "student.name_required",
            "First and last name are required.");
        FirstName = firstName.Trim();
        MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : middleName.Trim();
        LastName = lastName.Trim();
    }

    /// <summary>
    /// Moves the student to a new status and returns the history row. Leaving statuses end the active enrollment.
    /// </summary>
    public StudentStatusChange ChangeStatus(StudentStatus to, DateOnly effective, string? reason, string? destination, Guid? by)
    {
        Guard.Against(to == Status, "student.status_unchanged", $"The student is already {Describe(to)}.");
        Guard.Against(Status is StudentStatus.Alumni && to != StudentStatus.Alumni, "student.alumni_final",
            "Alumni records cannot be reactivated. Re-admit the student through admissions instead.");
        Guard.Against(to is StudentStatus.Alumni && Status != StudentStatus.Graduated, "student.alumni_requires_graduation",
            "Only graduated students become alumni.");
        Guard.Against(to is StudentStatus.Withdrawn or StudentStatus.TransferredOut && string.IsNullOrWhiteSpace(reason),
            "student.reason_required", "Record the reason the student is leaving.");

        var change = new StudentStatusChange(SchoolId, Id, Status, to, effective, reason, destination, by);
        if (to is StudentStatus.Withdrawn or StudentStatus.TransferredOut or StudentStatus.Graduated)
        {
            foreach (var enrollment in Enrollments.Where(e => e.Status == EnrollmentStatus.Active))
            {
                enrollment.Close(to switch
                {
                    StudentStatus.Graduated => EnrollmentStatus.Graduated,
                    StudentStatus.TransferredOut => EnrollmentStatus.Transferred,
                    _ => EnrollmentStatus.Withdrawn,
                }, effective, reason);
            }

            CurrentSectionId = null;
        }

        Status = to;
        StatusChangedOn = effective;
        return change;
    }

    public static string Describe(StudentStatus status) => status switch
    {
        StudentStatus.TransferredOut => "transferred out",
        _ => status.ToString().ToLowerInvariant(),
    };
}

public enum EnrollmentStatus { Active, Promoted, Retained, Transferred, Withdrawn, Graduated, Completed }

/// <summary>A student's placement in one section for one academic year. History is never rewritten.</summary>
public sealed class Enrollment : TenantEntity
{
    private Enrollment() { }

    public Enrollment(Guid schoolId, Guid studentId, Guid academicYearId, Guid campusId, Guid gradeId, Guid sectionId, DateOnly startDate,
        string? rollNumber = null)
    {
        SchoolId = schoolId;
        StudentId = studentId;
        AcademicYearId = academicYearId;
        CampusId = campusId;
        GradeId = gradeId;
        SectionId = sectionId;
        StartDate = startDate;
        RollNumber = rollNumber;
    }

    public Guid StudentId { get; private set; }
    public Guid AcademicYearId { get; private set; }
    public Guid CampusId { get; private set; }
    public Guid GradeId { get; private set; }
    public Guid SectionId { get; private set; }
    public string? RollNumber { get; set; }
    public EnrollmentStatus Status { get; private set; } = EnrollmentStatus.Active;
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public string? Reason { get; private set; }

    public void Close(EnrollmentStatus status, DateOnly endDate, string? reason)
    {
        Guard.Against(Status != EnrollmentStatus.Active, "enrollment.not_active", "Only an active enrollment can be closed.");
        Guard.Against(status == EnrollmentStatus.Active, "enrollment.close_active", "Choose how the enrollment ends.");
        Status = status;
        EndDate = endDate < StartDate ? StartDate : endDate;
        Reason = reason;
    }
}

public sealed class StudentStatusChange : TenantEntity
{
    private StudentStatusChange() { }

    internal StudentStatusChange(Guid schoolId, Guid studentId, StudentStatus from, StudentStatus to, DateOnly effectiveDate,
        string? reason, string? destinationSchool, Guid? changedBy)
    {
        SchoolId = schoolId;
        StudentId = studentId;
        FromStatus = from;
        ToStatus = to;
        EffectiveDate = effectiveDate;
        Reason = reason;
        DestinationSchool = destinationSchool;
        CreatedBy = changedBy;
    }

    public Guid StudentId { get; private set; }
    public StudentStatus FromStatus { get; private set; }
    public StudentStatus ToStatus { get; private set; }
    public DateOnly EffectiveDate { get; private set; }
    public string? Reason { get; private set; }
    public string? DestinationSchool { get; private set; }
}

/// <summary>Restricted health details (one per student). Teachers only ever see <see cref="Student.MedicalAlert"/>.</summary>
public sealed class StudentMedicalProfile : TenantEntity, IHasAuditTrail
{
    private StudentMedicalProfile() { }

    public StudentMedicalProfile(Guid schoolId, Guid studentId)
    {
        SchoolId = schoolId;
        StudentId = studentId;
    }

    public Guid StudentId { get; private set; }
    public string? Allergies { get; set; }
    public string? Conditions { get; set; }
    public string? Medications { get; set; }
    public string? SpecialNeeds { get; set; }
    public string? DoctorName { get; set; }
    public string? DoctorPhone { get; set; }
    public string? EmergencyInstructions { get; set; }
}

public sealed class EmergencyContact : TenantEntity
{
    private EmergencyContact() { }

    public EmergencyContact(Guid schoolId, Guid studentId, string name, string relationship, string phone, int priority)
    {
        SchoolId = schoolId;
        StudentId = studentId;
        Name = name.Trim();
        Relationship = relationship.Trim();
        Phone = phone.Trim();
        Priority = priority;
    }

    public Guid StudentId { get; private set; }
    public string Name { get; set; } = default!;
    public string Relationship { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string? AlternatePhone { get; set; }
    public int Priority { get; set; }
}
