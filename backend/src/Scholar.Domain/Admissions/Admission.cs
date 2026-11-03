using Scholar.Domain.Common;
using Scholar.Domain.Students;

namespace Scholar.Domain.Admissions;

public enum InquirySource { WalkIn, Phone, Website, Referral, SocialMedia, Event, Other }
public enum InquiryStatus { New, Contacted, ApplicationIssued, Converted, Closed }

/// <summary>A first contact from a family. Converted into an application when they apply.</summary>
public sealed class AdmissionInquiry : TenantEntity, IHasAuditTrail
{
    private AdmissionInquiry() { }

    public AdmissionInquiry(Guid schoolId, string studentName, string guardianName, string phone, InquirySource source)
    {
        SchoolId = schoolId;
        StudentName = studentName.Trim();
        GuardianName = guardianName.Trim();
        Phone = Guardian.NormalizePhone(phone);
        Source = source;
    }

    public Guid? CampusId { get; set; }
    public Guid? AcademicYearId { get; set; }
    public Guid? GradeId { get; set; }
    public string StudentName { get; set; } = default!;
    public DateOnly? DateOfBirth { get; set; }
    public string GuardianName { get; set; } = default!;
    public string Phone { get; private set; } = default!;
    public string? Email { get; set; }
    public InquirySource Source { get; set; }
    public InquiryStatus Status { get; private set; } = InquiryStatus.New;
    public DateOnly? FollowUpOn { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? Notes { get; set; }
    public string? ClosedReason { get; private set; }

    public void SetPhone(string phone) => Phone = Guardian.NormalizePhone(phone);

    public void MarkContacted() => Status = Status == InquiryStatus.New ? InquiryStatus.Contacted : Status;

    public void MarkConverted() => Status = InquiryStatus.Converted;

    public void Close(string reason)
    {
        Guard.Against(Status == InquiryStatus.Converted, "inquiry.converted", "This inquiry already became an application.");
        Status = InquiryStatus.Closed;
        ClosedReason = reason;
    }

    public void Reopen()
    {
        Guard.Against(Status != InquiryStatus.Closed, "inquiry.not_closed", "Only closed inquiries can be reopened.");
        Status = InquiryStatus.Contacted;
        ClosedReason = null;
    }
}

public enum ApplicationSource { Office, Online }

public enum ApplicationStatus
{
    Draft, Submitted, UnderReview, AssessmentScheduled, Assessed, Offered, Waitlisted, Accepted, Rejected, Enrolled, Withdrawn, Expired,
}

public sealed class AdmissionApplication : TenantEntity, IHasAuditTrail, IConcurrencyVersioned
{
    /// <summary>The lifecycle. Any move not listed here is refused.</summary>
    private static readonly Dictionary<ApplicationStatus, ApplicationStatus[]> Transitions = new()
    {
        [ApplicationStatus.Draft] = [ApplicationStatus.Submitted, ApplicationStatus.Withdrawn],
        [ApplicationStatus.Submitted] = [ApplicationStatus.UnderReview, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn],
        [ApplicationStatus.UnderReview] = [ApplicationStatus.AssessmentScheduled, ApplicationStatus.Offered, ApplicationStatus.Waitlisted,
            ApplicationStatus.Rejected, ApplicationStatus.Withdrawn],
        [ApplicationStatus.AssessmentScheduled] = [ApplicationStatus.Assessed, ApplicationStatus.Withdrawn, ApplicationStatus.Rejected],
        [ApplicationStatus.Assessed] = [ApplicationStatus.Offered, ApplicationStatus.Waitlisted, ApplicationStatus.Rejected,
            ApplicationStatus.AssessmentScheduled, ApplicationStatus.Withdrawn],
        [ApplicationStatus.Waitlisted] = [ApplicationStatus.Offered, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn],
        [ApplicationStatus.Offered] = [ApplicationStatus.Accepted, ApplicationStatus.Expired, ApplicationStatus.Withdrawn,
            ApplicationStatus.Enrolled],
        [ApplicationStatus.Accepted] = [ApplicationStatus.Enrolled, ApplicationStatus.Withdrawn],
        [ApplicationStatus.Expired] = [ApplicationStatus.Offered],
        [ApplicationStatus.Rejected] = [],
        [ApplicationStatus.Withdrawn] = [],
        [ApplicationStatus.Enrolled] = [],
    };

    private AdmissionApplication() { }

    public AdmissionApplication(Guid schoolId, string applicationNumber, Guid academicYearId, Guid campusId, Guid gradeId,
        ApplicationSource source)
    {
        SchoolId = schoolId;
        ApplicationNumber = applicationNumber;
        AcademicYearId = academicYearId;
        CampusId = campusId;
        GradeId = gradeId;
        Source = source;
    }

    public string ApplicationNumber { get; private set; } = default!;
    public Guid AcademicYearId { get; set; }
    public Guid CampusId { get; set; }
    public Guid GradeId { get; set; }
    public Guid? InquiryId { get; set; }
    public ApplicationSource Source { get; private set; }
    public ApplicationStatus Status { get; private set; } = ApplicationStatus.Draft;

    // Applicant
    public string FirstName { get; set; } = default!;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = default!;
    public Gender Gender { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string? NationalId { get; set; }
    public string? PreviousSchoolName { get; set; }
    public string? PreviousSchoolClass { get; set; }
    public string? PreviousResult { get; set; }
    public Address Address { get; set; } = new();

    // Primary guardian
    public string GuardianFirstName { get; set; } = default!;
    public string GuardianLastName { get; set; } = default!;
    public GuardianRelationship GuardianRelationship { get; set; } = GuardianRelationship.Father;
    public string GuardianPhone { get; set; } = default!;
    public string? GuardianEmail { get; set; }
    public string? GuardianNationalId { get; set; }
    public string? GuardianOccupation { get; set; }

    public string? Notes { get; set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public string? DecisionNote { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public int? WaitlistRank { get; private set; }
    public DateOnly? OfferExpiresOn { get; private set; }
    public Guid? StudentId { get; private set; }
    public uint Version { get; set; }

    public ICollection<ApplicationStatusChange> History { get; } = new List<ApplicationStatusChange>();
    public ICollection<ApplicationAssessment> Assessments { get; } = new List<ApplicationAssessment>();

    public string ApplicantName => string.Join(' ', new[] { FirstName, MiddleName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public bool IsOpen => Status is not (ApplicationStatus.Rejected or ApplicationStatus.Withdrawn or ApplicationStatus.Enrolled);

    public static IReadOnlyList<ApplicationStatus> AllowedFrom(ApplicationStatus status) => Transitions[status];

    public void Submit(DateTimeOffset now, Guid? by)
    {
        Move(ApplicationStatus.Submitted, null, by, now);
        SubmittedAt = now;
    }

    public void StartReview(Guid? by, DateTimeOffset now) => Move(ApplicationStatus.UnderReview, null, by, now);

    public ApplicationAssessment ScheduleAssessment(AssessmentType type, DateTimeOffset scheduledAt, string? location, Guid? by,
        DateTimeOffset now)
    {
        if (Status != ApplicationStatus.AssessmentScheduled)
        {
            Move(ApplicationStatus.AssessmentScheduled, $"{(type == AssessmentType.Interview ? "Interview" : "Entrance test")} scheduled", by, now);
        }

        var assessment = new ApplicationAssessment(SchoolId, Id, type, scheduledAt, location);
        Assessments.Add(assessment);
        return assessment;
    }

    public void RecordAssessment(Guid assessmentId, AssessmentOutcome outcome, decimal? score, decimal? maxScore, string? notes,
        Guid? by, DateTimeOffset now)
    {
        var assessment = Assessments.SingleOrDefault(a => a.Id == assessmentId)
            ?? throw new DomainException("assessment.not_found", "That assessment does not belong to this application.");
        Guard.Against(outcome == AssessmentOutcome.Pending, "assessment.outcome_required", "Record the outcome.");
        Guard.Against(score is not null && maxScore is not null && score > maxScore, "assessment.score_range",
            "The score cannot exceed the maximum.");
        assessment.Record(outcome, score, maxScore, notes);
        if (Assessments.All(a => a.Outcome != AssessmentOutcome.Pending) && Status == ApplicationStatus.AssessmentScheduled)
        {
            Move(ApplicationStatus.Assessed, null, by, now);
        }
    }

    public void Offer(DateOnly expiresOn, string? note, Guid by, DateTimeOffset now)
    {
        Guard.Against(expiresOn < DateOnly.FromDateTime(now.UtcDateTime), "application.offer_expiry_past", "The offer must expire in the future.");
        Move(ApplicationStatus.Offered, note, by, now);
        OfferExpiresOn = expiresOn;
        WaitlistRank = null;
        Decide(note, by, now);
    }

    public void Waitlist(int rank, string? note, Guid by, DateTimeOffset now)
    {
        Guard.Against(rank < 1, "application.waitlist_rank", "Waiting-list positions start at 1.");
        Move(ApplicationStatus.Waitlisted, note, by, now);
        WaitlistRank = rank;
        Decide(note, by, now);
    }

    public void Reject(string reason, Guid by, DateTimeOffset now)
    {
        Guard.Against(string.IsNullOrWhiteSpace(reason), "application.reason_required", "Record why the application was not successful.");
        Move(ApplicationStatus.Rejected, reason, by, now);
        WaitlistRank = null;
        Decide(reason, by, now);
    }

    public void Accept(Guid? by, DateTimeOffset now) => Move(ApplicationStatus.Accepted, "Offer accepted by family", by, now);

    public void Withdraw(string? reason, Guid? by, DateTimeOffset now) => Move(ApplicationStatus.Withdrawn, reason, by, now);

    public void Expire(DateTimeOffset now) => Move(ApplicationStatus.Expired, "Offer lapsed", null, now);

    public void MarkEnrolled(Guid studentId, Guid? by, DateTimeOffset now)
    {
        Move(ApplicationStatus.Enrolled, null, by, now);
        StudentId = studentId;
    }

    private void Decide(string? note, Guid by, DateTimeOffset now)
    {
        DecisionNote = note;
        DecidedBy = by;
        DecidedAt = now;
    }

    private void Move(ApplicationStatus to, string? note, Guid? by, DateTimeOffset now)
    {
        Guard.Against(!Transitions[Status].Contains(to), "application.invalid_transition",
            $"An application that is {Label(Status)} cannot become {Label(to)}.");
        History.Add(new ApplicationStatusChange(SchoolId, Id, Status, to, note, by, now));
        Status = to;
    }

    public static string Label(ApplicationStatus status) => status switch
    {
        ApplicationStatus.UnderReview => "under review",
        ApplicationStatus.AssessmentScheduled => "awaiting assessment",
        _ => status.ToString().ToLowerInvariant(),
    };
}

public sealed class ApplicationStatusChange : TenantEntity
{
    private ApplicationStatusChange() { }

    internal ApplicationStatusChange(Guid schoolId, Guid applicationId, ApplicationStatus from, ApplicationStatus to, string? note,
        Guid? changedBy, DateTimeOffset changedAt)
    {
        SchoolId = schoolId;
        ApplicationId = applicationId;
        FromStatus = from;
        ToStatus = to;
        Note = note;
        CreatedBy = changedBy;
        ChangedAt = changedAt;
    }

    public Guid ApplicationId { get; private set; }
    public ApplicationStatus FromStatus { get; private set; }
    public ApplicationStatus ToStatus { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }
}

public enum AssessmentType { EntranceTest, Interview }
public enum AssessmentOutcome { Pending, Passed, Failed, NoShow }

public sealed class ApplicationAssessment : TenantEntity
{
    private ApplicationAssessment() { }

    internal ApplicationAssessment(Guid schoolId, Guid applicationId, AssessmentType type, DateTimeOffset scheduledAt, string? location)
    {
        SchoolId = schoolId;
        ApplicationId = applicationId;
        Type = type;
        ScheduledAt = scheduledAt;
        Location = location;
    }

    public Guid ApplicationId { get; private set; }
    public AssessmentType Type { get; private set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public string? Location { get; set; }
    public AssessmentOutcome Outcome { get; private set; } = AssessmentOutcome.Pending;
    public decimal? Score { get; private set; }
    public decimal? MaxScore { get; private set; }
    public string? Notes { get; private set; }

    internal void Record(AssessmentOutcome outcome, decimal? score, decimal? maxScore, string? notes)
    {
        Outcome = outcome;
        Score = score;
        MaxScore = maxScore;
        Notes = notes;
    }
}
