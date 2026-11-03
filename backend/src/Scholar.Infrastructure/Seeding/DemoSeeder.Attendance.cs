using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scholar.Domain.Access;
using Scholar.Domain.Attendance;
using Scholar.Domain.Communication;
using Scholar.Domain.Identity;
using Scholar.Domain.Organization;
using Scholar.Domain.Platform;
using Scholar.Domain.Staffing;
using Scholar.Domain.Students;

namespace Scholar.Infrastructure.Seeding;

/// <summary>
/// Demo staff (every class has a class teacher; senior classes and specialist subjects have subject teachers), bell
/// times, and attendance from the first day of term: daily registers with realistic absence patterns, student and
/// staff leave, corrections in every state, the staff register, and the guardian alerts the school would have
/// recorded (as not sent: no SMS provider is connected in the demo).
/// </summary>
public sealed partial class DemoSeeder
{
    private static readonly string[] StaffWomen = ["Nadia", "Saima", "Rabia", "Farah", "Asma", "Sadia", "Uzma", "Bushra", "Humaira", "Samina", "Kiran", "Mehreen", "Ambreen", "Lubna", "Shazia", "Naila", "Fozia", "Sumaira", "Tahira", "Nazia", "Iram", "Saba", "Afshan", "Rehana"];
    private static readonly string[] StaffMen = ["Tariq", "Asif", "Kashif", "Nadeem", "Faisal", "Shahid", "Khalid", "Zahid", "Naveed", "Rizwan", "Adnan", "Waqas", "Salman", "Kamran", "Junaid", "Atif", "Sohail", "Arshad", "Amjad", "Yasir"];

    private sealed record Teacher(StaffMember Staff, User User);

    private async Task SeedStaffAsync(School school, SeedContext context, CancellationToken ct)
    {
        if (await db.Staff.AnyAsync(ct))
        {
            return;
        }

        var random = new Random(1947);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(school.Timezone)).DateTime);
        var designations = await db.Designations.ToDictionaryAsync(d => d.Name, ct);
        if (!designations.ContainsKey("School Administrator"))
        {
            var administrator = new Designation(school.Id, "School Administrator", DesignationCategory.Administrative);
            db.Designations.Add(administrator);
            designations[administrator.Name] = administrator;
        }

        var departments = await db.Departments.ToDictionaryAsync(d => d.Code, ct);
        var roles = await db.Roles.Where(r => r.Key != null).ToDictionaryAsync(r => r.Key!, ct);
        var number = 0;
        var usedNames = new HashSet<string>();

        StaffMember NewStaff(Campus campus, string first, string last, Gender gender, StaffType type, string designation, string? department,
            string? email = null)
        {
            var staff = new StaffMember(school.Id, campus.Id, $"EMP-{++number:0000}", first, last, gender, type,
                new DateOnly(2008 + random.Next(0, 18), 1 + random.Next(0, 12), 1).AddDays(random.Next(0, 27)))
            {
                DesignationId = designations[designation].Id,
                DepartmentId = department is null ? null : departments[department].Id,
                Phone = $"+923{random.Next(0, 5)}{random.Next(0, 10)}{random.Next(1000000, 9999999)}",
                Email = email,
                NationalId = $"35202{random.Next(1000000, 9999999)}{random.Next(1, 9)}",
                EmploymentType = random.NextDouble() < 0.82 ? EmploymentType.Permanent : EmploymentType.Contract,
                DateOfBirth = new DateOnly(1968 + random.Next(0, 32), 1 + random.Next(0, 12), 1 + random.Next(0, 28)),
                Address = new Domain.Common.Address { Line1 = $"House {random.Next(1, 300)}, Street {random.Next(1, 40)}", City = "Lahore", Province = "Punjab", CountryCode = "PK" },
            };
            if (staff.JoinDate > today.AddDays(-30)) staff.JoinDate = today.AddYears(-1);
            db.Staff.Add(staff);
            return staff;
        }

        (string First, string Last, Gender Gender) Person()
        {
            while (true)
            {
                var woman = random.NextDouble() < 0.68; // most school teachers in Lahore private schools are women
                var first = woman ? StaffWomen[random.Next(StaffWomen.Length)] : StaffMen[random.Next(StaffMen.Length)];
                var last = FamilyNames[random.Next(FamilyNames.Length)];
                if (usedNames.Add($"{first} {last}")) return (first, last, woman ? Gender.Female : Gender.Male);
            }
        }

        // The office: everyone who already signs in is also on the staff list.
        (string Email, string Designation, string? Department, StaffType Type, Gender Gender, bool Dha)[] office =
        [
            ("principal@greenfield.example", "Principal", "ADM", StaffType.Administrative, Gender.Male, false),
            ("admin@greenfield.example", "School Administrator", "ADM", StaffType.Administrative, Gender.Male, false),
            ("accountant@greenfield.example", "Accountant", "ACC", StaffType.Administrative, Gender.Male, false),
            ("admissions@greenfield.example", "Admissions Officer", "ADM", StaffType.Administrative, Gender.Female, false),
            ("registrar@greenfield.example", "Registrar", "ADM", StaffType.Administrative, Gender.Male, false),
            ("hr@greenfield.example", "HR Officer", "HR", StaffType.Administrative, Gender.Female, false),
            ("library@greenfield.example", "Librarian", "LIB", StaffType.NonTeaching, Gender.Female, false),
            ("transport@greenfield.example", "Transport Manager", "TRN", StaffType.NonTeaching, Gender.Male, false),
            ("nurse@greenfield.example", "School Nurse", null, StaffType.NonTeaching, Gender.Female, false),
            ("dha.coordinator@greenfield.example", "Vice Principal", "ADM", StaffType.Administrative, Gender.Female, true),
        ];
        foreach (var o in office)
        {
            var user = await users.FindByEmailAsync(o.Email);
            if (user is null) continue;
            usedNames.Add(user.FullName);
            var staff = NewStaff(o.Dha ? context.Dha : context.Main, user.FirstName, user.LastName, o.Gender, o.Type, o.Designation, o.Department, o.Email);
            staff.UserId = user.Id;
        }

        // A class teacher for every section; from Class 6 up, each class teacher also teaches a main subject.
        // Re-read (tracked) so the class-teacher assignments below are saved even if earlier seeding cleared the tracker.
        var sections = (await db.Sections.Where(s => s.AcademicYearId == context.CurrentYear.Id).ToListAsync(ct))
            .OrderBy(s => context.Grades.First(g => g.Id == s.GradeId).LevelOrder).ThenBy(s => s.CampusId != context.Main.Id).ThenBy(s => s.Name).ToList();
        var grade = context.Grades.ToDictionary(g => g.Id);
        var curriculum = await db.GradeSubjects.Where(g => g.AcademicYearId == context.CurrentYear.Id)
            .Select(g => new { g.GradeId, g.SubjectId }).ToListAsync(ct);
        var subjectByCode = context.Subjects;
        var pools = new Dictionary<(Guid Campus, Guid Subject), List<StaffMember>>();
        string[] seniorRotation = ["MATH", "ENG", "SCI", "URD", "CS", "ISL", "SST", "PHY", "CHEM", "ENG", "MATH", "URD"];
        var teachers = new List<Teacher>();
        var demoTeacherSection = sections.First(s => grade[s.GradeId].Name == "Class 5" && s.CampusId == context.Main.Id && s.Name == "A");
        var rotation = 0;
        foreach (var section in sections)
        {
            var campus = section.CampusId == context.Main.Id ? context.Main : context.Dha;
            var level = grade[section.GradeId].LevelOrder;
            var (first, last, gender) = section.Id == demoTeacherSection.Id ? ("Nadia", "Pervez", Gender.Female) : Person();
            usedNames.Add($"{first} {last}");
            var designation = level <= 3 ? "Assistant Teacher" : random.NextDouble() < 0.3 ? "Senior Teacher" : "Teacher";
            var department = level <= 3 ? "EY" : null;
            var teacher = NewStaff(campus, first, last, gender, StaffType.Teaching, designation, department);
            section.ClassTeacherId = teacher.Id;
            if (level >= 9)
            {
                var code = seniorRotation[rotation++ % seniorRotation.Length];
                if (subjectByCode.TryGetValue(code, out var subject))
                {
                    teacher.DepartmentId = departments[code switch { "MATH" => "MATH", "ENG" or "URD" => "LANG", "CS" => "CS", "ISL" or "SST" => "HUM", _ => "SCI" }].Id;
                    var key = (campus.Id, subject.Id);
                    if (!pools.TryGetValue(key, out var pool)) pools[key] = pool = [];
                    pool.Add(teacher);
                }
            }

            var email = section.Id == demoTeacherSection.Id ? "teacher@greenfield.example" : EmailFor(first, last);
            teacher.Email = email;
            teachers.Add(new Teacher(teacher, await TeacherAccountAsync(school, teacher, email, roles[SystemRoles.ClassTeacher], ct)));
        }

        // Specialists: whatever no class teacher covers (Quran, Islamiyat, Art, PE, computing, sciences).
        string[] specialistSubjects = ["MATH", "ENG", "URD", "SCI", "PHY", "CHEM", "BIO", "CS", "ISL", "PST", "SST", "QUR", "ART", "PE", "ECO"];
        foreach (var campus in new[] { context.Main, context.Dha })
        {
            var campusSections = sections.Where(s => s.CampusId == campus.Id).ToList();
            foreach (var code in specialistSubjects)
            {
                if (!subjectByCode.TryGetValue(code, out var subject)) continue;
                var needing = campusSections.Count(s => NeedsSubjectTeacher(grade[s.GradeId].LevelOrder, code)
                                                        && curriculum.Any(c => c.GradeId == s.GradeId && c.SubjectId == subject.Id));
                if (needing == 0) continue;
                var key = (campus.Id, subject.Id);
                if (!pools.TryGetValue(key, out var pool)) pools[key] = pool = [];
                while (pool.Count * 6 < needing)
                {
                    var (first, last, gender) = Person();
                    var teacher = NewStaff(campus, first, last, gender, StaffType.Teaching, "Teacher",
                        code switch { "MATH" => "MATH", "ENG" or "URD" => "LANG", "CS" => "CS", "ART" or "PE" => "ART", "ISL" or "QUR" or "SST" or "PST" => "HUM", _ => "SCI" });
                    var email = EmailFor(first, last);
                    teacher.Email = email;
                    pool.Add(teacher);
                    teachers.Add(new Teacher(teacher, await TeacherAccountAsync(school, teacher, email, roles[SystemRoles.Teacher], ct)));
                }
            }
        }

        // Subject teachers: senior classes for every subject; younger classes only for specialist subjects (their class
        // teacher teaches the rest). The demo teacher also teaches Class 5 maths in both main-campus sections.
        var loads = new Dictionary<Guid, int>();
        foreach (var section in sections)
        {
            var level = grade[section.GradeId].LevelOrder;
            foreach (var c in curriculum.Where(c => c.GradeId == section.GradeId))
            {
                var code = subjectByCode.First(s => s.Value.Id == c.SubjectId).Key;
                if (!NeedsSubjectTeacher(level, code)) continue;
                if (!pools.TryGetValue((section.CampusId, c.SubjectId), out var pool) || pool.Count == 0) continue;
                var chosen = pool.Contains(teachers.First(t => t.Staff.Id == section.ClassTeacherId).Staff)
                    ? teachers.First(t => t.Staff.Id == section.ClassTeacherId).Staff
                    : pool.OrderBy(p => loads.GetValueOrDefault(p.Id)).First();
                loads[chosen.Id] = loads.GetValueOrDefault(chosen.Id) + 1;
                db.SectionSubjectTeachers.Add(new SectionSubjectTeacher(school.Id, section.Id, c.SubjectId, chosen.Id));
            }
        }

        var demo = teachers.First(t => t.Staff.Id == demoTeacherSection.ClassTeacherId).Staff;
        foreach (var section in sections.Where(s => grade[s.GradeId].Name == "Class 5" && s.CampusId == context.Main.Id))
        {
            db.SectionSubjectTeachers.Add(new SectionSubjectTeacher(school.Id, section.Id, subjectByCode["MATH"].Id, demo.Id));
        }

        // Support staff (no sign-in).
        (string Designation, StaffType Type, int Main, int Dha, Gender? Gender, string? Department)[] support =
        [
            ("Lab Assistant", StaffType.NonTeaching, 1, 0, null, "SCI"), ("Office Assistant", StaffType.Support, 2, 1, null, "ADM"),
            ("Driver", StaffType.Support, 3, 2, Gender.Male, "TRN"), ("Bus Attendant", StaffType.Support, 2, 1, Gender.Female, "TRN"),
            ("Security Guard", StaffType.Support, 2, 1, Gender.Male, "FAC"), ("Housekeeping", StaffType.Support, 3, 2, null, "FAC"),
        ];
        foreach (var s in support)
        {
            for (var i = 0; i < s.Main + s.Dha; i++)
            {
                var (first, last, gender) = Person();
                if (s.Gender is { } fixedGender && gender != fixedGender)
                {
                    first = fixedGender == Gender.Male ? StaffMen[random.Next(StaffMen.Length)] : StaffWomen[random.Next(StaffWomen.Length)];
                    gender = fixedGender;
                }

                NewStaff(i < s.Main ? context.Main : context.Dha, first, last, gender, s.Type, s.Designation, s.Department);
            }
        }

        // Keep the employee-number sequence in step with the numbers used.
        var counter = await db.NumberSequenceCounters.SingleOrDefaultAsync(c => c.Key == SequenceKeys.Employee, ct);
        if (counter is null) db.NumberSequenceCounters.Add(new NumberSequenceCounter(school.Id, SequenceKeys.Employee, string.Empty, number + 1));
        else counter.NextValue = Math.Max(counter.NextValue, number + 1);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} staff, {Teachers} with teacher sign-ins", number, teachers.Count);

        static bool NeedsSubjectTeacher(int level, string code) =>
            level >= 9 || code is "ISL" or "QUR" or "ART" or "PE" or "CS";

        string EmailFor(string first, string last)
        {
            var baseName = $"{first}.{last}".ToLowerInvariant().Replace(' ', '.');
            var email = $"{baseName}@greenfield.example";
            for (var n = 2; usedEmails.Contains(email); n++) email = $"{baseName}{n}@greenfield.example";
            usedEmails.Add(email);
            return email;
        }
    }

    private readonly HashSet<string> usedEmails = new(StringComparer.OrdinalIgnoreCase);

    private async Task<User> TeacherAccountAsync(School school, StaffMember staff, string email, Role role, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new User(email, staff.FirstName, staff.LastName, email);
            await CreateUserAsync(user);
        }

        if (!await db.SchoolMemberships.AnyAsync(m => m.UserId == user.Id, ct))
        {
            db.SchoolMemberships.Add(new SchoolMembership(school.Id, user.Id));
            db.UserRoles.Add(new UserRole(school.Id, user.Id, role.Id, staff.CampusId == (await db.Campuses.SingleAsync(c => c.IsMain, ct)).Id ? null : staff.CampusId));
        }

        staff.UserId = user.Id;
        return user;
    }

    private async Task SeedBellSchedulesAsync(School school, SeedContext context, CancellationToken ct)
    {
        if (await db.BellSchedules.AnyAsync(ct))
        {
            return;
        }

        var main = new BellSchedule(school.Id, context.Main.Id, "Winter timings");
        main.ReplacePeriods([
            ("Assembly", new TimeOnly(7, 50), new TimeOnly(8, 0), true),
            ("", new TimeOnly(8, 0), new TimeOnly(8, 40), false), ("", new TimeOnly(8, 40), new TimeOnly(9, 20), false),
            ("", new TimeOnly(9, 20), new TimeOnly(10, 0), false), ("", new TimeOnly(10, 0), new TimeOnly(10, 40), false),
            ("Break", new TimeOnly(10, 40), new TimeOnly(11, 0), true),
            ("", new TimeOnly(11, 0), new TimeOnly(11, 40), false), ("", new TimeOnly(11, 40), new TimeOnly(12, 20), false),
            ("", new TimeOnly(12, 20), new TimeOnly(13, 0), false), ("", new TimeOnly(13, 0), new TimeOnly(13, 40), false),
        ]);
        var dha = new BellSchedule(school.Id, context.Dha.Id, "Winter timings");
        dha.ReplacePeriods([
            ("Assembly", new TimeOnly(7, 45), new TimeOnly(7, 55), true),
            ("", new TimeOnly(7, 55), new TimeOnly(8, 35), false), ("", new TimeOnly(8, 35), new TimeOnly(9, 15), false),
            ("", new TimeOnly(9, 15), new TimeOnly(9, 55), false),
            ("Break", new TimeOnly(9, 55), new TimeOnly(10, 15), true),
            ("", new TimeOnly(10, 15), new TimeOnly(10, 55), false), ("", new TimeOnly(10, 55), new TimeOnly(11, 35), false),
            ("", new TimeOnly(11, 35), new TimeOnly(12, 15), false), ("", new TimeOnly(12, 15), new TimeOnly(12, 55), false),
        ]);
        db.BellSchedules.AddRange(main, dha);

        // Term-time holidays before November (the calendar seed starts at Iqbal Day).
        (string Title, DateOnly Date, string Description)[] holidays =
        [
            ("Independence Day", new DateOnly(2026, 8, 14), "Public holiday"),
            ("Eid Milad-un-Nabi (12 Rabi-ul-Awwal)", new DateOnly(2026, 8, 26), "Public holiday; date subject to moon sighting."),
        ];
        foreach (var h in holidays)
        {
            if (!await db.CalendarEvents.AnyAsync(e => e.Title == h.Title && e.StartDate == h.Date, ct))
            {
                db.CalendarEvents.Add(new CalendarEvent(school.Id, h.Title, CalendarEventType.Holiday, h.Date, h.Date) { Description = h.Description });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Registers from the first day of term to today, with leave, corrections, alerts and the staff register.</summary>
    private async Task SeedAttendanceAsync(School school, SeedContext context, CancellationToken ct)
    {
        if (await db.AttendanceRegisters.AnyAsync(ct))
        {
            return;
        }

        var random = new Random(8_14);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(school.Timezone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
        DateTimeOffset At(DateOnly day, int hour, int minute) =>
            new(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified), zone), TimeSpan.Zero);

        var holidays = await db.CalendarEvents.Where(e => e.IsHoliday).Select(e => new { e.StartDate, e.EndDate, e.CampusId }).ToListAsync(ct);
        var working = school.Settings.WorkingDays.ToHashSet();
        bool SchoolDay(DateOnly d) => working.Contains(d.DayOfWeek) && !holidays.Any(h => d >= h.StartDate && d <= h.EndDate);
        var first = new DateOnly(2026, 8, 3);
        var days = new List<DateOnly>();
        for (var d = first; d <= today; d = d.AddDays(1))
        {
            if (SchoolDay(d)) days.Add(d);
        }

        var sections = await db.Sections.Where(s => s.AcademicYearId == context.CurrentYear.Id).Include(s => s.Grade).ToListAsync(ct);
        var staffUsers = await db.Staff.Where(s => s.UserId != null).ToDictionaryAsync(s => s.Id, s => s.UserId!.Value, ct);
        var enrollments = await db.Enrollments.Where(e => e.AcademicYearId == context.CurrentYear.Id)
            .Select(e => new { e.StudentId, e.SectionId, e.StartDate, e.EndDate }).ToListAsync(ct);
        var studentStatus = await db.Students.ToDictionaryAsync(s => s.Id, s => s.Status, ct);

        // Each child has their own habits: most are almost always in, a few are chronically absent.
        var habits = enrollments.Select(e => e.StudentId).Distinct().ToDictionary(id => id, _ =>
        {
            var roll = random.NextDouble();
            var absence = roll < 0.68 ? 0.008 + random.NextDouble() * 0.025 : roll < 0.9 ? 0.04 + random.NextDouble() * 0.05
                : roll < 0.97 ? 0.1 + random.NextDouble() * 0.08 : 0.24 + random.NextDouble() * 0.12;
            return (Absence: absence, Late: 0.005 + random.NextDouble() * 0.04);
        });

        // Student leave: approved family/sick leave in the past, a few awaiting a decision, one rejected.
        var leaveDays = new Dictionary<(Guid Student, DateOnly Day), Guid>();
        var principalId = (await users.FindByEmailAsync("principal@greenfield.example"))?.Id;
        var registrarId = (await users.FindByEmailAsync("registrar@greenfield.example"))?.Id;
        var current = enrollments.Where(e => e.EndDate == null && studentStatus.GetValueOrDefault(e.StudentId) == StudentStatus.Active).Select(e => e.StudentId).Distinct().ToList();
        string[] leaveReasons = ["Fever and flu (doctor's note to follow)", "Family wedding in Faisalabad", "Travelling to Karachi for a family emergency", "Umrah with family", "Dengue fever", "Grandmother's funeral", "Chickenpox — doctor advised rest"];
        for (var i = 0; i < 26; i++)
        {
            var student = current[random.Next(current.Count)];
            var start = days[random.Next(5, days.Count - 6)];
            var length = 1 + random.Next(0, 4);
            var end = start.AddDays(length + (start.DayOfWeek == DayOfWeek.Saturday ? 1 : 0));
            if (leaveDays.Keys.Any(k => k.Student == student)) continue;
            var reason = leaveReasons[random.Next(leaveReasons.Length)];
            var leave = new StudentLeaveRequest(school.Id, student, start, end, reason.Contains("fever", StringComparison.OrdinalIgnoreCase) || reason.Contains("pox") ? StudentLeaveKind.Sick
                    : reason.Contains("Umrah") ? StudentLeaveKind.Religious : reason.Contains("Travel") ? StudentLeaveKind.Travel : StudentLeaveKind.Family,
                reason, random.NextDouble() < 0.6 ? "Father, by phone" : "Mother, written application") { CreatedBy = registrarId };
            leave.Approve(principalId ?? Guid.Empty, At(start.AddDays(-1), 13, 15), null);
            db.StudentLeaveRequests.Add(leave);
            for (var d = start; d <= end; d = d.AddDays(1)) leaveDays[(student, d)] = leave.Id;
        }

        foreach (var (student, offset, reason) in new[] { (current[3], 1, "Sister's wedding"), (current[40], 2, "Medical check-up in Islamabad"), (current[77], 0, "Fever since last night"), (current[150], 3, "Family travelling to Multan") })
        {
            db.StudentLeaveRequests.Add(new StudentLeaveRequest(school.Id, student, today.AddDays(offset), today.AddDays(offset + 1 + random.Next(0, 2)),
                reason.Contains("Fever") || reason.Contains("Medical") ? StudentLeaveKind.Sick : StudentLeaveKind.Family, reason, "Mother, by phone") { CreatedBy = registrarId });
        }

        var rejected = new StudentLeaveRequest(school.Id, current[200], days[^8], days[^6], StudentLeaveKind.Travel, "Holiday trip to Hunza", "Father, written application")
        {
            CreatedBy = registrarId,
        };
        rejected.Reject(principalId ?? Guid.Empty, At(days[^9], 12, 0), "Term-time trips are not approved; the days will count as absences.");
        db.StudentLeaveRequests.Add(rejected);
        await db.SaveChangesAsync(ct);

        // Daily registers. Today, a few classes have not taken theirs yet.
        var guardians = await db.StudentGuardians.Where(l => l.ReceivesNotifications)
            .Select(l => new { l.StudentId, l.GuardianId, l.Guardian.FullName, l.Guardian.Phone, l.Guardian.Email, l.Guardian.NotifyBySms, l.Guardian.NotifyByWhatsApp, l.Guardian.NotifyByEmail })
            .ToListAsync(ct);
        var students = await db.Students.Select(s => new { s.Id, s.FirstName, s.LastName }).ToDictionaryAsync(s => s.Id, ct);
        var notTakenToday = sections.OrderBy(_ => random.Next()).Take(5).Select(s => s.Id).ToHashSet();
        var registerTimes = new List<(Guid Id, DateTimeOffset At)>();
        var absentRecords = new List<(AttendanceRegister Register, Guid Student, string ClassName)>();
        var schoolName = school.ShortName ?? school.Name;

        foreach (var day in days)
        {
            var rough = day.DayOfWeek == DayOfWeek.Saturday ? 1.35 : day.DayOfWeek == DayOfWeek.Monday ? 1.15 : 1.0;
            foreach (var section in sections)
            {
                if (day == today && notTakenToday.Contains(section.Id)) continue;
                var roster = enrollments.Where(e => e.SectionId == section.Id && e.StartDate <= day && (e.EndDate == null || e.EndDate >= day))
                    .Select(e => e.StudentId).Distinct().ToList();
                if (roster.Count == 0) continue;
                var register = new AttendanceRegister(school.Id, section.Id, section.AcademicYearId, section.CampusId, day, null, null, section.ClassTeacherId)
                {
                    CreatedBy = section.ClassTeacherId is { } t && staffUsers.TryGetValue(t, out var userId) ? userId : registrarId,
                };
                var className = $"{section.Grade.Name} – {section.Name}";
                foreach (var student in roster)
                {
                    var habit = habits[student];
                    if (leaveDays.TryGetValue((student, day), out var leaveId))
                    {
                        register.Mark(student, AttendanceStatus.Leave, null, null, leaveId);
                    }
                    else if (studentStatus.GetValueOrDefault(student) == StudentStatus.Suspended && day >= today.AddDays(-4))
                    {
                        register.Mark(student, AttendanceStatus.Absent, "Suspended", null);
                    }
                    else if (random.NextDouble() < habit.Absence * rough)
                    {
                        register.Mark(student, AttendanceStatus.Absent, null, null);
                        absentRecords.Add((register, student, className));
                    }
                    else if (random.NextDouble() < habit.Late)
                    {
                        register.Mark(student, AttendanceStatus.Late, random.NextDouble() < 0.3 ? "Van was late" : null,
                            new TimeOnly(8, 5 + random.Next(0, 40)));
                    }
                    else
                    {
                        register.Mark(student, AttendanceStatus.Present, null, null);
                    }
                }

                db.AttendanceRegisters.Add(register);
                registerTimes.Add((register.Id, At(day, 7, 52).AddMinutes(random.Next(0, 38))));
            }

            if (day.Day % 7 == 0 || day == today)
            {
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
            }
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        // The alerts the school recorded for each absence: none could be sent without a provider.
        var noProvider = new Dictionary<MessageChannel, string>
        {
            [MessageChannel.Sms] = "No SMS provider is connected, so the message was not sent.",
            [MessageChannel.WhatsApp] = "No WhatsApp provider is connected, so the message was not sent.",
            [MessageChannel.Email] = "No email provider is connected, so the message was not sent.",
        };
        var times = registerTimes.ToDictionary(r => r.Id, r => r.At);
        var batch = 0;
        foreach (var (register, student, className) in absentRecords)
        {
            if (studentStatus.GetValueOrDefault(student) != StudentStatus.Active || !students.TryGetValue(student, out var s)) continue;
            var body = $"{schoolName}: {s.FirstName} {s.LastName} ({className}) was marked absent today, {register.Date.ToString("ddd d MMM", CultureInfo.InvariantCulture)}. " +
                       $"If this is unexpected, please call the school office on {school.Phone}.";
            foreach (var g in guardians.Where(g => g.StudentId == student))
            {
                var channels = new List<MessageChannel>();
                if (g.NotifyBySms) channels.Add(MessageChannel.Sms);
                if (g.NotifyByWhatsApp) channels.Add(MessageChannel.WhatsApp);
                if (g.NotifyByEmail && g.Email != null) channels.Add(MessageChannel.Email);
                foreach (var channel in channels)
                {
                    var message = new OutboundMessage(school.Id, channel, channel == MessageChannel.Email ? g.Email! : g.Phone, g.FullName,
                        "attendance.absent", body, $"attendance:{register.Date:yyyyMMdd}:{student:N}:{g.GuardianId:N}:{channel}".ToLowerInvariant())
                    {
                        Subject = channel == MessageChannel.Email ? $"{s.FirstName} was absent today" : null,
                        GuardianId = g.GuardianId, StudentId = student, RelatedId = register.Id,
                    };
                    message.MarkNotSent(noProvider[channel], times[register.Id]);
                    db.OutboundMessages.Add(message);
                }
            }

            if (++batch % 500 == 0)
            {
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
            }
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        // Registers were taken in the morning of their day, not when the seed ran.
        foreach (var chunk in registerTimes.Chunk(500))
        {
            var values = string.Join(",", chunk.Select(r => $"('{r.Id}'::uuid, '{r.At.UtcDateTime:yyyy-MM-dd HH:mm:ss}+00'::timestamptz)"));
#pragma warning disable EF1002 // values are generated GUIDs and timestamps, not user input
            await db.Database.ExecuteSqlRawAsync($"UPDATE attendance_registers r SET created_at = v.at FROM (VALUES {values}) AS v(id, at) WHERE r.id = v.id", ct);
            await db.Database.ExecuteSqlRawAsync($"UPDATE attendance_records r SET created_at = v.at FROM (VALUES {values}) AS v(id, at) WHERE r.register_id = v.id", ct);
            await db.Database.ExecuteSqlRawAsync($"UPDATE outbound_messages m SET created_at = v.at FROM (VALUES {values}) AS v(id, at) WHERE m.related_id = v.id", ct);
#pragma warning restore EF1002
        }

        await SeedCorrectionsAsync(school, today, principalId, ct);
        await SeedStaffAttendanceAsync(school, context, days, today, principalId, ct);
        await BackdateAttendanceRecordsAsync(ct);
        logger.LogInformation("Seeded attendance for {Days} school days", days.Count);
    }

    /// <summary>
    /// The audit stamps say "now" for everything the seed inserted; requests and registers are moved back to when they
    /// would have been made (a correction the day after its register, leave the day before it starts…).
    /// </summary>
    private async Task BackdateAttendanceRecordsAsync(CancellationToken ct)
    {
        const string local = "'Asia/Karachi'";
        string[] statements =
        [
            "UPDATE attendance_corrections c SET created_at = r.created_at + interval '1 day 5 hours' FROM attendance_registers r WHERE r.id = c.register_id AND c.created_at > r.created_at + interval '3 days'",
            $"UPDATE student_leave_requests SET created_at = ((from_date - 1) + time '12:30') AT TIME ZONE {local} WHERE status <> 'Pending' AND created_at > ((from_date - 1) + time '12:30') AT TIME ZONE {local}",
            $"UPDATE student_leave_requests SET created_at = ((now() AT TIME ZONE {local})::date + time '07:40') AT TIME ZONE {local} WHERE status = 'Pending' AND created_at > ((now() AT TIME ZONE {local})::date + time '07:40') AT TIME ZONE {local}",
            $"UPDATE staff_leave_requests SET created_at = ((from_date - 2) + time '10:15') AT TIME ZONE {local} WHERE status = 'Approved' AND created_at > ((from_date - 2) + time '10:15') AT TIME ZONE {local}",
            "UPDATE staff_leave_requests SET created_at = decided_at - interval '20 hours' WHERE status = 'Rejected' AND decided_at IS NOT NULL AND created_at > decided_at",
            "UPDATE staff_leave_requests SET created_at = now() - interval '26 hours' WHERE status = 'Pending' AND created_at > now() - interval '26 hours'",
            $"UPDATE staff_attendance_corrections SET created_at = ((date + 1) + time '09:30') AT TIME ZONE {local} WHERE created_at > ((date + 1) + time '09:30') AT TIME ZONE {local}",
            $"UPDATE staff_attendance_records SET created_at = (date + time '09:05') AT TIME ZONE {local} WHERE created_at > (date + time '09:05') AT TIME ZONE {local}",
        ];
        foreach (var sql in statements)
        {
#pragma warning disable EF1002 // constant SQL
            await db.Database.ExecuteSqlRawAsync(sql, ct);
#pragma warning restore EF1002
        }
    }

    private async Task SeedCorrectionsAsync(School school, DateOnly today, Guid? principalId, CancellationToken ct)
    {
        // Locked registers from the last fortnight where a class teacher marked someone absent who in fact came late.
        var candidates = await db.AttendanceRecords.Include(r => r.Register)
            .Where(r => r.Status == AttendanceStatus.Absent && r.Register.Period == null && r.Register.Date < today.AddDays(-2) && r.Register.Date >= today.AddDays(-16))
            .OrderBy(r => r.Register.Date).ThenBy(r => r.Id)
            .Take(60).ToListAsync(ct);
        var picked = candidates.GroupBy(c => c.RegisterId).Select(g => g.First()).Take(6).ToList();
        (string Reason, AttendanceStatus To, string? Remark, CorrectionStatus Outcome, string? Note)[] plans =
        [
            ("Arrived at 9:10 with a note from home; I had already sent the register.", AttendanceStatus.Late, "Note from home", CorrectionStatus.Pending, null),
            ("She was in the nurse's room during roll call.", AttendanceStatus.Present, "In the sick room at roll call", CorrectionStatus.Pending, null),
            ("Medical certificate received today for this absence.", AttendanceStatus.Leave, "Medical certificate", CorrectionStatus.Pending, null),
            ("Came late with his father; gate register confirms 8:40.", AttendanceStatus.Late, "Gate register 8:40", CorrectionStatus.Approved, "Checked against the gate log."),
            ("Marked the wrong student — Hamza was present.", AttendanceStatus.Present, null, CorrectionStatus.Approved, null),
            ("Parents say he was ill.", AttendanceStatus.Leave, null, CorrectionStatus.Rejected, "No application or certificate was received. Ask the parents to send one."),
        ];
        for (var i = 0; i < picked.Count && i < plans.Length; i++)
        {
            var record = picked[i];
            var plan = plans[i];
            var correction = new AttendanceCorrection(school.Id, record.RegisterId, plan.Reason) { CreatedBy = record.Register.CreatedBy };
            correction.AddItem(record.StudentId, AttendanceStatus.Absent, plan.To, plan.Remark);
            if (plan.Outcome == CorrectionStatus.Approved)
            {
                correction.Approve(principalId ?? Guid.Empty, record.Register.CreatedAt.AddDays(2), plan.Note);
                record.Status = plan.To;
                record.Remark = plan.Remark;
            }
            else if (plan.Outcome == CorrectionStatus.Rejected)
            {
                correction.Reject(principalId ?? Guid.Empty, record.Register.CreatedAt.AddDays(2), plan.Note);
            }

            db.AttendanceCorrections.Add(correction);
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    private async Task SeedStaffAttendanceAsync(School school, SeedContext context, List<DateOnly> days, DateOnly today, Guid? principalId,
        CancellationToken ct)
    {
        var random = new Random(23_03);
        var staff = await db.Staff.ToListAsync(ct);
        var types = await db.LeaveTypes.ToDictionaryAsync(t => t.Code, ct);
        var hrId = (await users.FindByEmailAsync("hr@greenfield.example"))?.Id ?? principalId;

        // Staff leave: past casual and sick leave (approved), requests waiting for a decision, one refused.
        var onLeave = new Dictionary<(Guid Staff, DateOnly Day), Guid>();
        string[] casual = ["Family wedding", "Personal work at NADRA office", "Child's school admission interview", "Moving house"];
        string[] sick = ["Fever", "Migraine", "Food poisoning", "Back pain — doctor's advice"];
        var teachers = staff.Where(s => s.StaffType == StaffType.Teaching).ToList();
        for (var i = 0; i < 22; i++)
        {
            var person = staff[random.Next(staff.Count)];
            var isSick = random.NextDouble() < 0.5;
            var start = days[random.Next(3, days.Count - 4)];
            var schoolDays = days.Where(d => d >= start).Take(1 + random.Next(0, 2)).ToList();
            if (schoolDays.Any(d => onLeave.ContainsKey((person.Id, d)))) continue;
            var leave = new StaffLeaveRequest(school.Id, person.Id, types[isSick ? "SL" : "CL"].Id, schoolDays[0], schoolDays[^1], false, schoolDays.Count,
                isSick ? sick[random.Next(sick.Length)] : casual[random.Next(casual.Length)], person.UserId is not null)
            {
                CreatedBy = person.UserId ?? hrId,
            };
            leave.Approve(person.UserId == principalId ? hrId ?? Guid.Empty : principalId ?? Guid.Empty, At(schoolDays[0].AddDays(-1), 14), null);
            db.StaffLeaveRequests.Add(leave);
            foreach (var d in schoolDays) onLeave[(person.Id, d)] = leave.Id;
        }

        var demoTeacher = staff.FirstOrDefault(s => s.Email == "teacher@greenfield.example");
        var upcoming = new List<(StaffMember Person, int Offset, int Length, string Type, string Reason)>
        {
            (teachers[5], 2, 2, "CL", "Brother's wedding in Sialkot"),
            (teachers[11], 6, 1, "CL", "Appointment at the passport office"),
            (teachers[17], 1, 3, "SL", "Minor surgery — doctor advised three days' rest"),
        };
        if (demoTeacher is not null) upcoming.Add((demoTeacher, 9, 1, "CL", "Parent–teacher meeting at my son's school"));
        foreach (var (person, offset, length, type, reason) in upcoming)
        {
            var from = today.AddDays(offset);
            db.StaffLeaveRequests.Add(new StaffLeaveRequest(school.Id, person.Id, types[type].Id, from, from.AddDays(length - 1), false, length, reason, true)
            {
                CreatedBy = person.UserId,
            });
        }

        var refused = new StaffLeaveRequest(school.Id, teachers[8].Id, types["CL"].Id, today.AddDays(12), today.AddDays(16), false, 5,
            "Visiting family in Dubai", true) { CreatedBy = teachers[8].UserId };
        refused.Reject(principalId ?? Guid.Empty, At(today.AddDays(-1), 11), "Mid-term examinations start that week. Please choose dates in the winter break.");
        db.StaffLeaveRequests.Add(refused);
        await db.SaveChangesAsync(ct);

        // The daily staff register, kept by the office (at DHA by the coordinator).
        var keeperMain = (await users.FindByEmailAsync("admin@greenfield.example"))?.Id;
        var keeperDha = (await users.FindByEmailAsync("dha.coordinator@greenfield.example"))?.Id;
        var habits = staff.ToDictionary(s => s.Id, _ => (Absent: 0.006 + random.NextDouble() * 0.02, Late: 0.01 + random.NextDouble() * 0.06));
        var absentDays = new List<(StaffMember Person, DateOnly Day)>();
        foreach (var day in days)
        {
            if (day == today && TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(school.Timezone)).Hour < 9) break;
            foreach (var person in staff.Where(s => s.JoinDate <= day))
            {
                var habit = habits[person.Id];
                StaffAttendanceRecord record;
                if (onLeave.TryGetValue((person.Id, day), out var leaveId))
                {
                    record = new StaffAttendanceRecord(school.Id, person.Id, person.CampusId, day, StaffAttendanceStatus.OnLeave);
                    record.Set(StaffAttendanceStatus.OnLeave, null, null, null, leaveId);
                }
                else
                {
                    var roll = random.NextDouble();
                    var status = roll < habit.Absent ? StaffAttendanceStatus.Absent
                        : roll < habit.Absent + habit.Late ? StaffAttendanceStatus.Late
                        : person.StaffType == StaffType.Teaching && roll > 0.996 ? StaffAttendanceStatus.OnDuty
                        : roll > 0.993 ? StaffAttendanceStatus.HalfDay : StaffAttendanceStatus.Present;
                    record = new StaffAttendanceRecord(school.Id, person.Id, person.CampusId, day, status);
                    TimeOnly? checkIn = status switch
                    {
                        StaffAttendanceStatus.Present or StaffAttendanceStatus.HalfDay => new TimeOnly(7, 30 + random.Next(0, 25)),
                        StaffAttendanceStatus.Late => new TimeOnly(8, 10 + random.Next(0, 40)),
                        _ => null,
                    };
                    TimeOnly? checkOut = checkIn is null || day == today ? null
                        : status == StaffAttendanceStatus.HalfDay ? new TimeOnly(11, random.Next(0, 50)) : new TimeOnly(14, random.Next(0, 55));
                    record.Set(status, checkIn, checkOut, status switch
                    {
                        StaffAttendanceStatus.OnDuty => "Board examination duty",
                        StaffAttendanceStatus.HalfDay => "Left after break (approved by the principal)",
                        _ => null,
                    });
                    if (status == StaffAttendanceStatus.Absent) absentDays.Add((person, day));
                }

                record.CreatedBy = person.CampusId == context.Dha.Id ? keeperDha : keeperMain;
                db.StaffAttendanceRecords.Add(record);
            }

            if (day.Day % 10 == 0)
            {
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
            }
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        // Two teachers ask for an absence to be corrected (exam duty, training); one earlier request was approved.
        var withLogins = absentDays.Where(a => a.Person.UserId is not null && a.Day < today.AddDays(-2)).OrderByDescending(a => a.Day).Take(3).ToList();
        string[] reasons = ["I was invigilating the BISE practical examination at the Model Town centre.", "Attended the Punjab Curriculum Board training workshop (letter attached).", "I was here — signed the gate register at 7:45."];
        for (var i = 0; i < withLogins.Count; i++)
        {
            var (person, day) = withLogins[i];
            var correction = new StaffAttendanceCorrection(school.Id, person.Id, day, StaffAttendanceStatus.Absent,
                i == 2 ? StaffAttendanceStatus.Present : StaffAttendanceStatus.OnDuty, i == 2 ? new TimeOnly(7, 45) : null, null, reasons[i], true)
            {
                CreatedBy = person.UserId,
            };
            if (i == 2)
            {
                correction.Approve(hrId ?? Guid.Empty, At(day.AddDays(2), 10), "Gate register checked.");
                var record = await db.StaffAttendanceRecords.SingleAsync(r => r.StaffId == person.Id && r.Date == day, ct);
                record.Set(StaffAttendanceStatus.Present, new TimeOnly(7, 45), new TimeOnly(14, 10), $"Corrected: {reasons[i]}");
            }

            db.StaffAttendanceCorrections.Add(correction);
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        DateTimeOffset At(DateOnly day, int hour)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(school.Timezone);
            return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(hour, 0), DateTimeKind.Unspecified), zone), TimeSpan.Zero);
        }
    }
}
