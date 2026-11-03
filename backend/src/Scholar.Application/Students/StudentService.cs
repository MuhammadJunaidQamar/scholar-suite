using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Application.Organization;
using Scholar.Domain.Access;
using Scholar.Domain.Common;
using Scholar.Domain.Documents;
using Scholar.Domain.Organization;
using Scholar.Domain.Platform;
using Scholar.Domain.Students;

namespace Scholar.Application.Students;

public sealed class StudentService(IAppDbContext db, IAccessContext access, ISequenceGenerator sequences)
{
    private static readonly StudentStatus[] CurrentStatuses = [StudentStatus.Active, StudentStatus.Suspended];

    // ───────────────────────────── Queries ─────────────────────────────

    public async Task<PagedResult<StudentListItemDto>> ListAsync(StudentQuery query, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Students.View, ct);
        var students = await FilterAsync(query, ct);
        var sorted = students.ApplySort(query, new SortMap<Student>
        {
            ["name"] = s => s.FullName,
            ["admissionNumber"] = s => s.AdmissionNumber,
            ["admissionDate"] = s => s.AdmissionDate,
            // Grade order, then section name ("Class 5 – A" before "Class 5 – B"); the offset keeps the text sortable.
            ["class"] = s => db.Sections.Where(x => x.Id == s.CurrentSectionId)
                .Select(x => (x.Grade.LevelOrder + 10000).ToString() + x.Name).FirstOrDefault(),
        }, "name");

        return await sorted.ToPagedAsync(query, s => new StudentListItemDto(
            s.Id, s.AdmissionNumber, s.FullName, s.Gender, s.DateOfBirth, s.PhotoFileId, s.Status, s.CampusId,
            db.Campuses.Where(c => c.Id == s.CampusId).Select(c => c.Name).First(),
            s.CurrentSectionId,
            db.Sections.Where(x => x.Id == s.CurrentSectionId).Select(x => x.Grade.Name + " – " + x.Name).FirstOrDefault(),
            db.Sections.Where(x => x.Id == s.CurrentSectionId).Select(x => (int?)x.Grade.LevelOrder).FirstOrDefault(),
            s.Guardians.OrderByDescending(g => g.IsPrimary).Select(g => g.Guardian.FullName).FirstOrDefault(),
            s.Guardians.OrderByDescending(g => g.IsPrimary).Select(g => g.Guardian.Phone).FirstOrDefault(),
            s.AdmissionDate, s.MedicalAlert != null), ct);
    }

    /// <summary>The filtered (unsorted, unpaged) student set; shared by the list and exports.</summary>
    internal async Task<IQueryable<Student>> FilterAsync(StudentQuery query, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var students = db.Students.AsQueryable();
        if (!current.AllCampuses)
        {
            students = students.Where(s => current.CampusIds!.Contains(s.CampusId));
        }

        students = query.Status is { } status
            ? students.Where(s => s.Status == status)
            : query.Scope switch
            {
                "all" => students,
                "left" => students.Where(s => !CurrentStatuses.Contains(s.Status)),
                _ => students.Where(s => CurrentStatuses.Contains(s.Status)),
            };

        students = students
            .WhereIf(query.CampusId is not null, s => s.CampusId == query.CampusId)
            .WhereIf(query.SectionId is not null, s => s.CurrentSectionId == query.SectionId)
            .WhereIf(query.GradeId is not null, s => db.Sections.Any(x => x.Id == s.CurrentSectionId && x.GradeId == query.GradeId))
            .WhereIf(query.Gender is not null, s => s.Gender == query.Gender);

        if (query.Term is { } term)
        {
            var digits = new string(term.Where(char.IsDigit).ToArray());
            // Phones are stored as +92XXXXXXXXXX; match on the significant tail however the number was typed.
            var phoneTail = digits.Length >= 10 ? digits[^10..] : digits;
            var pattern = $"%{term}%";
            students = students.Where(s =>
                EF.Functions.ILike(s.FullName, pattern)
                || EF.Functions.ILike(s.AdmissionNumber, term + "%")
                || (digits.Length >= 5 && s.NationalId != null && s.NationalId.Contains(digits))
                || (digits.Length >= 6 && s.Guardians.Any(g => g.Guardian.Phone.Contains(phoneTail))));
        }

        return students;
    }

    public async Task<StudentDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Students.View, ct);
        var student = await LoadScopedAsync(id, ct);
        var current = await access.GetAsync(ct);

        var enrollments = await db.Enrollments.Where(e => e.StudentId == id)
            .OrderByDescending(e => e.StartDate)
            .Select(e => new EnrollmentDto(e.Id, e.AcademicYearId,
                db.AcademicYears.Where(y => y.Id == e.AcademicYearId).Select(y => y.Name).First(), e.SectionId,
                db.Sections.Where(x => x.Id == e.SectionId).Select(x => x.Grade.Name + " – " + x.Name).First(),
                db.Campuses.Where(c => c.Id == e.CampusId).Select(c => c.Name).First(),
                e.RollNumber, e.Status, e.StartDate, e.EndDate, e.Reason))
            .ToListAsync(ct);

        var guardians = await db.StudentGuardians.Where(l => l.StudentId == id)
            .OrderByDescending(l => l.IsPrimary).ThenBy(l => l.Relationship)
            .Select(l => new GuardianLinkDto(l.Id, l.GuardianId, l.Guardian.FullName, l.Relationship, l.Guardian.Phone, l.Guardian.Email,
                l.Guardian.Occupation, l.IsPrimary, l.IsEmergencyContact, l.LivesWithStudent, l.CanPickUp, l.IsFeePayer, l.PortalAccess,
                l.CanViewAttendance, l.CanViewResults, l.CanViewFees, l.ReceivesNotifications, l.Guardian.UserId != null))
            .ToListAsync(ct);

        var guardianIds = guardians.Select(g => g.GuardianId).ToList();
        var siblings = await db.Students
            .Where(s => s.Id != id && s.Guardians.Any(l => guardianIds.Contains(l.GuardianId)))
            .Select(s => new SiblingDto(s.Id, s.FullName, s.AdmissionNumber,
                db.Sections.Where(x => x.Id == s.CurrentSectionId).Select(x => x.Grade.Name + " – " + x.Name).FirstOrDefault(),
                s.PhotoFileId, s.Status))
            .ToListAsync(ct);

        var history = await db.StudentStatusChanges.Where(c => c.StudentId == id).OrderByDescending(c => c.CreatedAt)
            .Select(c => new StatusChangeDto(c.Id, c.FromStatus, c.ToStatus, c.EffectiveDate, c.Reason, c.DestinationSchool,
                db.Users.Where(u => u.Id == c.CreatedBy).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(), c.CreatedAt))
            .ToListAsync(ct);

        var contacts = await db.EmergencyContacts.Where(c => c.StudentId == id).OrderBy(c => c.Priority)
            .Select(c => new EmergencyContactDto(c.Id, c.Name, c.Relationship, c.Phone, c.AlternatePhone, c.Priority)).ToListAsync(ct);

        var canSeeDocuments = current.Has(Permissions.Students.ViewDocuments);
        var documents = canSeeDocuments
            ? await db.StoredFiles.CountAsync(f => f.OwnerType == FileOwners.Student && f.OwnerId == id && f.Category != FileCategories.Photo, ct)
            : 0;
        var campusName = await db.Campuses.Where(c => c.Id == student.CampusId).Select(c => c.Name).SingleAsync(ct);

        return new StudentDetailDto(student.Id, student.AdmissionNumber, student.RegistrationNumber, student.FirstName, student.MiddleName,
            student.LastName, student.FullName, student.Gender, student.DateOfBirth, student.PlaceOfBirth, student.NationalId,
            student.Nationality, student.Religion, student.BloodGroup, student.MotherTongue, student.PhotoFileId, student.Email,
            student.Phone, AddressDto.From(student.Address), student.PreviousSchoolName, student.PreviousSchoolClass, student.AdmissionDate,
            student.Status, student.StatusChangedOn, student.MedicalAlert, student.CampusId, campusName, student.UserId is not null,
            enrollments.FirstOrDefault(e => e.Status == EnrollmentStatus.Active), enrollments, guardians, contacts, siblings, history,
            documents, student.Version);
    }

    // ───────────────────────────── Commands ─────────────────────────────

    public async Task<StudentDetailDto> CreateAsync(CreateStudentRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Students.Create, ct);
        var id = await db.InTransactionAsync(async innerCt =>
        {
            var student = await CreateCoreAsync(request, innerCt);
            await db.SaveChangesAsync(innerCt);
            return student.Id;
        }, ct: ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Creates student, enrollment, guardians and links. Caller owns the transaction and SaveChanges.</summary>
    internal async Task<Student> CreateCoreAsync(CreateStudentRequest request, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var section = await LoadSectionForPlacementAsync(request.Placement.SectionId, current, ct);
        await EnsureCapacityAsync(section, request.OverrideCapacity, ct);

        var admissionNumber = string.IsNullOrWhiteSpace(request.AdmissionNumber)
            ? await sequences.NextAsync(SequenceKeys.Admission, request.Placement.AdmissionDate, ct)
            : request.AdmissionNumber.Trim().ToUpperInvariant();
        if (await db.Students.AnyAsync(s => s.AdmissionNumber == admissionNumber, ct))
        {
            throw new ConflictException("student.admission_number_taken", $"Admission number {admissionNumber} is already in use.");
        }

        var details = request.Student;
        var student = new Student(current.SchoolId, section.CampusId, admissionNumber, details.FirstName, details.LastName, details.Gender,
            details.DateOfBirth, request.Placement.AdmissionDate);
        Apply(student, details);
        db.Students.Add(student);

        var enrollment = new Enrollment(current.SchoolId, student.Id, section.AcademicYearId, section.CampusId, section.GradeId, section.Id,
            request.Placement.AdmissionDate, Clean(request.Placement.RollNumber));
        db.Enrollments.Add(enrollment);
        student.CurrentSectionId = section.Id;
        student.CurrentAcademicYearId = section.AcademicYearId;

        var links = new List<StudentGuardian>();
        foreach (var input in request.Guardians)
        {
            var guardian = await ResolveGuardianAsync(input, current.SchoolId, ct);
            if (links.Any(l => l.GuardianId == guardian.Id)) continue;
            var link = new StudentGuardian(current.SchoolId, student.Id, guardian.Id, input.Relationship);
            ApplyLink(link, input.IsPrimary, input.IsFeePayer, input.IsEmergencyContact, input.LivesWithStudent, input.CanPickUp,
                input.PortalAccess, input.CanViewAttendance, input.CanViewResults, input.CanViewFees, input.ReceivesNotifications);
            links.Add(link);
            db.StudentGuardians.Add(link);
        }

        if (!links.Any(l => l.IsPrimary)) links[0].IsPrimary = true;
        if (!links.Any(l => l.IsFeePayer)) links.First(l => l.IsPrimary).IsFeePayer = true;

        var priority = 1;
        foreach (var contact in request.EmergencyContacts ?? [])
        {
            db.EmergencyContacts.Add(new EmergencyContact(current.SchoolId, student.Id, contact.Name, contact.Relationship, contact.Phone, priority++)
            {
                AlternatePhone = Clean(contact.AlternatePhone),
            });
        }

        return student;
    }

    public async Task<StudentDetailDto> UpdateAsync(Guid id, UpdateStudentRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Students.Update, ct);
        var student = await LoadScopedAsync(id, ct);
        db.Entry(student).Property(s => s.Version).OriginalValue = request.Version;
        student.Rename(request.Student.FirstName, request.Student.MiddleName, request.Student.LastName);
        student.Gender = request.Student.Gender;
        student.DateOfBirth = request.Student.DateOfBirth;
        Apply(student, request.Student);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<StudentDetailDto> ChangeStatusAsync(Guid id, ChangeStudentStatusRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Students.ManageStatus, ct);
        var current = await access.GetAsync(ct);
        await db.InTransactionAsync(async innerCt =>
        {
            var student = await LoadScopedAsync(id, innerCt, includeEnrollments: true);
            var change = student.ChangeStatus(request.Status, request.EffectiveDate, Clean(request.Reason), Clean(request.DestinationSchool),
                current.UserId);
            db.StudentStatusChanges.Add(change);
            await db.SaveChangesAsync(innerCt);
            return true;
        }, ct: ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Moves a student to another section of the same academic year, keeping the old placement in history.</summary>
    public async Task<StudentDetailDto> TransferSectionAsync(Guid id, TransferSectionRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Students.Update, ct);
        var current = await access.GetAsync(ct);
        await db.InTransactionAsync(async innerCt =>
        {
            var student = await LoadScopedAsync(id, innerCt, includeEnrollments: true);
            Guard.Against(!student.IsCurrent, "student.not_current", "Only current students can change section.");
            var target = await LoadSectionForPlacementAsync(request.SectionId, current, innerCt);
            var active = student.Enrollments.SingleOrDefault(e => e.Status == EnrollmentStatus.Active && e.AcademicYearId == target.AcademicYearId)
                ?? throw new DomainException("student.no_enrollment", "The student has no placement in that academic year.");
            Guard.Against(active.SectionId == target.Id, "student.same_section", "The student is already in that section.");
            await EnsureCapacityAsync(target, request.OverrideCapacity, innerCt);

            active.Close(EnrollmentStatus.Transferred, request.EffectiveDate.AddDays(-1), Clean(request.Reason) ?? "Section change");
            await db.SaveChangesAsync(innerCt);
            db.Enrollments.Add(new Enrollment(current.SchoolId, student.Id, target.AcademicYearId, target.CampusId, target.GradeId, target.Id,
                request.EffectiveDate, active.RollNumber));
            student.CurrentSectionId = target.Id;
            student.CampusId = target.CampusId;
            await db.SaveChangesAsync(innerCt);
            return true;
        }, ct: ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Deletes a record created in error. Refused (by foreign keys) once other modules hold data about the student.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Students.Delete, ct);
        await db.InTransactionAsync(async innerCt =>
        {
            var student = await LoadScopedAsync(id, innerCt, includeEnrollments: true);
            Guard.Against(await db.AdmissionApplications.AnyAsync(a => a.StudentId == id, innerCt), "student.from_application",
                "This student was enrolled from an application. Withdraw the student instead of deleting the record.");
            db.Enrollments.RemoveRange(student.Enrollments);
            db.StudentStatusChanges.RemoveRange(await db.StudentStatusChanges.Where(c => c.StudentId == id).ToListAsync(innerCt));
            db.StudentMedicalProfiles.RemoveRange(await db.StudentMedicalProfiles.Where(m => m.StudentId == id).ToListAsync(innerCt));
            student.PhotoFileId = null;
            await db.SaveChangesAsync(innerCt);
            db.Students.Remove(student);
            await db.SaveChangesAsync(innerCt);
            return true;
        }, ct: ct);
    }

    // ───────────────────────────── Guardians on the record ─────────────────────────────

    public async Task<StudentDetailDto> AddGuardianAsync(Guid id, GuardianLinkInput input, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Students.Update, ct);
        var current = await access.GetAsync(ct);
        _ = await LoadScopedAsync(id, ct);
        var guardian = await ResolveGuardianAsync(input, current.SchoolId, ct);
        if (await db.StudentGuardians.AnyAsync(l => l.StudentId == id && l.GuardianId == guardian.Id, ct))
        {
            throw new ConflictException("student.guardian_linked", "This guardian is already linked to the student.");
        }

        var link = new StudentGuardian(current.SchoolId, id, guardian.Id, input.Relationship);
        ApplyLink(link, input.IsPrimary, input.IsFeePayer, input.IsEmergencyContact, input.LivesWithStudent, input.CanPickUp,
            input.PortalAccess, input.CanViewAttendance, input.CanViewResults, input.CanViewFees, input.ReceivesNotifications);
        if (input.IsPrimary) await ClearPrimaryAsync(id, ct);
        db.StudentGuardians.Add(link);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<StudentDetailDto> UpdateGuardianLinkAsync(Guid id, Guid linkId, UpdateGuardianLinkRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Students.Update, ct);
        _ = await LoadScopedAsync(id, ct);
        var link = await db.StudentGuardians.SingleOrDefaultAsync(l => l.Id == linkId && l.StudentId == id, ct)
            ?? throw new NotFoundException("Guardian link", linkId);
        if (request.IsPrimary && !link.IsPrimary) await ClearPrimaryAsync(id, ct);
        link.Relationship = request.Relationship;
        ApplyLink(link, request.IsPrimary, request.IsFeePayer, request.IsEmergencyContact, request.LivesWithStudent, request.CanPickUp,
            request.PortalAccess, request.CanViewAttendance, request.CanViewResults, request.CanViewFees, request.ReceivesNotifications);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<StudentDetailDto> RemoveGuardianLinkAsync(Guid id, Guid linkId, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Students.Update, ct);
        _ = await LoadScopedAsync(id, ct);
        var links = await db.StudentGuardians.Where(l => l.StudentId == id).ToListAsync(ct);
        var link = links.SingleOrDefault(l => l.Id == linkId) ?? throw new NotFoundException("Guardian link", linkId);
        Guard.Against(links.Count == 1, "student.last_guardian", "A student must keep at least one guardian. Add another before removing this one.");
        db.StudentGuardians.Remove(link);
        var remaining = links.Where(l => l.Id != linkId).ToList();
        if (link.IsPrimary) remaining[0].IsPrimary = true;
        if (link.IsFeePayer && !remaining.Any(l => l.IsFeePayer)) remaining.First(l => l.IsPrimary).IsFeePayer = true;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<StudentDetailDto> SetEmergencyContactsAsync(Guid id, IReadOnlyList<EmergencyContactInput> contacts, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Students.Update, ct);
        var student = await LoadScopedAsync(id, ct);
        db.EmergencyContacts.RemoveRange(await db.EmergencyContacts.Where(c => c.StudentId == id).ToListAsync(ct));
        var priority = 1;
        foreach (var c in contacts)
        {
            Guard.Against(string.IsNullOrWhiteSpace(c.Name) || string.IsNullOrWhiteSpace(c.Phone), "contact.incomplete",
                "Each emergency contact needs a name and a phone number.");
            db.EmergencyContacts.Add(new EmergencyContact(student.SchoolId, id, c.Name, c.Relationship, c.Phone, priority++)
            {
                AlternatePhone = Clean(c.AlternatePhone),
            });
        }

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    // ───────────────────────────── Medical (restricted) ─────────────────────────────

    public async Task<MedicalProfileDto> GetMedicalAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Students.ViewMedical, ct);
        var student = await LoadScopedAsync(id, ct);
        var m = await db.StudentMedicalProfiles.SingleOrDefaultAsync(x => x.StudentId == id, ct);
        return new MedicalProfileDto(m?.Allergies, m?.Conditions, m?.Medications, m?.SpecialNeeds, m?.DoctorName, m?.DoctorPhone,
            m?.EmergencyInstructions, student.BloodGroup, student.MedicalAlert);
    }

    public async Task<MedicalProfileDto> UpdateMedicalAsync(Guid id, MedicalProfileDto request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Students.ManageMedical, ct);
        var student = await LoadScopedAsync(id, ct);
        var profile = await db.StudentMedicalProfiles.SingleOrDefaultAsync(x => x.StudentId == id, ct);
        if (profile is null)
        {
            profile = new StudentMedicalProfile(student.SchoolId, id);
            db.StudentMedicalProfiles.Add(profile);
        }

        profile.Allergies = Clean(request.Allergies);
        profile.Conditions = Clean(request.Conditions);
        profile.Medications = Clean(request.Medications);
        profile.SpecialNeeds = Clean(request.SpecialNeeds);
        profile.DoctorName = Clean(request.DoctorName);
        profile.DoctorPhone = Clean(request.DoctorPhone);
        profile.EmergencyInstructions = Clean(request.EmergencyInstructions);
        student.BloodGroup = Clean(request.BloodGroup);
        student.MedicalAlert = Clean(request.MedicalAlert);
        await db.SaveChangesAsync(ct);
        return await GetMedicalAsync(id, ct);
    }

    // ───────────────────────────── Helpers ─────────────────────────────

    internal async Task<Student> LoadScopedAsync(Guid id, CancellationToken ct, bool includeEnrollments = false)
    {
        var current = await access.GetAsync(ct);
        var query = db.Students.AsQueryable();
        if (includeEnrollments) query = query.Include(s => s.Enrollments);
        var student = await query.SingleOrDefaultAsync(s => s.Id == id, ct);
        if (student is null || !current.CanAccessCampus(student.CampusId))
        {
            throw new NotFoundException("Student", id);
        }

        return student;
    }

    private async Task<Section> LoadSectionForPlacementAsync(Guid sectionId, EffectiveAccess current, CancellationToken ct)
    {
        var section = await db.Sections.Include(s => s.AcademicYear).SingleOrDefaultAsync(s => s.Id == sectionId, ct)
            ?? throw new NotFoundException("Section", sectionId);
        Guard.Against(!current.CanAccessCampus(section.CampusId), "campus.out_of_scope", "You can only place students on your own campuses.");
        Guard.Against(section.AcademicYear.Status == AcademicYearStatus.Closed, "academic_year.closed", "That academic year is closed.");
        Guard.Against(!section.IsActive, "section.inactive", "That section is not taking students.");
        return section;
    }

    private async Task EnsureCapacityAsync(Section section, bool overrideCapacity, CancellationToken ct)
    {
        if (overrideCapacity) return;
        var enrolled = await db.Enrollments.CountAsync(e => e.SectionId == section.Id && e.Status == EnrollmentStatus.Active, ct);
        if (enrolled >= section.Capacity)
        {
            throw new ConflictException("section.full", $"This section is full ({enrolled} of {section.Capacity} seats). Choose another section or confirm to exceed capacity.");
        }
    }

    /// <summary>
    /// Existing guardian by id, or a new one — unless a guardian with the same mobile number and first name already exists,
    /// in which case that record is reused (siblings share guardians; duplicates break the family portal and sibling discounts).
    /// </summary>
    private async Task<Guardian> ResolveGuardianAsync(GuardianLinkInput input, Guid schoolId, CancellationToken ct)
    {
        if (input.GuardianId is { } guardianId)
        {
            return await db.Guardians.SingleOrDefaultAsync(g => g.Id == guardianId, ct) ?? throw new NotFoundException("Guardian", guardianId);
        }

        var data = input.NewGuardian!;
        var phone = Guardian.NormalizePhone(data.Phone);
        var first = data.FirstName.Trim().ToLower();
        var local = db.Guardians.Local.FirstOrDefault(g => g.Phone == phone && g.FirstName.ToLower() == first);
        var existing = local ?? await db.Guardians.FirstOrDefaultAsync(g => g.Phone == phone && g.FirstName.ToLower() == first, ct);
        if (existing is not null) return existing;

        var guardian = new Guardian(schoolId, data.FirstName, data.LastName, data.Phone)
        {
            Email = Clean(data.Email), NationalId = Digits(data.NationalId), Occupation = Clean(data.Occupation),
            Employer = Clean(data.Employer), AlternatePhone = Clean(data.AlternatePhone), Address = data.Address?.ToAddress() ?? new Address(),
        };
        db.Guardians.Add(guardian);
        return guardian;
    }

    private async Task ClearPrimaryAsync(Guid studentId, CancellationToken ct)
    {
        foreach (var l in await db.StudentGuardians.Where(l => l.StudentId == studentId && l.IsPrimary).ToListAsync(ct))
        {
            l.IsPrimary = false;
        }
    }

    private static void ApplyLink(StudentGuardian link, bool primary, bool feePayer, bool emergency, bool livesWith, bool pickup,
        bool portal, bool attendance, bool results, bool fees, bool notifications)
    {
        link.IsPrimary = primary;
        link.IsFeePayer = feePayer;
        link.IsEmergencyContact = emergency;
        link.LivesWithStudent = livesWith;
        link.CanPickUp = pickup;
        link.PortalAccess = portal;
        link.CanViewAttendance = attendance;
        link.CanViewResults = results;
        link.CanViewFees = fees;
        link.ReceivesNotifications = notifications;
    }

    private static void Apply(Student student, StudentDetailsInput d)
    {
        student.PlaceOfBirth = Clean(d.PlaceOfBirth);
        student.NationalId = Digits(d.NationalId);
        student.Nationality = Clean(d.Nationality);
        student.Religion = Clean(d.Religion);
        student.BloodGroup = Clean(d.BloodGroup)?.ToUpperInvariant();
        student.MotherTongue = Clean(d.MotherTongue);
        student.Email = Clean(d.Email);
        student.Phone = Clean(d.Phone);
        student.Address = d.Address?.ToAddress() ?? new Address();
        student.PreviousSchoolName = Clean(d.PreviousSchoolName);
        student.PreviousSchoolClass = Clean(d.PreviousSchoolClass);
        student.MedicalAlert = Clean(d.MedicalAlert);
        student.RegistrationNumber = Clean(d.RegistrationNumber);
    }

    internal static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    internal static string? Digits(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var digits = new string(v.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }
}
