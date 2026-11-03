using Microsoft.EntityFrameworkCore;

namespace Scholar.Application.Abstractions;

/// <summary>"Today" and "now" in the school's own timezone (attendance, due dates and reports are local concepts).</summary>
public interface ISchoolClock
{
    Task<DateOnly> TodayAsync(CancellationToken ct = default);
    Task<TimeZoneInfo> TimeZoneAsync(CancellationToken ct = default);
}

internal sealed class SchoolClock(IAppDbContext db, ITenantContext tenant, TimeProvider clock) : ISchoolClock
{
    private TimeZoneInfo? _zone;

    public async Task<DateOnly> TodayAsync(CancellationToken ct = default)
    {
        var zone = await TimeZoneAsync(ct);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
    }

    public async Task<TimeZoneInfo> TimeZoneAsync(CancellationToken ct = default)
    {
        if (_zone is not null)
        {
            return _zone;
        }

        var id = tenant.SchoolId is { } schoolId
            ? await db.Schools.Where(s => s.Id == schoolId).Select(s => s.Timezone).SingleOrDefaultAsync(ct)
            : null;
        _zone = id is not null && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) ? zone : TimeZoneInfo.Utc;
        return _zone;
    }
}
