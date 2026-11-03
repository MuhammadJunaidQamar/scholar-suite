using Scholar.Domain.Common;
using Scholar.Domain.Students;

namespace Scholar.Domain.Staffing;

public enum StaffType { Teaching, NonTeaching, Administrative, Support }

public enum EmploymentType { Permanent, Contract, Probation, PartTime, Visiting }

public enum StaffStatus { Active, OnLeave, Suspended, Resigned, Terminated, Retired }

/// <summary>
/// A person employed by the school: teachers, office staff, drivers, guards. A staff record exists whether or not the
/// person signs in; when they do, <see cref="UserId"/> links the record to their account (teachers taking registers,
/// staff requesting leave). HR details (contracts, salary) attach to this record in the HR module.
/// </summary>
public sealed class StaffMember : TenantEntity, IHasAuditTrail, IConcurrencyVersioned
{
    private StaffMember() { }

    public StaffMember(Guid schoolId, Guid campusId, string employeeNumber, string firstName, string lastName, Gender gender,
        StaffType staffType, DateOnly joinDate)
    {
        SchoolId = schoolId;
        CampusId = campusId;
        EmployeeNumber = employeeNumber.Trim();
        Rename(firstName, lastName);
        Gender = gender;
        StaffType = staffType;
        JoinDate = joinDate;
    }

    public Guid CampusId { get; set; }
    public string EmployeeNumber { get; private set; } = default!;
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    /// <summary>Database-computed "first last", indexed for fuzzy search.</summary>
    public string FullName { get; private set; } = default!;
    public Gender Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    /// <summary>CNIC, digits only.</summary>
    public string? NationalId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public Address Address { get; set; } = new();
    public Guid? PhotoFileId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? DesignationId { get; set; }
    public StaffType StaffType { get; set; }
    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;
    public DateOnly JoinDate { get; set; }
    public StaffStatus Status { get; private set; } = StaffStatus.Active;
    public DateOnly? ExitDate { get; private set; }
    public string? ExitReason { get; private set; }
    public Guid? UserId { get; set; }
    public uint Version { get; set; }

    /// <summary>Still with the school (on leave and suspended staff keep their record and assignments).</summary>
    public bool IsCurrent => Status is StaffStatus.Active or StaffStatus.OnLeave or StaffStatus.Suspended;

    public void Rename(string firstName, string lastName)
    {
        Guard.Against(string.IsNullOrWhiteSpace(firstName), "staff.name_required", "Enter the first name.");
        FirstName = firstName.Trim();
        LastName = (lastName ?? string.Empty).Trim();
    }

    public void ChangeStatus(StaffStatus to, DateOnly effective, string? reason)
    {
        Guard.Against(to == Status, "staff.status_unchanged", "The staff member already has that status.");
        var leaving = to is StaffStatus.Resigned or StaffStatus.Terminated or StaffStatus.Retired;
        Guard.Against(leaving && string.IsNullOrWhiteSpace(reason), "staff.reason_required", "Record why the person is leaving.");
        Status = to;
        if (leaving)
        {
            ExitDate = effective;
            ExitReason = reason!.Trim();
        }
        else
        {
            ExitDate = null;
            ExitReason = null;
        }
    }
}

/// <summary>Who teaches which subject to a section (used for period registers now, and the timetable later).</summary>
public sealed class SectionSubjectTeacher : TenantEntity
{
    private SectionSubjectTeacher() { }

    public SectionSubjectTeacher(Guid schoolId, Guid sectionId, Guid subjectId, Guid staffId)
    {
        SchoolId = schoolId;
        SectionId = sectionId;
        SubjectId = subjectId;
        StaffId = staffId;
    }

    public Guid SectionId { get; private set; }
    public Guid SubjectId { get; private set; }
    public Guid StaffId { get; set; }
}
