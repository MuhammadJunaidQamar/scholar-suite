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

    /// <summary>Provisions a school the way the platform does (default roles, owner account with a password).</summary>
    public static async Task<SeededSchool> ProvisionSchoolAsync(this ApiFactory factory, string code = "greenfield")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<Application.Platform.SchoolProvisioningService>();
        var email = $"owner@{code}.test";
        var school = (await provisioning.ProvisionAsync(new Application.Platform.ProvisionSchoolRequest(code, $"{code} school",
            null, "Main Campus", new Application.Platform.ProvisionOwnerRequest("Olivia", "Owner", email, Password)), default)).School;
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var campusId = db.Campuses.IgnoreQueryFiltersFor(school.Id).Single(c => c.IsMain).Id;
        var ownerId = db.Users.Single(u => u.Email == email).Id;
        return new SeededSchool(school.Id, code, campusId, ownerId, email, Password);
    }

    /// <summary>Adds a member holding one system role (optionally campus-scoped). Returns the login email.</summary>
    public static async Task<(Guid UserId, string Email)> AddMemberAsync(this ApiFactory factory, SeededSchool school,
        string roleKey, Guid? campusId = null, string? email = null)
    {
        email ??= $"{roleKey}.{Guid.NewGuid():N}"[..24] + $"@{school.Code}.test";
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var tenant = services.GetRequiredService<ITenantContext>();
        var users = services.GetRequiredService<UserManager<User>>();
        using (tenant.Enter(school.SchoolId))
        {
            var user = await CreateUserAsync(users, email, Password, "Member", roleKey);
            var role = db.Roles.Single(r => r.Key == roleKey);
            db.SchoolMemberships.Add(new SchoolMembership(school.SchoolId, user.Id));
            db.UserRoles.Add(new Domain.Access.UserRole(school.SchoolId, user.Id, role.Id, campusId));
            await db.SaveChangesAsync();
            return (user.Id, email);
        }
    }

    public static async Task<string> CreatePlatformAdminAsync(this ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var email = $"platform.{Guid.NewGuid():N}"[..20] + "@scholar.test";
        var user = new User(email, "Platform", "Admin", email) { IsPlatformAdmin = true };
        var result = await users.CreateAsync(user, Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        return email;
    }

    private static IQueryable<Campus> IgnoreQueryFiltersFor(this Microsoft.EntityFrameworkCore.DbSet<Campus> campuses, Guid schoolId) =>
        Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IgnoreQueryFilters(campuses).Where(c => c.SchoolId == schoolId);

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
