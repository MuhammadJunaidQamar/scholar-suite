using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Application.Organization;
using Scholar.Domain.Access;
using Scholar.Domain.Students;

namespace Scholar.Application.Students;

/// <summary>Spreadsheet import and export of student records.</summary>
public sealed class StudentTransferService(
    IAppDbContext db,
    IAccessContext access,
    StudentService students,
    ITabularExporter exporter,
    ITabularReader reader,
    ISchoolClock clock)
{
    public static readonly string[] TemplateColumns =
    [
        "first_name", "last_name", "gender", "date_of_birth", "campus", "grade", "section", "admission_date", "admission_number",
        "b_form", "guardian_first_name", "guardian_last_name", "guardian_phone", "guardian_relationship", "guardian_email", "address", "city",
    ];

    public async Task<ExportFile> ExportAsync(StudentQuery query, ExportFormat format, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Students.Export, ct);
        var filtered = await students.FilterAsync(query, ct);
        var rows = await filtered.OrderBy(s => s.FullName)
            .Select(s => new
            {
                s.AdmissionNumber, s.FirstName, s.MiddleName, s.LastName, s.Gender, s.DateOfBirth, s.NationalId, s.Status, s.AdmissionDate,
                Campus = db.Campuses.Where(c => c.Id == s.CampusId).Select(c => c.Name).First(),
                Class = db.Sections.Where(x => x.Id == s.CurrentSectionId).Select(x => x.Grade.Name + " – " + x.Name).FirstOrDefault(),
                Guardian = s.Guardians.OrderByDescending(g => g.IsPrimary).Select(g => g.Guardian.FullName).FirstOrDefault(),
                Relationship = s.Guardians.OrderByDescending(g => g.IsPrimary).Select(g => (GuardianRelationship?)g.Relationship).FirstOrDefault(),
                Phone = s.Guardians.OrderByDescending(g => g.IsPrimary).Select(g => g.Guardian.Phone).FirstOrDefault(),
                s.Address.Line1, s.Address.City,
            })
            .ToListAsync(ct);

        string[] headers = ["Admission no.", "First name", "Middle name", "Last name", "Gender", "Date of birth", "B-Form", "Status",
            "Admission date", "Campus", "Class", "Primary guardian", "Relationship", "Guardian phone", "Address", "City"];
        return exporter.Write("Students", headers, rows.Select(r => (IReadOnlyList<object?>)
        [
            r.AdmissionNumber, r.FirstName, r.MiddleName, r.LastName, r.Gender.ToString(), r.DateOfBirth, r.NationalId, r.Status.ToString(),
            r.AdmissionDate, r.Campus, r.Class, r.Guardian, r.Relationship?.ToString(), r.Phone, r.Line1, r.City,
        ]), format);
    }

    public ExportFile Template() => exporter.Write("Student import template", TemplateColumns,
    [
        ["Ayesha", "Khan", "Female", "2016-04-12", "MAIN", "C5", "A", "", "", "3520212345678", "Tariq", "Khan", "0300 1234567", "Father",
            "tariq@example.com", "House 12, Street 4", "Lahore"],
    ], ExportFormat.Csv);

    /// <summary>
    /// Validates every row first; commits only when <paramref name="commit"/> is true and no row has an error — all rows
    /// are created in one transaction or none are.
    /// </summary>
    public async Task<ImportReport> ImportAsync(Stream content, bool commit, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Students.Import, ct);
        var rows = reader.ReadCsv(content);
        Guard(rows.Count == 0, "The file has no rows. Use the template's column names in the first row.");
        Guard(rows.Count > 2000, "Import at most 2,000 students at a time.");

        var yearId = await db.AcademicYears.Where(y => y.IsCurrent).Select(y => (Guid?)y.Id).FirstOrDefaultAsync(ct)
                     ?? throw new Domain.Common.DomainException("import.no_current_year", "Set a current academic year before importing students.");
        var campuses = await db.Campuses.Select(c => new { c.Id, c.Code, c.Name }).ToListAsync(ct);
        var sections = await db.Sections.Where(s => s.AcademicYearId == yearId)
            .Select(s => new { s.Id, s.CampusId, s.Name, GradeCode = s.Grade.Code, GradeName = s.Grade.Name }).ToListAsync(ct);
        var existingNumbers = (await db.Students.Select(s => s.AdmissionNumber).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var today = await clock.TodayAsync(ct);

        var errors = new List<ImportRowError>();
        var requests = new List<CreateStudentRequest>();
        var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var line = i + 2;
            string Get(string key) => row.TryGetValue(key, out var v) ? v : string.Empty;
            void Error(string column, string message) => errors.Add(new ImportRowError(line, column, message));

            var first = Get("first_name");
            var last = Get("last_name");
            if (first.Length == 0) Error("first_name", "Required.");
            if (last.Length == 0) Error("last_name", "Required.");

            Gender? gender = Get("gender").ToLowerInvariant() switch
            {
                "f" or "female" or "girl" => Gender.Female,
                "m" or "male" or "boy" => Gender.Male,
                "o" or "other" => Gender.Other,
                _ => null,
            };
            if (gender is null) Error("gender", "Use Female, Male or Other.");

            var dob = ParseDate(Get("date_of_birth"));
            if (dob is null) Error("date_of_birth", "Use YYYY-MM-DD or DD/MM/YYYY.");
            else if (dob > today.AddYears(-2) || dob < today.AddYears(-30)) Error("date_of_birth", "Check the date of birth.");

            var campusKey = Get("campus");
            var campus = campusKey.Length == 0 && campuses.Count == 1
                ? campuses[0]
                : campuses.FirstOrDefault(c => c.Code.Equals(campusKey, StringComparison.OrdinalIgnoreCase) || c.Name.Equals(campusKey, StringComparison.OrdinalIgnoreCase));
            if (campus is null) Error("campus", $"No campus '{campusKey}'. Use a campus code such as {string.Join(", ", campuses.Select(c => c.Code))}.");

            var gradeKey = Get("grade");
            var sectionKey = Get("section");
            var section = campus is null ? null : sections.FirstOrDefault(s => s.CampusId == campus.Id
                && (s.GradeCode.Equals(gradeKey, StringComparison.OrdinalIgnoreCase) || s.GradeName.Equals(gradeKey, StringComparison.OrdinalIgnoreCase))
                && s.Name.Equals(sectionKey, StringComparison.OrdinalIgnoreCase));
            if (campus is not null && section is null) Error("section", $"No section '{gradeKey} {sectionKey}' on that campus this year.");

            var admissionDate = Get("admission_date").Length == 0 ? today : ParseDate(Get("admission_date"));
            if (admissionDate is null) Error("admission_date", "Use YYYY-MM-DD or DD/MM/YYYY.");

            var number = Get("admission_number");
            if (number.Length > 0)
            {
                if (existingNumbers.Contains(number)) Error("admission_number", $"{number} is already used.");
                else if (!seenNumbers.Add(number)) Error("admission_number", $"{number} appears twice in the file.");
            }

            var bForm = new string(Get("b_form").Where(char.IsDigit).ToArray());
            if (bForm.Length is > 0 and not 13) Error("b_form", "B-Form numbers have 13 digits.");

            var guardianFirst = Get("guardian_first_name");
            var guardianPhone = Get("guardian_phone");
            if (guardianFirst.Length == 0) Error("guardian_first_name", "Required.");
            if (guardianPhone.Count(char.IsDigit) < 10) Error("guardian_phone", "Enter a mobile number.");
            var relationshipText = Get("guardian_relationship");
            GuardianRelationship relationship = GuardianRelationship.Guardian;
            if (relationshipText.Length > 0 && !Enum.TryParse(relationshipText, ignoreCase: true, out relationship))
            {
                Error("guardian_relationship", "Use Father, Mother, Guardian, Grandfather, Grandmother, Brother, Sister, Uncle, Aunt or Other.");
            }

            if (errors.Any(e => e.Row == line)) continue;
            requests.Add(new CreateStudentRequest(
                new StudentDetailsInput(first, null, last, gender!.Value, dob!.Value, null, bForm.Length == 0 ? null : bForm, "Pakistani",
                    null, null, null, null, null, new AddressDto(Get("address"), null, Get("city"), null, null, "PK"), null, null, null, null),
                new PlacementInput(section!.Id, admissionDate!.Value, null),
                [new GuardianLinkInput(null, new GuardianInput(guardianFirst, Get("guardian_last_name"), guardianPhone, NullIfEmpty(Get("guardian_email")),
                    null, null, null, null, null), relationship, IsPrimary: true, IsFeePayer: true)],
                null, number.Length == 0 ? null : number, OverrideCapacity: true));
        }

        if (!commit || errors.Count > 0)
        {
            return new ImportReport(rows.Count, requests.Count, errors, Committed: false, Created: 0);
        }

        await db.InTransactionAsync(async innerCt =>
        {
            foreach (var request in requests)
            {
                await students.CreateCoreAsync(request, innerCt);
                await db.SaveChangesAsync(innerCt);
            }

            return true;
        }, ct: ct);
        return new ImportReport(rows.Count, requests.Count, errors, Committed: true, Created: requests.Count);
    }

    private static DateOnly? ParseDate(string value)
    {
        string[] formats = ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy"];
        return DateOnly.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static void Guard(bool condition, string message)
    {
        if (condition) throw new Domain.Common.DomainException("import.invalid_file", message);
    }
}
