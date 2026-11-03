using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Scholar.Application.Access;
using Scholar.Application.Platform;
using Scholar.Domain.Access;
using Scholar.IntegrationTests.Infrastructure;

namespace Scholar.IntegrationTests.Access;

public sealed class AuthorizationTests(ApiFactory factory) : IntegrationTest(factory)
{
    private async Task<BrowserClient> SignInAsync(string email)
    {
        var browser = Factory.CreateBrowserClient();
        await browser.LoginAsync(email, TestData.Password);
        return browser;
    }

    [Fact]
    public async Task Owner_has_every_permission_and_teacher_only_teacher_rights()
    {
        var school = await Factory.ProvisionSchoolAsync();
        var (_, teacherEmail) = await Factory.AddMemberAsync(school, SystemRoles.Teacher);

        using var owner = await SignInAsync(school.Email);
        var ownerMe = await owner.GetJsonAsync<MeDto>("/api/auth/me");
        Assert.Equal(PermissionCatalog.Keys.Count, ownerMe.Access!.Permissions.Count);
        Assert.Equal(Portal.Admin, ownerMe.Access.Portals[0]);
        Assert.Null(ownerMe.Access.CampusIds);
        Assert.Equal("PKR", ownerMe.School!.CurrencyCode);

        using var teacher = await SignInAsync(teacherEmail);
        var teacherMe = await teacher.GetJsonAsync<MeDto>("/api/auth/me");
        Assert.Contains(Permissions.Attendance.Record, teacherMe.Access!.Permissions);
        Assert.DoesNotContain(Permissions.Fees.Collect, teacherMe.Access.Permissions);
        Assert.Equal([Portal.Teacher], teacherMe.Access.Portals);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync("/api/access/roles")).StatusCode);
    }

    [Fact]
    public async Task Owner_sees_the_seeded_system_roles()
    {
        var school = await Factory.ProvisionSchoolAsync();
        using var owner = await SignInAsync(school.Email);

        var roles = await owner.GetJsonAsync<List<RoleListItemDto>>("/api/access/roles");

        Assert.Equal(SystemRoles.All.Count, roles.Count);
        var ownerRole = Assert.Single(roles, r => r.Key == SystemRoles.Owner);
        Assert.True(ownerRole.IsLocked);
        Assert.Equal(1, ownerRole.MemberCount);
    }

    [Fact]
    public async Task Administrators_cannot_escalate_privileges()
    {
        var school = await Factory.ProvisionSchoolAsync();
        var (_, adminEmail) = await Factory.AddMemberAsync(school, SystemRoles.Administrator);
        var (teacherId, _) = await Factory.AddMemberAsync(school, SystemRoles.Teacher);
        using var admin = await SignInAsync(adminEmail);
        var roles = await admin.GetJsonAsync<List<RoleListItemDto>>("/api/access/roles");
        var ownerRoleId = roles.Single(r => r.Key == SystemRoles.Owner).Id;
        var teacherRoleId = roles.Single(r => r.Key == SystemRoles.Teacher).Id;

        // Cannot hand out the owner role…
        var promote = await admin.SendAsync(HttpMethod.Put, $"/api/access/members/{teacherId}/roles",
            new { roles = new[] { new { roleId = ownerRoleId, campusId = (Guid?)null } } });
        Assert.Equal(HttpStatusCode.Forbidden, promote.StatusCode);

        // …nor create a role with permissions they do not hold (school admins lack payroll approval).
        var escalate = await admin.PostAsync("/api/access/roles",
            new { name = "Payroll signer", portal = "Admin", permissions = new[] { Permissions.Payroll.Approve } });
        Assert.Equal(HttpStatusCode.Forbidden, escalate.StatusCode);

        // Within their own rights they can work normally.
        var create = await admin.PostAsync("/api/access/roles",
            new { name = "Front desk", portal = "Admin", permissions = new[] { Permissions.Students.View, Permissions.Guardians.View } });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var reassign = await admin.SendAsync(HttpMethod.Put, $"/api/access/members/{teacherId}/roles",
            new { roles = new[] { new { roleId = teacherRoleId, campusId = (Guid?)null } } });
        await reassign.EnsureSuccessWithBodyAsync();
    }

    [Fact]
    public async Task Campus_scoped_staff_are_limited_to_their_campus()
    {
        var school = await Factory.ProvisionSchoolAsync();
        var (_, viceEmail) = await Factory.AddMemberAsync(school, SystemRoles.VicePrincipal, school.CampusId);
        using var vice = await SignInAsync(viceEmail);

        var me = await vice.GetJsonAsync<MeDto>("/api/auth/me");

        Assert.Equal([school.CampusId], me.Access!.CampusIds);
    }

    [Fact]
    public async Task Ids_from_another_school_behave_as_not_found()
    {
        var alpha = await Factory.ProvisionSchoolAsync("alpha");
        var bravo = await Factory.ProvisionSchoolAsync("bravo");
        using var alphaOwner = await SignInAsync(alpha.Email);
        using var bravoOwner = await SignInAsync(bravo.Email);
        var bravoRoles = await bravoOwner.GetJsonAsync<List<RoleListItemDto>>("/api/access/roles");

        var foreignMember = await alphaOwner.GetAsync($"/api/access/members/{bravo.UserId}");
        Assert.Equal(HttpStatusCode.NotFound, foreignMember.StatusCode);

        var foreignRole = await alphaOwner.GetAsync($"/api/access/roles/{bravoRoles[0].Id}");
        Assert.Equal(HttpStatusCode.NotFound, foreignRole.StatusCode);

        var (alphaTeacher, _) = await Factory.AddMemberAsync(alpha, SystemRoles.Teacher);
        var assignForeign = await alphaOwner.SendAsync(HttpMethod.Put, $"/api/access/members/{alphaTeacher}/roles",
            new { roles = new[] { new { roleId = bravoRoles[0].Id, campusId = (Guid?)null } } });
        Assert.Equal(HttpStatusCode.NotFound, assignForeign.StatusCode);
    }

    [Fact]
    public async Task The_last_owner_cannot_be_removed()
    {
        var school = await Factory.ProvisionSchoolAsync();
        using var owner = await SignInAsync(school.Email);
        var roles = await owner.GetJsonAsync<List<RoleListItemDto>>("/api/access/roles");
        var principal = roles.Single(r => r.Key == SystemRoles.Principal).Id;

        var demote = await owner.SendAsync(HttpMethod.Put, $"/api/access/members/{school.UserId}/roles",
            new { roles = new[] { new { roleId = principal, campusId = (Guid?)null } } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, demote.StatusCode);
        Assert.Equal("member.last_owner", await demote.ProblemCodeAsync());
    }

    [Fact]
    public async Task Permission_changes_apply_immediately_and_suspension_ends_sessions()
    {
        var school = await Factory.ProvisionSchoolAsync();
        using var owner = await SignInAsync(school.Email);
        var create = await owner.PostAsync("/api/access/roles",
            new { name = "Auditor", portal = "Admin", permissions = new[] { Permissions.Roles.View } });
        var role = await create.ReadJsonAsync<RoleDetailDto>();
        var invite = await owner.PostAsync("/api/access/members", new
        {
            firstName = "Iqra", lastName = "Auditor", email = "iqra@greenfield.test",
            roles = new[] { new { roleId = role.Id, campusId = (Guid?)null } },
        });
        var member = await invite.ReadJsonAsync<MemberListItemDto>();
        await SetPasswordAsync(member.UserId);

        using var auditor = await SignInAsync("iqra@greenfield.test");
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync("/api/access/roles")).StatusCode);

        var revoke = await owner.SendAsync(HttpMethod.Put, $"/api/access/roles/{role.Id}",
            new { name = "Auditor", portal = "Admin", permissions = Array.Empty<string>() });
        await revoke.EnsureSuccessWithBodyAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync("/api/access/roles")).StatusCode);

        await (await owner.PostAsync($"/api/access/members/{member.UserId}/suspend", new { })).EnsureSuccessWithBodyAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await auditor.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Only_platform_administrators_can_provision_schools()
    {
        var school = await Factory.ProvisionSchoolAsync();
        using var owner = await SignInAsync(school.Email);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/api/platform/schools")).StatusCode);

        var platformEmail = await Factory.CreatePlatformAdminAsync();
        using var platform = await SignInAsync(platformEmail);
        var created = await platform.PostAsync("/api/platform/schools", new
        {
            code = "riverside", name = "Riverside Academy", mainCampusName = "Model Town",
            owner = new { firstName = "Rafay", lastName = "Owner", email = "rafay@riverside.test", password = TestData.Password },
        });
        var dto = await created.ReadJsonAsync<PlatformSchoolDto>();
        Assert.Equal(1, dto.CampusCount);
        Assert.Equal(1, dto.MemberCount);

        using var newOwner = await SignInAsync("rafay@riverside.test");
        var me = await newOwner.GetJsonAsync<MeDto>("/api/auth/me");
        Assert.Equal("riverside", me.School!.Code);
    }

    private async Task SetPasswordAsync(Guid userId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Domain.Identity.User>>();
        var user = await users.FindByIdAsync(userId.ToString());
        await users.AddPasswordAsync(user!, TestData.Password);
    }
}
