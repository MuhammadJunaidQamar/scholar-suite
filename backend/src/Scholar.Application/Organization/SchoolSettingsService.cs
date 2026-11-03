using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Access;
using Scholar.Application.Common;
using Scholar.Domain.Access;
using Scholar.Domain.Common;
using Scholar.Domain.Platform;

namespace Scholar.Application.Organization;

public sealed record AddressDto(string? Line1, string? Line2, string? City, string? Province, string? PostalCode, string? CountryCode)
{
    public static AddressDto From(Address a) => new(a.Line1, a.Line2, a.City, a.Province, a.PostalCode, a.CountryCode);

    public Address ToAddress() => new()
    {
        Line1 = Clean(Line1), Line2 = Clean(Line2), City = Clean(City), Province = Clean(Province),
        PostalCode = Clean(PostalCode), CountryCode = Clean(CountryCode)?.ToUpperInvariant(),
    };

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed record SchoolProfileDto(
    Guid Id, string Code, string Name, string? ShortName, string? LegalName, string? Motto, string? RegistrationNumber,
    string? Email, string? Phone, string? Website, AddressDto Address, string Timezone, string CurrencyCode, string Locale,
    string? BrandColor, Guid? LogoFileId, IReadOnlyList<string> EnabledModules, SchoolSettings Settings);

public sealed record UpdateSchoolProfileRequest(
    string Name, string? ShortName, string? LegalName, string? Motto, string? RegistrationNumber, string? Email,
    string? Phone, string? Website, AddressDto Address, string Timezone, string CurrencyCode, string Locale, string? BrandColor);

public sealed record UpdateModulesRequest(IReadOnlyList<string> Enabled);

public sealed record ModuleDto(string Key, bool Enabled, bool Core);

public sealed record SequenceDto(string Key, string Prefix, string Pattern, SequenceResetPolicy ResetPolicy, long NextValue,
    string Preview);

/// <param name="NextNumber">Optionally moves the current period's counter forward (e.g. to continue numbering from a
/// previous system). Counters never move backwards, so an issued number is never issued again.</param>
public sealed record UpdateSequenceRequest(string Prefix, string Pattern, SequenceResetPolicy ResetPolicy, long? NextNumber = null);

public sealed class UpdateSchoolProfileValidator : AbstractValidator<UpdateSchoolProfileRequest>
{
    public UpdateSchoolProfileValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ShortName).MaximumLength(60);
        RuleFor(x => x.LegalName).MaximumLength(250);
        RuleFor(x => x.Motto).MaximumLength(200);
        RuleFor(x => x.RegistrationNumber).MaximumLength(80);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(254).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.Website).MaximumLength(200)
            .Must(w => Uri.TryCreate(w, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http"))
            .When(x => !string.IsNullOrWhiteSpace(x.Website)).WithMessage("Enter a full web address, e.g. https://school.edu.pk");
        RuleFor(x => x.Timezone).NotEmpty().Must(t => TimeZoneInfo.TryFindSystemTimeZoneById(t, out _)).WithMessage("Unknown timezone.");
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3).Matches("^[A-Za-z]{3}$");
        RuleFor(x => x.Locale).NotEmpty().MaximumLength(16);
        RuleFor(x => x.BrandColor).Matches("^#[0-9A-Fa-f]{6}$").When(x => !string.IsNullOrWhiteSpace(x.BrandColor))
            .WithMessage("Use a hex colour such as #1F5F4A.");
        RuleFor(x => x.Address.CountryCode).Length(2).When(x => !string.IsNullOrWhiteSpace(x.Address.CountryCode));
    }
}

public sealed class SchoolSettingsValidator : AbstractValidator<SchoolSettings>
{
    public SchoolSettingsValidator()
    {
        RuleFor(x => x.WorkingDays).NotEmpty().WithMessage("Choose at least one working day.");
        RuleFor(x => x.Attendance.LockAfterHours).InclusiveBetween(0, 24 * 14);
        RuleFor(x => x.Attendance.MinimumPercentage).InclusiveBetween(0, 100);
        RuleFor(x => x.Finance.DefaultDueDay).InclusiveBetween(1, 28);
        RuleFor(x => x.Finance.LateFeeAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Finance.LateFeeGraceDays).InclusiveBetween(0, 60);
        RuleFor(x => x.Finance.LateFeeCap).GreaterThanOrEqualTo(0).When(x => x.Finance.LateFeeCap is not null);
        RuleFor(x => x.Finance.ChallanCopies).InclusiveBetween(1, 4);
        RuleFor(x => x.Admissions.OfferValidityDays).InclusiveBetween(1, 120);
        RuleFor(x => x.Academics.PassPercentage).InclusiveBetween(0, 100);
        RuleFor(x => x.Academics.MarksDecimals).InclusiveBetween(0, 2);
    }
}

public sealed class UpdateSequenceValidator : AbstractValidator<UpdateSequenceRequest>
{
    public UpdateSequenceValidator()
    {
        RuleFor(x => x.Prefix).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9/-]+$")
            .WithMessage("Use letters, digits, '/' or '-'.");
        RuleFor(x => x.Pattern).NotEmpty().MaximumLength(80).Must(p => p.Contains("{seq", StringComparison.Ordinal))
            .WithMessage("The pattern must contain {seq} or {seq:N}.");
        // A counter that restarts must carry its period in the number, or the restarted numbers would repeat old ones.
        RuleFor(x => x.Pattern)
            .Must(p => p.Contains("{yyyy}", StringComparison.Ordinal) || p.Contains("{yy}", StringComparison.Ordinal))
            .When(x => x.ResetPolicy != SequenceResetPolicy.Never)
            .WithMessage("Numbers that restart each year or month must include the year ({yyyy} or {yy}), or they would repeat.");
        RuleFor(x => x.Pattern).Must(p => p.Contains("{mm}", StringComparison.Ordinal))
            .When(x => x.ResetPolicy == SequenceResetPolicy.Monthly)
            .WithMessage("Numbers that restart each month must include the month ({mm}), or they would repeat.");
        RuleFor(x => x.NextNumber).InclusiveBetween(1, 999_999_999).When(x => x.NextNumber is not null);
    }
}

/// <summary>School profile, branding, optional modules, policy settings and document numbering.</summary>
public sealed class SchoolSettingsService(
    IAppDbContext db, IAccessContext access, ISchoolModuleGate modules, ITenantContext tenant, ISchoolClock schoolClock)
{
    private async Task<School> LoadAsync(CancellationToken ct) =>
        await db.Schools.SingleOrDefaultAsync(s => s.Id == tenant.SchoolId, ct) ?? throw new NotFoundException("School");

    public async Task<SchoolProfileDto> GetAsync(CancellationToken ct)
    {
        await access.RequireAsync(Permissions.School.View, ct);
        return ToDto(await LoadAsync(ct));
    }

    public async Task<SchoolProfileDto> UpdateProfileAsync(UpdateSchoolProfileRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.School.Manage, ct);
        var school = await LoadAsync(ct);
        school.Name = request.Name.Trim();
        school.ShortName = Clean(request.ShortName);
        school.LegalName = Clean(request.LegalName);
        school.Motto = Clean(request.Motto);
        school.RegistrationNumber = Clean(request.RegistrationNumber);
        school.Email = Clean(request.Email);
        school.Phone = Clean(request.Phone);
        school.Website = Clean(request.Website);
        school.Address = request.Address.ToAddress();
        school.Timezone = request.Timezone;
        school.CurrencyCode = request.CurrencyCode.ToUpperInvariant();
        school.Locale = request.Locale;
        school.BrandColor = Clean(request.BrandColor)?.ToUpperInvariant();
        await db.SaveChangesAsync(ct);
        return ToDto(school);
    }

    public async Task<IReadOnlyList<ModuleDto>> ListModulesAsync(CancellationToken ct)
    {
        await access.RequireAsync(Permissions.School.View, ct);
        var school = await LoadAsync(ct);
        return SchoolModules.All.Select(m => new ModuleDto(m, school.IsModuleEnabled(m), SchoolModules.Core.Contains(m))).ToList();
    }

    public async Task<IReadOnlyList<ModuleDto>> UpdateModulesAsync(UpdateModulesRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.School.Manage, ct);
        var unknown = request.Enabled.Where(m => !SchoolModules.All.Contains(m)).ToList();
        Guard.Against(unknown.Count > 0, "modules.unknown", $"Unknown module(s): {string.Join(", ", unknown)}.");
        Guard.Against(request.Enabled.Contains(SchoolModules.Payroll) && !request.Enabled.Contains(SchoolModules.Hr),
            "modules.payroll_requires_hr", "Payroll needs the HR module.");

        var school = await LoadAsync(ct);
        school.EnabledModules = SchoolModules.Optional.Where(request.Enabled.Contains).Concat(SchoolModules.Core).Distinct().ToList();
        await db.SaveChangesAsync(ct);
        modules.Invalidate(school.Id);
        return SchoolModules.All.Select(m => new ModuleDto(m, school.IsModuleEnabled(m), SchoolModules.Core.Contains(m))).ToList();
    }

    public async Task<SchoolSettings> UpdatePoliciesAsync(SchoolSettings settings, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.School.Manage, ct);
        var school = await LoadAsync(ct);
        settings.WorkingDays = settings.WorkingDays.Distinct().Order().ToList();
        school.Settings = settings;
        await db.SaveChangesAsync(ct);
        return school.Settings;
    }

    public async Task<IReadOnlyList<SequenceDto>> ListSequencesAsync(CancellationToken ct)
    {
        await access.RequireAsync(Permissions.School.View, ct);
        var today = await schoolClock.TodayAsync(ct);
        var sequences = await db.NumberSequences.OrderBy(s => s.Key).ToListAsync(ct);
        var periods = sequences.Select(s => NumberSequence.PeriodFor(s.ResetPolicy, today)).Distinct().ToList();
        var counters = await db.NumberSequenceCounters.Where(c => periods.Contains(c.PeriodKey))
            .ToDictionaryAsync(c => (c.Key, c.PeriodKey), c => c.NextValue, ct);
        return sequences
            .Select(s => ToDto(s, counters.GetValueOrDefault((s.Key, NumberSequence.PeriodFor(s.ResetPolicy, today)), 1), today))
            .ToList();
    }

    public async Task<SequenceDto> UpdateSequenceAsync(string key, UpdateSequenceRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.School.Manage, ct);
        var today = await schoolClock.TodayAsync(ct);
        var sequence = await db.NumberSequences.SingleOrDefaultAsync(s => s.Key == key, ct) ?? throw new NotFoundException("Sequence", key);
        var policyChanged = sequence.ResetPolicy != request.ResetPolicy;
        sequence.Prefix = request.Prefix.Trim().ToUpperInvariant();
        sequence.Pattern = request.Pattern.Trim();
        sequence.ResetPolicy = request.ResetPolicy;

        var period = NumberSequence.PeriodFor(sequence.ResetPolicy, today);
        var counter = await db.NumberSequenceCounters.SingleOrDefaultAsync(c => c.Key == key && c.PeriodKey == period, ct);
        var floor = counter?.NextValue ?? 1;
        if (policyChanged)
        {
            // A new rhythm continues from the highest number issued under any period, so a pattern that still prints the
            // same tokens can never repeat a number issued under the old rhythm.
            floor = Math.Max(floor, await db.NumberSequenceCounters.Where(c => c.Key == key).MaxAsync(c => (long?)c.NextValue, ct) ?? 1);
        }

        if (request.NextNumber is { } requested)
        {
            Guard.Against(requested < floor, "sequence.cannot_go_back",
                $"Numbers only move forward, so an issued number is never reused. The next number is already {floor}.");
            floor = requested;
        }

        if (floor > (counter?.NextValue ?? 1))
        {
            if (counter is null)
            {
                db.NumberSequenceCounters.Add(counter = new NumberSequenceCounter(sequence.SchoolId, key, period, floor));
            }
            else
            {
                counter.NextValue = floor;
            }
        }

        await db.SaveChangesAsync(ct);
        return ToDto(sequence, counter?.NextValue ?? 1, today);
    }

    /// <summary>Formats a sample number with the given pattern, used for live previews while editing.</summary>
    public static string Preview(string pattern, string prefix, long value, DateOnly date) => SequencePattern.Format(pattern, prefix, value, date);

    private static SequenceDto ToDto(NumberSequence s, long nextValue, DateOnly today) =>
        new(s.Key, s.Prefix, s.Pattern, s.ResetPolicy, nextValue, SequencePattern.Format(s.Pattern, s.Prefix, nextValue, today));

    private static SchoolProfileDto ToDto(School s) => new(s.Id, s.Code, s.Name, s.ShortName, s.LegalName, s.Motto,
        s.RegistrationNumber, s.Email, s.Phone, s.Website, AddressDto.From(s.Address), s.Timezone, s.CurrencyCode, s.Locale,
        s.BrandColor, s.LogoFileId, s.EnabledModules, s.Settings);

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
