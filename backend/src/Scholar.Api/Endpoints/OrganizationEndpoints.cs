using Scholar.Api.Auth;
using Scholar.Application.Organization;
using Scholar.Domain.Access;

namespace Scholar.Api.Endpoints;

internal static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this IEndpointRouteBuilder api)
    {
        var org = api.MapGroup("/org").WithTags("School configuration");

        org.MapGet("/overview", (OverviewService service, CancellationToken ct) => service.GetAsync(ct))
            .RequirePermission(Permissions.School.View)
            .WithSummary("Today's place in the academic calendar, structure counts, upcoming dates and setup progress.");

        org.MapGet("/campuses", (OverviewService service, CancellationToken ct) => service.ListCampusesAsync(ct))
            .WithSummary("Campuses of the school.");
    }
}
