using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Domain.Access;
using Scholar.Domain.Common;
using Scholar.Domain.Documents;

namespace Scholar.Application.Documents;

/// <summary>Private blob storage. Implementations must never expose public URLs.</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string extension, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}

public sealed record FileUpload(Stream Content, string FileName, string? ContentType, long Length);

public sealed record StoredFileDto(Guid Id, string OwnerType, Guid OwnerId, string Category, string FileName, string ContentType,
    long SizeBytes, FileSensitivity Sensitivity, string? Notes, DateTimeOffset UploadedAt, Guid? UploadedBy, string? UploadedByName);

public sealed record FileContent(Stream Content, string ContentType, string FileName);

/// <summary>
/// Upload, list, download and delete files attached to records. Every call is authorized against the owning record's
/// permissions (and campus scope), uploads are type-sniffed rather than trusting the client, and deletes are soft.
/// </summary>
public sealed class FileService(IAppDbContext db, IAccessContext access, IFileStorage storage)
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public const long MaxImageBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, (string Extension, byte[][] Signatures)> Allowed = new()
    {
        ["image/jpeg"] = (".jpg", [[0xFF, 0xD8, 0xFF]]),
        ["image/png"] = (".png", [[0x89, 0x50, 0x4E, 0x47]]),
        ["image/webp"] = (".webp", [[0x52, 0x49, 0x46, 0x46]]),
        ["application/pdf"] = (".pdf", [[0x25, 0x50, 0x44, 0x46]]),
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = (".docx", [[0x50, 0x4B, 0x03, 0x04]]),
    };

    public async Task<StoredFileDto> UploadAsync(string ownerType, Guid ownerId, string category, FileUpload upload, string? notes,
        CancellationToken ct)
    {
        Guard.Against(!FileCategories.All.Contains(category), "file.category_unknown", "Unknown document category.");
        await AuthorizeAsync(ownerType, ownerId, category, write: true, ct);
        Guard.Against(upload.Length <= 0, "file.empty", "The file is empty.");
        Guard.Against(upload.Length > MaxBytes, "file.too_large", "Files can be at most 10 MB.");

        // Read the head to identify the real type; never trust the browser-supplied content type.
        await using var buffer = new MemoryStream();
        await upload.Content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var detected = Allowed.FirstOrDefault(a => a.Value.Signatures.Any(sig => bytes.Length >= sig.Length && bytes.AsSpan(0, sig.Length).SequenceEqual(sig)));
        Guard.Against(detected.Key is null, "file.type_not_allowed", "Upload a JPG, PNG, WebP, PDF or Word document.");
        var contentType = detected.Key!;
        // RIFF is shared by WAV/AVI: WebP must carry "WEBP" at byte 8. ZIP is shared by every archive: require .docx.
        Guard.Against(contentType == "image/webp" && (bytes.Length < 12 || !bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8)),
            "file.type_not_allowed", "Upload a JPG, PNG, WebP, PDF or Word document.");
        Guard.Against(contentType.EndsWith("wordprocessingml.document", StringComparison.Ordinal)
                      && !upload.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase),
            "file.type_not_allowed", "Upload a JPG, PNG, WebP, PDF or Word document.");
        if (category is FileCategories.Photo or FileCategories.Logo)
        {
            Guard.Against(!contentType.StartsWith("image/", StringComparison.Ordinal), "file.image_required", "Photos must be JPG, PNG or WebP images.");
            Guard.Against(bytes.Length > MaxImageBytes, "file.image_too_large", "Photos can be at most 5 MB.");
        }

        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        buffer.Position = 0;
        var key = await storage.SaveAsync(buffer, detected.Value.Extension, ct);
        var current = await access.GetAsync(ct);
        var sensitivity = category == FileCategories.Medical ? FileSensitivity.Restricted : FileSensitivity.Normal;
        var file = new StoredFile(current.SchoolId, ownerType, ownerId, category, SafeName(upload.FileName, detected.Value.Extension),
            contentType, bytes.Length, sha, key, sensitivity) { Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim() };
        db.StoredFiles.Add(file);

        // Photos and crests replace the previous one and become the record's picture.
        if (category is FileCategories.Photo && ownerType == FileOwners.Student)
        {
            var student = await db.Students.SingleAsync(s => s.Id == ownerId, ct);
            await SoftDeleteAsync(student.PhotoFileId, current.UserId, ct);
            student.PhotoFileId = file.Id;
        }
        else if (category is FileCategories.Logo && ownerType == FileOwners.School)
        {
            var school = await db.Schools.SingleAsync(s => s.Id == current.SchoolId, ct);
            await SoftDeleteAsync(school.LogoFileId, current.UserId, ct);
            school.LogoFileId = file.Id;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        return await GetAsync(file.Id, ct);
    }

    public async Task<IReadOnlyList<StoredFileDto>> ListAsync(string ownerType, Guid ownerId, CancellationToken ct)
    {
        await AuthorizeAsync(ownerType, ownerId, FileCategories.Other, write: false, ct);
        var current = await access.GetAsync(ct);
        var canSeeMedical = current.HasAny(Permissions.Students.ViewMedical, Permissions.Welfare.ViewMedical);
        return await Project(db.StoredFiles.Where(f => f.OwnerType == ownerType && f.OwnerId == ownerId)
                .Where(f => canSeeMedical || f.Sensitivity != FileSensitivity.Restricted))
            .OrderByDescending(f => f.UploadedAt).ToListAsync(ct);
    }

    public async Task<FileContent> OpenAsync(Guid id, CancellationToken ct)
    {
        var file = await db.StoredFiles.SingleOrDefaultAsync(f => f.Id == id, ct) ?? throw new NotFoundException("File", id);
        await AuthorizeAsync(file.OwnerType, file.OwnerId, file.Category, write: false, ct);
        var stream = await storage.OpenReadAsync(file.StorageKey, ct);
        return new FileContent(stream, file.ContentType, file.FileName);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var file = await db.StoredFiles.SingleOrDefaultAsync(f => f.Id == id, ct) ?? throw new NotFoundException("File", id);
        await AuthorizeAsync(file.OwnerType, file.OwnerId, file.Category, write: true, ct);
        var current = await access.GetAsync(ct);
        await SoftDeleteAsync(file.Id, current.UserId, ct);
        if (file.OwnerType == FileOwners.Student)
        {
            var student = await db.Students.SingleOrDefaultAsync(s => s.Id == file.OwnerId && s.PhotoFileId == file.Id, ct);
            if (student is not null) student.PhotoFileId = null;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<StoredFileDto> GetAsync(Guid id, CancellationToken ct) =>
        await Project(db.StoredFiles.Where(f => f.Id == id)).SingleAsync(ct);

    private IQueryable<StoredFileDto> Project(IQueryable<StoredFile> files) => files.Select(f => new StoredFileDto(f.Id, f.OwnerType,
        f.OwnerId, f.Category, f.FileName, f.ContentType, f.SizeBytes, f.Sensitivity, f.Notes, f.CreatedAt, f.CreatedBy,
        db.Users.Where(u => u.Id == f.CreatedBy).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault()));

    private async Task SoftDeleteAsync(Guid? id, Guid? by, CancellationToken ct)
    {
        if (id is null) return;
        var file = await db.StoredFiles.SingleOrDefaultAsync(f => f.Id == id, ct);
        if (file is null) return;
        file.DeletedAt = DateTimeOffset.UtcNow;
        file.DeletedBy = by;
    }

    /// <summary>Who may touch which file: decided by what the file is attached to.</summary>
    private async Task AuthorizeAsync(string ownerType, Guid ownerId, string category, bool write, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        switch (ownerType)
        {
            case FileOwners.School:
                if (write ? !current.Has(Permissions.School.Manage) : false) throw new ForbiddenException();
                Guard.Against(ownerId != current.SchoolId, "file.owner_mismatch", "That file belongs to another school.");
                return;
            case FileOwners.Student:
            {
                var campus = await db.Students.Where(s => s.Id == ownerId).Select(s => (Guid?)s.CampusId).SingleOrDefaultAsync(ct)
                             ?? throw new NotFoundException("Student", ownerId);
                if (!current.CanAccessCampus(campus)) throw new NotFoundException("Student", ownerId);
                var needed = category switch
                {
                    FileCategories.Medical => write ? Permissions.Students.ManageMedical : Permissions.Students.ViewMedical,
                    FileCategories.Photo => write ? Permissions.Students.Update : Permissions.Students.View,
                    _ => write ? Permissions.Students.ManageDocuments : Permissions.Students.ViewDocuments,
                };
                if (!current.Has(needed)) throw new ForbiddenException();
                return;
            }
            case FileOwners.Application:
                if (!current.Has(write ? Permissions.Admissions.Update : Permissions.Admissions.View)) throw new ForbiddenException();
                if (!await db.AdmissionApplications.AnyAsync(a => a.Id == ownerId, ct)) throw new NotFoundException("Application", ownerId);
                return;
            default:
                throw new ForbiddenException("Files of this kind are not available.");
        }
    }

    private static string SafeName(string name, string extension)
    {
        var baseName = Path.GetFileNameWithoutExtension(name);
        var cleaned = new string(baseName.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '.').ToArray()).Trim();
        if (cleaned.Length == 0) cleaned = "document";
        if (cleaned.Length > 120) cleaned = cleaned[..120];
        return cleaned + extension;
    }
}
