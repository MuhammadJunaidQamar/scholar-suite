using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scholar.Application.Abstractions;
using Scholar.Domain.Access;
using Scholar.Domain.Organization;
using Scholar.Domain.Staffing;
using Scholar.Domain.Students;
using Scholar.Infrastructure.Persistence;
using Scholar.IntegrationTests.Infrastructure;

namespace Scholar.IntegrationTests.Attendance;

/// <summary>
/// A small school for attendance tests: one current year, Class 6 – A (three students) and Class 6 – B (one), a class
/// teacher with a login for each section, and a principal. Every weekday is a working day so tests do not depend on
/// the day they run; holidays are added per test.
/// </summary>
public sealed record AttendanceSchool(
    SeededSchool School, DateOnly Today, Guid YearId, Guid SectionA, Guid SectionB, Guid Ayesha, Guid Bilal, Guid Chand, Guid Danish,
    Guid SanaStaffId, string SanaEmail, Guid OmarStaffId, string OmarEmail, string PrincipalEmail, Guid MathsId);

public static class AttendanceTestData
{
    public static async Task<AttendanceSchool> CreateAttendanceSchoolAsync(this ApiFactory factory)
    {
        var school = await factory.ProvisionSchoolAsync();
        var (sanaUser, sanaEmail) = await factory.AddMemberAsync(school, SystemRoles.ClassTeacher, email: "sana@greenfield.test");
        var (omarUser, omarEmail) = await factory.AddMemberAsync(school, SystemRoles.ClassTeacher, email: "omar@greenfield.test");
        var (_, principalEmail) = await factory.AddMemberAsync(school, SystemRoles.Principal, email: "principal@greenfield.test");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var clock = scope.ServiceProvider.GetRequiredService<ISchoolClock>();
        using (tenant.Enter(school.SchoolId))
        {
            var today = await clock.TodayAsync();
            var entity = await db.Schools.SingleAsync(s => s.Id == school.SchoolId);
            entity.Settings.WorkingDays = [.. Enum.GetValues<DayOfWeek>()];
            entity.Phone = "042 111 222 333";
            db.Entry(entity).Property(s => s.Settings).IsModified = true;

            var year = new AcademicYear(school.SchoolId, "Current", today.AddDays(-60), today.AddDays(200));
            year.MakeCurrent();
            var grade = new Grade(school.SchoolId, "Class 6", "C6", 6, SchoolStage.Middle);
            var maths = new Subject(school.SchoolId, "MATH", "Mathematics", SubjectType.Core);
            db.AddRange(year, grade, maths);
            db.Add(new GradeSubject(school.SchoolId, year.Id, grade.Id, maths.Id, true, 6));
            var a = new Section(school.SchoolId, year.Id, school.CampusId, grade.Id, "A", 30);
            var b = new Section(school.SchoolId, year.Id, school.CampusId, grade.Id, "B", 30);
            db.AddRange(a, b);

            var sana = new StaffMember(school.SchoolId, school.CampusId, "T-0001", "Sana", "Iqbal", Gender.Female, StaffType.Teaching,
                today.AddYears(-2)) { UserId = sanaUser, Email = sanaEmail };
            var omar = new StaffMember(school.SchoolId, school.CampusId, "T-0002", "Omar", "Farooq", Gender.Male, StaffType.Teaching,
                today.AddYears(-3)) { UserId = omarUser, Email = omarEmail };
            db.AddRange(sana, omar);
            a.ClassTeacherId = sana.Id;
            b.ClassTeacherId = omar.Id;

            Guid AddStudent(Section section, string first, string last, string roll, string phone)
            {
                var student = new Student(school.SchoolId, school.CampusId, $"ADM-{roll}-{first}", first, last, Gender.Female,
                    new DateOnly(2014, 5, 1), today.AddDays(-60)) { CurrentSectionId = section.Id, CurrentAcademicYearId = year.Id };
                db.Students.Add(student);
                db.Enrollments.Add(new Enrollment(school.SchoolId, student.Id, year.Id, school.CampusId, grade.Id, section.Id, today.AddDays(-60), roll));
                var guardian = new Guardian(school.SchoolId, "Parent of", first, phone);
                db.Guardians.Add(guardian);
                db.StudentGuardians.Add(new StudentGuardian(school.SchoolId, student.Id, guardian.Id, GuardianRelationship.Father)
                {
                    IsPrimary = true,
                });
                return student.Id;
            }

            var ayesha = AddStudent(a, "Ayesha", "Khan", "1", "0300 1111111");
            var bilal = AddStudent(a, "Bilal", "Ahmed", "2", "0300 2222222");
            var chand = AddStudent(a, "Chand", "Bibi", "10", "0300 3333333");
            var danish = AddStudent(b, "Danish", "Ali", "1", "0300 4444444");
            await db.SaveChangesAsync();
            return new AttendanceSchool(school, today, year.Id, a.Id, b.Id, ayesha, bilal, chand, danish, sana.Id, sanaEmail, omar.Id, omarEmail,
                principalEmail, maths.Id);
        }
    }

    public static async Task<BrowserClient> SignInAsync(this ApiFactory factory, string email)
    {
        var client = factory.CreateBrowserClient();
        await client.LoginAsync(email, TestData.Password);
        return client;
    }

    /// <summary>Changes the school's attendance settings directly (the settings API is covered elsewhere).</summary>
    public static async Task ConfigureAttendanceAsync(this ApiFactory factory, Guid schoolId, Action<Domain.Platform.AttendanceSettings> change)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        using (tenant.Enter(schoolId))
        {
            var school = await db.Schools.SingleAsync(s => s.Id == schoolId);
            change(school.Settings.Attendance);
            db.Entry(school).Property(s => s.Settings).IsModified = true;
            await db.SaveChangesAsync();
        }
    }

    public static async Task<T> WithDbAsync<T>(this ApiFactory factory, Guid schoolId, Func<AppDbContext, Task<T>> work)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        using (tenant.Enter(schoolId))
        {
            return await work(db);
        }
    }
}
