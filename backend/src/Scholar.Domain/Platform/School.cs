using System.Text.RegularExpressions;
using Scholar.Domain.Common;

namespace Scholar.Domain.Platform;

public enum SchoolStatus { Active, Suspended, Archived }

/// <summary>The tenant. Everything a school owns references it through <c>school_id</c>.</summary>
public sealed partial class School : AuditableEntity, IHasAuditTrail, IConcurrencyVersioned
{
    private School() { }

    public School(string code, string name)
    {
        Code = NormalizeCode(code);
        Name = name.Trim();
    }

    public string Code { get; private set; } = default!;
    public string Name { get; set; } = default!;
    public string? ShortName { get; set; }
    public string? LegalName { get; set; }
    public string? Motto { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public Address Address { get; set; } = new();

    /// <summary>IANA timezone used to interpret "today" for attendance, due dates and reports.</summary>
    public string Timezone { get; set; } = "Asia/Karachi";
    public string CurrencyCode { get; set; } = "PKR";
    public string Locale { get; set; } = "en-PK";

    /// <summary>Accent colour (#RRGGBB) applied to the school's interface and documents.</summary>
    public string? BrandColor { get; set; }
    public Guid? LogoFileId { get; set; }

    public SchoolStatus Status { get; private set; } = SchoolStatus.Active;
    public List<string> EnabledModules { get; set; } = [.. SchoolModules.Defaults];
    public SchoolSettings Settings { get; set; } = new();
    public uint Version { get; set; }

    public ICollection<Campus> Campuses { get; } = new List<Campus>();

    public bool IsModuleEnabled(string module) => SchoolModules.Core.Contains(module) || EnabledModules.Contains(module);

    public void Suspend() => Status = SchoolStatus.Suspended;
    public void Activate() => Status = SchoolStatus.Active;

    public static string NormalizeCode(string code)
    {
        var normalized = code.Trim().ToLowerInvariant();
        Guard.Against(!CodePattern().IsMatch(normalized), "school.code_invalid",
            "School code must be 3–32 characters: lowercase letters, digits and hyphens.");
        return normalized;
    }

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{1,30})[a-z0-9]$")]
    private static partial Regex CodePattern();
}

public static class SchoolModules
{
    public const string Admissions = "admissions";
    public const string Attendance = "attendance";
    public const string Fees = "fees";
    public const string Exams = "exams";
    public const string Academics = "academics";
    public const string Hr = "hr";
    public const string Payroll = "payroll";
    public const string Communication = "communication";
    public const string Library = "library";
    public const string Transport = "transport";
    public const string Inventory = "inventory";
    public const string Welfare = "welfare";
    public const string Events = "events";
    public const string Hostel = "hostel";
    public const string Cafeteria = "cafeteria";

    /// <summary>Modules that cannot be switched off.</summary>
    public static readonly IReadOnlySet<string> Core = new HashSet<string> { Academics, Attendance, Communication };

    public static readonly IReadOnlyList<string> Optional =
        [Admissions, Fees, Exams, Hr, Payroll, Library, Transport, Inventory, Welfare, Events, Hostel, Cafeteria];

    public static readonly IReadOnlyList<string> Defaults =
        [Admissions, Attendance, Fees, Exams, Academics, Hr, Payroll, Communication, Library, Transport, Inventory, Welfare, Events];

    public static readonly IReadOnlyList<string> All = [.. Core.Order(), .. Optional];
}

/// <summary>Per-school behaviour switches, persisted as one jsonb document.</summary>
public sealed class SchoolSettings
{
    /// <summary>Teaching days (Pakistan default Monday–Saturday).</summary>
    public List<DayOfWeek> WorkingDays { get; set; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday];

    public AttendanceSettings Attendance { get; set; } = new();
    public FinanceSettings Finance { get; set; } = new();
    public AdmissionSettings Admissions { get; set; } = new();
    public AcademicSettings Academics { get; set; } = new();
}

public enum AttendanceMode { Daily, PeriodWise }

public sealed class AttendanceSettings
{
    public AttendanceMode Mode { get; set; } = AttendanceMode.Daily;
    /// <summary>Hours after the register date ends before submitted registers lock and edits require a correction.</summary>
    public int LockAfterHours { get; set; } = 24;
    public bool NotifyGuardiansOnAbsence { get; set; } = true;
    public bool NotifyGuardiansOnLate { get; set; }
    /// <summary>When true, Excused days count as present in percentages; otherwise they are excluded.</summary>
    public bool ExcusedCountsAsPresent { get; set; }
    public decimal MinimumPercentage { get; set; } = 75m;
}

public enum LateFeeMode { None, FixedOnce, PerDay }

public sealed class FinanceSettings
{
    public int DefaultDueDay { get; set; } = 10;
    public LateFeeMode LateFeeMode { get; set; } = LateFeeMode.FixedOnce;
    public decimal LateFeeAmount { get; set; } = 500m;
    public int LateFeeGraceDays { get; set; }
    public decimal? LateFeeCap { get; set; } = 1500m;
    public bool AllowPartialPayments { get; set; } = true;
    public bool ConcessionsRequireApproval { get; set; } = true;
    public bool RefundsRequireApproval { get; set; } = true;
    public int ChallanCopies { get; set; } = 3;
    public string? ChallanInstructions { get; set; } =
        "Fee is payable by the due date at any branch of the listed banks or at the school accounts office.";
}

public sealed class AdmissionSettings
{
    public bool OnlineApplicationsEnabled { get; set; } = true;
    public bool RequireEntranceTest { get; set; } = true;
    public bool RequireInterview { get; set; }
    public bool RequireAdmissionFeeBeforeEnrollment { get; set; } = true;
    public int OfferValidityDays { get; set; } = 14;
    public List<string> RequiredDocuments { get; set; } = ["Birth certificate / B-Form", "Previous school result", "Guardian CNIC", "Photograph"];
}

public sealed class AcademicSettings
{
    /// <summary>Minimum overall and per-subject percentage to pass (33% is the common Pakistani board threshold).</summary>
    public decimal PassPercentage { get; set; } = 33m;
    public bool ShowPositions { get; set; } = true;
    public int MarksDecimals { get; set; } = 1;
}
