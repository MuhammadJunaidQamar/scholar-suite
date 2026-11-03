using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Domain.Access;
using Scholar.Domain.Common;
using Scholar.Domain.Staffing;

namespace Scholar.Application.Staffing;

/// <summary>
/// Who teaches whom: each section's class teacher (takes the daily register) and subject teachers (take period
/// registers, and later enter marks). Assignments belong to a section, so they are per academic year.
/// </summary>
public sealed class TeachingAssignmentService(IAppDbContext db, IAccessContext access)
{
    public async Task<IReadOnlyList<SectionTeachingDto>> ListAsync(Guid? academicYearId, Guid? campusId, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        if (!current.HasAny(Permissions.Academics.View, Permissions.Staff.View))
        {
            throw new ForbiddenException();
        }

        var yearId = academicYearId ?? await db.AcademicYears.Where(y => y.IsCurrent).Select(y => (Guid?)y.Id).FirstOrDefaultAsync(ct);
        var sections = db.Sections.Where(s => s.AcademicYearId == yearId && s.IsActive)
            .WhereIf(campusId is not null, s => s.CampusId == campusId);
        if (!current.AllCampuses)
        {
            sections = sections.Where(s => current.CampusIds!.Contains(s.CampusId));
        }

        var rows = await sections
            .OrderBy(s => s.Grade.LevelOrder).ThenBy(s => s.CampusId).ThenBy(s => s.Name)
            .Select(s => new
            {
                s.Id, s.GradeId, s.CampusId, ClassName = s.Grade.Name + " – " + s.Name, s.Grade.LevelOrder,
                Campus = db.Campuses.Where(c => c.Id == s.CampusId).Select(c => c.Name).First(),
                s.ClassTeacherId,
                ClassTeacher = db.Staff.Where(t => t.Id == s.ClassTeacherId).Select(t => t.FullName).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var gradeIds = rows.Select(r => r.GradeId).Distinct().ToList();
        var offered = await db.GradeSubjects.Where(g => g.AcademicYearId == yearId && gradeIds.Contains(g.GradeId))
            .OrderBy(g => g.Subject.Name)
            .Select(g => new { g.GradeId, g.SubjectId, g.Subject.Name, g.Subject.Code })
            .ToListAsync(ct);
        var sectionIds = rows.Select(r => r.Id).ToList();
        var assigned = await (
                from t in db.SectionSubjectTeachers
                join subject in db.Subjects on t.SubjectId equals subject.Id
                join staff in db.Staff on t.StaffId equals staff.Id
                where sectionIds.Contains(t.SectionId)
                select new { t.SectionId, t.SubjectId, subject.Name, subject.Code, t.StaffId, StaffName = staff.FullName })
            .ToListAsync(ct);

        return rows.Select(r =>
        {
            var mine = assigned.Where(a => a.SectionId == r.Id).ToList();
            var subjects = offered.Where(o => o.GradeId == r.GradeId)
                .Select(o =>
                {
                    var teacher = mine.FirstOrDefault(a => a.SubjectId == o.SubjectId);
                    return new SubjectTeacherDto(o.SubjectId, o.Name, o.Code, true, teacher?.StaffId, teacher?.StaffName);
                })
                .Concat(mine.Where(a => offered.All(o => o.GradeId != r.GradeId || o.SubjectId != a.SubjectId))
                    .Select(a => new SubjectTeacherDto(a.SubjectId, a.Name, a.Code, false, a.StaffId, a.StaffName)))
                .ToList();
            return new SectionTeachingDto(r.Id, r.ClassName, r.CampusId, r.Campus, r.LevelOrder, r.ClassTeacherId, r.ClassTeacher, subjects);
        }).ToList();
    }

    public async Task<SectionTeachingDto> SaveAsync(Guid sectionId, SaveSectionTeachersRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Academics.Manage, ct);
        var current = await access.GetAsync(ct);
        var section = await db.Sections.SingleOrDefaultAsync(s => s.Id == sectionId, ct);
        if (section is null || !current.CanAccessCampus(section.CampusId))
        {
            throw new NotFoundException("Section", sectionId);
        }

        var staffIds = request.Subjects.Where(s => s.StaffId is not null).Select(s => s.StaffId!.Value)
            .Append(request.ClassTeacherId ?? Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToList();
        var staff = await db.Staff.Where(s => staffIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        foreach (var id in staffIds)
        {
            var person = staff.GetValueOrDefault(id) ?? throw new NotFoundException("Staff member", id);
            Guard.Against(!person.IsCurrent, "teaching.staff_not_current", $"{person.FullName} no longer works at the school.");
            if (person.CampusId != section.CampusId)
            {
                var campus = await db.Campuses.Where(c => c.Id == person.CampusId).Select(c => c.Name).SingleAsync(ct);
                throw new DomainException("teaching.other_campus",
                    $"{person.FullName} works at {campus}. Choose someone from this section's campus.");
            }
        }

        var subjectIds = request.Subjects.Select(s => s.SubjectId).ToList();
        var known = await db.Subjects.Where(s => subjectIds.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct);
        if (subjectIds.Except(known).FirstOrDefault() is var unknown && unknown != Guid.Empty)
        {
            throw new NotFoundException("Subject", unknown);
        }

        section.ClassTeacherId = request.ClassTeacherId;
        var existing = await db.SectionSubjectTeachers.Where(t => t.SectionId == sectionId).ToListAsync(ct);
        foreach (var input in request.Subjects)
        {
            var row = existing.FirstOrDefault(t => t.SubjectId == input.SubjectId);
            if (input.StaffId is null)
            {
                if (row is not null) db.SectionSubjectTeachers.Remove(row);
            }
            else if (row is null)
            {
                db.SectionSubjectTeachers.Add(new SectionSubjectTeacher(current.SchoolId, sectionId, input.SubjectId, input.StaffId.Value));
            }
            else
            {
                row.StaffId = input.StaffId.Value;
            }
        }

        await db.SaveChangesAsync(ct);
        return (await ListAsync(section.AcademicYearId, section.CampusId, ct)).Single(s => s.SectionId == sectionId);
    }
}
