using System.Net;
using Scholar.Application.Organization;
using Scholar.Domain.Access;
using Scholar.IntegrationTests.Infrastructure;

namespace Scholar.IntegrationTests.Organization;

public sealed class SchoolConfigurationTests(ApiFactory factory) : IntegrationTest(factory)
{
    private async Task<(SeededSchool School, BrowserClient Owner)> OwnerAsync()
    {
        var school = await Factory.ProvisionSchoolAsync();
        var owner = Factory.CreateBrowserClient();
        await owner.LoginAsync(school.Email, TestData.Password);
        return (school, owner);
    }

    private static async Task<AcademicYearDto> CreateYearAsync(BrowserClient client, string name, DateOnly start, DateOnly end,
        params object[] terms)
    {
        var response = await client.PostAsync("/api/org/academic-years", new { name, startDate = start, endDate = end, terms });
        return await response.ReadJsonAsync<AcademicYearDto>();
    }

    [Fact]
    public async Task Academic_years_cannot_overlap_and_exactly_one_is_current()
    {
        var (_, owner) = await OwnerAsync();
        using var _owner = owner;

        var first = await CreateYearAsync(owner, "2026–27", new(2026, 8, 1), new(2027, 7, 31),
            new { name = "First Term", startDate = new DateOnly(2026, 8, 3), endDate = new DateOnly(2026, 12, 18) });
        Assert.True(first.IsCurrent);

        var overlap = await owner.PostAsync("/api/org/academic-years", new { name = "Clash", startDate = new DateOnly(2027, 6, 1), endDate = new DateOnly(2028, 5, 31) });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, overlap.StatusCode);
        Assert.Equal("academic_year.overlap", await overlap.ProblemCodeAsync());

        var next = await CreateYearAsync(owner, "2027–28", new(2027, 8, 1), new(2028, 7, 31));
        Assert.False(next.IsCurrent);

        var switched = await (await owner.PostAsync($"/api/org/academic-years/{next.Id}/make-current", new { })).ReadJsonAsync<AcademicYearDto>();
        Assert.True(switched.IsCurrent);
        var years = await owner.GetJsonAsync<List<AcademicYearDto>>("/api/org/academic-years");
        Assert.Single(years, y => y.IsCurrent);

        var deleteCurrent = await owner.SendAsync(HttpMethod.Delete, $"/api/org/academic-years/{next.Id}");
        Assert.Equal("academic_year.delete_current", await deleteCurrent.ProblemCodeAsync());
    }

    [Fact]
    public async Task Terms_must_not_overlap_and_are_numbered_in_date_order()
    {
        var (_, owner) = await OwnerAsync();
        using var _owner = owner;
        var year = await CreateYearAsync(owner, "2026–27", new(2026, 8, 1), new(2027, 7, 31),
            new { name = "Spring", startDate = new DateOnly(2027, 1, 4), endDate = new DateOnly(2027, 6, 4) });

        var withAutumn = await (await owner.PostAsync($"/api/org/academic-years/{year.Id}/terms",
            new { name = "Autumn", startDate = new DateOnly(2026, 8, 3), endDate = new DateOnly(2026, 12, 18) })).ReadJsonAsync<AcademicYearDto>();
        Assert.Equal(["Autumn", "Spring"], withAutumn.Terms.Select(t => t.Name));
        Assert.Equal([1, 2], withAutumn.Terms.Select(t => t.Sequence));

        var overlap = await owner.PostAsync($"/api/org/academic-years/{year.Id}/terms",
            new { name = "Winter", startDate = new DateOnly(2026, 12, 1), endDate = new DateOnly(2027, 1, 20) });
        Assert.Equal("term.overlap", await overlap.ProblemCodeAsync());

        // Moving Spring before Autumn's dates swaps their numbers in a single save.
        var spring = withAutumn.Terms.Single(t => t.Name == "Spring");
        var autumn = withAutumn.Terms.Single(t => t.Name == "Autumn");
        await (await owner.SendAsync(HttpMethod.Put, $"/api/org/academic-years/{year.Id}/terms/{autumn.Id}",
            new { name = "Autumn", startDate = new DateOnly(2027, 6, 10), endDate = new DateOnly(2027, 7, 20) })).EnsureSuccessWithBodyAsync();
        var reordered = (await owner.GetJsonAsync<List<AcademicYearDto>>("/api/org/academic-years")).Single();
        Assert.Equal([spring.Id, autumn.Id], reordered.Terms.Select(t => t.Id));
    }

    [Fact]
    public async Task Sections_are_unique_per_grade_campus_and_year_and_rooms_must_match_the_campus()
    {
        var (school, owner) = await OwnerAsync();
        using var _owner = owner;
        var year = await CreateYearAsync(owner, "2026–27", new(2026, 8, 1), new(2027, 7, 31));
        var grade = await (await owner.PostAsync("/api/org/grades", new { name = "Class 5", code = "C5", stage = "Primary" })).ReadJsonAsync<GradeDto>();
        var otherCampus = await (await owner.PostAsync("/api/org/campus-management",
            new { code = "DHA", name = "DHA Campus", address = new { city = "Lahore" } })).ReadJsonAsync<CampusDto>();
        var dhaRoom = await (await owner.PostAsync("/api/org/rooms",
            new { campusId = otherCampus.Id, code = "D-1", name = "Room 1", type = "Classroom", capacity = 30, isBookable = false })).ReadJsonAsync<RoomDto>();

        var section = new { academicYearId = year.Id, campusId = school.CampusId, gradeId = grade.Id, name = "A", capacity = 30 };
        await (await owner.PostAsync("/api/org/sections", section)).EnsureSuccessWithBodyAsync();

        var duplicate = await owner.PostAsync("/api/org/sections", section);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var wrongRoom = await owner.PostAsync("/api/org/sections", new
        {
            academicYearId = year.Id, campusId = school.CampusId, gradeId = grade.Id, name = "B", capacity = 30, roomId = dhaRoom.Id,
        });
        Assert.Equal("section.room_campus", await wrongRoom.ProblemCodeAsync());

        var nextYear = await CreateYearAsync(owner, "2027–28", new(2027, 8, 1), new(2028, 7, 31));
        var copy = await (await owner.PostAsync("/api/org/sections/copy", new { fromAcademicYearId = year.Id, toAcademicYearId = nextYear.Id }))
            .ReadJsonAsync<CopyResultDto>();
        Assert.Equal(1, copy.Created);
        var again = await (await owner.PostAsync("/api/org/sections/copy", new { fromAcademicYearId = year.Id, toAcademicYearId = nextYear.Id }))
            .ReadJsonAsync<CopyResultDto>();
        Assert.Equal(0, again.Created);
        Assert.Equal(1, again.Skipped);
    }

    [Fact]
    public async Task Curriculum_is_replaced_as_a_whole_for_a_grade()
    {
        var (_, owner) = await OwnerAsync();
        using var _owner = owner;
        var year = await CreateYearAsync(owner, "2026–27", new(2026, 8, 1), new(2027, 7, 31));
        var grade = await (await owner.PostAsync("/api/org/grades", new { name = "Class 1", code = "C1", stage = "Primary" })).ReadJsonAsync<GradeDto>();
        var english = await (await owner.PostAsync("/api/org/subjects", new { code = "ENG", name = "English", type = "Language" })).ReadJsonAsync<SubjectDto>();
        var maths = await (await owner.PostAsync("/api/org/subjects", new { code = "MATH", name = "Mathematics", type = "Core" })).ReadJsonAsync<SubjectDto>();

        await (await owner.SendAsync(HttpMethod.Put, "/api/org/curriculum", new
        {
            academicYearId = year.Id, gradeId = grade.Id,
            items = new[] { new { subjectId = english.Id, isMandatory = true, periodsPerWeek = 6 }, new { subjectId = maths.Id, isMandatory = true, periodsPerWeek = 7 } },
        })).EnsureSuccessWithBodyAsync();

        var replaced = await (await owner.SendAsync(HttpMethod.Put, "/api/org/curriculum", new
        {
            academicYearId = year.Id, gradeId = grade.Id,
            items = new[] { new { subjectId = maths.Id, isMandatory = true, periodsPerWeek = 8 } },
        })).ReadJsonAsync<List<CurriculumEntryDto>>();
        var entry = Assert.Single(replaced);
        Assert.Equal(8, entry.PeriodsPerWeek);

        var deleteInUse = await owner.SendAsync(HttpMethod.Delete, $"/api/org/subjects/{maths.Id}");
        Assert.Equal("subject.in_use", await deleteInUse.ProblemCodeAsync());
    }

    [Fact]
    public async Task Profile_validation_modules_and_permissions()
    {
        var (school, owner) = await OwnerAsync();
        using var _owner = owner;
        var profile = await owner.GetJsonAsync<SchoolProfileDto>("/api/org/school");

        var badColour = await owner.SendAsync(HttpMethod.Put, "/api/org/school/profile", new
        {
            name = profile.Name, address = new { city = "Lahore" }, timezone = "Asia/Karachi", currencyCode = "PKR", locale = "en-PK",
            brandColor = "green",
        });
        Assert.Equal(HttpStatusCode.BadRequest, badColour.StatusCode);
        Assert.Contains("brandColor", await badColour.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var updated = await (await owner.SendAsync(HttpMethod.Put, "/api/org/school/profile", new
        {
            name = "Riverside Academy", shortName = "Riverside", address = new { line1 = "12 Canal Road", city = "Lahore", countryCode = "pk" },
            timezone = "Asia/Karachi", currencyCode = "pkr", locale = "en-PK", brandColor = "#1f5f4a",
        })).ReadJsonAsync<SchoolProfileDto>();
        Assert.Equal("PKR", updated.CurrencyCode);
        Assert.Equal("#1F5F4A", updated.BrandColor);
        Assert.Equal("PK", updated.Address.CountryCode);

        var payrollWithoutHr = await owner.SendAsync(HttpMethod.Put, "/api/org/school/modules", new { enabled = new[] { "payroll", "fees" } });
        Assert.Equal("modules.payroll_requires_hr", await payrollWithoutHr.ProblemCodeAsync());

        var sequence = await (await owner.SendAsync(HttpMethod.Put, "/api/org/school/sequences/admission",
            new { prefix = "gfs", pattern = "{prefix}/{yy}/{seq:5}", resetPolicy = "Yearly" })).ReadJsonAsync<SequenceDto>();
        Assert.StartsWith("GFS/", sequence.Preview, StringComparison.Ordinal);
        Assert.EndsWith("/00001", sequence.Preview, StringComparison.Ordinal);

        // A restarting counter must print its period, or restarted numbers would repeat earlier ones.
        var monthlyWithoutMonth = await owner.SendAsync(HttpMethod.Put, "/api/org/school/sequences/receipt",
            new { prefix = "RCT", pattern = "{prefix}-{yyyy}-{seq:5}", resetPolicy = "Monthly" });
        Assert.Equal("validation.failed", await monthlyWithoutMonth.ProblemCodeAsync());

        // Continuing from a previous system's numbering moves the counter forward…
        var continued = await (await owner.SendAsync(HttpMethod.Put, "/api/org/school/sequences/receipt",
            new { prefix = "RCT", pattern = "{prefix}-{yy}{mm}-{seq:5}", resetPolicy = "Monthly", nextNumber = 453 })).ReadJsonAsync<SequenceDto>();
        Assert.Equal(453, continued.NextValue);
        Assert.EndsWith("-00453", continued.Preview, StringComparison.Ordinal);

        // …but never backwards, so an issued number can't be issued twice.
        var backwards = await owner.SendAsync(HttpMethod.Put, "/api/org/school/sequences/receipt",
            new { prefix = "RCT", pattern = "{prefix}-{yy}{mm}-{seq:5}", resetPolicy = "Monthly", nextNumber = 10 });
        Assert.Equal("sequence.cannot_go_back", await backwards.ProblemCodeAsync());

        // Switching rhythm continues from the highest number already issued instead of starting again at 1.
        var yearly = await (await owner.SendAsync(HttpMethod.Put, "/api/org/school/sequences/receipt",
            new { prefix = "RCT", pattern = "{prefix}-{yy}{mm}-{seq:5}", resetPolicy = "Yearly" })).ReadJsonAsync<SequenceDto>();
        Assert.Equal(453, yearly.NextValue);
        var listed = await owner.GetJsonAsync<List<SequenceDto>>("/api/org/school/sequences");
        Assert.Equal(453, listed.Single(s => s.Key == "receipt").NextValue);

        var (_, teacherEmail) = await Factory.AddMemberAsync(school, SystemRoles.Teacher);
        using var teacher = Factory.CreateBrowserClient();
        await teacher.LoginAsync(teacherEmail, TestData.Password);
        var forbidden = await teacher.SendAsync(HttpMethod.Put, "/api/org/school/profile", new
        {
            name = "Hijacked", address = new { }, timezone = "Asia/Karachi", currencyCode = "PKR", locale = "en-PK",
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
