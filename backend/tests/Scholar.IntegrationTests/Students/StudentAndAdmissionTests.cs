using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Scholar.Application.Abstractions;
using Scholar.Application.Admissions;
using Scholar.Application.Common;
using Scholar.Application.Documents;
using Scholar.Application.Students;
using Scholar.Domain.Access;
using Scholar.Domain.Organization;
using Scholar.Infrastructure.Persistence;
using Scholar.IntegrationTests.Infrastructure;

namespace Scholar.IntegrationTests.Students;

public sealed class StudentAndAdmissionTests(ApiFactory factory) : IntegrationTest(factory)
{
    private sealed record Structure(Guid YearId, Guid GradeId, Guid SectionA, Guid SectionB, Guid OtherCampusId, Guid OtherCampusSection);

    private async Task<Structure> CreateStructureAsync(SeededSchool school, int capacity = 30)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        using (tenant.Enter(school.SchoolId))
        {
            var year = new AcademicYear(school.SchoolId, "2026–27", new DateOnly(2026, 8, 1), new DateOnly(2027, 7, 31));
            year.MakeCurrent();
            var grade = new Grade(school.SchoolId, "Class 5", "C5", 1, SchoolStage.Primary);
            var other = new Domain.Platform.Campus(school.SchoolId, "DHA", "DHA Campus");
            db.AddRange(year, grade, other);
            var a = new Section(school.SchoolId, year.Id, school.CampusId, grade.Id, "A", capacity);
            var b = new Section(school.SchoolId, year.Id, school.CampusId, grade.Id, "B", capacity);
            var d = new Section(school.SchoolId, year.Id, other.Id, grade.Id, "A", capacity);
            db.AddRange(a, b, d);
            await db.SaveChangesAsync();
            return new Structure(year.Id, grade.Id, a.Id, b.Id, other.Id, d.Id);
        }
    }

    private static object NewStudent(Guid sectionId, string first = "Ayesha", string last = "Khan", string guardianFirst = "Tariq",
        string phone = "0300 1234567", bool overrideCapacity = false) => new
    {
        student = new { firstName = first, lastName = last, gender = "Female", dateOfBirth = new DateOnly(2016, 4, 12), nationalId = "35202-1234567-8" },
        placement = new { sectionId, admissionDate = new DateOnly(2026, 8, 3) },
        guardians = new[] { new { newGuardian = new { firstName = guardianFirst, lastName = last, phone }, relationship = "Father", isPrimary = true, isFeePayer = true } },
        overrideCapacity,
    };

    private async Task<(SeededSchool School, Structure Structure, BrowserClient Owner)> SetupAsync(int capacity = 30)
    {
        var school = await Factory.ProvisionSchoolAsync();
        var structure = await CreateStructureAsync(school, capacity);
        var owner = Factory.CreateBrowserClient();
        await owner.LoginAsync(school.Email, TestData.Password);
        return (school, structure, owner);
    }

    [Fact]
    public async Task Enrolling_siblings_reuses_the_guardian_and_numbers_students()
    {
        var (_, s, owner) = await SetupAsync();
        using var _ = owner;

        var first = await (await owner.PostAsync("/api/students", NewStudent(s.SectionA))).ReadJsonAsync<StudentDetailDto>();
        var second = await (await owner.PostAsync("/api/students", NewStudent(s.SectionB, first: "Hamza", phone: "+92 300 1234567"))).ReadJsonAsync<StudentDetailDto>();

        Assert.Matches(@"^ADM-\d{4}-0001$", first.AdmissionNumber);
        Assert.Matches(@"^ADM-\d{4}-0002$", second.AdmissionNumber);
        Assert.Equal("3520212345678", first.NationalId);
        Assert.Equal("Class 5 – A", first.CurrentEnrollment!.ClassName);
        var link = Assert.Single(second.Guardians);
        Assert.Equal(first.Guardians[0].GuardianId, link.GuardianId);
        Assert.True(link.IsPrimary && link.IsFeePayer);
        Assert.Equal("+923001234567", link.Phone);
        Assert.Contains(second.Siblings, x => x.Id == first.Id);

        var list = await owner.GetJsonAsync<PagedResult<StudentListItemDto>>("/api/students?search=ayesha");
        Assert.Equal(first.Id, Assert.Single(list.Items).Id);
        var byPhone = await owner.GetJsonAsync<PagedResult<StudentListItemDto>>("/api/students?search=03001234567");
        Assert.Equal(2, byPhone.Total);
    }

    [Fact]
    public async Task Class_order_lists_sections_in_order_and_pages_never_repeat_students()
    {
        var (_, s, owner) = await SetupAsync();
        using var _ = owner;
        for (var i = 0; i < 7; i++)
        {
            // Identical names in two sections: every sort key ties, so only a stable tie-break keeps pages apart.
            await (await owner.PostAsync("/api/students", NewStudent(i % 2 == 0 ? s.SectionB : s.SectionA, phone: $"0300 12345{i:00}")))
                .EnsureSuccessWithBodyAsync();
        }

        var seen = new List<StudentListItemDto>();
        for (var page = 1; page <= 4; page++)
        {
            seen.AddRange((await owner.GetJsonAsync<PagedResult<StudentListItemDto>>($"/api/students?sort=class&pageSize=2&page={page}")).Items);
        }

        Assert.Equal(7, seen.Select(x => x.Id).Distinct().Count());
        Assert.Equal(["Class 5 – A", "Class 5 – A", "Class 5 – A", "Class 5 – B", "Class 5 – B", "Class 5 – B", "Class 5 – B"],
            seen.Select(x => x.ClassName));

        var byName = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            byName.AddRange((await owner.GetJsonAsync<PagedResult<StudentListItemDto>>($"/api/students?sort=name&pageSize=3&page={page}")).Items.Select(x => x.Id));
        }

        Assert.Equal(7, byName.Distinct().Count());
    }

    [Fact]
    public async Task Bulk_section_moves_check_capacity_for_the_group_and_are_all_or_nothing()
    {
        var (_, s, owner) = await SetupAsync(capacity: 3);
        using var _ = owner;
        var ids = new List<Guid>();
        foreach (var (first, phone) in new[] { ("Ayesha", "0300 1111111"), ("Hamza", "0300 2222222"), ("Zainab", "0300 3333333") })
        {
            ids.Add((await (await owner.PostAsync("/api/students", NewStudent(s.SectionA, first: first, phone: phone))).ReadJsonAsync<StudentDetailDto>()).Id);
        }

        await (await owner.PostAsync("/api/students", NewStudent(s.SectionB, first: "Bilal", phone: "0300 4444444"))).EnsureSuccessWithBodyAsync();

        // Section B has 1 of 3 seats taken: moving all three would need 4.
        var tooMany = await owner.PostAsync("/api/students/bulk/transfer",
            new { studentIds = ids, sectionId = s.SectionB, effectiveDate = new DateOnly(2026, 9, 1), reason = "Re-sectioning" });
        Assert.Equal("section.full", await tooMany.ProblemCodeAsync());
        var unchanged = await owner.GetJsonAsync<PagedResult<StudentListItemDto>>($"/api/students?sectionId={s.SectionA}");
        Assert.Equal(3, unchanged.Total);

        var moved = await (await owner.PostAsync("/api/students/bulk/transfer",
            new { studentIds = ids.Take(2), sectionId = s.SectionB, effectiveDate = new DateOnly(2026, 9, 1), reason = "Re-sectioning" }))
            .ReadJsonAsync<BulkResult>();
        Assert.Equal(2, moved.Affected);
        var detail = await owner.GetJsonAsync<StudentDetailDto>($"/api/students/{ids[0]}");
        Assert.Equal("Class 5 – B", detail.CurrentEnrollment!.ClassName);
        Assert.Contains(detail.Enrollments, e => e.Status == Domain.Students.EnrollmentStatus.Transferred && e.Reason == "Re-sectioning");

        // Exporting a selection returns exactly those students.
        var csv = await (await owner.GetAsync($"/api/students/export?ids={ids[0]}&ids={ids[2]}")).Content.ReadAsStringAsync();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.Contains(lines, l => l.Contains("Ayesha", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("Zainab", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Full_sections_refuse_students_unless_overridden()
    {
        var (_, s, owner) = await SetupAsync(capacity: 1);
        using var _ = owner;
        await (await owner.PostAsync("/api/students", NewStudent(s.SectionA))).EnsureSuccessWithBodyAsync();

        var full = await owner.PostAsync("/api/students", NewStudent(s.SectionA, first: "Sara", phone: "0301 7654321"));
        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);
        Assert.Equal("section.full", await full.ProblemCodeAsync());

        var forced = await owner.PostAsync("/api/students", NewStudent(s.SectionA, first: "Sara", phone: "0301 7654321", overrideCapacity: true));
        await forced.EnsureSuccessWithBodyAsync();
    }

    [Fact]
    public async Task Campus_scoped_staff_cannot_see_other_campuses_students()
    {
        var (school, s, owner) = await SetupAsync();
        using var _ = owner;
        var dhaStudent = await (await owner.PostAsync("/api/students", NewStudent(s.OtherCampusSection))).ReadJsonAsync<StudentDetailDto>();
        var (_, registrar) = await Factory.AddMemberAsync(school, SystemRoles.Registrar, school.CampusId);
        using var scoped = Factory.CreateBrowserClient();
        await scoped.LoginAsync(registrar, TestData.Password);

        var list = await scoped.GetJsonAsync<PagedResult<StudentListItemDto>>("/api/students");
        Assert.Equal(0, list.Total);
        Assert.Equal(HttpStatusCode.NotFound, (await scoped.GetAsync($"/api/students/{dhaStudent.Id}")).StatusCode);
        var place = await scoped.PostAsync("/api/students", NewStudent(s.OtherCampusSection, first: "Bilal", phone: "0333 1112223"));
        Assert.Equal("campus.out_of_scope", await place.ProblemCodeAsync());
    }

    [Fact]
    public async Task Withdrawal_requires_a_reason_closes_the_placement_and_keeps_history()
    {
        var (_, s, owner) = await SetupAsync();
        using var _ = owner;
        var student = await (await owner.PostAsync("/api/students", NewStudent(s.SectionA))).ReadJsonAsync<StudentDetailDto>();

        var noReason = await owner.PostAsync($"/api/students/{student.Id}/status", new { status = "Withdrawn", effectiveDate = new DateOnly(2026, 11, 1) });
        Assert.Equal("student.reason_required", await noReason.ProblemCodeAsync());

        var withdrawn = await (await owner.PostAsync($"/api/students/{student.Id}/status",
            new { status = "Withdrawn", effectiveDate = new DateOnly(2026, 11, 1), reason = "Family relocated to Karachi" })).ReadJsonAsync<StudentDetailDto>();
        Assert.Null(withdrawn.CurrentEnrollment);
        Assert.Equal("Withdrawn", Assert.Single(withdrawn.Enrollments).Status.ToString());
        Assert.Single(withdrawn.StatusHistory);

        Assert.Equal(0, (await owner.GetJsonAsync<PagedResult<StudentListItemDto>>("/api/students")).Total);
        Assert.Equal(1, (await owner.GetJsonAsync<PagedResult<StudentListItemDto>>("/api/students?scope=left")).Total);
    }

    [Fact]
    public async Task Section_transfer_keeps_the_previous_placement_in_history()
    {
        var (_, s, owner) = await SetupAsync();
        using var _ = owner;
        var student = await (await owner.PostAsync("/api/students", NewStudent(s.SectionA))).ReadJsonAsync<StudentDetailDto>();

        var moved = await (await owner.PostAsync($"/api/students/{student.Id}/transfer",
            new { sectionId = s.SectionB, effectiveDate = new DateOnly(2026, 9, 1), reason = "Balancing class sizes" })).ReadJsonAsync<StudentDetailDto>();

        Assert.Equal("Class 5 – B", moved.CurrentEnrollment!.ClassName);
        Assert.Equal(2, moved.Enrollments.Count);
        Assert.Contains(moved.Enrollments, e => e.Status.ToString() == "Transferred" && e.EndDate == new DateOnly(2026, 8, 31));
    }

    [Fact]
    public async Task Medical_details_are_restricted_and_edits_detect_conflicts()
    {
        var (school, s, owner) = await SetupAsync();
        using var _ = owner;
        var student = await (await owner.PostAsync("/api/students", NewStudent(s.SectionA))).ReadJsonAsync<StudentDetailDto>();
        var (_, teacher) = await Factory.AddMemberAsync(school, SystemRoles.ClassTeacher);
        using var teacherClient = Factory.CreateBrowserClient();
        await teacherClient.LoginAsync(teacher, TestData.Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacherClient.GetAsync($"/api/students/{student.Id}/medical")).StatusCode);

        await (await owner.SendAsync(HttpMethod.Put, $"/api/students/{student.Id}/medical",
            new { allergies = "Peanuts", medicalAlert = "Severe nut allergy — EpiPen in office", bloodGroup = "B+" })).EnsureSuccessWithBodyAsync();
        var asTeacher = await teacherClient.GetJsonAsync<StudentDetailDto>($"/api/students/{student.Id}");
        Assert.Equal("Severe nut allergy — EpiPen in office", asTeacher.MedicalAlert);

        var details = new { firstName = "Ayesha", lastName = "Khan", gender = "Female", dateOfBirth = new DateOnly(2016, 4, 12) };
        var fresh = await owner.GetJsonAsync<StudentDetailDto>($"/api/students/{student.Id}");
        await (await owner.SendAsync(HttpMethod.Put, $"/api/students/{student.Id}", new { student = details, version = fresh.Version })).EnsureSuccessWithBodyAsync();
        var stale = await owner.SendAsync(HttpMethod.Put, $"/api/students/{student.Id}", new { student = details, version = fresh.Version });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency.conflict", await stale.ProblemCodeAsync());
    }

    [Fact]
    public async Task Application_moves_through_review_assessment_offer_and_enrolment()
    {
        var (school, s, owner) = await SetupAsync();
        using var _ = owner;
        var created = await (await owner.PostAsync("/api/admissions/applications", new
        {
            academicYearId = s.YearId, campusId = school.CampusId, gradeId = s.GradeId,
            applicant = new
            {
                firstName = "Zainab", lastName = "Riaz", gender = "Female", dateOfBirth = new DateOnly(2016, 2, 3),
                guardianFirstName = "Riaz", guardianLastName = "Ahmed", guardianRelationship = "Father", guardianPhone = "0321 5550001",
            },
        })).ReadJsonAsync<ApplicationDetailDto>();
        Assert.Equal("Submitted", created.Status.ToString());

        var jump = await owner.PostAsync($"/api/admissions/applications/{created.Id}/enroll", new { sectionId = s.SectionA, admissionDate = new DateOnly(2026, 9, 1) });
        Assert.Equal("application.not_enrollable", await jump.ProblemCodeAsync());

        var scheduled = await (await owner.PostAsync($"/api/admissions/applications/{created.Id}/assessments",
            new { type = "EntranceTest", scheduledAt = DateTimeOffset.UtcNow.AddDays(2), location = "Hall" })).ReadJsonAsync<ApplicationDetailDto>();
        Assert.Equal("AssessmentScheduled", scheduled.Status.ToString());
        var assessed = await (await owner.SendAsync(HttpMethod.Put, $"/api/admissions/applications/{created.Id}/assessments/{scheduled.Assessments[0].Id}",
            new { outcome = "Passed", score = 41, maxScore = 50 })).ReadJsonAsync<ApplicationDetailDto>();
        Assert.Equal("Assessed", assessed.Status.ToString());

        var offered = await (await owner.PostAsync($"/api/admissions/applications/{created.Id}/decision", new { decision = "offer" })).ReadJsonAsync<ApplicationDetailDto>();
        Assert.Equal("Offered", offered.Status.ToString());
        Assert.NotNull(offered.OfferExpiresOn);

        var enrolled = await (await owner.PostAsync($"/api/admissions/applications/{created.Id}/enroll",
            new { sectionId = s.SectionA, admissionDate = new DateOnly(2026, 9, 1) })).ReadJsonAsync<ApplicationDetailDto>();
        Assert.Equal("Enrolled", enrolled.Status.ToString());
        Assert.Empty(enrolled.AllowedTransitions);
        var student = await owner.GetJsonAsync<StudentDetailDto>($"/api/students/{enrolled.StudentId}");
        Assert.Equal("Zainab Riaz", student.FullName);
        Assert.Equal("Riaz Ahmed", student.Guardians[0].FullName);
        Assert.True(enrolled.History.Count >= 5);
    }

    [Fact]
    public async Task Public_online_application_is_received_once()
    {
        var (school, s, owner) = await SetupAsync();
        using var _ = owner;
        using var anonymous = Factory.CreateClient();
        var info = await anonymous.GetFromJsonAsync<PublicSchoolDto>($"/api/public/schools/{school.Code}", BrowserClient.Json);
        Assert.True(info!.AcceptingApplications);

        var form = new
        {
            campusId = school.CampusId, gradeId = s.GradeId,
            applicant = new
            {
                firstName = "Omar", lastName = "Farooq", gender = "Male", dateOfBirth = new DateOnly(2015, 6, 9),
                guardianFirstName = "Farooq", guardianLastName = "Ali", guardianRelationship = "Father", guardianPhone = "0345 9988776",
            },
        };
        var receipt = await (await anonymous.PostAsJsonAsync($"/api/public/schools/{school.Code}/applications", form, BrowserClient.Json))
            .ReadJsonAsync<PublicApplicationReceipt>();
        Assert.StartsWith("APP-", receipt.ApplicationNumber, StringComparison.Ordinal);

        var again = await anonymous.PostAsJsonAsync($"/api/public/schools/{school.Code}/applications", form, BrowserClient.Json);
        Assert.Equal("admissions.duplicate", await again.ProblemCodeAsync());

        var apps = await owner.GetJsonAsync<PagedResult<ApplicationListItemDto>>("/api/admissions/applications?source=Online");
        Assert.Equal("Omar Farooq", Assert.Single(apps.Items).ApplicantName);
    }

    [Fact]
    public async Task Uploads_are_type_checked_and_downloads_authorized()
    {
        var (school, s, owner) = await SetupAsync();
        using var _ = owner;
        var student = await (await owner.PostAsync("/api/students", NewStudent(s.SectionA))).ReadJsonAsync<StudentDetailDto>();

        async Task<HttpResponseMessage> Upload(BrowserClient client, byte[] bytes, string name, string category)
        {
            await client.EnsureCsrfAsync();
            using var content = new MultipartFormDataContent();
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(file, "file", name);
            content.Add(new StringContent(category), "category");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/students/{student.Id}/documents") { Content = content };
            request.Headers.Add("X-CSRF-Token", client.CsrfToken);
            return await client.Http.SendAsync(request);
        }

        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
        var uploaded = await (await Upload(owner, png, "b-form.png", "birth_certificate")).ReadJsonAsync<StoredFileDto>();
        Assert.Equal("image/png", uploaded.ContentType);

        var spoofed = await Upload(owner, Encoding.UTF8.GetBytes("<script>alert(1)</script>"), "evil.png", "other");
        Assert.Equal("file.type_not_allowed", await spoofed.ProblemCodeAsync());

        var content = await owner.Http.GetAsync($"/api/files/{uploaded.Id}/content");
        Assert.Equal(png, await content.Content.ReadAsByteArrayAsync());

        var (_, librarian) = await Factory.AddMemberAsync(school, SystemRoles.Librarian);
        using var other = Factory.CreateBrowserClient();
        await other.LoginAsync(librarian, TestData.Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/files/{uploaded.Id}/content")).StatusCode);
    }

    [Fact]
    public async Task Import_validates_every_row_and_commits_all_or_nothing()
    {
        var (_, _, owner) = await SetupAsync();
        using var _ = owner;
        const string csv = """
            first_name,last_name,gender,date_of_birth,campus,grade,section,guardian_first_name,guardian_phone,guardian_relationship
            Ali,Hassan,Male,12/03/2016,MAIN,C5,A,Hassan,0300 1111111,Father
            Sana,Mir,F,2016-07-21,MAIN,C5,B,Mir,0300 2222222,Mother
            Nobody,Here,Robot,2016-07-21,MAIN,C9,A,,123,Pilot
            """;

        async Task<ImportReport> Import(string body, bool commit)
        {
            await owner.EnsureCsrfAsync();
            using var content = new MultipartFormDataContent { { new StringContent(body, Encoding.UTF8, "text/csv"), "file", "students.csv" } };
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/students/import?commit={commit}") { Content = content };
            request.Headers.Add("X-CSRF-Token", owner.CsrfToken);
            return await (await owner.Http.SendAsync(request)).ReadJsonAsync<ImportReport>();
        }

        var dryRun = await Import(csv, commit: true);
        Assert.False(dryRun.Committed);
        Assert.Equal(2, dryRun.ValidRows);
        Assert.Contains(dryRun.Errors, e => e.Row == 4 && e.Column == "gender");
        Assert.Contains(dryRun.Errors, e => e.Row == 4 && e.Column == "section");
        Assert.Equal(0, (await owner.GetJsonAsync<PagedResult<StudentListItemDto>>("/api/students")).Total);

        var clean = string.Join('\n', csv.Split('\n').Take(3));
        var committed = await Import(clean, commit: true);
        Assert.True(committed.Committed);
        Assert.Equal(2, (await owner.GetJsonAsync<PagedResult<StudentListItemDto>>("/api/students")).Total);
    }
}
