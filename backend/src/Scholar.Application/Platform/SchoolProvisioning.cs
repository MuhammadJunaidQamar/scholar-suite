using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Application.Identity;
using Scholar.Domain.Access;
using Scholar.Domain.Identity;
using Scholar.Domain.Organization;
using Scholar.Domain.Platform;

namespace Scholar.Application.Platform;

public sealed record ProvisionSchoolRequest(
    string Code,
    string Name,
    string? ShortName,
    string MainCampusName,
    ProvisionOwnerRequest Owner,
    string Timezone = "Asia/Karachi",
    string CurrencyCode = "PKR",
    string Locale = "en-PK");

public sealed record ProvisionOwnerRequest(string FirstName, string LastName, string Email, string? Password = null);

public sealed record PlatformSchoolDto(Guid Id, string Code, string Name, string? ShortName, SchoolStatus Status,
    string Timezone, string CurrencyCode, int CampusCount, int MemberCount, DateTimeOffset CreatedAt);

/// <param name="OwnerHandover">How the new owner's first sign-in was handed over; null when they already had an account
/// or a password was supplied.</param>
public sealed record ProvisionedSchoolDto(PlatformSchoolDto School, Identity.AccountHandover? OwnerHandover);

/// <summary>
/// A module's contribution to a brand-new school (default roles, chart of accounts, attendance statuses…).
/// Runs inside the provisioning transaction with the new school as tenant.
/// </summary>
public interface ISchoolProvisioningStep
{
    int Order { get; }
    Task ApplyAsync(School school, CancellationToken ct);
}

public sealed class ProvisionSchoolRequestValidator : AbstractValidator<ProvisionSchoolRequest>
{
    public ProvisionSchoolRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches("^[a-z0-9][a-z0-9-]{1,30}[a-z0-9]$")
            .WithMessage("Use 3–32 lowercase letters, digits or hyphens.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ShortName).MaximumLength(60);
        RuleFor(x => x.MainCampusName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Timezone).NotEmpty().Must(BeKnownTimezone).WithMessage("Unknown timezone.");
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.Owner).NotNull();
        RuleFor(x => x.Owner.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Owner.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Owner.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Owner.Password!).ApplyPasswordPolicy().When(x => x.Owner.Password is not null);
    }

    private static bool BeKnownTimezone(string id) => TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
}

public sealed class SchoolProvisioningService(
    IAppDbContext db,
    ITenantContext tenant,
    IEnumerable<ISchoolProvisioningStep> steps,
    UserManager<User> users,
    AccountOnboarding onboarding)
{
    public async Task<ProvisionedSchoolDto> ProvisionAsync(ProvisionSchoolRequest request, CancellationToken ct)
    {
        var code = School.NormalizeCode(request.Code);
        if (await db.Schools.AnyAsync(s => s.Code == code, ct))
        {
            throw new ConflictException("school.code_taken", "A school with this code already exists.");
        }

        var (schoolId, inviteUser) = await db.InTransactionAsync(async innerCt =>
        {
            var school = new School(code, request.Name)
            {
                ShortName = request.ShortName,
                Timezone = request.Timezone,
                CurrencyCode = request.CurrencyCode.ToUpperInvariant(),
                Locale = request.Locale,
                Address = new Domain.Common.Address { CountryCode = "PK" },
            };
            db.Schools.Add(school);
            await db.SaveChangesAsync(innerCt);

            using (tenant.Enter(school.Id))
            {
                db.Campuses.Add(new Campus(school.Id, "MAIN", request.MainCampusName, isMain: true));
                foreach (var step in steps.OrderBy(s => s.Order))
                {
                    await step.ApplyAsync(school, innerCt);
                }

                await db.SaveChangesAsync(innerCt);

                var (owner, needsInvite) = await FindOrCreateOwnerAsync(request.Owner);
                db.SchoolMemberships.Add(new SchoolMembership(school.Id, owner.Id));
                var ownerRole = await db.Roles.SingleAsync(r => r.Key == SystemRoles.Owner, innerCt);
                db.UserRoles.Add(new UserRole(school.Id, owner.Id, ownerRole.Id));
                owner.LastSchoolId ??= school.Id;
                await db.SaveChangesAsync(innerCt);
                return (school.Id, needsInvite ? owner : null);
            }
        }, ct: ct);

        var handover = inviteUser is not null ? await onboarding.HandOverAsync(inviteUser, request.Name, ct) : null;
        return new ProvisionedSchoolDto(await GetAsync(schoolId, ct), handover);
    }

    public async Task<IReadOnlyList<PlatformSchoolDto>> ListAsync(CancellationToken ct) =>
        await QueryAsync(null, ct);

    public async Task<PlatformSchoolDto> GetAsync(Guid id, CancellationToken ct) =>
        (await QueryAsync(id, ct)).SingleOrDefault() ?? throw new NotFoundException("School", id);

    public async Task SetStatusAsync(Guid id, bool active, CancellationToken ct)
    {
        var school = await db.Schools.SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("School", id);
        if (active)
        {
            school.Activate();
        }
        else
        {
            school.Suspend();
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Platform-wide listing; tenant filters are deliberately bypassed (platform administrators only).</summary>
    private async Task<IReadOnlyList<PlatformSchoolDto>> QueryAsync(Guid? id, CancellationToken ct)
    {
        var schools = await db.Schools.WhereIf(id is not null, s => s.Id == id).OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Code, s.Name, s.ShortName, s.Status, s.Timezone, s.CurrencyCode, s.CreatedAt })
            .ToListAsync(ct);
        var ids = schools.Select(s => s.Id).ToList();
        var campuses = await db.Campuses.IgnoreQueryFilters().Where(c => ids.Contains(c.SchoolId))
            .GroupBy(c => c.SchoolId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        var members = await db.SchoolMemberships.IgnoreQueryFilters().Where(m => ids.Contains(m.SchoolId))
            .GroupBy(m => m.SchoolId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        return schools.Select(s => new PlatformSchoolDto(s.Id, s.Code, s.Name, s.ShortName, s.Status, s.Timezone,
            s.CurrencyCode, campuses.GetValueOrDefault(s.Id), members.GetValueOrDefault(s.Id), s.CreatedAt)).ToList();
    }

    private async Task<(User User, bool NeedsInvite)> FindOrCreateOwnerAsync(ProvisionOwnerRequest owner)
    {
        var normalized = users.NormalizeEmail(owner.Email);
        var existing = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalized);
        if (existing is not null)
        {
            return (existing, false);
        }

        var user = new User(owner.Email, owner.FirstName, owner.LastName, owner.Email)
        {
            Status = owner.Password is null ? UserStatus.Invited : UserStatus.Active,
        };
        var result = owner.Password is null ? await users.CreateAsync(user) : await users.CreateAsync(user, owner.Password);
        if (!result.Succeeded)
        {
            throw new DomainExceptionFromIdentity(result);
        }

        return (user, owner.Password is null);
    }
}

/// <summary>Seeds the built-in roles with their default permissions.</summary>
internal sealed class SystemRolesProvisioningStep(IAppDbContext db) : ISchoolProvisioningStep
{
    public int Order => 10;

    public Task ApplyAsync(School school, CancellationToken ct)
    {
        foreach (var definition in SystemRoles.All)
        {
            var role = new Role(school.Id, definition.Name, definition.Portal, definition.Key, isSystem: true, isLocked: definition.Locked)
            {
                Description = definition.Description,
            };
            if (!definition.Locked)
            {
                role.SetPermissions(definition.Permissions);
            }

            db.Roles.Add(role);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Numbering, designations, departments and a grading scale so a new school is usable immediately.</summary>
internal sealed class OrganizationDefaultsProvisioningStep(IAppDbContext db) : ISchoolProvisioningStep
{
    public int Order => 20;

    public Task ApplyAsync(School school, CancellationToken ct)
    {
        foreach (var (key, (prefix, pattern, reset)) in SequenceKeys.Defaults)
        {
            db.NumberSequences.Add(new NumberSequence(school.Id, key, prefix, pattern, reset));
        }

        (string Name, DesignationCategory Category)[] designations =
        [
            ("Principal", DesignationCategory.Administrative), ("Vice Principal", DesignationCategory.Administrative),
            ("Academic Coordinator", DesignationCategory.Teaching), ("Senior Teacher", DesignationCategory.Teaching),
            ("Teacher", DesignationCategory.Teaching), ("Assistant Teacher", DesignationCategory.Teaching),
            ("Accountant", DesignationCategory.Administrative), ("Admissions Officer", DesignationCategory.Administrative),
            ("Registrar", DesignationCategory.Administrative), ("HR Officer", DesignationCategory.Administrative),
            ("Librarian", DesignationCategory.NonTeaching), ("Lab Assistant", DesignationCategory.NonTeaching),
            ("School Nurse", DesignationCategory.NonTeaching), ("Transport Manager", DesignationCategory.NonTeaching),
            ("Driver", DesignationCategory.Support), ("Bus Attendant", DesignationCategory.Support),
            ("Office Assistant", DesignationCategory.Support), ("Security Guard", DesignationCategory.Support),
            ("Housekeeping", DesignationCategory.Support),
        ];
        foreach (var (name, category) in designations)
        {
            db.Designations.Add(new Designation(school.Id, name, category));
        }

        (string Code, string Name)[] departments =
        [
            ("ADM", "Administration"), ("ACC", "Accounts & Finance"), ("HR", "Human Resources"),
            ("EY", "Early Years"), ("SCI", "Sciences"), ("MATH", "Mathematics"), ("LANG", "Languages"),
            ("HUM", "Humanities & Islamiyat"), ("CS", "Computer Science"), ("ART", "Arts & Physical Education"),
            ("LIB", "Library"), ("TRN", "Transport"), ("FAC", "Facilities"),
        ];
        foreach (var (code, name) in departments)
        {
            db.Departments.Add(new Department(school.Id, code, name));
        }

        var scale = new GradingScale(school.Id, "Standard percentage scale", GradingScaleType.Percentage) { IsDefault = true };
        scale.ReplaceBands([
            (90m, 100m, "A+", 4.0m, "Outstanding", true),
            (80m, 89.99m, "A", 4.0m, "Excellent", true),
            (70m, 79.99m, "B", 3.0m, "Very good", true),
            (60m, 69.99m, "C", 2.5m, "Good", true),
            (50m, 59.99m, "D", 2.0m, "Satisfactory", true),
            (33m, 49.99m, "E", 1.0m, "Pass", true),
            (0m, 32.99m, "F", 0m, "Fail", false),
        ]);
        db.GradingScales.Add(scale);
        return Task.CompletedTask;
    }
}
