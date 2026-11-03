using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Domain.Platform;

namespace Scholar.Application.Attendance;

/// <summary>
/// The school's attendance calendar: which days are school days for a campus (working days minus holidays), what
/// "today" is in the school's timezone, and when a day's registers lock. Scoped, so settings are read once per request.
/// </summary>
public sealed class AttendanceCalendar(IAppDbContext db, ISchoolClock clock, ITenantContext tenant, TimeProvider time)
{
    private SchoolSettings? _settings;
    private (DateOnly From, DateOnly To, List<Holiday> Items)? _holidays;

    private sealed record Holiday(DateOnly Start, DateOnly End, Guid? CampusId, string Title);

    public async Task<SchoolSettings> SettingsAsync(CancellationToken ct)
    {
        if (_settings is not null) return _settings;
        var schoolId = tenant.SchoolId ?? throw new InvalidOperationException("No school in context.");
        _settings = await db.Schools.Where(s => s.Id == schoolId).Select(s => s.Settings).SingleAsync(ct);
        return _settings;
    }

    public Task<DateOnly> TodayAsync(CancellationToken ct) => clock.TodayAsync(ct);

    public DateTimeOffset Now => time.GetUtcNow();

    /// <summary>Why <paramref name="date"/> is not a school day at the campus, or null when it is one.</summary>
    public async Task<Func<DateOnly, Guid, string?>> ClosedReasonsAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var settings = await SettingsAsync(ct);
        var working = settings.WorkingDays.ToHashSet();
        var holidays = await HolidaysAsync(from, to, ct);
        return (date, campusId) =>
        {
            var holiday = holidays.FirstOrDefault(h => date >= h.Start && date <= h.End && (h.CampusId is null || h.CampusId == campusId));
            if (holiday is not null) return $"Holiday: {holiday.Title}";
            return working.Contains(date.DayOfWeek) ? null : $"{date.DayOfWeek} is not a working day";
        };
    }

    public async Task<DayStatusDto> DescribeAsync(DateOnly date, Guid campusId, CancellationToken ct)
    {
        var today = await TodayAsync(ct);
        var closed = (await ClosedReasonsAsync(date, date, ct))(date, campusId);
        return new DayStatusDto(date, closed is null, closed, date > today, date == today);
    }

    /// <summary>School days for the campus in a range (inclusive).</summary>
    public async Task<List<DateOnly>> SchoolDaysAsync(DateOnly from, DateOnly to, Guid campusId, CancellationToken ct)
    {
        var closed = await ClosedReasonsAsync(from, to, ct);
        var days = new List<DateOnly>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (closed(d, campusId) is null) days.Add(d);
        }

        return days;
    }

    /// <summary>When registers for <paramref name="date"/> stop being editable: the end of that day plus the school's lock window.</summary>
    public async Task<DateTimeOffset> LocksAtAsync(DateOnly date, CancellationToken ct)
    {
        var settings = await SettingsAsync(ct);
        var zone = await clock.TimeZoneAsync(ct);
        var endOfDay = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(endOfDay, zone);
        return new DateTimeOffset(utc, TimeSpan.Zero).AddHours(settings.Attendance.LockAfterHours);
    }

    public async Task<bool> IsLockedAsync(DateOnly date, CancellationToken ct) => Now >= await LocksAtAsync(date, ct);

    /// <summary>A moment as people at the school would say it ("3 Nov, 12:00 AM").</summary>
    public async Task<string> DescribeMomentAsync(DateTimeOffset moment, CancellationToken ct)
    {
        var local = TimeZoneInfo.ConvertTime(moment, await clock.TimeZoneAsync(ct));
        return local.ToString("d MMM, h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The share of counted days attended; leave is either attended or left out, per the school's policy.</summary>
    public static decimal? Percentage(int present, int late, int absent, int leave, bool leaveCountsAsPresent)
    {
        var attended = present + late + (leaveCountsAsPresent ? leave : 0);
        var counted = present + late + absent + (leaveCountsAsPresent ? leave : 0);
        return counted == 0 ? null : Math.Round(100m * attended / counted, 1);
    }

    private async Task<List<Holiday>> HolidaysAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (_holidays is { } cached && cached.From <= from && cached.To >= to) return cached.Items;
        var items = await db.CalendarEvents.Where(e => e.IsHoliday && e.StartDate <= to && e.EndDate >= from)
            .Select(e => new Holiday(e.StartDate, e.EndDate, e.CampusId, e.Title)).ToListAsync(ct);
        _holidays = (from, to, items);
        return items;
    }
}
