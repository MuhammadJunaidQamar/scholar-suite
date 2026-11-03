using Scholar.Domain.Common;

namespace Scholar.Domain.Students;

public enum GuardianRelationship { Father, Mother, Guardian, Grandfather, Grandmother, Brother, Sister, Uncle, Aunt, Other }

/// <summary>
/// A parent or guardian. One guardian can be linked to several students (siblings share guardians, which is also how
/// siblings are recognised for discounts and in the family portal).
/// </summary>
public sealed class Guardian : TenantEntity, IHasAuditTrail
{
    private Guardian() { }

    public Guardian(Guid schoolId, string firstName, string lastName, string phone)
    {
        SchoolId = schoolId;
        Rename(firstName, lastName);
        Phone = NormalizePhone(phone);
    }

    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string FullName { get; private set; } = default!;
    public string? NationalId { get; set; }
    public string? Email { get; set; }
    public string Phone { get; private set; } = default!;
    public string? AlternatePhone { get; set; }
    public string? Occupation { get; set; }
    public string? Employer { get; set; }
    public Address Address { get; set; } = new();
    public string? PreferredLanguage { get; set; }
    public bool NotifyBySms { get; set; } = true;
    public bool NotifyByEmail { get; set; } = true;
    public bool NotifyByWhatsApp { get; set; }
    public Guid? UserId { get; set; }

    public ICollection<StudentGuardian> Students { get; } = new List<StudentGuardian>();

    public void Rename(string firstName, string lastName)
    {
        Guard.Against(string.IsNullOrWhiteSpace(firstName), "guardian.name_required", "Enter the guardian's name.");
        FirstName = firstName.Trim();
        LastName = string.IsNullOrWhiteSpace(lastName) ? string.Empty : lastName.Trim();
    }

    public void SetPhone(string phone) => Phone = NormalizePhone(phone);

    /// <summary>Normalises Pakistani mobile numbers to +92XXXXXXXXXX so the same parent is recognised however it is typed.</summary>
    public static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
        Guard.Against(digits.Count(char.IsDigit) < 7, "guardian.phone_invalid", "Enter a valid phone number.");
        if (digits.StartsWith("+", StringComparison.Ordinal)) return digits;
        if (digits.StartsWith("0092", StringComparison.Ordinal)) return "+" + digits[2..];
        if (digits.StartsWith("92", StringComparison.Ordinal) && digits.Length == 12) return "+" + digits;
        if (digits.StartsWith("03", StringComparison.Ordinal) && digits.Length == 11) return "+92" + digits[1..];
        return digits;
    }
}

/// <summary>The link between a student and a guardian, with what that guardian may see and do for this child.</summary>
public sealed class StudentGuardian : TenantEntity
{
    private StudentGuardian() { }

    public StudentGuardian(Guid schoolId, Guid studentId, Guid guardianId, GuardianRelationship relationship)
    {
        SchoolId = schoolId;
        StudentId = studentId;
        GuardianId = guardianId;
        Relationship = relationship;
    }

    public Guid StudentId { get; private set; }
    public Student Student { get; private set; } = default!;
    public Guid GuardianId { get; private set; }
    public Guardian Guardian { get; private set; } = default!;
    public GuardianRelationship Relationship { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsEmergencyContact { get; set; } = true;
    public bool LivesWithStudent { get; set; } = true;
    public bool CanPickUp { get; set; } = true;
    public bool IsFeePayer { get; set; }
    public bool PortalAccess { get; set; } = true;
    public bool CanViewAttendance { get; set; } = true;
    public bool CanViewResults { get; set; } = true;
    public bool CanViewFees { get; set; } = true;
    public bool ReceivesNotifications { get; set; } = true;
}
