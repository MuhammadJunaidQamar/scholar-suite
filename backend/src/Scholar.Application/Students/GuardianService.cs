using System.Security.Cryptography;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scholar.Application.Abstractions;
using Scholar.Application.Access;
using Scholar.Application.Common;
using Scholar.Application.Identity;
using Scholar.Application.Organization;
using Scholar.Domain.Access;
using Scholar.Domain.Common;
using Scholar.Domain.Identity;
using Scholar.Domain.Students;

namespace Scholar.Application.Students;

public sealed record GuardianQuery : PageQuery
{
    public bool? HasPortalAccount { get; init; }
}

public sealed record GuardianChildDto(Guid StudentId, Guid LinkId, string FullName, string AdmissionNumber, string? ClassName,
    Guid? PhotoFileId, StudentStatus Status, GuardianRelationship Relationship, bool IsPrimary, bool IsFeePayer, bool PortalAccess);

public sealed record GuardianListItemDto(Guid Id, string FullName, string Phone, string? Email, string? Occupation, bool HasLogin,
    int ChildCount, IReadOnlyList<string> Children);

public sealed record GuardianDetailDto(Guid Id, string FirstName, string LastName, string FullName, string Phone, string? AlternatePhone,
    string? Email, string? NationalId, string? Occupation, string? Employer, AddressDto Address, string? PreferredLanguage,
    bool NotifyBySms, bool NotifyByEmail, bool NotifyByWhatsApp, bool HasLogin, string? LoginName, IReadOnlyList<GuardianChildDto> Children);

public sealed record SaveGuardianRequest(string FirstName, string LastName, string Phone, string? AlternatePhone, string? Email,
    string? NationalId, string? Occupation, string? Employer, AddressDto? Address, string? PreferredLanguage, bool NotifyBySms,
    bool NotifyByEmail, bool NotifyByWhatsApp);

public sealed record GuardianMatchDto(Guid Id, string FullName, string Phone, string? Email, IReadOnlyList<string> Children);

/// <summary>Credentials are shown once to the office when a guardian has no e-mail (printed or sent by SMS later).</summary>
public sealed record PortalAccessResult(string LoginName, string? TemporaryPassword, bool InvitationEmailed);

public sealed class SaveGuardianValidator : AbstractValidator<SaveGuardianRequest>
{
    public SaveGuardianValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).MaximumLength(100);
        RuleFor(x => x.Phone).NotEmpty().Must(p => p.Count(char.IsDigit) >= 10).WithMessage("Enter a mobile number, e.g. 0300 1234567.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.NationalId).NationalId();
    }
}

public sealed class GuardianService(
    IAppDbContext db,
    IAccessContext access,
    UserManager<User> users,
    IAccountNotifier notifier,
    IOptions<AuthOptions> authOptions)
{
    public async Task<PagedResult<GuardianListItemDto>> ListAsync(GuardianQuery query, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Guardians.View, ct);
        var guardians = db.Guardians.AsQueryable()
            .WhereIf(query.HasPortalAccount == true, g => g.UserId != null)
            .WhereIf(query.HasPortalAccount == false, g => g.UserId == null);
        if (query.Term is { } term)
        {
            var digits = new string(term.Where(char.IsDigit).ToArray());
            var phoneTail = digits.Length >= 10 ? digits[^10..] : digits;
            var pattern = $"%{term}%";
            guardians = guardians.Where(g => EF.Functions.ILike(g.FullName, pattern)
                                             || (digits.Length >= 4 && g.Phone.Contains(phoneTail))
                                             || (digits.Length >= 5 && g.NationalId != null && g.NationalId.Contains(digits))
                                             || g.Students.Any(l => EF.Functions.ILike(l.Student.FullName, pattern)));
        }

        var sorted = guardians.ApplySort(query, new SortMap<Guardian> { ["name"] = g => g.FullName, ["children"] = g => g.Students.Count }, "name");
        return await sorted.ToPagedAsync(query, g => new GuardianListItemDto(g.Id, g.FullName, g.Phone, g.Email, g.Occupation,
            g.UserId != null, g.Students.Count, g.Students.Select(l => l.Student.FirstName).ToList()), ct);
    }

    public async Task<GuardianDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Guardians.View, ct);
        var g = await db.Guardians.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Guardian", id);
        var children = await db.StudentGuardians.Where(l => l.GuardianId == id)
            .Select(l => new GuardianChildDto(l.StudentId, l.Id, l.Student.FullName, l.Student.AdmissionNumber,
                db.Sections.Where(s => s.Id == l.Student.CurrentSectionId).Select(s => s.Grade.Name + " – " + s.Name).FirstOrDefault(),
                l.Student.PhotoFileId, l.Student.Status, l.Relationship, l.IsPrimary, l.IsFeePayer, l.PortalAccess))
            .ToListAsync(ct);
        var login = g.UserId is null ? null : await db.Users.Where(u => u.Id == g.UserId).Select(u => u.Email ?? u.UserName).FirstOrDefaultAsync(ct);
        return new GuardianDetailDto(g.Id, g.FirstName, g.LastName, g.FullName, g.Phone, g.AlternatePhone, g.Email, g.NationalId,
            g.Occupation, g.Employer, AddressDto.From(g.Address), g.PreferredLanguage, g.NotifyBySms, g.NotifyByEmail, g.NotifyByWhatsApp,
            g.UserId is not null, login, children);
    }

    /// <summary>Exact-phone lookup so the office links an existing parent instead of creating a duplicate.</summary>
    public async Task<IReadOnlyList<GuardianMatchDto>> FindAsync(string phone, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Guardians.View, ct);
        if (phone.Count(char.IsDigit) < 7) return [];
        var normalized = Guardian.NormalizePhone(phone);
        return await db.Guardians.Where(g => g.Phone == normalized || g.AlternatePhone == phone)
            .Select(g => new GuardianMatchDto(g.Id, g.FullName, g.Phone, g.Email, g.Students.Select(l => l.Student.FullName).ToList()))
            .ToListAsync(ct);
    }

    public async Task<GuardianDetailDto> CreateAsync(SaveGuardianRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Guardians.Create, ct);
        var current = await access.GetAsync(ct);
        var guardian = new Guardian(current.SchoolId, request.FirstName, request.LastName, request.Phone);
        Apply(guardian, request);
        db.Guardians.Add(guardian);
        await db.SaveChangesAsync(ct);
        return await GetAsync(guardian.Id, ct);
    }

    public async Task<GuardianDetailDto> UpdateAsync(Guid id, SaveGuardianRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Guardians.Update, ct);
        var guardian = await db.Guardians.SingleOrDefaultAsync(g => g.Id == id, ct) ?? throw new NotFoundException("Guardian", id);
        guardian.Rename(request.FirstName, request.LastName);
        guardian.SetPhone(request.Phone);
        Apply(guardian, request);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Guardians.Delete, ct);
        var guardian = await db.Guardians.SingleOrDefaultAsync(g => g.Id == id, ct) ?? throw new NotFoundException("Guardian", id);
        Guard.Against(await db.StudentGuardians.AnyAsync(l => l.GuardianId == id, ct), "guardian.has_children",
            "Unlink this guardian from all students before deleting the record.");
        db.Guardians.Remove(guardian);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Gives a guardian a family-portal login. With an e-mail address an invitation is sent; otherwise a username based on
    /// the mobile number and a one-time password are returned for the office to hand over. The password must be changed at
    /// first sign-in.
    /// </summary>
    public async Task<PortalAccessResult> EnablePortalAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Guardians.ManagePortal, ct);
        var current = await access.GetAsync(ct);
        var guardian = await db.Guardians.SingleOrDefaultAsync(g => g.Id == id, ct) ?? throw new NotFoundException("Guardian", id);
        Guard.Against(guardian.UserId is not null, "guardian.portal_exists", "This guardian already has a portal login.");

        var role = await db.Roles.SingleAsync(r => r.Key == SystemRoles.Guardian, ct);
        User? user = null;
        if (!string.IsNullOrWhiteSpace(guardian.Email))
        {
            var normalizedEmail = users.NormalizeEmail(guardian.Email);
            user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
        }

        string? temporaryPassword = null;
        var emailed = false;
        await db.InTransactionAsync(async innerCt =>
        {
            if (user is null)
            {
                var userName = !string.IsNullOrWhiteSpace(guardian.Email) ? guardian.Email.Trim().ToLowerInvariant() : await UniqueUserNameAsync(guardian, innerCt);
                user = new User(userName, guardian.FirstName, guardian.LastName == string.Empty ? guardian.FirstName : guardian.LastName, guardian.Email)
                {
                    Status = UserStatus.Invited,
                    PhoneNumber = guardian.Phone,
                };
                IdentityResult created;
                if (string.IsNullOrWhiteSpace(guardian.Email))
                {
                    temporaryPassword = GeneratePassword();
                    user.MustChangePassword = true;
                    created = await users.CreateAsync(user, temporaryPassword);
                }
                else
                {
                    created = await users.CreateAsync(user);
                }

                if (!created.Succeeded) throw new DomainExceptionFromIdentity(created);
            }

            if (!await db.SchoolMemberships.AnyAsync(m => m.UserId == user.Id, innerCt))
            {
                db.SchoolMemberships.Add(new SchoolMembership(current.SchoolId, user.Id));
            }

            if (!await db.UserRoles.AnyAsync(r => r.UserId == user.Id && r.RoleId == role.Id, innerCt))
            {
                db.UserRoles.Add(new UserRole(current.SchoolId, user.Id, role.Id));
            }

            guardian.UserId = user.Id;
            await db.SaveChangesAsync(innerCt);
            return true;
        }, ct: ct);

        access.Invalidate(user!.Id);
        if (temporaryPassword is null && user.PasswordHash is null)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var link = $"{authOptions.Value.PublicAppUrl.TrimEnd('/')}/accept-invite?uid={user.Id}&token={Uri.EscapeDataString(token)}";
            var schoolName = await db.Schools.Where(s => s.Id == current.SchoolId).Select(s => s.Name).SingleAsync(ct);
            await notifier.SendInvitationAsync(user, schoolName, link, ct);
            emailed = true;
        }

        return new PortalAccessResult(user.Email ?? user.UserName!, temporaryPassword, emailed);
    }

    private async Task<string> UniqueUserNameAsync(Guardian guardian, CancellationToken ct)
    {
        var digits = new string(guardian.Phone.Where(char.IsDigit).ToArray());
        var candidate = $"p{digits}";
        var suffix = 1;
        while (await db.Users.AnyAsync(u => u.NormalizedUserName == users.NormalizeName(candidate), ct))
        {
            candidate = $"p{digits}-{++suffix}";
        }

        return candidate;
    }

    private static string GeneratePassword()
    {
        const string letters = "abcdefghjkmnpqrstuvwxyz";
        var bytes = RandomNumberGenerator.GetBytes(8);
        var word = new string(bytes.Take(6).Select(b => letters[b % letters.Length]).ToArray());
        return $"{char.ToUpperInvariant(word[0])}{word[1..]}-{RandomNumberGenerator.GetInt32(1000, 9999)}";
    }

    private static void Apply(Guardian g, SaveGuardianRequest r)
    {
        g.AlternatePhone = StudentService.Clean(r.AlternatePhone);
        g.Email = StudentService.Clean(r.Email);
        g.NationalId = StudentService.Digits(r.NationalId);
        g.Occupation = StudentService.Clean(r.Occupation);
        g.Employer = StudentService.Clean(r.Employer);
        g.Address = r.Address?.ToAddress() ?? new Address();
        g.PreferredLanguage = StudentService.Clean(r.PreferredLanguage);
        g.NotifyBySms = r.NotifyBySms;
        g.NotifyByEmail = r.NotifyByEmail;
        g.NotifyByWhatsApp = r.NotifyByWhatsApp;
    }
}

// ───────────────────────────── Global search ─────────────────────────────

public sealed record SearchResultDto(string Kind, Guid Id, string Title, string? Subtitle, string Href, Guid? PhotoFileId);

/// <summary>Backs the ⌘K palette: a few best matches per kind the caller may see.</summary>
public sealed class SearchService(IAppDbContext db, IAccessContext access)
{
    public async Task<IReadOnlyList<SearchResultDto>> SearchAsync(string? q, CancellationToken ct)
    {
        var term = q?.Trim();
        if (string.IsNullOrEmpty(term) || term.Length < 2) return [];
        var current = await access.GetAsync(ct);
        var pattern = $"%{term}%";
        var digits = new string(term.Where(char.IsDigit).ToArray());
        var phoneTail = digits.Length >= 10 ? digits[^10..] : digits;
        var results = new List<SearchResultDto>();

        if (current.Has(Permissions.Students.View))
        {
            var students = db.Students.Where(s => EF.Functions.ILike(s.FullName, pattern) || EF.Functions.ILike(s.AdmissionNumber, term + "%"));
            if (!current.AllCampuses) students = students.Where(s => current.CampusIds!.Contains(s.CampusId));
            results.AddRange(await students
                .OrderByDescending(s => s.Status == StudentStatus.Active).ThenBy(s => s.FullName).Take(6)
                .Select(s => new SearchResultDto("student", s.Id, s.FullName,
                    s.AdmissionNumber + " · " + (db.Sections.Where(x => x.Id == s.CurrentSectionId).Select(x => x.Grade.Name + " – " + x.Name).FirstOrDefault() ?? s.Status.ToString()),
                    "/students/" + s.Id, s.PhotoFileId))
                .ToListAsync(ct));
        }

        if (current.Has(Permissions.Guardians.View))
        {
            results.AddRange(await db.Guardians
                .Where(g => EF.Functions.ILike(g.FullName, pattern) || (digits.Length >= 4 && g.Phone.Contains(phoneTail)))
                .OrderBy(g => g.FullName).Take(4)
                .Select(g => new SearchResultDto("guardian", g.Id, g.FullName, g.Phone, "/guardians/" + g.Id, null))
                .ToListAsync(ct));
        }

        if (current.Has(Permissions.Admissions.View))
        {
            results.AddRange(await db.AdmissionApplications
                .Where(a => EF.Functions.ILike(a.FirstName + " " + a.LastName, pattern) || EF.Functions.ILike(a.ApplicationNumber, term + "%"))
                .OrderByDescending(a => a.CreatedAt).Take(4)
                .Select(a => new SearchResultDto("application", a.Id, a.FirstName + " " + a.LastName, a.ApplicationNumber + " · " + a.Status,
                    "/admissions/applications/" + a.Id, null))
                .ToListAsync(ct));
        }

        return results;
    }
}
