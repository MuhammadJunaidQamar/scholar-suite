using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Scholar.Application.Abstractions;
using Scholar.Domain.Identity;
using Scholar.Domain.Platform;
using Scholar.Infrastructure.Persistence;

namespace Scholar.IntegrationTests.Infrastructure;

public sealed record SeededSchool(Guid SchoolId, string Code, Guid CampusId, Guid UserId, string Email, string Password);

public static class TestData
{
    public const string Password = "Correct-horse-42";

    /// <summary>Creates a school with one campus and a member user (no roles).</summary>
    public static async Task<SeededSchool> CreateSchoolWithUserAsync(this ApiFactory factory, string code = "greenfield",
        string? email = null, string password = Password)
    {
        email ??= $"admin@{code}.test";
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var tenant = services.GetRequiredService<ITenantContext>();
        var users = services.GetRequiredService<UserManager<User>>();

        var school = new School(code, $"{char.ToUpperInvariant(code[0])}{code[1..]} School");
        db.Schools.Add(school);
        await db.SaveChangesAsync();

        using (tenant.Enter(school.Id))
        {
            var campus = new Campus(school.Id, "MAIN", "Main Campus", isMain: true);
            db.Campuses.Add(campus);
            var user = await CreateUserAsync(users, email, password);
            db.SchoolMemberships.Add(new SchoolMembership(school.Id, user.Id));
            await db.SaveChangesAsync();
            return new SeededSchool(school.Id, school.Code, campus.Id, user.Id, email, password);
        }
    }

    public static async Task<User> CreateUserAsync(UserManager<User> users, string email, string password,
        string firstName = "Test", string lastName = "User")
    {
        var user = new User(email, firstName, lastName, email);
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        return user;
    }
}
