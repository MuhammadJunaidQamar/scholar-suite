using System.Net;
using Scholar.Application.Access;
using Scholar.Application.Common;
using Scholar.Application.Organization;
using Scholar.IntegrationTests.Infrastructure;

namespace Scholar.IntegrationTests.Access;

public sealed class ListEndpointTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task List_endpoints_accept_omitted_paging_and_filters()
    {
        var school = await Factory.ProvisionSchoolAsync();
        using var owner = Factory.CreateBrowserClient();
        await owner.LoginAsync(school.Email, TestData.Password);

        var members = await owner.GetJsonAsync<PagedResult<MemberListItemDto>>("/api/access/members");
        Assert.Equal(1, members.Page);
        Assert.Equal(25, members.PageSize);
        Assert.Single(members.Items);

        var audit = await owner.GetJsonAsync<PagedResult<AuditEntryDto>>("/api/access/audit?pageSize=5&changesOnly=true");
        Assert.InRange(audit.Items.Count, 1, 5);
        Assert.All(audit.Items, a => Assert.Contains(a.Action, new[] { "Created", "Updated", "Deleted" }));

        var tooBig = await owner.GetJsonAsync<PagedResult<AuditEntryDto>>("/api/access/audit?pageSize=5000");
        Assert.Equal(200, tooBig.PageSize);
    }

    [Fact]
    public async Task Overview_and_campuses_reflect_the_provisioned_school()
    {
        var school = await Factory.ProvisionSchoolAsync();
        using var owner = Factory.CreateBrowserClient();
        await owner.LoginAsync(school.Email, TestData.Password);

        var overview = await owner.GetJsonAsync<OrganizationOverviewDto>("/api/org/overview");
        Assert.Equal(1, overview.Counts.Campuses);
        Assert.Equal(1, overview.Counts.Members);
        Assert.Contains(overview.Setup, s => s.Key == "year" && !s.Done);

        var campuses = await owner.GetJsonAsync<List<CampusListItemDto>>("/api/org/campuses");
        Assert.True(Assert.Single(campuses).IsMain);
    }

    [Fact]
    public async Task Audit_trail_is_restricted()
    {
        var school = await Factory.ProvisionSchoolAsync();
        var (_, teacher) = await Factory.AddMemberAsync(school, Domain.Access.SystemRoles.Teacher);
        using var browser = Factory.CreateBrowserClient();
        await browser.LoginAsync(teacher, TestData.Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.GetAsync("/api/access/audit")).StatusCode);
    }
}
