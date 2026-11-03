using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Domain.Access;
using Scholar.Domain.Common;
using Scholar.Domain.Organization;

namespace Scholar.Application.Organization;

// ───────────────────────────── Academic years ─────────────────────────────

public sealed record AcademicYearDto(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, AcademicYearStatus Status,
    bool IsCurrent, IReadOnlyList<TermDto> Terms, int SectionCount);

public sealed record TermInput(string Name, DateOnly StartDate, DateOnly EndDate);

public sealed record SaveAcademicYearRequest(string Name, DateOnly StartDate, DateOnly EndDate, IReadOnlyList<TermInput>? Terms);

public sealed class SaveAcademicYearValidator : AbstractValidator<SaveAcademicYearRequest>
{
    public SaveAcademicYearValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(40);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("The year must end after it starts.");
        RuleForEach(x => x.Terms).ChildRules(t =>
        {
            t.RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
            t.RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("A term must end after it starts.");
        });
    }
}

public sealed class TermInputValidator : AbstractValidator<TermInput>
{
    public TermInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("A term must end after it starts.");
    }
}

public sealed class AcademicYearService(IAppDbContext db, IAccessContext access)
{
    public async Task<IReadOnlyList<AcademicYearDto>> ListAsync(CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.View, ct);
        var years = await db.AcademicYears.Include(y => y.Terms).OrderByDescending(y => y.StartDate).ToListAsync(ct);
        var counts = await db.Sections.GroupBy(s => s.AcademicYearId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        return years.Select(y => ToDto(y, counts.GetValueOrDefault(y.Id))).ToList();
    }

    public async Task<AcademicYearDto> CreateAsync(SaveAcademicYearRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        await EnsureNoOverlapAsync(request.StartDate, request.EndDate, null, ct);
        var current = await access.GetAsync(ct);
        var year = new AcademicYear(current.SchoolId, request.Name, request.StartDate, request.EndDate);
        foreach (var term in request.Terms ?? [])
        {
            year.AddTerm(term.Name, term.StartDate, term.EndDate);
        }

        db.AcademicYears.Add(year);
        // The first year a school creates becomes current automatically.
        if (!await db.AcademicYears.AnyAsync(y => y.IsCurrent, ct))
        {
            year.MakeCurrent();
        }

        await db.SaveChangesAsync(ct);
        return ToDto(year, 0);
    }

    public async Task<AcademicYearDto> UpdateAsync(Guid id, SaveAcademicYearRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var year = await LoadAsync(id, ct);
        Guard.Against(year.Status == AcademicYearStatus.Closed, "academic_year.closed", "A closed academic year cannot be edited.");
        await EnsureNoOverlapAsync(request.StartDate, request.EndDate, id, ct);
        year.Rename(request.Name);
        year.SetDates(request.StartDate, request.EndDate);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AcademicYearDto> AddTermAsync(Guid id, TermInput term, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var year = await LoadAsync(id, ct);
        year.AddTerm(term.Name, term.StartDate, term.EndDate);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AcademicYearDto> UpdateTermAsync(Guid id, Guid termId, TermInput term, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var year = await LoadAsync(id, ct);
        year.UpdateTerm(termId, term.Name, term.StartDate, term.EndDate);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AcademicYearDto> RemoveTermAsync(Guid id, Guid termId, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var year = await LoadAsync(id, ct);
        year.RemoveTerm(termId);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Makes the year current in one transaction so there is never zero or two current years.</summary>
    public async Task<AcademicYearDto> MakeCurrentAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        await db.InTransactionAsync(async innerCt =>
        {
            var year = await LoadAsync(id, innerCt);
            var previous = await db.AcademicYears.Where(y => y.IsCurrent && y.Id != id).ToListAsync(innerCt);
            foreach (var p in previous) p.ClearCurrent();
            await db.SaveChangesAsync(innerCt);
            year.MakeCurrent();
            await db.SaveChangesAsync(innerCt);
            return true;
        }, ct: ct);
        return await GetAsync(id, ct);
    }

    public async Task<AcademicYearDto> CloseAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var year = await LoadAsync(id, ct);
        year.Close();
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var year = await LoadAsync(id, ct);
        Guard.Against(year.IsCurrent, "academic_year.delete_current", "The current academic year cannot be deleted.");
        Guard.Against(await db.Sections.AnyAsync(s => s.AcademicYearId == id, ct), "academic_year.in_use",
            "This year already has sections. Close it instead of deleting it.");
        db.AcademicYears.Remove(year);
        await db.SaveChangesAsync(ct);
    }

    private async Task<AcademicYearDto> GetAsync(Guid id, CancellationToken ct)
    {
        var year = await LoadAsync(id, ct);
        return ToDto(year, await db.Sections.CountAsync(s => s.AcademicYearId == id, ct));
    }

    private async Task<AcademicYear> LoadAsync(Guid id, CancellationToken ct) =>
        await db.AcademicYears.Include(y => y.Terms).SingleOrDefaultAsync(y => y.Id == id, ct)
        ?? throw new NotFoundException("Academic year", id);

    private async Task EnsureNoOverlapAsync(DateOnly start, DateOnly end, Guid? exceptId, CancellationToken ct)
    {
        var clash = await db.AcademicYears.Where(y => y.Id != exceptId && start <= y.EndDate && end >= y.StartDate)
            .Select(y => y.Name).FirstOrDefaultAsync(ct);
        Guard.Against(clash is not null, "academic_year.overlap", $"These dates overlap the {clash} academic year.");
    }

    private static AcademicYearDto ToDto(AcademicYear y, int sections) => new(y.Id, y.Name, y.StartDate, y.EndDate, y.Status,
        y.IsCurrent, y.Terms.OrderBy(t => t.Sequence).Select(t => new TermDto(t.Id, t.Name, t.Sequence, t.StartDate, t.EndDate)).ToList(),
        sections);
}

// ───────────────────────────── Grades & sections ─────────────────────────────

public sealed record GradeDto(Guid Id, string Name, string Code, int LevelOrder, SchoolStage Stage, Guid? NextGradeId,
    string? NextGradeName, bool IsActive, int SectionCount);

public sealed record SaveGradeRequest(string Name, string Code, SchoolStage Stage, Guid? NextGradeId, bool IsActive = true);

public sealed record ReorderGradesRequest(IReadOnlyList<Guid> OrderedIds);

public sealed record SectionDto(Guid Id, Guid AcademicYearId, Guid CampusId, string CampusName, Guid GradeId, string GradeName,
    int GradeOrder, string Name, string DisplayName, int Capacity, Guid? RoomId, string? RoomName, Guid? SubjectGroupId,
    bool IsActive, int Enrolled);

public sealed record SaveSectionRequest(Guid AcademicYearId, Guid CampusId, Guid GradeId, string Name, int Capacity,
    Guid? RoomId, Guid? SubjectGroupId, bool IsActive = true);

public sealed record SectionQuery(Guid? AcademicYearId, Guid? CampusId, Guid? GradeId);

public sealed record CopySectionsRequest(Guid FromAcademicYearId, Guid ToAcademicYearId);

public sealed record CopyResultDto(int Created, int Skipped);

public sealed class SaveGradeValidator : AbstractValidator<SaveGradeRequest>
{
    public SaveGradeValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9-]+$").WithMessage("Use letters, digits or '-'.");
    }
}

public sealed class SaveSectionValidator : AbstractValidator<SaveSectionRequest>
{
    public SaveSectionValidator()
    {
        RuleFor(x => x.AcademicYearId).NotEmpty();
        RuleFor(x => x.CampusId).NotEmpty();
        RuleFor(x => x.GradeId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Capacity).InclusiveBetween(1, 200);
    }
}

public sealed class GradeService(IAppDbContext db, IAccessContext access)
{
    public async Task<IReadOnlyList<GradeDto>> ListAsync(CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.View, ct);
        var currentYear = await db.AcademicYears.Where(y => y.IsCurrent).Select(y => (Guid?)y.Id).FirstOrDefaultAsync(ct);
        var grades = await db.Grades.OrderBy(g => g.LevelOrder).ToListAsync(ct);
        var counts = await db.Sections.Where(s => s.AcademicYearId == currentYear).GroupBy(s => s.GradeId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        return grades.Select(g => new GradeDto(g.Id, g.Name, g.Code, g.LevelOrder, g.Stage, g.NextGradeId,
            grades.FirstOrDefault(n => n.Id == g.NextGradeId)?.Name, g.IsActive, counts.GetValueOrDefault(g.Id))).ToList();
    }

    public async Task<GradeDto> CreateAsync(SaveGradeRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        await EnsureUniqueCodeAsync(request.Code, null, ct);
        var current = await access.GetAsync(ct);
        var order = (await db.Grades.MaxAsync(g => (int?)g.LevelOrder, ct) ?? 0) + 1;
        var grade = new Grade(current.SchoolId, request.Name, request.Code, order, request.Stage) { IsActive = request.IsActive };
        await SetNextAsync(grade, request.NextGradeId, ct);
        db.Grades.Add(grade);
        await db.SaveChangesAsync(ct);
        return (await ListAsync(ct)).Single(g => g.Id == grade.Id);
    }

    public async Task<GradeDto> UpdateAsync(Guid id, SaveGradeRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var grade = await db.Grades.SingleOrDefaultAsync(g => g.Id == id, ct) ?? throw new NotFoundException("Grade", id);
        await EnsureUniqueCodeAsync(request.Code, id, ct);
        grade.Name = request.Name.Trim();
        grade.Code = request.Code.Trim().ToUpperInvariant();
        grade.Stage = request.Stage;
        grade.IsActive = request.IsActive;
        await SetNextAsync(grade, request.NextGradeId, ct);
        await db.SaveChangesAsync(ct);
        return (await ListAsync(ct)).Single(g => g.Id == id);
    }

    public async Task<IReadOnlyList<GradeDto>> ReorderAsync(ReorderGradesRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var grades = await db.Grades.ToListAsync(ct);
        Guard.Against(request.OrderedIds.Count != grades.Count || grades.Any(g => !request.OrderedIds.Contains(g.Id)),
            "grades.reorder_incomplete", "Include every grade exactly once.");
        for (var i = 0; i < request.OrderedIds.Count; i++)
        {
            grades.Single(g => g.Id == request.OrderedIds[i]).LevelOrder = i + 1;
        }

        await db.SaveChangesAsync(ct);
        return await ListAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var grade = await db.Grades.SingleOrDefaultAsync(g => g.Id == id, ct) ?? throw new NotFoundException("Grade", id);
        Guard.Against(await db.Sections.AnyAsync(s => s.GradeId == id, ct), "grade.in_use",
            "This grade has sections. Mark it inactive instead of deleting it.");
        foreach (var previous in await db.Grades.Where(g => g.NextGradeId == id).ToListAsync(ct))
        {
            previous.NextGradeId = null;
        }

        db.Grades.Remove(grade);
        await db.SaveChangesAsync(ct);
    }

    private async Task SetNextAsync(Grade grade, Guid? nextId, CancellationToken ct)
    {
        if (nextId is null)
        {
            grade.NextGradeId = null;
            return;
        }

        Guard.Against(nextId == grade.Id, "grade.next_self", "A grade cannot promote into itself.");
        var exists = await db.Grades.AnyAsync(g => g.Id == nextId, ct);
        if (!exists) throw new NotFoundException("Grade", nextId);
        grade.NextGradeId = nextId;
    }

    private async Task EnsureUniqueCodeAsync(string code, Guid? exceptId, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (await db.Grades.AnyAsync(g => g.Code == normalized && g.Id != exceptId, ct))
        {
            throw new ConflictException("grade.code_taken", $"Another grade already uses the code {normalized}.");
        }
    }
}

public sealed class SectionService(IAppDbContext db, IAccessContext access)
{
    public async Task<IReadOnlyList<SectionDto>> ListAsync(SectionQuery query, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        if (!current.HasAny(Permissions.Academics.View, Permissions.Students.View, Permissions.Attendance.View))
        {
            throw new ForbiddenException();
        }

        var yearId = query.AcademicYearId ?? await db.AcademicYears.Where(y => y.IsCurrent).Select(y => (Guid?)y.Id).FirstOrDefaultAsync(ct);
        var sections = db.Sections.Where(s => s.AcademicYearId == yearId)
            .WhereIf(query.CampusId is not null, s => s.CampusId == query.CampusId)
            .WhereIf(query.GradeId is not null, s => s.GradeId == query.GradeId);
        if (!current.AllCampuses)
        {
            sections = sections.Where(s => current.CampusIds!.Contains(s.CampusId));
        }

        return await sections
            .OrderBy(s => s.Grade.LevelOrder).ThenBy(s => s.CampusId).ThenBy(s => s.Name)
            .Select(s => new SectionDto(s.Id, s.AcademicYearId, s.CampusId,
                db.Campuses.Where(c => c.Id == s.CampusId).Select(c => c.Name).First(),
                s.GradeId, s.Grade.Name, s.Grade.LevelOrder, s.Name, s.Grade.Name + " – " + s.Name, s.Capacity, s.RoomId,
                s.Room != null ? s.Room.Name : null, s.SubjectGroupId, s.IsActive,
                db.Enrollments.Count(e => e.SectionId == s.Id && e.Status == Domain.Students.EnrollmentStatus.Active)))
            .ToListAsync(ct);
    }

    public async Task<SectionDto> CreateAsync(SaveSectionRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var current = await access.GetAsync(ct);
        await ValidateReferencesAsync(request, current, ct);
        await EnsureUniqueAsync(request, null, ct);
        var section = new Section(current.SchoolId, request.AcademicYearId, request.CampusId, request.GradeId, request.Name,
            request.Capacity) { RoomId = request.RoomId, SubjectGroupId = request.SubjectGroupId, IsActive = request.IsActive };
        db.Sections.Add(section);
        await db.SaveChangesAsync(ct);
        return (await ListAsync(new SectionQuery(request.AcademicYearId, null, null), ct)).Single(s => s.Id == section.Id);
    }

    public async Task<SectionDto> UpdateAsync(Guid id, SaveSectionRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var current = await access.GetAsync(ct);
        var section = await db.Sections.SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Section", id);
        Guard.Against(section.AcademicYearId != request.AcademicYearId || section.GradeId != request.GradeId
                      || section.CampusId != request.CampusId, "section.immutable_placement",
            "A section's year, campus and grade cannot change. Create a new section instead.");
        await ValidateReferencesAsync(request, current, ct);
        await EnsureUniqueAsync(request, id, ct);
        section.Name = request.Name.Trim();
        section.Capacity = request.Capacity;
        section.RoomId = request.RoomId;
        section.SubjectGroupId = request.SubjectGroupId;
        section.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return (await ListAsync(new SectionQuery(section.AcademicYearId, null, null), ct)).Single(s => s.Id == id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var section = await db.Sections.SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Section", id);
        // Enrolment, attendance and timetables reference sections through foreign keys; the database refuses
        // deletion once a section has history, which the API reports as a conflict.
        db.Sections.Remove(section);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Recreates last year's sections (same campus, grade, name, capacity, room) in a new year.</summary>
    public async Task<CopyResultDto> CopyAsync(CopySectionsRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        Guard.Against(request.FromAcademicYearId == request.ToAcademicYearId, "sections.copy_same_year", "Choose two different years.");
        var target = await db.AcademicYears.SingleOrDefaultAsync(y => y.Id == request.ToAcademicYearId, ct)
            ?? throw new NotFoundException("Academic year", request.ToAcademicYearId);
        Guard.Against(target.Status == AcademicYearStatus.Closed, "academic_year.closed", "Sections cannot be added to a closed year.");

        var source = await db.Sections.Where(s => s.AcademicYearId == request.FromAcademicYearId).ToListAsync(ct);
        var existing = await db.Sections.Where(s => s.AcademicYearId == request.ToAcademicYearId)
            .Select(s => new { s.CampusId, s.GradeId, s.Name }).ToListAsync(ct);
        var created = 0;
        foreach (var s in source)
        {
            if (existing.Any(e => e.CampusId == s.CampusId && e.GradeId == s.GradeId && e.Name == s.Name)) continue;
            db.Sections.Add(new Section(s.SchoolId, target.Id, s.CampusId, s.GradeId, s.Name, s.Capacity)
            {
                RoomId = s.RoomId, SubjectGroupId = s.SubjectGroupId, IsActive = s.IsActive,
            });
            created++;
        }

        await db.SaveChangesAsync(ct);
        return new CopyResultDto(created, source.Count - created);
    }

    private async Task ValidateReferencesAsync(SaveSectionRequest request, EffectiveAccess current, CancellationToken ct)
    {
        Guard.Against(!current.CanAccessCampus(request.CampusId), "campus.out_of_scope", "You can only manage sections of your own campuses.");
        var year = await db.AcademicYears.SingleOrDefaultAsync(y => y.Id == request.AcademicYearId, ct)
            ?? throw new NotFoundException("Academic year", request.AcademicYearId);
        Guard.Against(year.Status == AcademicYearStatus.Closed, "academic_year.closed", "Sections of a closed year cannot change.");
        if (!await db.Campuses.AnyAsync(c => c.Id == request.CampusId, ct)) throw new NotFoundException("Campus", request.CampusId);
        if (!await db.Grades.AnyAsync(g => g.Id == request.GradeId, ct)) throw new NotFoundException("Grade", request.GradeId);
        if (request.RoomId is { } roomId)
        {
            var room = await db.Rooms.SingleOrDefaultAsync(r => r.Id == roomId, ct) ?? throw new NotFoundException("Room", roomId);
            Guard.Against(room.CampusId != request.CampusId, "section.room_campus", "The room must be on the section's campus.");
        }

        if (request.SubjectGroupId is { } groupId && !await db.SubjectGroups.AnyAsync(g => g.Id == groupId, ct))
        {
            throw new NotFoundException("Subject group", groupId);
        }
    }

    private async Task EnsureUniqueAsync(SaveSectionRequest request, Guid? exceptId, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (await db.Sections.AnyAsync(s => s.AcademicYearId == request.AcademicYearId && s.CampusId == request.CampusId
                                            && s.GradeId == request.GradeId && s.Name == name && s.Id != exceptId, ct))
        {
            throw new ConflictException("section.duplicate", $"Section {name} already exists for this grade and campus this year.");
        }
    }
}
