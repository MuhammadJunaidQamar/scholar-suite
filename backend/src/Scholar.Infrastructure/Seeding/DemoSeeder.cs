using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Scholar.Application.Abstractions;
using Scholar.Application.Platform;
using Scholar.Domain.Access;
using Scholar.Domain.Common;
using Scholar.Domain.Identity;
using Scholar.Domain.Organization;
using Scholar.Domain.Platform;
using Scholar.Infrastructure.Persistence;

namespace Scholar.Infrastructure.Seeding;

/// <summary>
/// Development-only demo data: a realistic Lahore school with two campuses. Idempotent — every part checks before it
/// creates. The demo password comes from configuration (<c>Seed:DemoPassword</c>) and is never committed.
/// </summary>
public sealed partial class DemoSeeder(
    AppDbContext db,
    ITenantContext tenant,
    SchoolProvisioningService provisioning,
    UserManager<User> users,
    IConfiguration configuration,
    TimeProvider clock,
    ILogger<DemoSeeder> logger)
{
    public const string SchoolCode = "greenfield";
    private string _password = string.Empty;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        _password = configuration["Seed:DemoPassword"] ?? GeneratePassword();
        await SeedPlatformAdminAsync();
        var school = await EnsureSchoolAsync(ct);
        using (tenant.Enter(school.Id))
        {
            var context = await SeedOrganizationAsync(school, ct);
            await SeedStaffAccountsAsync(school, context, ct);
            await SeedModulesAsync(school, context, ct);
        }

        logger.LogInformation("Demo data ready. Sign in as owner@greenfield.example (or principal@, accountant@ …) with the configured demo password");
    }

    /// <summary>Extension point: later modules add their demo data here (students, fees, attendance…).</summary>
    private partial Task SeedModulesAsync(School school, SeedContext context, CancellationToken ct);

    private async Task SeedPlatformAdminAsync()
    {
        const string email = "platform@scholar.example";
        if (await users.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var admin = new User(email, "Platform", "Administrator", email) { IsPlatformAdmin = true };
        await CreateUserAsync(admin);
    }

    private async Task<School> EnsureSchoolAsync(CancellationToken ct)
    {
        var existing = await db.Schools.SingleOrDefaultAsync(s => s.Code == SchoolCode, ct);
        if (existing is not null)
        {
            return existing;
        }

        await provisioning.ProvisionAsync(new ProvisionSchoolRequest(SchoolCode, "Greenfield Grammar School", "Greenfield",
            "Gulberg Campus", new ProvisionOwnerRequest("Farah", "Siddiqui", "owner@greenfield.example", _password)), ct);

        var school = await db.Schools.SingleAsync(s => s.Code == SchoolCode, ct);
        school.LegalName = "Greenfield Educational Trust";
        school.Motto = "Learning for life";
        school.Email = "info@greenfield.example";
        school.Phone = "+92 42 3571 0000";
        school.Website = "https://greenfield.example";
        school.RegistrationNumber = "PEIRA-LHR-2011-0417";
        school.BrandColor = "#1F5F4A";
        school.Address = new Address
        {
            Line1 = "21-B, Main Boulevard", Line2 = "Gulberg III", City = "Lahore", Province = "Punjab",
            PostalCode = "54660", CountryCode = "PK",
        };
        await db.SaveChangesAsync(ct);
        return school;
    }

    public sealed record SeedContext(
        Campus Main,
        Campus Dha,
        AcademicYear CurrentYear,
        AcademicYear PreviousYear,
        IReadOnlyList<Grade> Grades,
        IReadOnlyList<Section> Sections,
        IReadOnlyDictionary<string, Subject> Subjects);

    private async Task<SeedContext> SeedOrganizationAsync(School school, CancellationToken ct)
    {
        var main = await db.Campuses.SingleAsync(c => c.IsMain, ct);
        if (main.Address.City is null)
        {
            main.Address = school.Address;
            main.Phone = school.Phone;
        }

        var dha = await db.Campuses.SingleOrDefaultAsync(c => c.Code == "DHA", ct);
        if (dha is null)
        {
            dha = new Campus(school.Id, "DHA", "DHA Phase 5 Campus")
            {
                Phone = "+92 42 3589 2200",
                Address = new Address { Line1 = "Plot 112, Block C", Line2 = "DHA Phase 5", City = "Lahore", Province = "Punjab", PostalCode = "54792", CountryCode = "PK" },
            };
            db.Campuses.Add(dha);
        }

        var previous = await db.AcademicYears.SingleOrDefaultAsync(y => y.Name == "2025–26", ct);
        if (previous is null)
        {
            previous = new AcademicYear(school.Id, "2025–26", new DateOnly(2025, 8, 1), new DateOnly(2026, 7, 31));
            previous.AddTerm("First Term", new DateOnly(2025, 8, 4), new DateOnly(2025, 12, 19));
            previous.AddTerm("Second Term", new DateOnly(2026, 1, 5), new DateOnly(2026, 6, 5));
            db.AcademicYears.Add(previous);
        }

        var current = await db.AcademicYears.SingleOrDefaultAsync(y => y.Name == "2026–27", ct);
        if (current is null)
        {
            current = new AcademicYear(school.Id, "2026–27", new DateOnly(2026, 8, 1), new DateOnly(2027, 7, 31));
            current.AddTerm("First Term", new DateOnly(2026, 8, 3), new DateOnly(2026, 12, 18));
            current.AddTerm("Second Term", new DateOnly(2027, 1, 4), new DateOnly(2027, 6, 4));
            db.AcademicYears.Add(current);
            await db.SaveChangesAsync(ct);
            current.MakeCurrent();
            previous.ClearCurrent();
            previous.Close();
        }

        await db.SaveChangesAsync(ct);

        var grades = await SeedGradesAsync(school, ct);
        var rooms = await SeedRoomsAsync(school, main, dha, ct);
        var sections = await SeedSectionsAsync(school, current, main, dha, grades, rooms, ct);
        var subjects = await SeedSubjectsAsync(school, ct);
        await SeedCurriculumAsync(school, current, grades, subjects, ct);
        await SeedCalendarAsync(school, ct);
        return new SeedContext(main, dha, current, previous, grades, sections, subjects);
    }

    private async Task<List<Grade>> SeedGradesAsync(School school, CancellationToken ct)
    {
        var existing = await db.Grades.OrderBy(g => g.LevelOrder).ToListAsync(ct);
        if (existing.Count > 0)
        {
            return existing;
        }

        (string Name, string Code, SchoolStage Stage)[] levels =
        [
            ("Playgroup", "PG", SchoolStage.EarlyYears), ("Nursery", "NUR", SchoolStage.EarlyYears), ("Prep", "PREP", SchoolStage.EarlyYears),
            ("Class 1", "C1", SchoolStage.Primary), ("Class 2", "C2", SchoolStage.Primary), ("Class 3", "C3", SchoolStage.Primary),
            ("Class 4", "C4", SchoolStage.Primary), ("Class 5", "C5", SchoolStage.Primary), ("Class 6", "C6", SchoolStage.Middle),
            ("Class 7", "C7", SchoolStage.Middle), ("Class 8", "C8", SchoolStage.Middle), ("Class 9", "C9", SchoolStage.Secondary),
            ("Class 10", "C10", SchoolStage.Secondary),
        ];
        var grades = levels.Select((l, i) => new Grade(school.Id, l.Name, l.Code, i + 1, l.Stage)).ToList();
        for (var i = 0; i < grades.Count - 1; i++)
        {
            grades[i].NextGradeId = grades[i + 1].Id;
        }

        db.Grades.AddRange(grades);
        await db.SaveChangesAsync(ct);
        return grades;
    }

    private async Task<List<Room>> SeedRoomsAsync(School school, Campus main, Campus dha, CancellationToken ct)
    {
        var existing = await db.Rooms.ToListAsync(ct);
        if (existing.Count > 0)
        {
            return existing;
        }

        var rooms = new List<Room>();
        for (var i = 1; i <= 24; i++)
        {
            var floor = i <= 8 ? "Ground" : i <= 16 ? "First" : "Second";
            rooms.Add(new Room(school.Id, main.Id, $"G-{100 + i}", $"Room {100 + i}", RoomType.Classroom, 35) { Building = "Main Block", Floor = floor });
        }

        for (var i = 1; i <= 8; i++)
        {
            rooms.Add(new Room(school.Id, dha.Id, $"D-{10 + i}", $"Room {10 + i}", RoomType.Classroom, 30) { Building = "Junior Block", Floor = i <= 4 ? "Ground" : "First" });
        }

        rooms.AddRange(
        [
            new Room(school.Id, main.Id, "LAB-PHY", "Physics Laboratory", RoomType.Laboratory, 30) { Building = "Science Block", IsBookable = true },
            new Room(school.Id, main.Id, "LAB-CHEM", "Chemistry Laboratory", RoomType.Laboratory, 30) { Building = "Science Block", IsBookable = true },
            new Room(school.Id, main.Id, "LAB-BIO", "Biology Laboratory", RoomType.Laboratory, 30) { Building = "Science Block", IsBookable = true },
            new Room(school.Id, main.Id, "LAB-CS", "Computer Lab", RoomType.ComputerLab, 32) { Building = "Science Block", IsBookable = true },
            new Room(school.Id, main.Id, "LIB", "Library", RoomType.Library, 60) { Building = "Main Block", IsBookable = true },
            new Room(school.Id, main.Id, "HALL", "Auditorium", RoomType.Hall, 400) { Building = "Main Block", IsBookable = true },
            new Room(school.Id, main.Id, "GYM", "Sports Hall", RoomType.Sports, 120) { IsBookable = true },
            new Room(school.Id, dha.Id, "D-ACT", "Activity Room", RoomType.Hall, 60) { Building = "Junior Block", IsBookable = true },
        ]);
        db.Rooms.AddRange(rooms);
        await db.SaveChangesAsync(ct);
        return rooms;
    }

    private async Task<List<Section>> SeedSectionsAsync(School school, AcademicYear year, Campus main, Campus dha,
        IReadOnlyList<Grade> grades, IReadOnlyList<Room> rooms, CancellationToken ct)
    {
        var existing = await db.Sections.Where(s => s.AcademicYearId == year.Id).ToListAsync(ct);
        if (existing.Count > 0)
        {
            return existing;
        }

        var mainRooms = new Queue<Room>(rooms.Where(r => r.CampusId == main.Id && r.Type == RoomType.Classroom));
        var dhaRooms = new Queue<Room>(rooms.Where(r => r.CampusId == dha.Id && r.Type == RoomType.Classroom));
        var sections = new List<Section>();
        foreach (var grade in grades)
        {
            var mainSections = grade.LevelOrder switch { <= 3 => new[] { "A" }, <= 8 => new[] { "A", "B" }, _ => new[] { "A", "B" } };
            foreach (var name in mainSections)
            {
                sections.Add(new Section(school.Id, year.Id, main.Id, grade.Id, name, 32) { RoomId = mainRooms.TryDequeue(out var r) ? r.Id : null });
            }

            if (grade.LevelOrder <= 8)
            {
                sections.Add(new Section(school.Id, year.Id, dha.Id, grade.Id, "A", 28) { RoomId = dhaRooms.TryDequeue(out var r) ? r.Id : null });
            }
        }

        db.Sections.AddRange(sections);
        await db.SaveChangesAsync(ct);
        return sections;
    }

    private async Task<Dictionary<string, Subject>> SeedSubjectsAsync(School school, CancellationToken ct)
    {
        var existing = await db.Subjects.ToListAsync(ct);
        if (existing.Count > 0)
        {
            return existing.ToDictionary(s => s.Code);
        }

        var science = new SubjectGroup(school.Id, "Science") { Description = "Physics, Chemistry and Biology or Computer Science (Matric)" };
        var humanities = new SubjectGroup(school.Id, "Humanities") { Description = "Arts group for Matric" };
        db.SubjectGroups.AddRange(science, humanities);

        (string Code, string Name, string Short, SubjectType Type, string Color, Guid? Group)[] list =
        [
            ("ENG", "English", "Eng", SubjectType.Language, "#3B6EA8", null),
            ("URD", "Urdu", "Urd", SubjectType.Language, "#8A5A44", null),
            ("MATH", "Mathematics", "Math", SubjectType.Core, "#5B4FA3", null),
            ("SCI", "General Science", "Sci", SubjectType.Core, "#2F7D5B", null),
            ("PHY", "Physics", "Phy", SubjectType.Core, "#2E6F8E", science.Id),
            ("CHEM", "Chemistry", "Chem", SubjectType.Core, "#9A5B2E", science.Id),
            ("BIO", "Biology", "Bio", SubjectType.Elective, "#4F7F2E", science.Id),
            ("CS", "Computer Science", "CS", SubjectType.Elective, "#455A80", science.Id),
            ("ISL", "Islamiyat", "Isl", SubjectType.Core, "#3E7A6E", null),
            ("PST", "Pakistan Studies", "Pak St", SubjectType.Core, "#5E7043", null),
            ("SST", "Social Studies", "SST", SubjectType.Core, "#7A6A3A", null),
            ("QUR", "Nazra Quran", "Quran", SubjectType.Core, "#47705F", null),
            ("ART", "Art & Craft", "Art", SubjectType.CoCurricular, "#A3536B", null),
            ("PE", "Physical Education", "PE", SubjectType.CoCurricular, "#6B7280", null),
            ("ECO", "Economics", "Eco", SubjectType.Elective, "#80603A", humanities.Id),
            ("ETH", "Ethics", "Eth", SubjectType.Core, "#6A6A8A", null),
        ];
        var subjects = list.Select(s => new Subject(school.Id, s.Code, s.Name, s.Type)
        {
            ShortName = s.Short, Color = s.Color, SubjectGroupId = s.Group,
        }).ToList();
        db.Subjects.AddRange(subjects);
        await db.SaveChangesAsync(ct);
        return subjects.ToDictionary(s => s.Code);
    }

    private async Task SeedCurriculumAsync(School school, AcademicYear year, IReadOnlyList<Grade> grades,
        IReadOnlyDictionary<string, Subject> subjects, CancellationToken ct)
    {
        if (await db.GradeSubjects.AnyAsync(g => g.AcademicYearId == year.Id, ct))
        {
            return;
        }

        foreach (var grade in grades)
        {
            (string Code, int Periods, bool Mandatory)[] plan = grade.LevelOrder switch
            {
                <= 3 => [("ENG", 8, true), ("URD", 6, true), ("MATH", 6, true), ("QUR", 3, true), ("ART", 4, true), ("PE", 3, true)],
                <= 5 => [("ENG", 7, true), ("URD", 6, true), ("MATH", 7, true), ("SCI", 4, true), ("ISL", 3, true), ("SST", 3, true), ("QUR", 2, true), ("ART", 2, true), ("PE", 2, true), ("CS", 2, false)],
                <= 8 => [("ENG", 6, true), ("URD", 5, true), ("MATH", 7, true), ("SCI", 5, true), ("ISL", 3, true), ("SST", 3, true), ("CS", 3, true), ("QUR", 2, true), ("ART", 1, false), ("PE", 2, true)],
                <= 11 => [("ENG", 6, true), ("URD", 5, true), ("MATH", 7, true), ("SCI", 5, true), ("ISL", 3, true), ("SST", 3, true), ("CS", 3, true), ("QUR", 2, true), ("PE", 2, true)],
                _ => [("ENG", 6, true), ("URD", 5, true), ("MATH", 6, true), ("PHY", 5, true), ("CHEM", 5, true), ("BIO", 4, false), ("CS", 4, false), ("ISL", 2, true), ("PST", 2, true), ("PE", 1, true)],
            };
            foreach (var (code, periods, mandatory) in plan)
            {
                db.GradeSubjects.Add(new GradeSubject(school.Id, year.Id, grade.Id, subjects[code].Id, mandatory, periods));
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedCalendarAsync(School school, CancellationToken ct)
    {
        if (await db.CalendarEvents.AnyAsync(ct))
        {
            return;
        }

        (string Title, CalendarEventType Type, DateOnly Start, DateOnly End, string? Description)[] events =
        [
            ("Iqbal Day", CalendarEventType.Holiday, new(2026, 11, 9), new(2026, 11, 9), "Public holiday"),
            ("Annual Sports Day", CalendarEventType.Sports, new(2026, 11, 14), new(2026, 11, 14), "Inter-house athletics at the Gulberg campus grounds."),
            ("Parent–Teacher Meeting", CalendarEventType.ParentTeacherMeeting, new(2026, 11, 21), new(2026, 11, 21), "First-term progress discussions, 9:00–1:00."),
            ("Mid-Term Examinations", CalendarEventType.Exam, new(2026, 12, 7), new(2026, 12, 16), "Classes 1–10."),
            ("Winter Vacation", CalendarEventType.Holiday, new(2026, 12, 21), new(2026, 12, 31), "Includes Quaid-e-Azam Day (25 December)."),
            ("Second Term Begins", CalendarEventType.TermDates, new(2027, 1, 4), new(2027, 1, 4), null),
            ("Kashmir Solidarity Day", CalendarEventType.Holiday, new(2027, 2, 5), new(2027, 2, 5), "Public holiday"),
            ("Eid ul Fitr", CalendarEventType.Holiday, new(2027, 3, 9), new(2027, 3, 12), "Dates subject to moon sighting."),
            ("Pakistan Day", CalendarEventType.Holiday, new(2027, 3, 23), new(2027, 3, 23), "Public holiday"),
            ("Labour Day", CalendarEventType.Holiday, new(2027, 5, 1), new(2027, 5, 1), "Public holiday"),
            ("Eid ul Adha", CalendarEventType.Holiday, new(2027, 5, 16), new(2027, 5, 19), "Dates subject to moon sighting."),
            ("Final Examinations", CalendarEventType.Exam, new(2027, 5, 24), new(2027, 6, 3), "Annual examinations, all classes."),
            ("Summer Vacation", CalendarEventType.Holiday, new(2027, 6, 7), new(2027, 7, 31), null),
        ];
        foreach (var e in events)
        {
            db.CalendarEvents.Add(new CalendarEvent(school.Id, e.Title, e.Type, e.Start, e.End) { Description = e.Description });
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedStaffAccountsAsync(School school, SeedContext context, CancellationToken ct)
    {
        (string Email, string First, string Last, string Role, Guid? Campus)[] accounts =
        [
            ("principal@greenfield.example", "Imran", "Qureshi", SystemRoles.Principal, null),
            ("admin@greenfield.example", "Saad", "Malik", SystemRoles.Administrator, null),
            ("accountant@greenfield.example", "Bilal", "Ahmed", SystemRoles.Accountant, null),
            ("admissions@greenfield.example", "Sana", "Javed", SystemRoles.AdmissionsOfficer, null),
            ("registrar@greenfield.example", "Usman", "Tariq", SystemRoles.Registrar, null),
            ("hr@greenfield.example", "Hina", "Aslam", SystemRoles.HrManager, null),
            ("library@greenfield.example", "Rukhsana", "Bibi", SystemRoles.Librarian, null),
            ("transport@greenfield.example", "Kashif", "Mehmood", SystemRoles.TransportManager, null),
            ("nurse@greenfield.example", "Ayesha", "Noor", SystemRoles.Nurse, null),
            ("dha.coordinator@greenfield.example", "Mariam", "Zafar", SystemRoles.VicePrincipal, context.Dha.Id),
        ];

        var roles = await db.Roles.Where(r => r.Key != null).ToDictionaryAsync(r => r.Key!, ct);
        foreach (var account in accounts)
        {
            var user = await users.FindByEmailAsync(account.Email);
            if (user is null)
            {
                user = new User(account.Email, account.First, account.Last, account.Email);
                await CreateUserAsync(user);
            }

            if (!await db.SchoolMemberships.AnyAsync(m => m.UserId == user.Id, ct))
            {
                db.SchoolMemberships.Add(new SchoolMembership(school.Id, user.Id));
                db.UserRoles.Add(new UserRole(school.Id, user.Id, roles[account.Role].Id, account.Campus));
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task CreateUserAsync(User user)
    {
        user.CreatedAt = clock.GetUtcNow();
        var result = await users.CreateAsync(user, _password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    private string GeneratePassword()
    {
        var password = "Demo-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant() + "7";
        logger.LogWarning("Seed:DemoPassword is not configured; generated demo password for new accounts: {Password}", password);
        return password;
    }
}
