using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Application.Organization;
using Scholar.Application.Staffing;
using Scholar.Domain.Access;
using Scholar.Domain.Organization;
using Scholar.Infrastructure.Persistence;
using Scholar.IntegrationTests.Infrastructure;

namespace Scholar.IntegrationTests.Staff;

public sealed class StaffDirectoryTests(ApiFactory factory) : IntegrationTest(factory)
{
    private sealed record Structure(Guid YearId, Guid SectionA, Guid OtherCampusId, Guid OtherCampusSection, Guid MathsId, Guid UrduId);

    private async Task<Structure> CreateStructureAsync(SeededSchool school)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        using (tenant.Enter(school.SchoolId))
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var year = new AcademicYear(school.SchoolId, "Current", today.AddDays(-120), today.AddDays(200));
            year.MakeCurrent();
            var grade = new Grade(school.SchoolId, "Class 5", "C5", 1, SchoolStage.Primary);
            var other = new Domain.Platform.Campus(school.SchoolId, "DHA", "DHA Campus");
            var maths = new Subject(school.SchoolId, "MATH", "Mathematics", SubjectType.Core);
            var urdu = new Subject(school.SchoolId, "URDU", "Urdu", SubjectType.Core);
            db.AddRange(year, grade, other, maths, urdu);
            db.Add(new GradeSubject(school.SchoolId, year.Id, grade.Id, maths.Id, true, 6));
            db.Add(new GradeSubject(school.SchoolId, year.Id, grade.Id, urdu.Id, true, 5));
            var a = new Section(school.SchoolId, year.Id, school.CampusId, grade.Id, "A", 30);
            var d = new Section(school.SchoolId, year.Id, other.Id, grade.Id, "A", 30);
            db.AddRange(a, d);
            await db.SaveChangesAsync();
            return new Structure(year.Id, a.Id, other.Id, d.Id, maths.Id, urdu.Id);
        }
    }

    private static object NewStaff(Guid campusId, string first = "Sana", string last = "Iqbal", string? email = null,
        string type = "Teaching") => new
    {
        firstName = first, lastName = last, gender = "Female", phone = "0321 4567890", email, nationalId = "35202-7654321-0",
        campusId, staffType = type, employmentType = "Permanent", joinDate = new DateOnly(2024, 8, 1),
    };

    private async Task<(SeededSchool School, Structure Structure, BrowserClient Owner)> SetupAsync()
    {
        var school = await Factory.ProvisionSchoolAsync();
        var structure = await CreateStructureAsync(school);
        var owner = Factory.CreateBrowserClient();
        await owner.LoginAsync(school.Email, TestData.Password);
        return (school, structure, owner);
    }

    [Fact]
    public async Task Staff_are_numbered_normalised_searchable_and_campus_scoped()
    {
        var (school, s, owner) = await SetupAsync();
        using var _ = owner;

        var sana = await (await owner.PostAsync("/api/staff", NewStaff(school.CampusId))).ReadJsonAsync<StaffDetailDto>();
        var bilal = await (await owner.PostAsync("/api/staff", NewStaff(s.OtherCampusId, "Bilal", "Ahmed"))).ReadJsonAsync<StaffDetailDto>();
        Assert.Equal("EMP-0001", sana.EmployeeNumber);
        Assert.Equal("EMP-0002", bilal.EmployeeNumber);
        Assert.Equal("+923214567890", sana.Phone);
        Assert.Equal("3520276543210", sana.NationalId);

        var byPhone = await owner.GetJsonAsync<PagedResult<StaffListItemDto>>("/api/staff?search=0321%204567890");
        Assert.Equal(2, byPhone.Total);
        var byName = await owner.GetJsonAsync<PagedResult<StaffListItemDto>>("/api/staff?search=bilal");
        Assert.Equal(bilal.Id, Assert.Single(byName.Items).Id);

        var duplicate = await owner.PostAsync("/api/staff", new
        {
            firstName = "Copy", lastName = "Cat", gender = "Male", campusId = school.CampusId, staffType = "Support",
            employmentType = "Contract", joinDate = new DateOnly(2025, 1, 1), employeeNumber = "emp-0001",
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("staff.number_taken", await duplicate.ProblemCodeAsync());

        // An HR manager limited to the main campus never sees the other campus's staff.
        var (_, hrEmail) = await Factory.AddMemberAsync(school, SystemRoles.HrManager, school.CampusId);
        using var hr = Factory.CreateBrowserClient();
        await hr.LoginAsync(hrEmail, TestData.Password);
        var visible = await hr.GetJsonAsync<PagedResult<StaffListItemDto>>("/api/staff");
        Assert.Equal(sana.Id, Assert.Single(visible.Items).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await hr.GetAsync($"/api/staff/{bilal.Id}")).StatusCode);
        var moved = await hr.SendAsync(HttpMethod.Put, $"/api/staff/{sana.Id}", NewStaff(s.OtherCampusId));
        Assert.Equal(HttpStatusCode.NotFound, moved.StatusCode);

        // Teachers have no access to the directory.
        var (_, teacherEmail) = await Factory.AddMemberAsync(school, SystemRoles.Teacher);
        using var teacher = Factory.CreateBrowserClient();
        await teacher.LoginAsync(teacherEmail, TestData.Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync("/api/staff")).StatusCode);
    }

    [Fact]
    public async Task Class_and_subject_teachers_are_assigned_per_section_and_released_when_staff_leave()
    {
        var (school, s, owner) = await SetupAsync();
        using var _ = owner;
        var sana = await (await owner.PostAsync("/api/staff", NewStaff(school.CampusId))).ReadJsonAsync<StaffDetailDto>();
        var bilal = await (await owner.PostAsync("/api/staff", NewStaff(s.OtherCampusId, "Bilal", "Ahmed"))).ReadJsonAsync<StaffDetailDto>();

        var otherCampus = await owner.SendAsync(HttpMethod.Put, $"/api/org/teaching/sections/{s.SectionA}",
            new { classTeacherId = bilal.Id, subjects = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, otherCampus.StatusCode);
        Assert.Equal("teaching.other_campus", await otherCampus.ProblemCodeAsync());

        var saved = await (await owner.SendAsync(HttpMethod.Put, $"/api/org/teaching/sections/{s.SectionA}", new
        {
            classTeacherId = sana.Id,
            subjects = new[] { new { subjectId = s.MathsId, staffId = (Guid?)sana.Id }, new { subjectId = s.UrduId, staffId = (Guid?)null } },
        })).ReadJsonAsync<SectionTeachingDto>();
        Assert.Equal(sana.Id, saved.ClassTeacherId);
        Assert.Equal(sana.Id, saved.Subjects.Single(x => x.SubjectId == s.MathsId).StaffId);
        Assert.Null(saved.Subjects.Single(x => x.SubjectId == s.UrduId).StaffId);
        Assert.All(saved.Subjects, x => Assert.True(x.Offered));

        var profile = await owner.GetJsonAsync<StaffDetailDto>($"/api/staff/{sana.Id}");
        Assert.Equal("Class 5 – A", Assert.Single(profile.ClassTeacherOf).ClassName);
        Assert.Equal("Mathematics", Assert.Single(profile.Teaches).SubjectName);
        var sections = await owner.GetJsonAsync<List<SectionDto>>("/api/org/sections");
        Assert.Equal("Sana Iqbal", sections.Single(x => x.Id == s.SectionA).ClassTeacherName);
        var list = await owner.GetJsonAsync<PagedResult<StaffListItemDto>>("/api/staff?search=sana");
        Assert.Equal("Class 5 – A", list.Items.Single().ClassTeacherOf);

        // Someone with classes cannot be deleted (or silently moved to another campus).
        var delete = await owner.SendAsync(HttpMethod.Delete, $"/api/staff/{sana.Id}");
        Assert.Equal("staff.has_history", await delete.ProblemCodeAsync());
        var move = await owner.SendAsync(HttpMethod.Put, $"/api/staff/{sana.Id}", NewStaff(s.OtherCampusId));
        Assert.Equal("staff.campus_has_assignments", await move.ProblemCodeAsync());

        // Leaving needs a reason and releases every class and subject.
        var noReason = await owner.PostAsync($"/api/staff/{sana.Id}/status", new { status = "Resigned", effectiveDate = new DateOnly(2026, 10, 31) });
        Assert.Equal("staff.reason_required", await noReason.ProblemCodeAsync());
        var left = await (await owner.PostAsync($"/api/staff/{sana.Id}/status",
            new { status = "Resigned", effectiveDate = new DateOnly(2026, 10, 31), reason = "Moved to Karachi" })).ReadJsonAsync<StaffStatusResult>();
        Assert.Equal(2, left.RemovedAssignments);
        Assert.Empty(left.Staff.ClassTeacherOf);
        Assert.Empty(left.Staff.Teaches);
        Assert.Equal(new DateOnly(2026, 10, 31), left.Staff.ExitDate);
        var current = await owner.GetJsonAsync<PagedResult<StaffListItemDto>>("/api/staff");
        Assert.DoesNotContain(current.Items, x => x.Id == sana.Id);
        var leavers = await owner.GetJsonAsync<PagedResult<StaffListItemDto>>("/api/staff?scope=left");
        Assert.Contains(leavers.Items, x => x.Id == sana.Id);

        var assignLeaver = await owner.SendAsync(HttpMethod.Put, $"/api/org/teaching/sections/{s.SectionA}",
            new { classTeacherId = sana.Id, subjects = Array.Empty<object>() });
        Assert.Equal("teaching.staff_not_current", await assignLeaver.ProblemCodeAsync());

        var unassigned = await owner.SendAsync(HttpMethod.Delete, $"/api/staff/{bilal.Id}");
        Assert.Equal(HttpStatusCode.NoContent, unassigned.StatusCode);
    }

    [Fact]
    public async Task Staff_get_sign_in_access_with_an_honest_handover_and_can_then_sign_in()
    {
        var (school, _, owner) = await SetupAsync();
        using var _ = owner;
        var teacherRole = (await owner.GetJsonAsync<List<Application.Access.RoleListItemDto>>("/api/access/roles"))
            .Single(r => r.Key == SystemRoles.Teacher).Id;

        var noEmail = await (await owner.PostAsync("/api/staff", NewStaff(school.CampusId))).ReadJsonAsync<StaffDetailDto>();
        var refused = await owner.PostAsync($"/api/staff/{noEmail.Id}/account", new { roles = new[] { new { roleId = teacherRole } } });
        Assert.Equal("staff.email_required", await refused.ProblemCodeAsync());

        var withEmail = await (await owner.PostAsync("/api/staff", NewStaff(school.CampusId, "Hira", "Malik", "hira.malik@greenfield.test")))
            .ReadJsonAsync<StaffDetailDto>();
        var granted = await (await owner.PostAsync($"/api/staff/{withEmail.Id}/account",
            new { roles = new[] { new { roleId = teacherRole } } })).ReadJsonAsync<StaffAccessResult>();
        Assert.NotNull(granted.Staff.Account);
        Assert.Equal(["Teacher"], granted.Staff.Account!.Roles);
        // No email provider is configured in tests: nothing is claimed as sent, a one-time password is handed over instead.
        Assert.NotNull(granted.Handover);
        Assert.Equal(Application.Identity.DeliveryState.NotSent, granted.Handover!.Invitation);
        Assert.False(string.IsNullOrEmpty(granted.Handover.TemporaryPassword));

        var again = await owner.PostAsync($"/api/staff/{withEmail.Id}/account", new { roles = new[] { new { roleId = teacherRole } } });
        Assert.Equal("staff.has_account", await again.ProblemCodeAsync());

        using var teacher = Factory.CreateBrowserClient();
        await teacher.LoginAsync("hira.malik@greenfield.test", granted.Handover.TemporaryPassword!);
        var me = await teacher.GetJsonAsync<Application.Access.MeDto>("/api/auth/me");
        Assert.Contains(Portal.Teacher, me.Access!.Portals);
    }
}
