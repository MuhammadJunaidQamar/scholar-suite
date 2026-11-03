using Scholar.Domain.Platform;

namespace Scholar.Infrastructure.Seeding;

public sealed partial class DemoSeeder
{
    // Module demo data is added here as each module lands (students, guardians, staff, fees, attendance, exams…).
    private partial Task SeedModulesAsync(School school, SeedContext context, CancellationToken ct) => Task.CompletedTask;
}
