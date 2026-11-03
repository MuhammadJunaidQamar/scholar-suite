using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Access;
using Scholar.Application.Common;
using Scholar.Application.Organization;
using Scholar.Domain.Access;
using Scholar.Domain.Common;
using Scholar.Domain.Identity;
using Scholar.Domain.Platform;
using Scholar.Domain.Staffing;

namespace Scholar.Application.Staffing;

/// <summary>The staff directory: records, status, sign-in access. Campus-scoped like students.</summary>
public sealed class StaffService(
    IAppDbContext db,
    IAccessContext access,
    ISequenceGenerator sequences,
    MemberService members,
    ITabularExporter exporter)
{
    internal static readonly StaffStatus[] CurrentStatuses = [StaffStatus.Active, StaffStatus.OnLeave, StaffStatus.Suspended];

    // ───────────────────────────── Queries ─────────────────────────────

    public async Task<PagedResult<StaffListItemDto>> ListAsync(StaffQuery query, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Staff.View, ct);
        var yearId = await CurrentYearIdAsync(ct);
        var staff = await FilterAsync(query, ct);
        var sorted = staff.ApplySort(query, new SortMap<StaffMember>
        {
            ["name"] = s => s.FullName,
            ["employeeNumber"] = s => s.EmployeeNumber,
            ["joinDate"] = s => s.JoinDate,
            ["designation"] = s => db.Designations.Where(d => d.Id == s.DesignationId).Select(d => d.Name).FirstOrDefault(),
        }, "name");

        var page = await sorted.ToPagedAsync(query, s => new
        {
            s.Id, s.EmployeeNumber, s.FullName, s.Gender, s.PhotoFileId, s.StaffType, s.Status, s.CampusId,
            Campus = db.Campuses.Where(c => c.Id == s.CampusId).Select(c => c.Name).First(),
            Department = db.Departments.Where(d => d.Id == s.DepartmentId).Select(d => d.Name).FirstOrDefault(),
            Designation = db.Designations.Where(d => d.Id == s.DesignationId).Select(d => d.Name).FirstOrDefault(),
            s.Phone, s.Email, s.JoinDate, HasLogin = s.UserId != null,
        }, ct);

        var ids = page.Items.Select(i => i.Id).ToList();
        var classes = (await db.Sections.Where(x => x.AcademicYearId == yearId && x.ClassTeacherId != null && ids.Contains(x.ClassTeacherId!.Value))
                .OrderBy(x => x.Grade.LevelOrder).ThenBy(x => x.Name)
                .Select(x => new { StaffId = x.ClassTeacherId!.Value, Name = x.Grade.Name + " – " + x.Name })
                .ToListAsync(ct))
            .GroupBy(x => x.StaffId).ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.Name)));

        return new PagedResult<StaffListItemDto>(page.Items.Select(i => new StaffListItemDto(i.Id, i.EmployeeNumber, i.FullName, i.Gender,
            i.PhotoFileId, i.StaffType, i.Status, i.CampusId, i.Campus, i.Department, i.Designation, i.Phone, i.Email, i.JoinDate,
            i.HasLogin, classes.GetValueOrDefault(i.Id))).ToList(), page.Page, page.PageSize, page.Total);
    }

    internal async Task<IQueryable<StaffMember>> FilterAsync(StaffQuery query, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var staff = Visible(db.Staff, current);
        staff = query.Status is { } status
            ? staff.Where(s => s.Status == status)
            : query.Scope switch
            {
                "all" => staff,
                "left" => staff.Where(s => !CurrentStatuses.Contains(s.Status)),
                _ => staff.Where(s => CurrentStatuses.Contains(s.Status)),
            };

        staff = staff
            .WhereIf(query.CampusId is not null, s => s.CampusId == query.CampusId)
            .WhereIf(query.DepartmentId is not null, s => s.DepartmentId == query.DepartmentId)
            .WhereIf(query.DesignationId is not null, s => s.DesignationId == query.DesignationId)
            .WhereIf(query.StaffType is not null, s => s.StaffType == query.StaffType);

        if (query.Term is { } term)
        {
            var digits = new string(term.Where(char.IsDigit).ToArray());
            var phoneTail = digits.Length >= 10 ? digits[^10..] : digits;
            var pattern = $"%{term}%";
            staff = staff.Where(s =>
                EF.Functions.ILike(s.FullName, pattern)
                || EF.Functions.ILike(s.EmployeeNumber, term + "%")
                || (s.Email != null && EF.Functions.ILike(s.Email, pattern))
                || (digits.Length >= 6 && s.Phone != null && s.Phone.Contains(phoneTail))
                || (digits.Length >= 5 && s.NationalId != null && s.NationalId.Contains(digits)));
        }

        return staff;
    }

    /// <summary>Current staff for pickers (class teacher, subject teacher), teaching staff first.</summary>
    public async Task<IReadOnlyList<StaffOptionDto>> OptionsAsync(Guid? campusId, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        if (!current.HasAny(Permissions.Staff.View, Permissions.Academics.Manage, Permissions.Staff.RecordAttendance,
                Permissions.Hr.ApproveLeave))
        {
            throw new ForbiddenException();
        }

        var yearId = await CurrentYearIdAsync(ct);
        return await Visible(db.Staff, current)
            .Where(s => CurrentStatuses.Contains(s.Status))
            .WhereIf(campusId is not null, s => s.CampusId == campusId)
            .OrderBy(s => s.StaffType).ThenBy(s => s.FullName)
            .Select(s => new StaffOptionDto(s.Id, s.FullName, s.EmployeeNumber, s.CampusId,
                db.Designations.Where(d => d.Id == s.DesignationId).Select(d => d.Name).FirstOrDefault(), s.StaffType,
                db.Sections.Count(x => x.AcademicYearId == yearId && x.ClassTeacherId == s.Id),
                db.SectionSubjectTeachers.Count(t => t.StaffId == s.Id && db.Sections.Any(x => x.Id == t.SectionId && x.AcademicYearId == yearId))))
            .ToListAsync(ct);
    }

    public async Task<StaffDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Staff.View, ct);
        return await DetailAsync(await LoadScopedAsync(id, ct), ct);
    }

    /// <summary>Builds the profile without a permission check; callers have already authorised (e.g. "my profile").</summary>
    internal async Task<StaffDetailDto> DetailAsync(StaffMember s, CancellationToken ct)
    {
        var yearId = await CurrentYearIdAsync(ct);
        var campus = await db.Campuses.Where(c => c.Id == s.CampusId).Select(c => c.Name).SingleAsync(ct);
        var department = s.DepartmentId is null ? null
            : await db.Departments.Where(d => d.Id == s.DepartmentId).Select(d => d.Name).SingleOrDefaultAsync(ct);
        var designation = s.DesignationId is null ? null
            : await db.Designations.Where(d => d.Id == s.DesignationId).Select(d => d.Name).SingleOrDefaultAsync(ct);

        var classTeacherOf = await db.Sections.Where(x => x.AcademicYearId == yearId && x.ClassTeacherId == s.Id)
            .OrderBy(x => x.Grade.LevelOrder).ThenBy(x => x.Name)
            .Select(x => new StaffAssignmentDto(x.Id, x.Grade.Name + " – " + x.Name, x.Grade.LevelOrder, null, null))
            .ToListAsync(ct);
        var teaches = await (
                from t in db.SectionSubjectTeachers
                join x in db.Sections on t.SectionId equals x.Id
                join subject in db.Subjects on t.SubjectId equals subject.Id
                where t.StaffId == s.Id && x.AcademicYearId == yearId
                orderby x.Grade.LevelOrder, x.Name, subject.Name
                select new StaffAssignmentDto(x.Id, x.Grade.Name + " – " + x.Name, x.Grade.LevelOrder, subject.Id, subject.Name))
            .ToListAsync(ct);

        StaffAccountDto? account = null;
        if (s.UserId is { } userId)
        {
            var user = await db.Users.Where(u => u.Id == userId)
                .Select(u => new { u.Id, u.UserName, u.Email, u.Status, u.LastLoginAt }).SingleOrDefaultAsync(ct);
            if (user is not null)
            {
                var suspended = await db.SchoolMemberships.AnyAsync(m => m.UserId == userId && m.Status == MembershipStatus.Suspended, ct);
                var roles = await db.UserRoles.Where(r => r.UserId == userId).OrderBy(r => r.Role.Name).Select(r => r.Role.Name)
                    .Distinct().ToListAsync(ct);
                account = new StaffAccountDto(user.Id, user.UserName!, user.Email, user.Status, suspended, roles, user.LastLoginAt);
            }
        }

        return new StaffDetailDto(s.Id, s.EmployeeNumber, s.FirstName, s.LastName, s.FullName, s.Gender, s.DateOfBirth, s.NationalId,
            s.Phone, s.Email, AddressDto.From(s.Address), s.PhotoFileId, s.CampusId, campus, s.DepartmentId, department, s.DesignationId,
            designation, s.StaffType, s.EmploymentType, s.JoinDate, s.Status, s.ExitDate, s.ExitReason, account, classTeacherOf, teaches,
            s.Version);
    }

    public async Task<ExportFile> ExportAsync(StaffQuery query, ExportFormat format, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Staff.Export, ct);
        var rows = await (await FilterAsync(query, ct)).OrderBy(s => s.FullName)
            .Select(s => new
            {
                s.EmployeeNumber, s.FirstName, s.LastName, s.Gender, s.StaffType, s.EmploymentType, s.Status, s.JoinDate, s.Phone, s.Email,
                s.NationalId,
                Campus = db.Campuses.Where(c => c.Id == s.CampusId).Select(c => c.Name).First(),
                Department = db.Departments.Where(d => d.Id == s.DepartmentId).Select(d => d.Name).FirstOrDefault(),
                Designation = db.Designations.Where(d => d.Id == s.DesignationId).Select(d => d.Name).FirstOrDefault(),
            })
            .ToListAsync(ct);
        string[] headers = ["Employee no.", "First name", "Last name", "Gender", "Type", "Employment", "Status", "Joined", "Phone", "Email",
            "CNIC", "Campus", "Department", "Designation"];
        return exporter.Write("Staff", headers, rows.Select(r => (IReadOnlyList<object?>)
        [
            r.EmployeeNumber, r.FirstName, r.LastName, r.Gender.ToString(), r.StaffType.ToString(), r.EmploymentType.ToString(),
            r.Status.ToString(), r.JoinDate, r.Phone, r.Email, r.NationalId, r.Campus, r.Department, r.Designation,
        ]), format);
    }

    // ───────────────────────────── Commands ─────────────────────────────

    public async Task<StaffDetailDto> CreateAsync(SaveStaffRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Staff.Create, ct);
        var current = await access.GetAsync(ct);
        await ValidateReferencesAsync(request, current, ct);

        var id = await db.InTransactionAsync(async innerCt =>
        {
            var number = string.IsNullOrWhiteSpace(request.EmployeeNumber)
                ? await sequences.NextAsync(SequenceKeys.Employee, request.JoinDate, innerCt)
                : request.EmployeeNumber.Trim().ToUpperInvariant();
            if (await db.Staff.AnyAsync(s => s.EmployeeNumber == number, innerCt))
            {
                throw new ConflictException("staff.number_taken", $"Employee number {number} is already in use.");
            }

            var staff = new StaffMember(current.SchoolId, request.CampusId, number, request.FirstName, request.LastName, request.Gender,
                request.StaffType, request.JoinDate);
            Apply(staff, request);
            db.Staff.Add(staff);
            await db.SaveChangesAsync(innerCt);
            return staff.Id;
        }, ct: ct);
        return await GetAsync(id, ct);
    }

    public async Task<StaffDetailDto> UpdateAsync(Guid id, SaveStaffRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Staff.Update, ct);
        var current = await access.GetAsync(ct);
        var staff = await LoadScopedAsync(id, ct);
        if (request.Version is { } version)
        {
            db.Entry(staff).Property(s => s.Version).OriginalValue = version;
        }

        await ValidateReferencesAsync(request, current, ct);
        if (staff.CampusId != request.CampusId)
        {
            // Registers and assignments are per campus: moving someone must not leave them teaching at the old one.
            var yearId = await CurrentYearIdAsync(ct);
            var assigned = await db.Sections.AnyAsync(x => x.AcademicYearId == yearId && x.ClassTeacherId == id, ct)
                           || await db.SectionSubjectTeachers.AnyAsync(t => t.StaffId == id && db.Sections.Any(x => x.Id == t.SectionId && x.AcademicYearId == yearId), ct);
            Guard.Against(assigned, "staff.campus_has_assignments",
                "Hand over this person's classes and subjects before moving them to another campus.");
        }

        staff.Rename(request.FirstName, request.LastName);
        staff.Gender = request.Gender;
        staff.CampusId = request.CampusId;
        staff.StaffType = request.StaffType;
        staff.JoinDate = request.JoinDate;
        Apply(staff, request);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<StaffStatusResult> ChangeStatusAsync(Guid id, ChangeStaffStatusRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Staff.Update, ct);
        var removed = await db.InTransactionAsync(async innerCt =>
        {
            var staff = await LoadScopedAsync(id, innerCt);
            staff.ChangeStatus(request.Status, request.EffectiveDate, request.Reason);
            var released = 0;
            if (!staff.IsCurrent)
            {
                // A leaver cannot stay on registers: release their classes and subjects so someone else can take them.
                foreach (var section in await db.Sections.Where(x => x.ClassTeacherId == id).ToListAsync(innerCt))
                {
                    section.ClassTeacherId = null;
                    released++;
                }

                var subjects = await db.SectionSubjectTeachers.Where(t => t.StaffId == id).ToListAsync(innerCt);
                db.SectionSubjectTeachers.RemoveRange(subjects);
                released += subjects.Count;
            }

            await db.SaveChangesAsync(innerCt);
            return released;
        }, ct: ct);
        return new StaffStatusResult(await GetAsync(id, ct), removed);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Staff.Delete, ct);
        var staff = await LoadScopedAsync(id, ct);
        var hasHistory = await db.Sections.AnyAsync(x => x.ClassTeacherId == id, ct)
                         || await db.SectionSubjectTeachers.AnyAsync(t => t.StaffId == id, ct)
                         || await db.StaffAttendanceRecords.AnyAsync(r => r.StaffId == id, ct)
                         || await db.StaffLeaveRequests.AnyAsync(l => l.StaffId == id, ct)
                         || await db.AttendanceRegisters.AnyAsync(r => r.TakenByStaffId == id, ct);
        if (hasHistory)
        {
            throw new ConflictException("staff.has_history",
                "This person has classes or attendance on record. Mark them as resigned or retired instead of deleting.");
        }

        db.Staff.Remove(staff);
        await db.SaveChangesAsync(ct);
    }

    // ───────────────────────────── Sign-in access ─────────────────────────────

    public async Task<StaffAccessResult> GrantAccessAsync(Guid id, GrantStaffAccessRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Staff.Update, ct);
        var staff = await LoadScopedAsync(id, ct);
        Guard.Against(staff.UserId is not null, "staff.has_account", "This person already has a sign-in account.");
        Guard.Against(!staff.IsCurrent, "staff.not_current", "Only current staff can be given sign-in access.");
        Guard.Against(string.IsNullOrWhiteSpace(staff.Email), "staff.email_required",
            "Add an email address to the staff record first; it becomes their sign-in name.");

        // MemberService enforces access.users.manage and that the caller may grant each role.
        var result = await members.InviteAsync(new InviteMemberRequest(staff.FirstName,
            string.IsNullOrWhiteSpace(staff.LastName) ? "-" : staff.LastName, staff.Email!, request.Roles), ct);
        await LinkAsync(staff, result.Member.UserId, ct);
        return new StaffAccessResult(await GetAsync(id, ct), result.Handover);
    }

    public async Task<StaffDetailDto> LinkAccountAsync(Guid id, LinkStaffAccountRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Staff.Update, ct);
        await access.RequireAsync(Permissions.Users.Manage, ct);
        var staff = await LoadScopedAsync(id, ct);
        Guard.Against(staff.UserId is not null, "staff.has_account", "Unlink the current account first.");
        Guard.Against(!await db.SchoolMemberships.AnyAsync(m => m.UserId == request.UserId, ct), "staff.account_not_member",
            "That account does not have access to this school.");
        await LinkAsync(staff, request.UserId, ct);
        return await GetAsync(id, ct);
    }

    public async Task<StaffDetailDto> UnlinkAccountAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Staff.Update, ct);
        await access.RequireAsync(Permissions.Users.Manage, ct);
        var staff = await LoadScopedAsync(id, ct);
        staff.UserId = null;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task LinkAsync(StaffMember staff, Guid userId, CancellationToken ct)
    {
        if (await db.Staff.AnyAsync(s => s.UserId == userId && s.Id != staff.Id, ct))
        {
            throw new ConflictException("staff.account_linked", "That account already belongs to another staff record.");
        }

        staff.UserId = userId;
        await db.SaveChangesAsync(ct);
    }

    // ───────────────────────────── Helpers ─────────────────────────────

    internal static IQueryable<StaffMember> Visible(IQueryable<StaffMember> staff, EffectiveAccess current) =>
        current.AllCampuses ? staff : staff.Where(s => current.CampusIds!.Contains(s.CampusId));

    internal async Task<StaffMember> LoadScopedAsync(Guid id, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        return await Visible(db.Staff, current).SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Staff member", id);
    }

    private async Task<Guid?> CurrentYearIdAsync(CancellationToken ct) =>
        await db.AcademicYears.Where(y => y.IsCurrent).Select(y => (Guid?)y.Id).FirstOrDefaultAsync(ct);

    private async Task ValidateReferencesAsync(SaveStaffRequest request, EffectiveAccess current, CancellationToken ct)
    {
        if (!current.CanAccessCampus(request.CampusId) || !await db.Campuses.AnyAsync(c => c.Id == request.CampusId, ct))
        {
            throw new NotFoundException("Campus", request.CampusId);
        }

        if (request.DepartmentId is { } departmentId && !await db.Departments.AnyAsync(d => d.Id == departmentId, ct))
        {
            throw new NotFoundException("Department", departmentId);
        }

        if (request.DesignationId is { } designationId && !await db.Designations.AnyAsync(d => d.Id == designationId, ct))
        {
            throw new NotFoundException("Designation", designationId);
        }
    }

    private static void Apply(StaffMember staff, SaveStaffRequest r)
    {
        staff.DateOfBirth = r.DateOfBirth;
        staff.NationalId = Digits(r.NationalId);
        staff.Phone = string.IsNullOrWhiteSpace(r.Phone) ? null : PhoneNumber.Normalize(r.Phone, "staff.phone_invalid");
        staff.Email = Clean(r.Email)?.ToLowerInvariant();
        staff.Address = r.Address?.ToAddress() ?? new Address();
        staff.DepartmentId = r.DepartmentId;
        staff.DesignationId = r.DesignationId;
        staff.EmploymentType = r.EmploymentType;
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static string? Digits(string? v)
    {
        var digits = new string((v ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }
}
