using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Application.Organization;
using Scholar.Application.Students;
using Scholar.Domain.Access;
using Scholar.Domain.Admissions;
using Scholar.Domain.Common;
using Scholar.Domain.Platform;
using Scholar.Domain.Students;

namespace Scholar.Application.Admissions;

// ───────────────────────────── Contracts ─────────────────────────────

public sealed record InquiryQuery : PageQuery
{
    public InquiryStatus? Status { get; init; }
    public bool? FollowUpDue { get; init; }
    public Guid? GradeId { get; init; }
}

public sealed record InquiryDto(Guid Id, string StudentName, DateOnly? DateOfBirth, string GuardianName, string Phone, string? Email,
    InquirySource Source, InquiryStatus Status, Guid? CampusId, string? CampusName, Guid? GradeId, string? GradeName,
    DateOnly? FollowUpOn, string? Notes, string? ClosedReason, DateTimeOffset CreatedAt, Guid? ApplicationId);

public sealed record SaveInquiryRequest(string StudentName, DateOnly? DateOfBirth, string GuardianName, string Phone, string? Email,
    InquirySource Source, Guid? CampusId, Guid? GradeId, DateOnly? FollowUpOn, string? Notes);

public sealed record ApplicationQuery : PageQuery
{
    public ApplicationStatus? Status { get; init; }
    public Guid? GradeId { get; init; }
    public Guid? CampusId { get; init; }
    public Guid? AcademicYearId { get; init; }
    public ApplicationSource? Source { get; init; }
    /// <summary>"open" (default) or "all".</summary>
    public string? Scope { get; init; }
}

public sealed record ApplicationListItemDto(Guid Id, string ApplicationNumber, string ApplicantName, Gender Gender, DateOnly DateOfBirth,
    Guid GradeId, string GradeName, string CampusName, string GuardianName, string GuardianPhone, ApplicationStatus Status,
    ApplicationSource Source, DateTimeOffset? SubmittedAt, int? WaitlistRank, DateOnly? OfferExpiresOn, DateTimeOffset UpdatedAt);

public sealed record PipelineStageDto(ApplicationStatus Status, int Count);

public sealed record AssessmentDto(Guid Id, AssessmentType Type, DateTimeOffset ScheduledAt, string? Location, AssessmentOutcome Outcome,
    decimal? Score, decimal? MaxScore, string? Notes);

public sealed record ApplicationHistoryDto(ApplicationStatus FromStatus, ApplicationStatus ToStatus, string? Note, string? ChangedByName,
    DateTimeOffset ChangedAt);

public sealed record ApplicationDetailDto(
    Guid Id, string ApplicationNumber, ApplicationStatus Status, ApplicationSource Source, Guid AcademicYearId, string AcademicYearName,
    Guid CampusId, string CampusName, Guid GradeId, string GradeName, Guid? InquiryId,
    string FirstName, string? MiddleName, string LastName, string ApplicantName, Gender Gender, DateOnly DateOfBirth, string? NationalId,
    string? PreviousSchoolName, string? PreviousSchoolClass, string? PreviousResult, AddressDto Address,
    string GuardianFirstName, string GuardianLastName, GuardianRelationship GuardianRelationship, string GuardianPhone,
    string? GuardianEmail, string? GuardianNationalId, string? GuardianOccupation, string? Notes,
    DateTimeOffset? SubmittedAt, string? DecisionNote, DateTimeOffset? DecidedAt, int? WaitlistRank, DateOnly? OfferExpiresOn,
    Guid? StudentId, IReadOnlyList<ApplicationStatus> AllowedTransitions, IReadOnlyList<AssessmentDto> Assessments,
    IReadOnlyList<ApplicationHistoryDto> History, uint Version);

public sealed record ApplicantInput(
    string FirstName, string? MiddleName, string LastName, Gender Gender, DateOnly DateOfBirth, string? NationalId,
    string? PreviousSchoolName, string? PreviousSchoolClass, string? PreviousResult, AddressDto? Address,
    string GuardianFirstName, string GuardianLastName, GuardianRelationship GuardianRelationship, string GuardianPhone,
    string? GuardianEmail, string? GuardianNationalId, string? GuardianOccupation, string? Notes);

public sealed record CreateApplicationRequest(Guid AcademicYearId, Guid CampusId, Guid GradeId, ApplicantInput Applicant, Guid? InquiryId,
    bool Submit = true);

public sealed record UpdateApplicationRequest(Guid CampusId, Guid GradeId, ApplicantInput Applicant, uint Version);

public sealed record ScheduleAssessmentRequest(AssessmentType Type, DateTimeOffset ScheduledAt, string? Location);
public sealed record RecordAssessmentRequest(AssessmentOutcome Outcome, decimal? Score, decimal? MaxScore, string? Notes);
public sealed record DecisionRequest(string Decision, string? Note, DateOnly? OfferExpiresOn, int? WaitlistRank);
public sealed record EnrollApplicantRequest(Guid SectionId, DateOnly AdmissionDate, string? RollNumber, bool OverrideCapacity = false);

public sealed class ApplicantInputValidator : AbstractValidator<ApplicantInput>
{
    public ApplicantInputValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DateOfBirth).Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-2))
            .WithMessage("Applicants must be at least 2 years old.");
        RuleFor(x => x.NationalId).Must(v => string.IsNullOrWhiteSpace(v) || v.Count(char.IsDigit) == 13)
            .WithMessage("B-Form numbers have 13 digits.");
        RuleFor(x => x.GuardianFirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.GuardianPhone).NotEmpty().Must(p => p.Count(char.IsDigit) >= 10).WithMessage("Enter a mobile number, e.g. 0300 1234567.");
        RuleFor(x => x.GuardianEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.GuardianEmail));
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class CreateApplicationValidator : AbstractValidator<CreateApplicationRequest>
{
    public CreateApplicationValidator() => RuleFor(x => x.Applicant).NotNull().SetValidator(new ApplicantInputValidator());
}

public sealed class UpdateApplicationValidator : AbstractValidator<UpdateApplicationRequest>
{
    public UpdateApplicationValidator() => RuleFor(x => x.Applicant).NotNull().SetValidator(new ApplicantInputValidator());
}

public sealed class SaveInquiryValidator : AbstractValidator<SaveInquiryRequest>
{
    public SaveInquiryValidator()
    {
        RuleFor(x => x.StudentName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GuardianName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().Must(p => p.Count(char.IsDigit) >= 10).WithMessage("Enter a mobile number.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

// ───────────────────────────── Service ─────────────────────────────

public sealed class AdmissionService(
    IAppDbContext db,
    IAccessContext access,
    ISequenceGenerator sequences,
    StudentService students,
    ISchoolClock schoolClock,
    TimeProvider clock)
{
    private static readonly ApplicationStatus[] Closed = [ApplicationStatus.Rejected, ApplicationStatus.Withdrawn, ApplicationStatus.Enrolled, ApplicationStatus.Expired];

    // Inquiries

    public async Task<PagedResult<InquiryDto>> ListInquiriesAsync(InquiryQuery query, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Admissions.View, ct);
        var today = await schoolClock.TodayAsync(ct);
        var inquiries = db.AdmissionInquiries
            .WhereIf(query.Status is not null, i => i.Status == query.Status)
            .WhereIf(query.Status is null, i => i.Status != InquiryStatus.Closed && i.Status != InquiryStatus.Converted)
            .WhereIf(query.FollowUpDue == true, i => i.FollowUpOn != null && i.FollowUpOn <= today)
            .WhereIf(query.GradeId is not null, i => i.GradeId == query.GradeId);
        if (query.Term is { } term)
        {
            var pattern = $"%{term}%";
            var digits = new string(term.Where(char.IsDigit).ToArray());
            var phoneTail = digits.Length >= 10 ? digits[^10..] : digits;
            inquiries = inquiries.Where(i => EF.Functions.ILike(i.StudentName, pattern) || EF.Functions.ILike(i.GuardianName, pattern)
                                             || (digits.Length >= 4 && i.Phone.Contains(phoneTail)));
        }

        var sorted = inquiries.ApplySort(query, new SortMap<AdmissionInquiry>
        {
            ["created"] = i => i.CreatedAt, ["followUp"] = i => i.FollowUpOn, ["student"] = i => i.StudentName,
        }, "created", defaultDescending: true);
        return await sorted.ToPagedAsync(query, ProjectInquiry(), ct);
    }

    public async Task<InquiryDto> SaveInquiryAsync(Guid? id, SaveInquiryRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Admissions.Create, ct);
        var current = await access.GetAsync(ct);
        AdmissionInquiry inquiry;
        if (id is null)
        {
            inquiry = new AdmissionInquiry(current.SchoolId, request.StudentName, request.GuardianName, request.Phone, request.Source)
            {
                AcademicYearId = await CurrentOrNextYearAsync(ct),
            };
            db.AdmissionInquiries.Add(inquiry);
        }
        else
        {
            inquiry = await db.AdmissionInquiries.SingleOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException("Inquiry", id);
            inquiry.StudentName = request.StudentName.Trim();
            inquiry.GuardianName = request.GuardianName.Trim();
            inquiry.SetPhone(request.Phone);
            inquiry.Source = request.Source;
        }

        inquiry.DateOfBirth = request.DateOfBirth;
        inquiry.Email = StudentService.Clean(request.Email);
        inquiry.CampusId = request.CampusId;
        inquiry.GradeId = request.GradeId;
        inquiry.FollowUpOn = request.FollowUpOn;
        inquiry.Notes = StudentService.Clean(request.Notes);
        await db.SaveChangesAsync(ct);
        return await db.AdmissionInquiries.Where(i => i.Id == inquiry.Id).Select(ProjectInquiry()).SingleAsync(ct);
    }

    public async Task<InquiryDto> SetInquiryStateAsync(Guid id, string action, string? reason, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Admissions.Update, ct);
        var inquiry = await db.AdmissionInquiries.SingleOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException("Inquiry", id);
        switch (action)
        {
            case "contacted": inquiry.MarkContacted(); break;
            case "close": inquiry.Close(string.IsNullOrWhiteSpace(reason) ? "Not interested" : reason.Trim()); break;
            case "reopen": inquiry.Reopen(); break;
            default: throw new DomainException("inquiry.unknown_action", "Unknown action.");
        }

        await db.SaveChangesAsync(ct);
        return await db.AdmissionInquiries.Where(i => i.Id == id).Select(ProjectInquiry()).SingleAsync(ct);
    }

    private System.Linq.Expressions.Expression<Func<AdmissionInquiry, InquiryDto>> ProjectInquiry() => i => new InquiryDto(i.Id,
        i.StudentName, i.DateOfBirth, i.GuardianName, i.Phone, i.Email, i.Source, i.Status, i.CampusId,
        db.Campuses.Where(c => c.Id == i.CampusId).Select(c => c.Name).FirstOrDefault(), i.GradeId,
        db.Grades.Where(g => g.Id == i.GradeId).Select(g => g.Name).FirstOrDefault(), i.FollowUpOn, i.Notes, i.ClosedReason, i.CreatedAt,
        db.AdmissionApplications.Where(a => a.InquiryId == i.Id).Select(a => (Guid?)a.Id).FirstOrDefault());

    // Applications

    public async Task<PagedResult<ApplicationListItemDto>> ListApplicationsAsync(ApplicationQuery query, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Admissions.View, ct);
        var current = await access.GetAsync(ct);
        var apps = db.AdmissionApplications.AsQueryable();
        if (!current.AllCampuses) apps = apps.Where(a => current.CampusIds!.Contains(a.CampusId));
        apps = apps
            .WhereIf(query.Status is not null, a => a.Status == query.Status)
            .WhereIf(query.Status is null && query.Scope != "all", a => !Closed.Contains(a.Status))
            .WhereIf(query.GradeId is not null, a => a.GradeId == query.GradeId)
            .WhereIf(query.CampusId is not null, a => a.CampusId == query.CampusId)
            .WhereIf(query.AcademicYearId is not null, a => a.AcademicYearId == query.AcademicYearId)
            .WhereIf(query.Source is not null, a => a.Source == query.Source);
        if (query.Term is { } term)
        {
            var pattern = $"%{term}%";
            var digits = new string(term.Where(char.IsDigit).ToArray());
            var phoneTail = digits.Length >= 10 ? digits[^10..] : digits;
            apps = apps.Where(a => EF.Functions.ILike(a.FirstName + " " + a.LastName, pattern) || EF.Functions.ILike(a.ApplicationNumber, term + "%")
                                   || EF.Functions.ILike(a.GuardianFirstName + " " + a.GuardianLastName, pattern)
                                   || (digits.Length >= 4 && a.GuardianPhone.Contains(phoneTail)));
        }

        var sorted = apps.ApplySort(query, new SortMap<AdmissionApplication>
        {
            ["submitted"] = a => a.SubmittedAt, ["name"] = a => a.FirstName + " " + a.LastName, ["number"] = a => a.ApplicationNumber,
            ["grade"] = a => db.Grades.Where(g => g.Id == a.GradeId).Select(g => g.LevelOrder).FirstOrDefault(),
            ["status"] = a => a.Status, ["updated"] = a => a.UpdatedAt ?? a.CreatedAt,
        }, "updated", defaultDescending: true);

        return await sorted.ToPagedAsync(query, a => new ApplicationListItemDto(a.Id, a.ApplicationNumber,
            a.FirstName + " " + a.LastName, a.Gender, a.DateOfBirth, a.GradeId,
            db.Grades.Where(g => g.Id == a.GradeId).Select(g => g.Name).First(),
            db.Campuses.Where(c => c.Id == a.CampusId).Select(c => c.Name).First(),
            a.GuardianFirstName + " " + a.GuardianLastName, a.GuardianPhone, a.Status, a.Source, a.SubmittedAt, a.WaitlistRank,
            a.OfferExpiresOn, a.UpdatedAt ?? a.CreatedAt), ct);
    }

    public async Task<IReadOnlyList<PipelineStageDto>> PipelineAsync(Guid? academicYearId, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Admissions.View, ct);
        var current = await access.GetAsync(ct);
        var apps = db.AdmissionApplications.WhereIf(academicYearId is not null, a => a.AcademicYearId == academicYearId);
        if (!current.AllCampuses) apps = apps.Where(a => current.CampusIds!.Contains(a.CampusId));
        var counts = await apps.GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        return Enum.GetValues<ApplicationStatus>().Select(s => new PipelineStageDto(s, counts.FirstOrDefault(c => c.Key == s)?.Count ?? 0)).ToList();
    }

    public async Task<ApplicationDetailDto> GetApplicationAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Admissions.View, ct);
        var a = await LoadAsync(id, ct);
        var names = await db.AcademicYears.Where(y => y.Id == a.AcademicYearId).Select(y => y.Name).SingleAsync(ct);
        var campus = await db.Campuses.Where(c => c.Id == a.CampusId).Select(c => c.Name).SingleAsync(ct);
        var grade = await db.Grades.Where(g => g.Id == a.GradeId).Select(g => g.Name).SingleAsync(ct);
        var userIds = a.History.Select(h => h.CreatedBy).Where(u => u != null).Distinct().ToList();
        var people = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName, ct);

        return new ApplicationDetailDto(a.Id, a.ApplicationNumber, a.Status, a.Source, a.AcademicYearId, names, a.CampusId, campus, a.GradeId,
            grade, a.InquiryId, a.FirstName, a.MiddleName, a.LastName, a.ApplicantName, a.Gender, a.DateOfBirth, a.NationalId,
            a.PreviousSchoolName, a.PreviousSchoolClass, a.PreviousResult, AddressDto.From(a.Address), a.GuardianFirstName,
            a.GuardianLastName, a.GuardianRelationship, a.GuardianPhone, a.GuardianEmail, a.GuardianNationalId, a.GuardianOccupation,
            a.Notes, a.SubmittedAt, a.DecisionNote, a.DecidedAt, a.WaitlistRank, a.OfferExpiresOn, a.StudentId,
            AdmissionApplication.AllowedFrom(a.Status),
            a.Assessments.OrderBy(x => x.ScheduledAt).Select(x => new AssessmentDto(x.Id, x.Type, x.ScheduledAt, x.Location, x.Outcome,
                x.Score, x.MaxScore, x.Notes)).ToList(),
            a.History.OrderByDescending(h => h.ChangedAt).Select(h => new ApplicationHistoryDto(h.FromStatus, h.ToStatus, h.Note,
                h.CreatedBy is { } by && people.TryGetValue(by, out var n) ? n : null, h.ChangedAt)).ToList(),
            a.Version);
    }

    public async Task<ApplicationDetailDto> CreateApplicationAsync(CreateApplicationRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Admissions.Create, ct);
        var current = await access.GetAsync(ct);
        Guard.Against(!current.CanAccessCampus(request.CampusId), "campus.out_of_scope", "You can only record applications for your campuses.");
        var id = await db.InTransactionAsync(async innerCt =>
        {
            var application = await NewApplicationAsync(current.SchoolId, request.AcademicYearId, request.CampusId, request.GradeId,
                ApplicationSource.Office, request.Applicant, innerCt);
            if (request.InquiryId is { } inquiryId)
            {
                var inquiry = await db.AdmissionInquiries.SingleOrDefaultAsync(i => i.Id == inquiryId, innerCt) ?? throw new NotFoundException("Inquiry", inquiryId);
                inquiry.MarkConverted();
                application.InquiryId = inquiryId;
            }

            if (request.Submit) application.Submit(clock.GetUtcNow(), current.UserId);
            await db.SaveChangesAsync(innerCt);
            return application.Id;
        }, ct: ct);
        return await GetApplicationAsync(id, ct);
    }

    /// <summary>Shared by office entry and the public online form.</summary>
    internal async Task<AdmissionApplication> NewApplicationAsync(Guid schoolId, Guid academicYearId, Guid campusId, Guid gradeId,
        ApplicationSource source, ApplicantInput applicant, CancellationToken ct)
    {
        var year = await db.AcademicYears.SingleOrDefaultAsync(y => y.Id == academicYearId, ct) ?? throw new NotFoundException("Academic year", academicYearId);
        Guard.Against(year.Status == Domain.Organization.AcademicYearStatus.Closed, "academic_year.closed", "That academic year is closed.");
        if (!await db.Campuses.AnyAsync(c => c.Id == campusId && c.IsActive, ct)) throw new NotFoundException("Campus", campusId);
        if (!await db.Grades.AnyAsync(g => g.Id == gradeId && g.IsActive, ct)) throw new NotFoundException("Grade", gradeId);

        var number = await sequences.NextAsync(SequenceKeys.Application, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), ct);
        var application = new AdmissionApplication(schoolId, number, academicYearId, campusId, gradeId, source);
        ApplyApplicant(application, applicant);
        db.AdmissionApplications.Add(application);
        return application;
    }

    public async Task<ApplicationDetailDto> UpdateApplicationAsync(Guid id, UpdateApplicationRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Admissions.Update, ct);
        var application = await LoadAsync(id, ct);
        Guard.Against(!application.IsOpen, "application.closed", "Closed applications cannot be edited.");
        db.Entry(application).Property(a => a.Version).OriginalValue = request.Version;
        if (!await db.Grades.AnyAsync(g => g.Id == request.GradeId, ct)) throw new NotFoundException("Grade", request.GradeId);
        if (!await db.Campuses.AnyAsync(c => c.Id == request.CampusId, ct)) throw new NotFoundException("Campus", request.CampusId);
        application.CampusId = request.CampusId;
        application.GradeId = request.GradeId;
        ApplyApplicant(application, request.Applicant);
        await db.SaveChangesAsync(ct);
        return await GetApplicationAsync(id, ct);
    }

    public async Task<ApplicationDetailDto> TransitionAsync(Guid id, string action, string? note, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var application = await LoadAsync(id, ct);
        var now = clock.GetUtcNow();
        switch (action)
        {
            case "submit":
                await access.RequireAsync(Permissions.Admissions.Update, ct);
                application.Submit(now, current.UserId);
                break;
            case "review":
                await access.RequireAsync(Permissions.Admissions.Review, ct);
                application.StartReview(current.UserId, now);
                break;
            case "accept":
                await access.RequireAsync(Permissions.Admissions.Update, ct);
                application.Accept(current.UserId, now);
                break;
            case "withdraw":
                await access.RequireAsync(Permissions.Admissions.Update, ct);
                application.Withdraw(StudentService.Clean(note) ?? "Withdrawn by family", current.UserId, now);
                break;
            default:
                throw new DomainException("application.unknown_action", "Unknown action.");
        }

        await db.SaveChangesAsync(ct);
        return await GetApplicationAsync(id, ct);
    }

    public async Task<ApplicationDetailDto> DecideAsync(Guid id, DecisionRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Admissions.Decide, ct);
        var current = await access.GetAsync(ct);
        var application = await LoadAsync(id, ct);
        var now = clock.GetUtcNow();
        var settings = await db.Schools.Where(s => s.Id == current.SchoolId).Select(s => s.Settings).SingleAsync(ct);
        switch (request.Decision)
        {
            case "offer":
                var today = await schoolClock.TodayAsync(ct);
                application.Offer(request.OfferExpiresOn ?? today.AddDays(settings.Admissions.OfferValidityDays), StudentService.Clean(request.Note),
                    current.UserId, now);
                break;
            case "waitlist":
                var rank = request.WaitlistRank ?? (await db.AdmissionApplications.Where(a => a.Status == ApplicationStatus.Waitlisted
                    && a.GradeId == application.GradeId && a.AcademicYearId == application.AcademicYearId).MaxAsync(a => (int?)a.WaitlistRank, ct) ?? 0) + 1;
                application.Waitlist(rank, StudentService.Clean(request.Note), current.UserId, now);
                break;
            case "reject":
                application.Reject(request.Note ?? string.Empty, current.UserId, now);
                break;
            default:
                throw new DomainException("application.unknown_decision", "Choose offer, waitlist or reject.");
        }

        await db.SaveChangesAsync(ct);
        return await GetApplicationAsync(id, ct);
    }

    public async Task<ApplicationDetailDto> ScheduleAssessmentAsync(Guid id, ScheduleAssessmentRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Admissions.Review, ct);
        var current = await access.GetAsync(ct);
        var application = await LoadAsync(id, ct);
        if (application.Status == ApplicationStatus.Submitted) application.StartReview(current.UserId, clock.GetUtcNow());
        application.ScheduleAssessment(request.Type, request.ScheduledAt, StudentService.Clean(request.Location), current.UserId, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await GetApplicationAsync(id, ct);
    }

    public async Task<ApplicationDetailDto> RecordAssessmentAsync(Guid id, Guid assessmentId, RecordAssessmentRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Admissions.Review, ct);
        var current = await access.GetAsync(ct);
        var application = await LoadAsync(id, ct);
        application.RecordAssessment(assessmentId, request.Outcome, request.Score, request.MaxScore, StudentService.Clean(request.Notes),
            current.UserId, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await GetApplicationAsync(id, ct);
    }

    /// <summary>
    /// Turns an accepted (or offered) applicant into a student: creates the student record with an admission number,
    /// the guardian (reusing an existing parent with the same mobile number and name), the placement and the link — one
    /// transaction. The application keeps a pointer to the new student and becomes read-only.
    /// </summary>
    public async Task<ApplicationDetailDto> EnrollAsync(Guid id, EnrollApplicantRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Admissions.Enroll, ct);
        await access.RequireAsync(Permissions.Students.Create, ct);
        var current = await access.GetAsync(ct);
        await db.InTransactionAsync(async innerCt =>
        {
            var application = await LoadAsync(id, innerCt);
            Guard.Against(application.Status is not (ApplicationStatus.Accepted or ApplicationStatus.Offered), "application.not_enrollable",
                "Only applicants who were offered a place can be enrolled.");
            var section = await db.Sections.SingleOrDefaultAsync(s => s.Id == request.SectionId, innerCt) ?? throw new NotFoundException("Section", request.SectionId);
            Guard.Against(section.GradeId != application.GradeId, "application.section_grade",
                "Choose a section of the grade the applicant applied for (or change the application's grade first).");

            var student = await students.CreateCoreAsync(new CreateStudentRequest(
                new StudentDetailsInput(application.FirstName, application.MiddleName, application.LastName, application.Gender,
                    application.DateOfBirth, null, application.NationalId, "Pakistani", null, null, null, null, null,
                    AddressDto.From(application.Address), application.PreviousSchoolName, application.PreviousSchoolClass, null, null),
                new PlacementInput(request.SectionId, request.AdmissionDate, request.RollNumber),
                [new GuardianLinkInput(null, new GuardianInput(application.GuardianFirstName, application.GuardianLastName, application.GuardianPhone,
                    application.GuardianEmail, application.GuardianNationalId, application.GuardianOccupation, null, null, AddressDto.From(application.Address)),
                    application.GuardianRelationship, IsPrimary: true, IsFeePayer: true)],
                null, null, request.OverrideCapacity), innerCt);
            await db.SaveChangesAsync(innerCt);

            // Documents collected during admission follow the student.
            var files = await db.StoredFiles.Where(f => f.OwnerType == Domain.Documents.FileOwners.Application && f.OwnerId == id).ToListAsync(innerCt);
            foreach (var file in files)
            {
                file.MoveTo(Domain.Documents.FileOwners.Student, student.Id);
            }

            application.MarkEnrolled(student.Id, current.UserId, clock.GetUtcNow());
            await db.SaveChangesAsync(innerCt);
            return true;
        }, ct: ct);
        return await GetApplicationAsync(id, ct);
    }

    private async Task<AdmissionApplication> LoadAsync(Guid id, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var application = await db.AdmissionApplications.Include(a => a.History).Include(a => a.Assessments)
            .SingleOrDefaultAsync(a => a.Id == id, ct);
        if (application is null || !current.CanAccessCampus(application.CampusId)) throw new NotFoundException("Application", id);
        return application;
    }

    private async Task<Guid?> CurrentOrNextYearAsync(CancellationToken ct) =>
        await db.AcademicYears.Where(y => y.Status != Domain.Organization.AcademicYearStatus.Closed)
            .OrderByDescending(y => y.IsCurrent).ThenBy(y => y.StartDate).Select(y => (Guid?)y.Id).FirstOrDefaultAsync(ct);

    internal static void ApplyApplicant(AdmissionApplication a, ApplicantInput i)
    {
        a.FirstName = i.FirstName.Trim();
        a.MiddleName = StudentService.Clean(i.MiddleName);
        a.LastName = i.LastName.Trim();
        a.Gender = i.Gender;
        a.DateOfBirth = i.DateOfBirth;
        a.NationalId = StudentService.Digits(i.NationalId);
        a.PreviousSchoolName = StudentService.Clean(i.PreviousSchoolName);
        a.PreviousSchoolClass = StudentService.Clean(i.PreviousSchoolClass);
        a.PreviousResult = StudentService.Clean(i.PreviousResult);
        a.Address = i.Address?.ToAddress() ?? new Address();
        a.GuardianFirstName = i.GuardianFirstName.Trim();
        a.GuardianLastName = i.GuardianLastName?.Trim() ?? string.Empty;
        a.GuardianRelationship = i.GuardianRelationship;
        a.GuardianPhone = Guardian.NormalizePhone(i.GuardianPhone);
        a.GuardianEmail = StudentService.Clean(i.GuardianEmail);
        a.GuardianNationalId = StudentService.Digits(i.GuardianNationalId);
        a.GuardianOccupation = StudentService.Clean(i.GuardianOccupation);
        a.Notes = StudentService.Clean(i.Notes);
    }
}

// ───────────────────────────── Public online form ─────────────────────────────

public sealed record PublicGradeDto(Guid Id, string Name);
public sealed record PublicCampusDto(Guid Id, string Name, string? City);
public sealed record PublicSchoolDto(string Code, string Name, string? ShortName, string? Motto, string? BrandColor, Guid? LogoFileId,
    string? Phone, string? Email, bool AcceptingApplications, string? AcademicYearName, Guid? AcademicYearId,
    IReadOnlyList<PublicCampusDto> Campuses, IReadOnlyList<PublicGradeDto> Grades, IReadOnlyList<string> RequiredDocuments);
public sealed record PublicApplicationRequest(Guid CampusId, Guid GradeId, ApplicantInput Applicant);
public sealed record PublicApplicationReceipt(string ApplicationNumber, string SchoolName, string ApplicantName);

public sealed class PublicApplicationValidator : AbstractValidator<PublicApplicationRequest>
{
    public PublicApplicationValidator() => RuleFor(x => x.Applicant).NotNull().SetValidator(new ApplicantInputValidator());
}

/// <summary>Anonymous endpoints for the school's public application form (rate-limited; enters the school's tenant explicitly).</summary>
public sealed class PublicAdmissionService(IAppDbContext db, ITenantContext tenant, AdmissionService admissions, TimeProvider clock)
{
    public async Task<PublicSchoolDto> GetSchoolAsync(string code, CancellationToken ct)
    {
        var school = await FindSchoolAsync(code, ct);
        using (tenant.Enter(school.Id))
        {
            var year = await TargetYearAsync(ct);
            return new PublicSchoolDto(school.Code, school.Name, school.ShortName, school.Motto, school.BrandColor, school.LogoFileId,
                school.Phone, school.Email, school.Settings.Admissions.OnlineApplicationsEnabled && year is not null, year?.Name, year?.Id,
                await db.Campuses.Where(c => c.IsActive).OrderByDescending(c => c.IsMain).Select(c => new PublicCampusDto(c.Id, c.Name, c.Address.City)).ToListAsync(ct),
                await db.Grades.Where(g => g.IsActive).OrderBy(g => g.LevelOrder).Select(g => new PublicGradeDto(g.Id, g.Name)).ToListAsync(ct),
                school.Settings.Admissions.RequiredDocuments);
        }
    }

    public async Task<PublicApplicationReceipt> SubmitAsync(string code, PublicApplicationRequest request, CancellationToken ct)
    {
        var school = await FindSchoolAsync(code, ct);
        Guard.Against(!school.Settings.Admissions.OnlineApplicationsEnabled, "admissions.online_closed",
            "This school is not accepting online applications at the moment.");
        using (tenant.Enter(school.Id))
        {
            var year = await TargetYearAsync(ct) ?? throw new DomainException("admissions.no_year", "Admissions are not open yet.");
            var phone = Guardian.NormalizePhone(request.Applicant.GuardianPhone);
            var duplicate = await db.AdmissionApplications.AnyAsync(a => a.AcademicYearId == year.Id && a.GuardianPhone == phone
                && a.FirstName.ToLower() == request.Applicant.FirstName.Trim().ToLower()
                && a.DateOfBirth == request.Applicant.DateOfBirth
                && a.Status != ApplicationStatus.Withdrawn && a.Status != ApplicationStatus.Rejected, ct);
            Guard.Against(duplicate, "admissions.duplicate", "An application for this child has already been received. The school will be in touch.");

            var application = await db.InTransactionAsync(async innerCt =>
            {
                var created = await admissions.NewApplicationAsync(school.Id, year.Id, request.CampusId, request.GradeId, ApplicationSource.Online,
                    request.Applicant, innerCt);
                created.Submit(clock.GetUtcNow(), null);
                await db.SaveChangesAsync(innerCt);
                return created;
            }, ct: ct);
            return new PublicApplicationReceipt(application.ApplicationNumber, school.Name, application.ApplicantName);
        }
    }

    private async Task<School> FindSchoolAsync(string code, CancellationToken ct)
    {
        var normalized = code.Trim().ToLowerInvariant();
        return await db.Schools.SingleOrDefaultAsync(s => s.Code == normalized && s.Status == SchoolStatus.Active, ct)
            ?? throw new NotFoundException("School", code);
    }

    private async Task<Domain.Organization.AcademicYear?> TargetYearAsync(CancellationToken ct) =>
        await db.AcademicYears.Where(y => y.Status != Domain.Organization.AcademicYearStatus.Closed)
            .OrderByDescending(y => y.Status == Domain.Organization.AcademicYearStatus.Planned).ThenByDescending(y => y.IsCurrent)
            .ThenBy(y => y.StartDate).FirstOrDefaultAsync(ct);
}
