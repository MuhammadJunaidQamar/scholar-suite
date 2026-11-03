using Scholar.Domain.Common;

namespace Scholar.Domain.Organization;

/// <summary>Stream/elective group (e.g. Pre-Medical, Pre-Engineering, Computer Science, Humanities).</summary>
public sealed class SubjectGroup : TenantEntity
{
    private SubjectGroup() { }

    public SubjectGroup(Guid schoolId, string name)
    {
        SchoolId = schoolId;
        Name = name.Trim();
    }

    public string Name { get; set; } = default!;
    public string? Description { get; set; }
}

public enum SubjectType { Core, Elective, Language, CoCurricular }

public sealed class Subject : TenantEntity, IHasAuditTrail
{
    private Subject() { }

    public Subject(Guid schoolId, string code, string name, SubjectType type)
    {
        SchoolId = schoolId;
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Type = type;
    }

    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? ShortName { get; set; }
    public SubjectType Type { get; set; }
    public Guid? SubjectGroupId { get; set; }
    /// <summary>Hex colour used to tint the subject in timetables.</summary>
    public string? Color { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Curriculum entry: a subject taught in a grade during an academic year.</summary>
public sealed class GradeSubject : TenantEntity
{
    private GradeSubject() { }

    public GradeSubject(Guid schoolId, Guid academicYearId, Guid gradeId, Guid subjectId, bool isMandatory, int periodsPerWeek)
    {
        SchoolId = schoolId;
        AcademicYearId = academicYearId;
        GradeId = gradeId;
        SubjectId = subjectId;
        IsMandatory = isMandatory;
        PeriodsPerWeek = periodsPerWeek;
    }

    public Guid AcademicYearId { get; private set; }
    public Guid GradeId { get; private set; }
    public Guid SubjectId { get; private set; }
    public Subject Subject { get; private set; } = default!;
    public bool IsMandatory { get; set; }
    public int PeriodsPerWeek { get; set; }
    /// <summary>When set, only sections following this stream take the subject.</summary>
    public Guid? SubjectGroupId { get; set; }
}

public enum CalendarEventType { Holiday, Event, Exam, ParentTeacherMeeting, Sports, SpecialDay, Meeting, TermDates }
public enum EventAudience { Everyone, Staff, Students, Guardians }

public sealed class CalendarEvent : TenantEntity, IHasAuditTrail
{
    private CalendarEvent() { }

    public CalendarEvent(Guid schoolId, string title, CalendarEventType type, DateOnly startDate, DateOnly endDate)
    {
        SchoolId = schoolId;
        Title = title.Trim();
        Type = type;
        SetDates(startDate, endDate);
        IsHoliday = type == CalendarEventType.Holiday;
    }

    public Guid? CampusId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public CalendarEventType Type { get; set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    /// <summary>Holidays close the school: no attendance is expected and the day is excluded from percentages.</summary>
    public bool IsHoliday { get; set; }
    public EventAudience Audience { get; set; } = EventAudience.Everyone;
    public string? Location { get; set; }
    public Guid? RoomId { get; set; }

    public void SetDates(DateOnly startDate, DateOnly endDate)
    {
        Guard.Against(endDate < startDate, "event.dates", "The event must end on or after its start date.");
        StartDate = startDate;
        EndDate = endDate;
    }
}

public enum GradingScaleType { Percentage, GradePoint }

public sealed class GradingScale : TenantEntity, IHasAuditTrail
{
    private GradingScale() { }

    public GradingScale(Guid schoolId, string name, GradingScaleType type)
    {
        SchoolId = schoolId;
        Name = name.Trim();
        Type = type;
    }

    public string Name { get; set; } = default!;
    public GradingScaleType Type { get; set; }
    public bool IsDefault { get; set; }
    public ICollection<GradingBand> Bands { get; } = new List<GradingBand>();

    public void ReplaceBands(IEnumerable<(decimal Min, decimal Max, string Grade, decimal? Point, string? Remark, bool Passing)> bands)
    {
        var ordered = bands.OrderBy(b => b.Min).ToList();
        Guard.Against(ordered.Count == 0, "grading.empty", "A grading scale needs at least one band.");
        for (var i = 0; i < ordered.Count; i++)
        {
            var band = ordered[i];
            Guard.Against(band.Min < 0 || band.Max > 100 || band.Min > band.Max, "grading.band_range",
                $"Band {band.Grade} must be within 0–100 with minimum ≤ maximum.");
            if (i > 0 && band.Min <= ordered[i - 1].Max)
            {
                throw new DomainException("grading.overlap", $"Band {band.Grade} overlaps band {ordered[i - 1].Grade}.");
            }
        }

        Bands.Clear();
        foreach (var band in ordered)
        {
            Bands.Add(new GradingBand(SchoolId, Id, band.Min, band.Max, band.Grade, band.Point, band.Remark, band.Passing));
        }
    }

    /// <summary>Finds the band for a percentage. Gaps between bands (e.g. 79.5) resolve to the band below.</summary>
    public GradingBand? Resolve(decimal percentage) =>
        Bands.Where(b => percentage >= b.MinPercent).MaxBy(b => b.MinPercent);
}

public sealed class GradingBand : TenantEntity
{
    private GradingBand() { }

    internal GradingBand(Guid schoolId, Guid gradingScaleId, decimal minPercent, decimal maxPercent, string grade,
        decimal? gradePoint, string? remark, bool isPassing)
    {
        SchoolId = schoolId;
        GradingScaleId = gradingScaleId;
        MinPercent = minPercent;
        MaxPercent = maxPercent;
        Grade = grade.Trim();
        GradePoint = gradePoint;
        Remark = remark;
        IsPassing = isPassing;
    }

    public Guid GradingScaleId { get; private set; }
    public decimal MinPercent { get; private set; }
    public decimal MaxPercent { get; private set; }
    public string Grade { get; private set; } = default!;
    public decimal? GradePoint { get; private set; }
    public string? Remark { get; private set; }
    public bool IsPassing { get; private set; }
}
