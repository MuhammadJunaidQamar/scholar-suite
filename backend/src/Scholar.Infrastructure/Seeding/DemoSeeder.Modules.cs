using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scholar.Domain.Admissions;
using Scholar.Domain.Common;
using Scholar.Domain.Organization;
using Scholar.Domain.Platform;
using Scholar.Domain.Students;

namespace Scholar.Infrastructure.Seeding;

/// <summary>Demo students, families and admissions for Greenfield (deterministic: the same seed gives the same school).</summary>
public sealed partial class DemoSeeder
{
    private static readonly string[] GirlNames =
    [
        "Ayesha", "Fatima", "Zainab", "Maryam", "Hira", "Iqra", "Sana", "Amna", "Mahnoor", "Aiza", "Hania", "Eman", "Areeba", "Laiba",
        "Noor", "Anaya", "Zoya", "Rabia", "Sidra", "Khadija", "Alishba", "Inaya", "Hafsa", "Minahil", "Zara", "Bisma", "Ramsha", "Hoorain",
        "Manahil", "Anabia", "Mishal", "Haniya", "Rida", "Dua", "Aleena", "Javeria", "Mehak", "Kinza", "Hadia", "Wania",
    ];

    private static readonly string[] BoyNames =
    [
        "Muhammad", "Ahmed", "Ali", "Hamza", "Hassan", "Hussain", "Usman", "Bilal", "Abdullah", "Ibrahim", "Zain", "Saad", "Talha",
        "Rayyan", "Arham", "Ayaan", "Huzaifa", "Haider", "Faizan", "Omar", "Shayan", "Daniyal", "Moiz", "Musa", "Rehan", "Taha", "Saim",
        "Zohaib", "Hasnain", "Abdul Hadi", "Muhammad Ali", "Muhammad Ahmed", "Ammar", "Yahya", "Zaid", "Aliyan", "Rafay", "Hadi", "Arsal", "Shahzaib",
    ];

    private static readonly string[] FamilyNames =
    [
        "Khan", "Ahmed", "Ali", "Malik", "Qureshi", "Butt", "Chaudhry", "Sheikh", "Siddiqui", "Raza", "Hussain", "Iqbal", "Shah", "Mirza",
        "Javed", "Aslam", "Akhtar", "Rana", "Baig", "Abbasi", "Hashmi", "Rizvi", "Zaidi", "Bhatti", "Gondal", "Cheema", "Warraich", "Awan",
        "Niazi", "Durrani", "Farooqui", "Nawaz", "Riaz", "Saeed", "Anwar", "Latif", "Mahmood", "Kazmi", "Tirmizi", "Gilani",
    ];

    private static readonly string[] FatherNames =
    [
        "Imran", "Tariq", "Asif", "Kashif", "Nadeem", "Faisal", "Shahid", "Khalid", "Zahid", "Naveed", "Rizwan", "Adnan", "Waqas", "Salman",
        "Kamran", "Junaid", "Omer", "Babar", "Atif", "Sohail", "Fahad", "Arshad", "Amjad", "Shoaib", "Yasir", "Sajid", "Haroon", "Mudassar",
    ];

    private static readonly string[] MotherNames =
    [
        "Saima", "Nadia", "Rubina", "Shazia", "Farah", "Asma", "Sadia", "Uzma", "Bushra", "Sobia", "Humaira", "Samina", "Tahira", "Nazia",
        "Kiran", "Mehreen", "Fozia", "Ambreen", "Sumaira", "Lubna", "Rabia", "Shumaila", "Naila", "Sana",
    ];

    private static readonly string[] Occupations =
    [
        "Engineer", "Doctor", "Business owner", "Banker", "Teacher", "Government officer", "Accountant", "Software developer", "Pharmacist",
        "Trader", "Army officer", "Lawyer", "Shopkeeper", "Architect", "Professor", "Contractor",
    ];

    private static readonly (string Area, string City)[] Areas =
    [
        ("Gulberg III", "Lahore"), ("Model Town", "Lahore"), ("Johar Town", "Lahore"), ("DHA Phase 5", "Lahore"), ("DHA Phase 6", "Lahore"),
        ("Garden Town", "Lahore"), ("Faisal Town", "Lahore"), ("Cavalry Ground", "Lahore"), ("Askari 10", "Lahore"), ("Wapda Town", "Lahore"),
        ("Iqbal Town", "Lahore"), ("Bahria Town", "Lahore"),
    ];

    private static readonly string[] MedicalAlerts =
    [
        "Asthma — inhaler in school bag", "Severe peanut allergy — EpiPen in the office", "Type 1 diabetes — insulin before lunch",
        "Epilepsy — seizure plan with the nurse", "Wears glasses — sit near the board", "Lactose intolerant",
    ];

    private static readonly string[] PreviousSchools =
        ["Beaconhouse School System", "The City School", "Lahore Grammar School", "Allied School", "Dar-e-Arqam School", "Govt. Model School"];

    private static readonly string[] BloodGroups = ["A+", "B+", "O+", "AB+", "A-", "B-", "O-"];

    private static readonly TimeSpan Pakistan = TimeSpan.FromHours(5);

    private partial async Task SeedModulesAsync(School school, SeedContext context, CancellationToken ct)
    {
        await SeedStudentsAsync(school, context, ct);
        await SeedAdmissionsAsync(school, context, ct);
        await SeedStaffAsync(school, context, ct);
        await SeedBellSchedulesAsync(school, context, ct);
        await SeedAttendanceAsync(school, context, ct);
    }

    /// <summary>~600 students in families of one to three children, enrolled in this year's sections, plus a few leavers.</summary>
    private async Task SeedStudentsAsync(School school, SeedContext context, CancellationToken ct)
    {
        if (await db.Students.AnyAsync(ct))
        {
            return;
        }

        var random = new Random(2026);
        T Pick<T>(IReadOnlyList<T> list) => list[random.Next(list.Count)];
        var phones = new HashSet<string>();
        string NewPhone()
        {
            string phone;
            do
            {
                phone = $"+923{random.Next(0, 5)}{random.Next(0, 10)}{random.Next(1000000, 9999999)}";
            }
            while (!phones.Add(phone));
            return phone;
        }

        string NationalId() => $"35202{random.Next(1000000, 9999999)}{random.Next(1, 9)}";

        var seats = context.Sections.ToDictionary(s => s.Id, s => (int)Math.Round(s.Capacity * (0.78 + random.NextDouble() * 0.18)));
        var placed = context.Sections.ToDictionary(s => s.Id, _ => 0);
        var rolls = new Dictionary<Guid, int>();
        var start = context.CurrentYear.StartDate;
        var admissionsByYear = new SortedDictionary<int, int>();

        string AdmissionNumber(DateOnly admitted)
        {
            admissionsByYear[admitted.Year] = admissionsByYear.GetValueOrDefault(admitted.Year) + 1;
            return $"ADM-{admitted.Year}-{admissionsByYear[admitted.Year]:0000}";
        }

        Section? FreeSection(Grade grade, Guid preferredCampus)
        {
            var open = context.Sections.Where(s => s.GradeId == grade.Id && placed[s.Id] < seats[s.Id]).ToList();
            return open.Where(s => s.CampusId == preferredCampus).OrderBy(s => placed[s.Id]).FirstOrDefault()
                   ?? open.OrderBy(s => placed[s.Id]).FirstOrDefault();
        }

        var created = 0;
        while (context.Sections.Any(s => placed[s.Id] < seats[s.Id]) && created < 900)
        {
            var family = Pick(FamilyNames);
            var (area, city) = Pick(Areas);
            var campus = area.StartsWith("DHA", StringComparison.Ordinal) || random.NextDouble() < 0.18 ? context.Dha.Id : context.Main.Id;

            // Seat the children first; a family whose classes are all full doesn't join.
            var children = random.NextDouble() switch { < 0.58 => 1, < 0.9 => 2, _ => 3 };
            var seated = new List<(Grade Grade, Section Section)>();
            foreach (var level in context.Grades.Select(g => g.LevelOrder).OrderBy(_ => random.Next()).Take(children).Order())
            {
                var grade = context.Grades.Single(g => g.LevelOrder == level);
                if (FreeSection(grade, campus) is { } section)
                {
                    placed[section.Id]++;
                    seated.Add((grade, section));
                }
            }

            if (seated.Count == 0)
            {
                continue;
            }

            var address = new Address
            {
                Line1 = $"House {random.Next(1, 400)}, Street {random.Next(1, 40)}", Line2 = area, City = city, Province = "Punjab", CountryCode = "PK",
            };

            var father = new Guardian(school.Id, Pick(FatherNames), family, NewPhone())
            {
                Occupation = Pick(Occupations), Address = address, NationalId = NationalId(), NotifyByWhatsApp = random.NextDouble() < 0.6,
                Email = random.NextDouble() < 0.35 ? $"{family.ToLowerInvariant()}.{random.Next(10, 99)}@example.pk" : null,
            };
            db.Guardians.Add(father);
            Guardian? mother = null;
            if (random.NextDouble() < 0.62)
            {
                mother = new Guardian(school.Id, Pick(MotherNames), family, NewPhone())
                {
                    Occupation = random.NextDouble() < 0.6 ? "Homemaker" : Pick(Occupations), Address = address,
                };
                db.Guardians.Add(mother);
            }

            foreach (var (grade, section) in seated)
            {
                var girl = random.NextDouble() < 0.48;
                var dob = start.AddYears(-(grade.LevelOrder + 2)).AddDays(-random.Next(0, 360));
                var yearsHere = Math.Min(random.Next(0, grade.LevelOrder), 9);
                var admitted = new DateOnly(start.Year - yearsHere, 8, random.Next(1, 20));
                var student = new Student(school.Id, section.CampusId, AdmissionNumber(admitted), girl ? Pick(GirlNames) : Pick(BoyNames), family,
                    girl ? Gender.Female : Gender.Male, dob, admitted)
                {
                    Address = address,
                    Nationality = "Pakistani",
                    Religion = random.NextDouble() < 0.97 ? "Islam" : "Christianity",
                    NationalId = random.NextDouble() < 0.7 ? NationalId() : null,
                    BloodGroup = random.NextDouble() < 0.5 ? Pick(BloodGroups) : null,
                    MedicalAlert = random.NextDouble() < 0.03 ? Pick(MedicalAlerts) : null,
                    PreviousSchoolName = yearsHere == 0 && grade.LevelOrder > 3 ? Pick(PreviousSchools) : null,
                    CurrentSectionId = section.Id,
                    CurrentAcademicYearId = context.CurrentYear.Id,
                };
                db.Students.Add(student);
                rolls[section.Id] = rolls.GetValueOrDefault(section.Id) + 1;
                db.Enrollments.Add(new Enrollment(school.Id, student.Id, context.CurrentYear.Id, section.CampusId, section.GradeId, section.Id,
                    start.AddDays(2), rolls[section.Id].ToString(CultureInfo.InvariantCulture)));
                db.StudentGuardians.Add(new StudentGuardian(school.Id, student.Id, father.Id, GuardianRelationship.Father) { IsPrimary = true, IsFeePayer = true });
                if (mother is not null)
                {
                    db.StudentGuardians.Add(new StudentGuardian(school.Id, student.Id, mother.Id, GuardianRelationship.Mother));
                }

                if (random.NextDouble() < 0.4)
                {
                    db.EmergencyContacts.Add(new EmergencyContact(school.Id, student.Id, $"{Pick(FatherNames)} {family}", "Uncle", NewPhone(), 1));
                }

                created++;
            }
        }

        await db.SaveChangesAsync(ct);

        // A few students have already left this year, with their history recorded the way the app records it.
        string[] reasons = ["Family relocated to Karachi", "Moved abroad (UAE)", "Fee affordability", "Joined a school closer to home", "Family relocated to Islamabad"];
        var everyone = await db.Students.Include(s => s.Enrollments).OrderBy(s => s.AdmissionNumber).ToListAsync(ct);
        foreach (var (student, index) in everyone.Where((_, i) => i % 53 == 7).Take(11).Select((s, i) => (s, i)))
        {
            var to = index % 3 == 0 ? StudentStatus.TransferredOut : StudentStatus.Withdrawn;
            db.StudentStatusChanges.Add(student.ChangeStatus(to, start.AddDays(18 + index * 6), reasons[index % reasons.Length],
                to == StudentStatus.TransferredOut ? "The City School, Gulshan Campus, Karachi" : null, null));
        }

        // Continue numbering after the seeded admission numbers, per year.
        foreach (var (year, count) in admissionsByYear)
        {
            db.NumberSequenceCounters.Add(new NumberSequenceCounter(school.Id, SequenceKeys.Admission, year.ToString(CultureInfo.InvariantCulture), count + 1));
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} students", created);
    }

    /// <summary>Applications for next session at every pipeline stage, and a list of open inquiries.</summary>
    private async Task SeedAdmissionsAsync(School school, SeedContext context, CancellationToken ct)
    {
        if (await db.AdmissionApplications.AnyAsync(ct))
        {
            return;
        }

        var random = new Random(77);
        T Pick<T>(IReadOnlyList<T> list) => list[random.Next(list.Count)];
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.ToOffset(Pakistan).DateTime);
        DateTimeOffset At(DateOnly day, int hour) => new DateTimeOffset(day.ToDateTime(new TimeOnly(hour, 0)), Pakistan).ToUniversalTime();

        // Admissions are open for the 2027–28 session, planned with this year's sections.
        var next = await db.AcademicYears.SingleOrDefaultAsync(y => y.Name == "2027–28", ct);
        if (next is null)
        {
            next = new AcademicYear(school.Id, "2027–28", new DateOnly(2027, 8, 1), new DateOnly(2028, 7, 31));
            next.AddTerm("First Term", new DateOnly(2027, 8, 2), new DateOnly(2027, 12, 17));
            next.AddTerm("Second Term", new DateOnly(2028, 1, 3), new DateOnly(2028, 6, 2));
            db.AcademicYears.Add(next);
            foreach (var s in context.Sections)
            {
                db.Sections.Add(new Section(school.Id, next.Id, s.CampusId, s.GradeId, s.Name, s.Capacity) { RoomId = s.RoomId });
            }

            await db.SaveChangesAsync(ct);
        }

        var officer = await db.Users.Where(u => u.Email == "admissions@greenfield.example").Select(u => u.Id).SingleAsync(ct);
        var entryGrades = context.Grades.Where(g => g.LevelOrder <= 4 || g.LevelOrder == 7 || g.LevelOrder == 10).ToList();

        // Days of history each stage needs, so every recorded step lies in the past.
        var plan = new (ApplicationStatus Status, int Count, int Days)[]
        {
            (ApplicationStatus.Submitted, 9, 0), (ApplicationStatus.UnderReview, 6, 1), (ApplicationStatus.AssessmentScheduled, 5, 2),
            (ApplicationStatus.Assessed, 4, 6), (ApplicationStatus.Offered, 5, 8), (ApplicationStatus.Waitlisted, 3, 8),
            (ApplicationStatus.Accepted, 2, 10), (ApplicationStatus.Rejected, 3, 4), (ApplicationStatus.Withdrawn, 2, 3),
        };
        var drafts = plan
            .SelectMany(p => Enumerable.Range(0, p.Count).Select(_ =>
                (p.Status, Submitted: now.AddDays(-(p.Days + 1 + random.Next(0, 30))).AddMinutes(-random.Next(0, 600)))))
            .OrderBy(d => d.Submitted)
            .ToList();

        var perMonth = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var waitlistRank = 0;
        foreach (var (target, submitted) in drafts)
        {
            var month = NumberSequence.PeriodFor(SequenceResetPolicy.Monthly, DateOnly.FromDateTime(submitted.ToOffset(Pakistan).DateTime));
            perMonth[month] = perMonth.GetValueOrDefault(month) + 1;
            var family = Pick(FamilyNames);
            var girl = random.NextDouble() < 0.5;
            var grade = Pick(entryGrades);
            var application = new AdmissionApplication(school.Id, $"APP-{month[2..]}-{perMonth[month]:0000}", next.Id,
                random.NextDouble() < 0.75 ? context.Main.Id : context.Dha.Id, grade.Id,
                random.NextDouble() < 0.55 ? ApplicationSource.Online : ApplicationSource.Office)
            {
                FirstName = girl ? Pick(GirlNames) : Pick(BoyNames),
                LastName = family,
                Gender = girl ? Gender.Female : Gender.Male,
                DateOfBirth = next.StartDate.AddYears(-(grade.LevelOrder + 2)).AddDays(-random.Next(0, 360)),
                GuardianFirstName = Pick(FatherNames),
                GuardianLastName = family,
                GuardianRelationship = GuardianRelationship.Father,
                GuardianPhone = $"+923{random.Next(0, 5)}{random.Next(0, 10)}{random.Next(1000000, 9999999)}",
                GuardianOccupation = Pick(Occupations),
                PreviousSchoolName = grade.LevelOrder > 3 ? Pick(PreviousSchools) : null,
                PreviousResult = grade.LevelOrder > 3 ? $"{random.Next(62, 96)}%" : null,
                Address = new Address { Line2 = Pick(Areas).Area, City = "Lahore", Province = "Punjab", CountryCode = "PK" },
            };
            db.AdmissionApplications.Add(application);
            application.Submit(submitted, null);
            Advance(application, target, submitted);
        }

        void Advance(AdmissionApplication application, ApplicationStatus target, DateTimeOffset submitted)
        {
            if (target == ApplicationStatus.Submitted)
            {
                return;
            }

            if (target == ApplicationStatus.Withdrawn)
            {
                application.Withdraw("Family chose another school", officer, submitted.AddDays(3));
                return;
            }

            application.StartReview(officer, submitted.AddDays(1));
            if (target == ApplicationStatus.UnderReview)
            {
                return;
            }

            if (target == ApplicationStatus.Rejected)
            {
                application.Reject("No seat available in the requested class this session.", officer, submitted.AddDays(4));
                return;
            }

            var testDay = target == ApplicationStatus.AssessmentScheduled
                ? today.AddDays(random.Next(2, 14))
                : DateOnly.FromDateTime(submitted.AddDays(5).UtcDateTime);
            var assessment = application.ScheduleAssessment(AssessmentType.EntranceTest, At(testDay, 9), "Examination Hall, Gulberg Campus",
                officer, submitted.AddDays(2));
            if (target == ApplicationStatus.AssessmentScheduled)
            {
                return;
            }

            application.RecordAssessment(assessment.Id, AssessmentOutcome.Passed, random.Next(28, 49), 50, null, officer, submitted.AddDays(6));
            if (target == ApplicationStatus.Assessed)
            {
                return;
            }

            if (target == ApplicationStatus.Waitlisted)
            {
                application.Waitlist(++waitlistRank, "Strong applicant; the class is currently full.", officer, submitted.AddDays(8));
                return;
            }

            application.Offer(today.AddDays(random.Next(3, 14)), null, officer, submitted.AddDays(8));
            if (target == ApplicationStatus.Accepted)
            {
                application.Accept(officer, submitted.AddDays(10));
            }
        }

        string[] notes = ["Asked about transport from Johar Town.", "Interested in the Science group for Matric.", "Wants a campus visit on Saturday.", "Sibling already studies here."];
        for (var i = 0; i < 14; i++)
        {
            var family = Pick(FamilyNames);
            var inquiry = new AdmissionInquiry(school.Id, $"{(random.NextDouble() < 0.5 ? Pick(GirlNames) : Pick(BoyNames))} {family}",
                $"{Pick(FatherNames)} {family}", $"+923{random.Next(0, 5)}{random.Next(0, 10)}{random.Next(1000000, 9999999)}",
                Pick(Enum.GetValues<InquirySource>()))
            {
                AcademicYearId = next.Id,
                GradeId = Pick(entryGrades).Id,
                CampusId = context.Main.Id,
                FollowUpOn = today.AddDays(random.Next(-3, 9)),
                Notes = random.NextDouble() < 0.6 ? Pick(notes) : null,
            };
            if (i % 3 == 0)
            {
                inquiry.MarkContacted();
            }

            db.AdmissionInquiries.Add(inquiry);
        }

        // Continue numbering after the seeded application numbers, per month.
        foreach (var (month, count) in perMonth)
        {
            db.NumberSequenceCounters.Add(new NumberSequenceCounter(school.Id, SequenceKeys.Application, month, count + 1));
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Applications} applications and 14 inquiries", drafts.Count);
    }
}
