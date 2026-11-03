using Microsoft.Extensions.DependencyInjection;
using Scholar.Application.Access;
using Scholar.Application.Identity;
using Scholar.Application.Students;
using Scholar.Domain.Access;
using Scholar.IntegrationTests.Infrastructure;

namespace Scholar.IntegrationTests.Access;

/// <summary>New accounts are handed over truthfully: nothing claims an e-mail went out when no provider sent it.</summary>
public sealed class OnboardingTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Staff_invitations_report_that_nothing_was_sent_and_give_a_one_time_password()
    {
        var school = await Factory.ProvisionSchoolAsync();
        using var owner = Factory.CreateBrowserClient();
        await owner.LoginAsync(school.Email, TestData.Password);
        var role = await (await owner.PostAsync("/api/access/roles",
            new { name = "Front office", portal = "Admin", permissions = new[] { Permissions.Students.View } })).ReadJsonAsync<RoleDetailDto>();

        var result = await (await owner.PostAsync("/api/access/members", new
        {
            firstName = "Sana", lastName = "Office", email = "sana@greenfield.test",
            roles = new[] { new { roleId = role.Id, campusId = (Guid?)null } },
        })).ReadJsonAsync<InviteMemberResult>();

        var handover = Assert.IsType<AccountHandover>(result.Handover);
        Assert.Equal(DeliveryState.NotSent, handover.Invitation);
        Assert.Null(handover.SentTo);
        Assert.NotNull(handover.Detail);
        Assert.False(string.IsNullOrWhiteSpace(handover.TemporaryPassword));

        // The one-time password works once, and the person must choose their own straight away.
        using var sana = Factory.CreateBrowserClient();
        await sana.LoginAsync("sana@greenfield.test", handover.TemporaryPassword!);
        var me = await sana.GetJsonAsync<MeDto>("/api/auth/me");
        Assert.True(me.User.MustChangePassword);
    }

    [Fact]
    public async Task Sign_in_pages_learn_that_reset_links_cannot_be_emailed()
    {
        using var anonymous = Factory.CreateBrowserClient();
        var options = await anonymous.GetJsonAsync<Api.Endpoints.SignInOptionsResponse>("/api/auth/options");
        Assert.False(options.PasswordResetByEmail);
    }

    [Fact]
    public async Task Administrators_can_issue_a_one_time_password_within_their_own_authority()
    {
        var school = await Factory.ProvisionSchoolAsync();
        var (teacherId, teacherEmail) = await Factory.AddMemberAsync(school, SystemRoles.Teacher);
        var (_, adminEmail) = await Factory.AddMemberAsync(school, SystemRoles.Administrator);
        using var teacher = Factory.CreateBrowserClient();
        await teacher.LoginAsync(teacherEmail, TestData.Password);
        using var admin = Factory.CreateBrowserClient();
        await admin.LoginAsync(adminEmail, TestData.Password);

        var handover = await (await admin.PostAsync($"/api/access/members/{teacherId}/reset-password", new { })).ReadJsonAsync<AccountHandover>();
        Assert.False(string.IsNullOrWhiteSpace(handover.TemporaryPassword));

        // The teacher is signed out everywhere; the old password no longer works and the new one must be changed.
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await teacher.GetAsync("/api/auth/me")).StatusCode);
        using var again = Factory.CreateBrowserClient();
        await again.LoginAsync(teacherEmail, handover.TemporaryPassword!);
        Assert.True((await again.GetJsonAsync<MeDto>("/api/auth/me")).User.MustChangePassword);

        // An administrator can't take over the owner's account, or reset their own this way.
        var owner = await admin.PostAsync($"/api/access/members/{school.UserId}/reset-password", new { });
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, owner.StatusCode);
        var me = await admin.GetJsonAsync<MeDto>("/api/auth/me");
        Assert.Equal("member.self", await (await admin.PostAsync($"/api/access/members/{me.User.Id}/reset-password", new { })).ProblemCodeAsync());
    }

    [Fact]
    public async Task Accounts_shared_with_another_school_cannot_be_reset_by_either_school()
    {
        var alpha = await Factory.ProvisionSchoolAsync("alpha");
        var bravo = await Factory.ProvisionSchoolAsync("bravo");
        var (teacherId, _) = await Factory.AddMemberAsync(alpha, SystemRoles.Teacher);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Scholar.Infrastructure.Persistence.AppDbContext>();
            var tenant = scope.ServiceProvider.GetRequiredService<Application.Abstractions.ITenantContext>();
            using (tenant.Enter(bravo.SchoolId))
            {
                db.SchoolMemberships.Add(new Domain.Identity.SchoolMembership(bravo.SchoolId, teacherId));
                await db.SaveChangesAsync();
            }
        }

        using var owner = Factory.CreateBrowserClient();
        await owner.LoginAsync(alpha.Email, TestData.Password);
        var refused = await owner.PostAsync($"/api/access/members/{teacherId}/reset-password", new { });
        Assert.Equal("member.shared_account", await refused.ProblemCodeAsync());
    }

    [Fact]
    public async Task Family_portal_logins_are_handed_over_the_same_way()
    {
        var school = await Factory.ProvisionSchoolAsync();
        using var owner = Factory.CreateBrowserClient();
        await owner.LoginAsync(school.Email, TestData.Password);
        var guardian = await (await owner.PostAsync("/api/guardians", new
        {
            firstName = "Imran", lastName = "Khan", phone = "0300 7654321", notifyBySms = true, notifyByEmail = false, notifyByWhatsApp = true,
        })).ReadJsonAsync<GuardianDetailDto>();

        var access = await (await owner.PostAsync($"/api/guardians/{guardian.Id}/portal", new { })).ReadJsonAsync<PortalAccessResult>();
        var handover = Assert.IsType<AccountHandover>(access.Handover);
        Assert.Equal(DeliveryState.NotSent, handover.Invitation);
        Assert.StartsWith("p92300", access.LoginName, StringComparison.Ordinal);

        using var parent = Factory.CreateBrowserClient();
        await parent.LoginAsync(access.LoginName, handover.TemporaryPassword!);
        Assert.True((await parent.GetJsonAsync<MeDto>("/api/auth/me")).User.MustChangePassword);

        var again = await owner.PostAsync($"/api/guardians/{guardian.Id}/portal", new { });
        Assert.Equal("guardian.portal_exists", await again.ProblemCodeAsync());

        // A parent who forgets the password gets a new one-time password from the office and is signed out.
        var reset = await (await owner.PostAsync($"/api/guardians/{guardian.Id}/portal/reset", new { })).ReadJsonAsync<AccountHandover>();
        Assert.NotEqual(handover.TemporaryPassword, reset.TemporaryPassword);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await parent.GetAsync("/api/auth/me")).StatusCode);
        using var later = Factory.CreateBrowserClient();
        await later.LoginAsync(access.LoginName, reset.TemporaryPassword!);
    }

    [Fact]
    public async Task The_front_office_cannot_reset_a_staff_login_through_the_family_portal()
    {
        var school = await Factory.ProvisionSchoolAsync();
        var (_, teacherEmail) = await Factory.AddMemberAsync(school, SystemRoles.Teacher);
        using var owner = Factory.CreateBrowserClient();
        await owner.LoginAsync(school.Email, TestData.Password);
        // The teacher is also a parent here, with the same e-mail, so the portal reuses their staff login.
        var guardian = await (await owner.PostAsync("/api/guardians", new
        {
            firstName = "Nadia", lastName = "Teacher", phone = "0301 5556667", email = teacherEmail, notifyBySms = true, notifyByEmail = true, notifyByWhatsApp = false,
        })).ReadJsonAsync<GuardianDetailDto>();
        var access = await (await owner.PostAsync($"/api/guardians/{guardian.Id}/portal", new { })).ReadJsonAsync<PortalAccessResult>();
        Assert.Null(access.Handover); // existing account: nothing to hand over

        var refused = await owner.PostAsync($"/api/guardians/{guardian.Id}/portal/reset", new { });
        Assert.Equal("guardian.shared_account", await refused.ProblemCodeAsync());
        using var teacher = Factory.CreateBrowserClient();
        await teacher.LoginAsync(teacherEmail, TestData.Password); // unchanged
    }
}
