using Scholar.Domain.Common;

namespace Scholar.Domain.Platform;

/// <summary>A physical branch of a school. Most operational data (students, sections, registers, cash) is campus-owned.</summary>
public sealed class Campus : TenantEntity, IHasAuditTrail
{
    private Campus() { }

    public Campus(Guid schoolId, string code, string name, bool isMain = false)
    {
        SchoolId = schoolId;
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        IsMain = isMain;
    }

    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public Address Address { get; set; } = new();
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsMain { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Configurable document numbering (admission numbers, receipts, challans…). Advanced atomically in SQL.</summary>
public sealed class NumberSequence : TenantEntity
{
    private NumberSequence() { }

    public NumberSequence(Guid schoolId, string key, string prefix, string pattern, SequenceResetPolicy resetPolicy)
    {
        SchoolId = schoolId;
        Key = key;
        Prefix = prefix;
        Pattern = pattern;
        ResetPolicy = resetPolicy;
    }

    public string Key { get; private set; } = default!;
    public string Prefix { get; set; } = default!;
    /// <summary>Tokens: {prefix} {yyyy} {yy} {mm} {seq} {seq:N} (N = zero-padded width).</summary>
    public string Pattern { get; set; } = default!;
    public SequenceResetPolicy ResetPolicy { get; set; }
    public string PeriodKey { get; set; } = string.Empty;
    public long NextValue { get; set; } = 1;
}

public enum SequenceResetPolicy { Never, Yearly, Monthly }

public static class SequenceKeys
{
    public const string Admission = "admission";
    public const string Application = "application";
    public const string Employee = "employee";
    public const string Invoice = "invoice";
    public const string Receipt = "receipt";
    public const string Refund = "refund";
    public const string Journal = "journal";
    public const string Certificate = "certificate";
    public const string PurchaseOrder = "purchase_order";
    public const string Accession = "accession";
    public const string Payroll = "payroll";

    /// <summary>Default prefix, pattern and reset policy per key.</summary>
    public static readonly IReadOnlyDictionary<string, (string Prefix, string Pattern, SequenceResetPolicy Reset)> Defaults =
        new Dictionary<string, (string, string, SequenceResetPolicy)>
        {
            [Admission] = ("ADM", "{prefix}-{yyyy}-{seq:4}", SequenceResetPolicy.Yearly),
            [Application] = ("APP", "{prefix}-{yy}{mm}-{seq:4}", SequenceResetPolicy.Monthly),
            [Employee] = ("EMP", "{prefix}-{seq:4}", SequenceResetPolicy.Never),
            [Invoice] = ("CH", "{prefix}-{yy}{mm}-{seq:5}", SequenceResetPolicy.Monthly),
            [Receipt] = ("RCT", "{prefix}-{yy}{mm}-{seq:5}", SequenceResetPolicy.Monthly),
            [Refund] = ("RFD", "{prefix}-{yyyy}-{seq:4}", SequenceResetPolicy.Yearly),
            [Journal] = ("JE", "{prefix}-{yyyy}-{seq:6}", SequenceResetPolicy.Yearly),
            [Certificate] = ("CERT", "{prefix}-{yyyy}-{seq:4}", SequenceResetPolicy.Yearly),
            [PurchaseOrder] = ("PO", "{prefix}-{yyyy}-{seq:4}", SequenceResetPolicy.Yearly),
            [Accession] = ("ACC", "{prefix}-{seq:6}", SequenceResetPolicy.Never),
            [Payroll] = ("PR", "{prefix}-{yyyy}{mm}", SequenceResetPolicy.Never),
        };
}
