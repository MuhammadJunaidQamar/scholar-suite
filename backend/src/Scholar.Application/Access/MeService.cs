using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Scholar.Application.Abstractions;
using Scholar.Application.Identity;
using Scholar.Domain.Access;
using Scholar.Domain.Platform;

namespace Scholar.Application.Access;

/// <summary>Everything the client needs to shape the UI for the signed-in user. The API remains the authority.</summary>
public sealed class MeService(IAppDbContext db, AuthService auth, IAccessContext access)
{
    private static readonly Portal[] PortalOrder = [Portal.Admin, Portal.Teacher, Portal.Family, Portal.Student];

    public async Task<MeDto> GetAsync(CancellationToken ct)
    {
        var user = await auth.GetUserAsync(ct);
        var schools = await auth.ListMySchoolsAsync(ct);
        if (user.SchoolId is not { } schoolId)
        {
            return new MeDto(user, null, schools, null);
        }

        var school = await db.Schools.Where(s => s.Id == schoolId)
            .Select(s => new SchoolContextDto(s.Id, s.Code, s.Name, s.ShortName, s.LogoFileId, s.BrandColor, s.Timezone,
                s.CurrencyCode, s.Locale, s.EnabledModules))
            .SingleOrDefaultAsync(ct);
        var effective = await access.GetAsync(ct);
        var accessDto = new AccessDto(
            effective.Permissions.Order(StringComparer.Ordinal).ToList(),
            PortalOrder.Where(effective.Portals.Contains).ToList(),
            effective.RoleKeys,
            effective.CampusIds?.ToList());

        if (school is not null && schools.All(s => s.Id != school.Id))
        {
            // Platform administrators can work in schools they are not members of.
            schools = [.. schools, new SchoolSummaryDto(school.Id, school.Code, school.Name, school.ShortName, school.LogoFileId, school.BrandColor)];
        }

        return new MeDto(user, school, schools, accessDto);
    }
}

/// <summary>Answers whether an optional module is switched on for the current school (cached briefly).</summary>
public interface ISchoolModuleGate
{
    Task<bool> IsEnabledAsync(string module, CancellationToken ct);
    void Invalidate(Guid schoolId);
}

internal sealed class SchoolModuleGate(IAppDbContext db, ITenantContext tenant, IMemoryCache cache) : ISchoolModuleGate
{
    public async Task<bool> IsEnabledAsync(string module, CancellationToken ct)
    {
        if (SchoolModules.Core.Contains(module))
        {
            return true;
        }

        if (tenant.SchoolId is not { } schoolId)
        {
            return false;
        }

        var modules = await cache.GetOrCreateAsync($"modules:{schoolId:N}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return await db.Schools.Where(s => s.Id == schoolId).Select(s => s.EnabledModules).SingleOrDefaultAsync(ct) ?? [];
        });
        return modules!.Contains(module);
    }

    public void Invalidate(Guid schoolId) => cache.Remove($"modules:{schoolId:N}");
}
