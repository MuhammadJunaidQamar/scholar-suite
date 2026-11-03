using Scholar.Domain.Common;

namespace Scholar.Domain.Documents;

public enum FileSensitivity { Normal, Restricted }

/// <summary>Owners a file can belong to; each owner type has its own authorization rules.</summary>
public static class FileOwners
{
    public const string School = "school";
    public const string Student = "student";
    public const string Guardian = "guardian";
    public const string Staff = "staff";
    public const string Application = "application";
}

public static class FileCategories
{
    public const string Photo = "photo";
    public const string Logo = "logo";
    public const string BirthCertificate = "birth_certificate";
    public const string NationalId = "national_id";
    public const string PreviousResult = "previous_result";
    public const string TransferCertificate = "transfer_certificate";
    public const string Medical = "medical";
    public const string Other = "other";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Photo, Logo, BirthCertificate, NationalId, PreviousResult, TransferCertificate, Medical, Other,
    };
}

/// <summary>
/// Metadata for a privately stored file. The bytes live in <c>IFileStorage</c> under an unguessable key and are only
/// ever served through an authorized endpoint — there are no public URLs.
/// </summary>
public sealed class StoredFile : TenantEntity, ISoftDeletable
{
    private StoredFile() { }

    public StoredFile(Guid schoolId, string ownerType, Guid ownerId, string category, string fileName, string contentType,
        long sizeBytes, string sha256, string storageKey, FileSensitivity sensitivity)
    {
        SchoolId = schoolId;
        OwnerType = ownerType;
        OwnerId = ownerId;
        Category = category;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
        StorageKey = storageKey;
        Sensitivity = sensitivity;
    }

    public string OwnerType { get; private set; } = default!;
    public Guid OwnerId { get; private set; }
    public string Category { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = default!;
    public string StorageKey { get; private set; } = default!;
    public FileSensitivity Sensitivity { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Re-files the document under another record (e.g. admission documents follow the new student).</summary>
    public void MoveTo(string ownerType, Guid ownerId)
    {
        OwnerType = ownerType;
        OwnerId = ownerId;
    }
}
