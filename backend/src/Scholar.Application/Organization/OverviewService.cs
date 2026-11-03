using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Domain.Access;
using Scholar.Domain.Organization;

namespace Scholar.Application.Organization;

public sealed record TermDto(Guid Id, string Name, int Sequence, DateOnly StartDate, DateOnly EndDate);

public sealed record CurrentYearDto(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, TermDto? CurrentTerm,
    int? WeekOfTerm);

public sealed record UpcomingEventDto(Guid Id, string Title, CalendarEventType Type, DateOnly StartDate, DateOnly EndDate,
    bool IsHoliday);

public sealed record SchoolCountsDto(int Campuses, int Grades, int Sections, int Subjects, int Rooms, int Members);

public sealed record CampusListItemDto(Guid Id, string Code, string Name, bool IsMain, bool IsActive, string? City);

public sealed record SetupStepDto(string Key, string Title, string Description, bool Done, string Href);

public sealed record OrganizationOverviewDto(
    DateOnly Today,
    CurrentYearDto? CurrentYear,
    SchoolCountsDto Counts,
    IReadOnlyList<UpcomingEventDto> Upcoming,
    IReadOnlyList<SetupStepDto> Setup);

/// <summary>The school at a glance: today's place in the academic calendar, structure counts and setup progress.</summary>
public sealed class OverviewService(IAppDbContext db, IAccessContext access, ISchoolClock clock)
{
    public async Task<OrganizationOverviewDto> GetAsync(CancellationToken ct)
    {
        await access.RequireAsync(Permissions.School.View, ct);
        var today = await clock.TodayAsync(ct);

        var year = await db.AcademicYears.Include(y => y.Terms).SingleOrDefaultAsync(y => y.IsCurrent, ct);
        CurrentYearDto? current = null;
        if (year is not null)
        {
            var term = year.Terms.OrderBy(t => t.Sequence).FirstOrDefault(t => today >= t.StartDate && today <= t.EndDate);
            int? week = term is null ? null : (today.DayNumber - term.StartDate.DayNumber) / 7 + 1;
            current = new CurrentYearDto(year.Id, year.Name, year.StartDate, year.EndDate,
                term is null ? null : new TermDto(term.Id, term.Name, term.Sequence, term.StartDate, term.EndDate), week);
        }

        var counts = new SchoolCountsDto(
            await db.Campuses.CountAsync(c => c.IsActive, ct),
            await db.Grades.CountAsync(g => g.IsActive, ct),
            year is null ? 0 : await db.Sections.CountAsync(s => s.AcademicYearId == year.Id && s.IsActive, ct),
            await db.Subjects.CountAsync(s => s.IsActive, ct),
            await db.Rooms.CountAsync(r => r.IsActive, ct),
            await db.SchoolMemberships.CountAsync(ct));

        var upcoming = await db.CalendarEvents
            .Where(e => e.EndDate >= today)
            .OrderBy(e => e.StartDate).Take(6)
            .Select(e => new UpcomingEventDto(e.Id, e.Title, e.Type, e.StartDate, e.EndDate, e.IsHoliday))
            .ToListAsync(ct);

        var school = await db.Schools.SingleAsync(ct);
        var gradeSubjects = year is not null && await db.GradeSubjects.AnyAsync(g => g.AcademicYearId == year.Id, ct);
        var setup = new List<SetupStepDto>
        {
            new("profile", "School profile", "Name, address, contact details and crest.",
                school.Address.City is not null && school.Phone is not null, "/settings/school"),
            new("year", "Academic year", "The current year and its terms.", year is not null && year.Terms.Count > 0, "/settings/academic-years"),
            new("structure", "Grades and sections", "Levels of study and this year's sections.", counts.Sections > 0, "/settings/grades"),
            new("curriculum", "Subjects and curriculum", "What each grade studies.", gradeSubjects, "/settings/subjects"),
            new("calendar", "Calendar", "Holidays and key dates.", upcoming.Count > 0, "/settings/calendar"),
            new("people", "Staff accounts", "Invite the people who run the school.", counts.Members > 1, "/settings/users"),
        };

        return new OrganizationOverviewDto(today, current, counts, upcoming, setup);
    }

    /// <summary>Campus list used across the app (filters, scoping, assignment pickers).</summary>
    public async Task<IReadOnlyList<CampusListItemDto>> ListCampusesAsync(CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        if (!current.HasAny(Permissions.School.View, Permissions.Academics.View, Permissions.Users.View,
                Permissions.Students.View, Permissions.Staff.View))
        {
            throw new Common.ForbiddenException();
        }

        return await db.Campuses.OrderByDescending(c => c.IsMain).ThenBy(c => c.Name)
            .Select(c => new CampusListItemDto(c.Id, c.Code, c.Name, c.IsMain, c.IsActive, c.Address.City))
            .ToListAsync(ct);
    }
}
