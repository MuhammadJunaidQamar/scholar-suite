using Scholar.Domain.Common;

namespace Scholar.Domain.Organization;

public enum AcademicYearStatus { Planned, Active, Closed }

public sealed class AcademicYear : TenantEntity, IHasAuditTrail
{
    private AcademicYear() { }

    public AcademicYear(Guid schoolId, string name, DateOnly startDate, DateOnly endDate)
    {
        SchoolId = schoolId;
        Rename(name);
        SetDates(startDate, endDate);
    }

    public string Name { get; private set; } = default!;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public AcademicYearStatus Status { get; private set; } = AcademicYearStatus.Planned;
    public bool IsCurrent { get; private set; }

    public ICollection<AcademicTerm> Terms { get; } = new List<AcademicTerm>();

    public void Rename(string name) => Name = name.Trim();

    public void SetDates(DateOnly startDate, DateOnly endDate)
    {
        Guard.Against(endDate <= startDate, "academic_year.dates", "The academic year must end after it starts.");
        Guard.Against(endDate.DayNumber - startDate.DayNumber > 550, "academic_year.too_long",
            "An academic year cannot be longer than 18 months.");
        Guard.Against(Terms.Any(t => t.StartDate < startDate || t.EndDate > endDate), "academic_year.terms_outside",
            "All terms must fall inside the academic year.");
        StartDate = startDate;
        EndDate = endDate;
    }

    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;

    /// <summary>Marks this year as the school's current year. The caller clears the flag on the previous one.</summary>
    public void MakeCurrent()
    {
        Guard.Against(Status == AcademicYearStatus.Closed, "academic_year.closed", "A closed academic year cannot be made current.");
        IsCurrent = true;
        Status = AcademicYearStatus.Active;
    }

    public void ClearCurrent() => IsCurrent = false;

    public void Close()
    {
        Guard.Against(IsCurrent, "academic_year.close_current", "Make another year current before closing this one.");
        Status = AcademicYearStatus.Closed;
    }

    public AcademicTerm AddTerm(string name, DateOnly startDate, DateOnly endDate)
    {
        Guard.Against(endDate <= startDate, "term.dates", "A term must end after it starts.");
        Guard.Against(startDate < StartDate || endDate > EndDate, "term.outside_year", "The term must fall inside the academic year.");
        Guard.Against(Terms.Any(t => startDate <= t.EndDate && endDate >= t.StartDate), "term.overlap",
            "Terms cannot overlap.");
        var term = new AcademicTerm(SchoolId, Id, name, Terms.Count + 1, startDate, endDate);
        Terms.Add(term);
        Resequence();
        return term;
    }

    public void UpdateTerm(Guid termId, string name, DateOnly startDate, DateOnly endDate)
    {
        var term = Terms.SingleOrDefault(t => t.Id == termId)
            ?? throw new DomainException("term.not_found", "That term does not belong to this academic year.");
        Guard.Against(endDate <= startDate, "term.dates", "A term must end after it starts.");
        Guard.Against(startDate < StartDate || endDate > EndDate, "term.outside_year", "The term must fall inside the academic year.");
        Guard.Against(Terms.Any(t => t.Id != termId && startDate <= t.EndDate && endDate >= t.StartDate), "term.overlap",
            "Terms cannot overlap.");
        term.Name = name.Trim();
        term.StartDate = startDate;
        term.EndDate = endDate;
        Resequence();
    }

    public void RemoveTerm(Guid termId)
    {
        var term = Terms.SingleOrDefault(t => t.Id == termId)
            ?? throw new DomainException("term.not_found", "That term does not belong to this academic year.");
        Terms.Remove(term);
        Resequence();
    }

    /// <summary>Terms are numbered in date order so "Term 1" is always the earliest.</summary>
    private void Resequence()
    {
        var ordered = Terms.OrderBy(t => t.StartDate).ToList();
        for (var i = 0; i < ordered.Count; i++) ordered[i].Sequence = i + 1;
    }

    public AcademicTerm? TermOn(DateOnly date) => Terms.FirstOrDefault(t => date >= t.StartDate && date <= t.EndDate);
}

public sealed class AcademicTerm : TenantEntity
{
    private AcademicTerm() { }

    internal AcademicTerm(Guid schoolId, Guid academicYearId, string name, int sequence, DateOnly startDate, DateOnly endDate)
    {
        SchoolId = schoolId;
        AcademicYearId = academicYearId;
        Name = name.Trim();
        Sequence = sequence;
        StartDate = startDate;
        EndDate = endDate;
    }

    public Guid AcademicYearId { get; private set; }
    public string Name { get; set; } = default!;
    public int Sequence { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

public enum SchoolStage { EarlyYears, Primary, Middle, Secondary, HigherSecondary }

/// <summary>A level of study ("Class 5", "Grade 9"). Sections are created per grade per academic year.</summary>
public sealed class Grade : TenantEntity, IHasAuditTrail
{
    private Grade() { }

    public Grade(Guid schoolId, string name, string code, int levelOrder, SchoolStage stage)
    {
        SchoolId = schoolId;
        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        LevelOrder = levelOrder;
        Stage = stage;
    }

    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public int LevelOrder { get; set; }
    public SchoolStage Stage { get; set; }
    /// <summary>Where students go on promotion. Null for the final grade (students graduate).</summary>
    public Guid? NextGradeId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Section : TenantEntity, IHasAuditTrail
{
    private Section() { }

    public Section(Guid schoolId, Guid academicYearId, Guid campusId, Guid gradeId, string name, int capacity)
    {
        SchoolId = schoolId;
        AcademicYearId = academicYearId;
        CampusId = campusId;
        GradeId = gradeId;
        Name = name.Trim();
        Capacity = capacity;
    }

    public Guid AcademicYearId { get; private set; }
    public AcademicYear AcademicYear { get; private set; } = default!;
    public Guid CampusId { get; private set; }
    public Guid GradeId { get; private set; }
    public Grade Grade { get; private set; } = default!;
    public string Name { get; set; } = default!;
    public int Capacity { get; set; }
    public Guid? RoomId { get; set; }
    public Room? Room { get; private set; }
    public Guid? SubjectGroupId { get; set; }
    public bool IsActive { get; set; } = true;

    public string DisplayName => Grade is null ? Name : $"{Grade.Name} – {Name}";
}

public enum RoomType { Classroom, Laboratory, ComputerLab, Library, Hall, Sports, Office, Staffroom, Other }

public sealed class Room : TenantEntity, IHasAuditTrail
{
    private Room() { }

    public Room(Guid schoolId, Guid campusId, string code, string name, RoomType type, int capacity)
    {
        SchoolId = schoolId;
        CampusId = campusId;
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Type = type;
        Capacity = capacity;
    }

    public Guid CampusId { get; private set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public RoomType Type { get; set; }
    public string? Building { get; set; }
    public string? Floor { get; set; }
    public int Capacity { get; set; }
    public bool IsBookable { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Department : TenantEntity, IHasAuditTrail
{
    private Department() { }

    public Department(Guid schoolId, string code, string name)
    {
        SchoolId = schoolId;
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
    }

    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? HeadStaffId { get; set; }
}

public enum DesignationCategory { Teaching, NonTeaching, Administrative, Support }

public sealed class Designation : TenantEntity, IHasAuditTrail
{
    private Designation() { }

    public Designation(Guid schoolId, string name, DesignationCategory category)
    {
        SchoolId = schoolId;
        Name = name.Trim();
        Category = category;
    }

    public string Name { get; set; } = default!;
    public DesignationCategory Category { get; set; }
}
