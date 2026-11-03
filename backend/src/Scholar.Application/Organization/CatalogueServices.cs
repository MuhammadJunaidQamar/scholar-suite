using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Domain.Access;
using Scholar.Domain.Common;
using Scholar.Domain.Organization;
using Scholar.Domain.Platform;

namespace Scholar.Application.Organization;

// ───────────────────────────── Campuses ─────────────────────────────

public sealed record CampusDto(Guid Id, string Code, string Name, AddressDto Address, string? Phone, string? Email, bool IsMain,
    bool IsActive, int SectionCount, int RoomCount);

public sealed record SaveCampusRequest(string Code, string Name, AddressDto Address, string? Phone, string? Email, bool IsActive = true);

public sealed class SaveCampusValidator : AbstractValidator<SaveCampusRequest>
{
    public SaveCampusValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9-]+$").WithMessage("Use letters, digits or '-'.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(40);
    }
}

public sealed class CampusService(IAppDbContext db, IAccessContext access)
{
    public async Task<IReadOnlyList<CampusDto>> ListAsync(CancellationToken ct)
    {
        await access.RequireAsync(Permissions.School.View, ct);
        var currentYear = await db.AcademicYears.Where(y => y.IsCurrent).Select(y => (Guid?)y.Id).FirstOrDefaultAsync(ct);
        return await db.Campuses.OrderByDescending(c => c.IsMain).ThenBy(c => c.Name)
            .Select(c => new CampusDto(c.Id, c.Code, c.Name,
                new AddressDto(c.Address.Line1, c.Address.Line2, c.Address.City, c.Address.Province, c.Address.PostalCode, c.Address.CountryCode),
                c.Phone, c.Email, c.IsMain, c.IsActive,
                db.Sections.Count(s => s.CampusId == c.Id && s.AcademicYearId == currentYear),
                db.Rooms.Count(r => r.CampusId == c.Id)))
            .ToListAsync(ct);
    }

    public async Task<CampusDto> CreateAsync(SaveCampusRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.School.Manage, ct);
        await EnsureUniqueAsync(request.Code, null, ct);
        var current = await access.GetAsync(ct);
        var campus = new Campus(current.SchoolId, request.Code, request.Name)
        {
            Address = request.Address.ToAddress(), Phone = Clean(request.Phone), Email = Clean(request.Email), IsActive = request.IsActive,
        };
        db.Campuses.Add(campus);
        await db.SaveChangesAsync(ct);
        return (await ListAsync(ct)).Single(c => c.Id == campus.Id);
    }

    public async Task<CampusDto> UpdateAsync(Guid id, SaveCampusRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.School.Manage, ct);
        var campus = await db.Campuses.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Campus", id);
        await EnsureUniqueAsync(request.Code, id, ct);
        Guard.Against(campus.IsMain && !request.IsActive, "campus.main_inactive", "The main campus cannot be deactivated.");
        campus.Code = request.Code.Trim().ToUpperInvariant();
        campus.Name = request.Name.Trim();
        campus.Address = request.Address.ToAddress();
        campus.Phone = Clean(request.Phone);
        campus.Email = Clean(request.Email);
        campus.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return (await ListAsync(ct)).Single(c => c.Id == id);
    }

    public async Task<IReadOnlyList<CampusDto>> MakeMainAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.School.Manage, ct);
        await db.InTransactionAsync(async innerCt =>
        {
            var campuses = await db.Campuses.ToListAsync(innerCt);
            var target = campuses.SingleOrDefault(c => c.Id == id) ?? throw new NotFoundException("Campus", id);
            Guard.Against(!target.IsActive, "campus.inactive", "Activate the campus before making it the main campus.");
            foreach (var c in campuses.Where(c => c.IsMain)) c.IsMain = false;
            await db.SaveChangesAsync(innerCt);
            target.IsMain = true;
            await db.SaveChangesAsync(innerCt);
            return true;
        }, ct: ct);
        return await ListAsync(ct);
    }

    private async Task EnsureUniqueAsync(string code, Guid? exceptId, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (await db.Campuses.AnyAsync(c => c.Code == normalized && c.Id != exceptId, ct))
        {
            throw new ConflictException("campus.code_taken", $"Another campus already uses the code {normalized}.");
        }
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

// ───────────────────────────── Rooms ─────────────────────────────

public sealed record RoomDto(Guid Id, Guid CampusId, string CampusName, string Code, string Name, RoomType Type, string? Building,
    string? Floor, int Capacity, bool IsBookable, bool IsActive);

public sealed record SaveRoomRequest(Guid CampusId, string Code, string Name, RoomType Type, string? Building, string? Floor,
    int Capacity, bool IsBookable, bool IsActive = true);

public sealed record RoomQuery(Guid? CampusId, RoomType? Type);

public sealed class SaveRoomValidator : AbstractValidator<SaveRoomRequest>
{
    public SaveRoomValidator()
    {
        RuleFor(x => x.CampusId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Capacity).InclusiveBetween(0, 5000);
        RuleFor(x => x.Building).MaximumLength(80);
        RuleFor(x => x.Floor).MaximumLength(30);
    }
}

public sealed class RoomService(IAppDbContext db, IAccessContext access)
{
    public async Task<IReadOnlyList<RoomDto>> ListAsync(RoomQuery query, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.View, ct);
        return await db.Rooms
            .WhereIf(query.CampusId is not null, r => r.CampusId == query.CampusId)
            .WhereIf(query.Type is not null, r => r.Type == query.Type)
            .OrderBy(r => r.CampusId).ThenBy(r => r.Code)
            .Select(r => new RoomDto(r.Id, r.CampusId, db.Campuses.Where(c => c.Id == r.CampusId).Select(c => c.Name).First(),
                r.Code, r.Name, r.Type, r.Building, r.Floor, r.Capacity, r.IsBookable, r.IsActive))
            .ToListAsync(ct);
    }

    public async Task<RoomDto> SaveAsync(Guid? id, SaveRoomRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        if (!await db.Campuses.AnyAsync(c => c.Id == request.CampusId, ct)) throw new NotFoundException("Campus", request.CampusId);
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Rooms.AnyAsync(r => r.CampusId == request.CampusId && r.Code == code && r.Id != id, ct))
        {
            throw new ConflictException("room.code_taken", $"Room code {code} is already used on this campus.");
        }

        Room room;
        if (id is null)
        {
            var current = await access.GetAsync(ct);
            room = new Room(current.SchoolId, request.CampusId, code, request.Name, request.Type, request.Capacity);
            db.Rooms.Add(room);
        }
        else
        {
            room = await db.Rooms.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Room", id);
            Guard.Against(room.CampusId != request.CampusId, "room.campus_change", "A room cannot move to another campus.");
            room.Code = code;
            room.Name = request.Name.Trim();
            room.Type = request.Type;
            room.Capacity = request.Capacity;
        }

        room.Building = string.IsNullOrWhiteSpace(request.Building) ? null : request.Building.Trim();
        room.Floor = string.IsNullOrWhiteSpace(request.Floor) ? null : request.Floor.Trim();
        room.IsBookable = request.IsBookable;
        room.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return (await ListAsync(new RoomQuery(room.CampusId, null), ct)).Single(r => r.Id == room.Id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var room = await db.Rooms.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Room", id);
        Guard.Against(await db.Sections.AnyAsync(s => s.RoomId == id, ct), "room.in_use",
            "A section uses this room as its home room. Mark the room inactive instead.");
        db.Rooms.Remove(room);
        await db.SaveChangesAsync(ct);
    }
}

// ───────────────────────────── Departments & designations ─────────────────────────────

public sealed record DepartmentDto(Guid Id, string Code, string Name, string? Description);
public sealed record SaveDepartmentRequest(string Code, string Name, string? Description);
public sealed record DesignationDto(Guid Id, string Name, DesignationCategory Category);
public sealed record SaveDesignationRequest(string Name, DesignationCategory Category);

public sealed class SaveDepartmentValidator : AbstractValidator<SaveDepartmentRequest>
{
    public SaveDepartmentValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class SaveDesignationValidator : AbstractValidator<SaveDesignationRequest>
{
    public SaveDesignationValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
}

public sealed class StaffStructureService(IAppDbContext db, IAccessContext access)
{
    public async Task<IReadOnlyList<DepartmentDto>> ListDepartmentsAsync(CancellationToken ct)
    {
        await access.RequireAsync(Permissions.School.View, ct);
        return await db.Departments.OrderBy(d => d.Name).Select(d => new DepartmentDto(d.Id, d.Code, d.Name, d.Description)).ToListAsync(ct);
    }

    public async Task<DepartmentDto> SaveDepartmentAsync(Guid? id, SaveDepartmentRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.School.Manage, ct);
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Departments.AnyAsync(d => d.Code == code && d.Id != id, ct))
        {
            throw new ConflictException("department.code_taken", $"Department code {code} is already used.");
        }

        Department department;
        if (id is null)
        {
            var current = await access.GetAsync(ct);
            department = new Department(current.SchoolId, code, request.Name);
            db.Departments.Add(department);
        }
        else
        {
            department = await db.Departments.SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Department", id);
            department.Code = code;
            department.Name = request.Name.Trim();
        }

        department.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        await db.SaveChangesAsync(ct);
        return new DepartmentDto(department.Id, department.Code, department.Name, department.Description);
    }

    public async Task DeleteDepartmentAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.School.Manage, ct);
        var department = await db.Departments.SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Department", id);
        db.Departments.Remove(department);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DesignationDto>> ListDesignationsAsync(CancellationToken ct)
    {
        await access.RequireAsync(Permissions.School.View, ct);
        return await db.Designations.OrderBy(d => d.Category).ThenBy(d => d.Name)
            .Select(d => new DesignationDto(d.Id, d.Name, d.Category)).ToListAsync(ct);
    }

    public async Task<DesignationDto> SaveDesignationAsync(Guid? id, SaveDesignationRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.School.Manage, ct);
        var name = request.Name.Trim();
        if (await db.Designations.AnyAsync(d => d.Name == name && d.Id != id, ct))
        {
            throw new ConflictException("designation.name_taken", $"The designation {name} already exists.");
        }

        Designation designation;
        if (id is null)
        {
            var current = await access.GetAsync(ct);
            designation = new Designation(current.SchoolId, name, request.Category);
            db.Designations.Add(designation);
        }
        else
        {
            designation = await db.Designations.SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Designation", id);
            designation.Name = name;
            designation.Category = request.Category;
        }

        await db.SaveChangesAsync(ct);
        return new DesignationDto(designation.Id, designation.Name, designation.Category);
    }

    public async Task DeleteDesignationAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.School.Manage, ct);
        var designation = await db.Designations.SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Designation", id);
        db.Designations.Remove(designation);
        await db.SaveChangesAsync(ct);
    }
}

// ───────────────────────────── Subjects & curriculum ─────────────────────────────

public sealed record SubjectDto(Guid Id, string Code, string Name, string? ShortName, SubjectType Type, Guid? SubjectGroupId,
    string? SubjectGroupName, string? Color, bool IsActive, int GradeCount);

public sealed record SaveSubjectRequest(string Code, string Name, string? ShortName, SubjectType Type, Guid? SubjectGroupId,
    string? Color, bool IsActive = true);

public sealed record SubjectGroupDto(Guid Id, string Name, string? Description, int SubjectCount);
public sealed record SaveSubjectGroupRequest(string Name, string? Description);

public sealed record CurriculumEntryDto(Guid Id, Guid GradeId, Guid SubjectId, string SubjectName, string SubjectCode,
    string? SubjectColor, bool IsMandatory, int PeriodsPerWeek, Guid? SubjectGroupId);

public sealed record CurriculumItemInput(Guid SubjectId, bool IsMandatory, int PeriodsPerWeek, Guid? SubjectGroupId);

public sealed record SetCurriculumRequest(Guid AcademicYearId, Guid GradeId, IReadOnlyList<CurriculumItemInput> Items);

public sealed record CopyCurriculumRequest(Guid FromAcademicYearId, Guid ToAcademicYearId);

public sealed class SaveSubjectValidator : AbstractValidator<SaveSubjectRequest>
{
    public SaveSubjectValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9-]+$").WithMessage("Use letters, digits or '-'.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ShortName).MaximumLength(30);
        RuleFor(x => x.Color).Matches("^#[0-9A-Fa-f]{6}$").When(x => !string.IsNullOrWhiteSpace(x.Color));
    }
}

public sealed class SaveSubjectGroupValidator : AbstractValidator<SaveSubjectGroupRequest>
{
    public SaveSubjectGroupValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class SetCurriculumValidator : AbstractValidator<SetCurriculumRequest>
{
    public SetCurriculumValidator()
    {
        RuleFor(x => x.Items).Must(items => items.Select(i => i.SubjectId).Distinct().Count() == items.Count)
            .WithMessage("Each subject can appear only once per grade.");
        RuleForEach(x => x.Items).ChildRules(i => i.RuleFor(x => x.PeriodsPerWeek).InclusiveBetween(0, 40));
        RuleFor(x => x.Items.Sum(i => i.PeriodsPerWeek)).LessThanOrEqualTo(80).WithName("Periods per week")
            .WithMessage("More than 80 periods a week is not realistic — check the numbers.");
    }
}

public sealed class SubjectService(IAppDbContext db, IAccessContext access)
{
    public async Task<IReadOnlyList<SubjectDto>> ListAsync(CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.View, ct);
        var yearId = await db.AcademicYears.Where(y => y.IsCurrent).Select(y => (Guid?)y.Id).FirstOrDefaultAsync(ct);
        return await db.Subjects.OrderBy(s => s.Name)
            .Select(s => new SubjectDto(s.Id, s.Code, s.Name, s.ShortName, s.Type, s.SubjectGroupId,
                db.SubjectGroups.Where(g => g.Id == s.SubjectGroupId).Select(g => g.Name).FirstOrDefault(),
                s.Color, s.IsActive, db.GradeSubjects.Count(g => g.SubjectId == s.Id && g.AcademicYearId == yearId)))
            .ToListAsync(ct);
    }

    public async Task<SubjectDto> SaveAsync(Guid? id, SaveSubjectRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Subjects.AnyAsync(s => s.Code == code && s.Id != id, ct))
        {
            throw new ConflictException("subject.code_taken", $"Subject code {code} is already used.");
        }

        if (request.SubjectGroupId is { } groupId && !await db.SubjectGroups.AnyAsync(g => g.Id == groupId, ct))
        {
            throw new NotFoundException("Subject group", groupId);
        }

        Subject subject;
        if (id is null)
        {
            var current = await access.GetAsync(ct);
            subject = new Subject(current.SchoolId, code, request.Name, request.Type);
            db.Subjects.Add(subject);
        }
        else
        {
            subject = await db.Subjects.SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Subject", id);
            subject.Code = code;
            subject.Name = request.Name.Trim();
            subject.Type = request.Type;
        }

        subject.ShortName = string.IsNullOrWhiteSpace(request.ShortName) ? null : request.ShortName.Trim();
        subject.SubjectGroupId = request.SubjectGroupId;
        subject.Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.ToUpperInvariant();
        subject.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return (await ListAsync(ct)).Single(s => s.Id == subject.Id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var subject = await db.Subjects.SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Subject", id);
        Guard.Against(await db.GradeSubjects.AnyAsync(g => g.SubjectId == id, ct), "subject.in_use",
            "This subject is part of a curriculum. Mark it inactive instead of deleting it.");
        db.Subjects.Remove(subject);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SubjectGroupDto>> ListGroupsAsync(CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.View, ct);
        return await db.SubjectGroups.OrderBy(g => g.Name)
            .Select(g => new SubjectGroupDto(g.Id, g.Name, g.Description, db.Subjects.Count(s => s.SubjectGroupId == g.Id)))
            .ToListAsync(ct);
    }

    public async Task<SubjectGroupDto> SaveGroupAsync(Guid? id, SaveSubjectGroupRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var name = request.Name.Trim();
        if (await db.SubjectGroups.AnyAsync(g => g.Name == name && g.Id != id, ct))
        {
            throw new ConflictException("subject_group.name_taken", $"The group {name} already exists.");
        }

        SubjectGroup group;
        if (id is null)
        {
            var current = await access.GetAsync(ct);
            group = new SubjectGroup(current.SchoolId, name);
            db.SubjectGroups.Add(group);
        }
        else
        {
            group = await db.SubjectGroups.SingleOrDefaultAsync(g => g.Id == id, ct) ?? throw new NotFoundException("Subject group", id);
            group.Name = name;
        }

        group.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        await db.SaveChangesAsync(ct);
        return (await ListGroupsAsync(ct)).Single(g => g.Id == group.Id);
    }

    public async Task<IReadOnlyList<CurriculumEntryDto>> GetCurriculumAsync(Guid? academicYearId, Guid? gradeId, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.View, ct);
        var yearId = academicYearId ?? await db.AcademicYears.Where(y => y.IsCurrent).Select(y => (Guid?)y.Id).FirstOrDefaultAsync(ct);
        return await db.GradeSubjects.Where(g => g.AcademicYearId == yearId)
            .WhereIf(gradeId is not null, g => g.GradeId == gradeId)
            .OrderBy(g => g.GradeId).ThenByDescending(g => g.PeriodsPerWeek).ThenBy(g => g.Subject.Name)
            .Select(g => new CurriculumEntryDto(g.Id, g.GradeId, g.SubjectId, g.Subject.Name, g.Subject.Code, g.Subject.Color,
                g.IsMandatory, g.PeriodsPerWeek, g.SubjectGroupId))
            .ToListAsync(ct);
    }

    /// <summary>Replaces one grade's curriculum for a year (add, update and remove in one save).</summary>
    public async Task<IReadOnlyList<CurriculumEntryDto>> SetCurriculumAsync(SetCurriculumRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var year = await db.AcademicYears.SingleOrDefaultAsync(y => y.Id == request.AcademicYearId, ct)
            ?? throw new NotFoundException("Academic year", request.AcademicYearId);
        Guard.Against(year.Status == AcademicYearStatus.Closed, "academic_year.closed", "The curriculum of a closed year cannot change.");
        if (!await db.Grades.AnyAsync(g => g.Id == request.GradeId, ct)) throw new NotFoundException("Grade", request.GradeId);
        var subjectIds = request.Items.Select(i => i.SubjectId).ToList();
        if (await db.Subjects.CountAsync(s => subjectIds.Contains(s.Id), ct) != subjectIds.Count) throw new NotFoundException("Subject");

        var current = await access.GetAsync(ct);
        var existing = await db.GradeSubjects.Where(g => g.AcademicYearId == year.Id && g.GradeId == request.GradeId).ToListAsync(ct);
        db.GradeSubjects.RemoveRange(existing.Where(e => !subjectIds.Contains(e.SubjectId)));
        foreach (var item in request.Items)
        {
            var entry = existing.SingleOrDefault(e => e.SubjectId == item.SubjectId);
            if (entry is null)
            {
                db.GradeSubjects.Add(new GradeSubject(current.SchoolId, year.Id, request.GradeId, item.SubjectId, item.IsMandatory,
                    item.PeriodsPerWeek) { SubjectGroupId = item.SubjectGroupId });
            }
            else
            {
                entry.IsMandatory = item.IsMandatory;
                entry.PeriodsPerWeek = item.PeriodsPerWeek;
                entry.SubjectGroupId = item.SubjectGroupId;
            }
        }

        await db.SaveChangesAsync(ct);
        return await GetCurriculumAsync(year.Id, request.GradeId, ct);
    }

    public async Task<CopyResultDto> CopyCurriculumAsync(CopyCurriculumRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        Guard.Against(request.FromAcademicYearId == request.ToAcademicYearId, "curriculum.copy_same_year", "Choose two different years.");
        var source = await db.GradeSubjects.Where(g => g.AcademicYearId == request.FromAcademicYearId).ToListAsync(ct);
        var existing = await db.GradeSubjects.Where(g => g.AcademicYearId == request.ToAcademicYearId)
            .Select(g => new { g.GradeId, g.SubjectId }).ToListAsync(ct);
        var created = 0;
        foreach (var s in source.Where(s => !existing.Any(e => e.GradeId == s.GradeId && e.SubjectId == s.SubjectId)))
        {
            db.GradeSubjects.Add(new GradeSubject(s.SchoolId, request.ToAcademicYearId, s.GradeId, s.SubjectId, s.IsMandatory, s.PeriodsPerWeek)
            {
                SubjectGroupId = s.SubjectGroupId,
            });
            created++;
        }

        await db.SaveChangesAsync(ct);
        return new CopyResultDto(created, source.Count - created);
    }
}

// ───────────────────────────── Calendar ─────────────────────────────

public sealed record CalendarEventDto(Guid Id, string Title, string? Description, CalendarEventType Type, DateOnly StartDate,
    DateOnly EndDate, TimeOnly? StartTime, TimeOnly? EndTime, bool IsHoliday, EventAudience Audience, Guid? CampusId,
    string? CampusName, string? Location);

public sealed record SaveCalendarEventRequest(string Title, string? Description, CalendarEventType Type, DateOnly StartDate,
    DateOnly EndDate, TimeOnly? StartTime, TimeOnly? EndTime, bool IsHoliday, EventAudience Audience, Guid? CampusId,
    string? Location);

public sealed record CalendarQuery(DateOnly? From, DateOnly? To, CalendarEventType? Type, Guid? CampusId);

public sealed class SaveCalendarEventValidator : AbstractValidator<SaveCalendarEventRequest>
{
    public SaveCalendarEventValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Location).MaximumLength(200);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).WithMessage("The event must end on or after its start date.");
        RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime).When(x => x.StartTime is not null && x.EndTime is not null
                                                                       && x.StartDate == x.EndDate)
            .WithMessage("The end time must be after the start time.");
    }
}

public sealed class CalendarService(IAppDbContext db, IAccessContext access)
{
    public async Task<IReadOnlyList<CalendarEventDto>> ListAsync(CalendarQuery query, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        if (!current.HasAny(Permissions.Academics.View, Permissions.Events.View))
        {
            throw new ForbiddenException();
        }

        return await db.CalendarEvents
            .WhereIf(query.From is not null, e => e.EndDate >= query.From)
            .WhereIf(query.To is not null, e => e.StartDate <= query.To)
            .WhereIf(query.Type is not null, e => e.Type == query.Type)
            .WhereIf(query.CampusId is not null, e => e.CampusId == null || e.CampusId == query.CampusId)
            .OrderBy(e => e.StartDate).ThenBy(e => e.StartTime)
            .Select(e => new CalendarEventDto(e.Id, e.Title, e.Description, e.Type, e.StartDate, e.EndDate, e.StartTime, e.EndTime,
                e.IsHoliday, e.Audience, e.CampusId, db.Campuses.Where(c => c.Id == e.CampusId).Select(c => c.Name).FirstOrDefault(),
                e.Location))
            .ToListAsync(ct);
    }

    public async Task<CalendarEventDto> SaveAsync(Guid? id, SaveCalendarEventRequest request, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        if (!current.HasAny(Permissions.Academics.Manage, Permissions.Events.Manage)) throw new ForbiddenException();
        if (request.CampusId is { } campusId && !await db.Campuses.AnyAsync(c => c.Id == campusId, ct))
        {
            throw new NotFoundException("Campus", campusId);
        }

        CalendarEvent calendarEvent;
        if (id is null)
        {
            calendarEvent = new CalendarEvent(current.SchoolId, request.Title, request.Type, request.StartDate, request.EndDate);
            db.CalendarEvents.Add(calendarEvent);
        }
        else
        {
            calendarEvent = await db.CalendarEvents.SingleOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException("Event", id);
            calendarEvent.Title = request.Title.Trim();
            calendarEvent.Type = request.Type;
            calendarEvent.SetDates(request.StartDate, request.EndDate);
        }

        calendarEvent.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        calendarEvent.StartTime = request.StartTime;
        calendarEvent.EndTime = request.EndTime;
        calendarEvent.IsHoliday = request.IsHoliday;
        calendarEvent.Audience = request.Audience;
        calendarEvent.CampusId = request.CampusId;
        calendarEvent.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        await db.SaveChangesAsync(ct);
        return (await ListAsync(new CalendarQuery(calendarEvent.StartDate, calendarEvent.EndDate, null, null), ct))
            .Single(e => e.Id == calendarEvent.Id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        if (!current.HasAny(Permissions.Academics.Manage, Permissions.Events.Manage)) throw new ForbiddenException();
        var calendarEvent = await db.CalendarEvents.SingleOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException("Event", id);
        db.CalendarEvents.Remove(calendarEvent);
        await db.SaveChangesAsync(ct);
    }
}

// ───────────────────────────── Grading ─────────────────────────────

public sealed record GradingBandDto(decimal MinPercent, decimal MaxPercent, string Grade, decimal? GradePoint, string? Remark, bool IsPassing);

public sealed record GradingScaleDto(Guid Id, string Name, GradingScaleType Type, bool IsDefault, IReadOnlyList<GradingBandDto> Bands);

public sealed record SaveGradingScaleRequest(string Name, GradingScaleType Type, IReadOnlyList<GradingBandDto> Bands);

public sealed class SaveGradingScaleValidator : AbstractValidator<SaveGradingScaleRequest>
{
    public SaveGradingScaleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Bands).NotEmpty().WithMessage("Add at least one band.");
        RuleForEach(x => x.Bands).ChildRules(b =>
        {
            b.RuleFor(x => x.Grade).NotEmpty().MaximumLength(10);
            b.RuleFor(x => x.MinPercent).InclusiveBetween(0, 100);
            b.RuleFor(x => x.MaxPercent).InclusiveBetween(0, 100).GreaterThanOrEqualTo(x => x.MinPercent);
            b.RuleFor(x => x.Remark).MaximumLength(60);
        });
    }
}

public sealed class GradingService(IAppDbContext db, IAccessContext access)
{
    public async Task<IReadOnlyList<GradingScaleDto>> ListAsync(CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.View, ct);
        var scales = await db.GradingScales.Include(s => s.Bands).OrderByDescending(s => s.IsDefault).ThenBy(s => s.Name).ToListAsync(ct);
        return scales.Select(ToDto).ToList();
    }

    public async Task<GradingScaleDto> SaveAsync(Guid? id, SaveGradingScaleRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var name = request.Name.Trim();
        if (await db.GradingScales.AnyAsync(s => s.Name == name && s.Id != id, ct))
        {
            throw new ConflictException("grading.name_taken", $"A grading scale named {name} already exists.");
        }

        GradingScale scale;
        if (id is null)
        {
            var current = await access.GetAsync(ct);
            scale = new GradingScale(current.SchoolId, name, request.Type) { IsDefault = !await db.GradingScales.AnyAsync(ct) };
            db.GradingScales.Add(scale);
        }
        else
        {
            scale = await db.GradingScales.Include(s => s.Bands).SingleOrDefaultAsync(s => s.Id == id, ct)
                    ?? throw new NotFoundException("Grading scale", id);
            scale.Name = name;
            scale.Type = request.Type;
        }

        scale.ReplaceBands(request.Bands.Select(b => (b.MinPercent, b.MaxPercent, b.Grade, b.GradePoint, b.Remark, b.IsPassing)));
        await db.SaveChangesAsync(ct);
        return ToDto(scale);
    }

    public async Task<IReadOnlyList<GradingScaleDto>> MakeDefaultAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var scales = await db.GradingScales.ToListAsync(ct);
        _ = scales.SingleOrDefault(s => s.Id == id) ?? throw new NotFoundException("Grading scale", id);
        foreach (var s in scales) s.IsDefault = s.Id == id;
        await db.SaveChangesAsync(ct);
        return await ListAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var scale = await db.GradingScales.SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Grading scale", id);
        Guard.Against(scale.IsDefault, "grading.delete_default", "Make another scale the default before deleting this one.");
        db.GradingScales.Remove(scale);
        await db.SaveChangesAsync(ct);
    }

    private static GradingScaleDto ToDto(GradingScale s) => new(s.Id, s.Name, s.Type, s.IsDefault,
        s.Bands.OrderByDescending(b => b.MinPercent)
            .Select(b => new GradingBandDto(b.MinPercent, b.MaxPercent, b.Grade, b.GradePoint, b.Remark, b.IsPassing)).ToList());
}
