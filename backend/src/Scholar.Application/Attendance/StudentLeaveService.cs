using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Domain.Access;
using Scholar.Domain.Attendance;
using Scholar.Domain.Common;
using Scholar.Domain.Communication;
using Scholar.Domain.Students;

namespace Scholar.Application.Attendance;

/// <summary>
/// Student leave applications. Approval is itself the authorisation to change the covered days: absences already
/// recorded inside the leave become Leave (audited on each record) and unsent absence alerts are cancelled; registers
/// taken later start those students on Leave.
/// </summary>
public sealed class StudentLeaveService(IAppDbContext db, IAccessContext access, AttendanceCalendar calendar, RegisterService registers)
{
    public async Task<PagedResult<StudentLeaveDto>> ListAsync(StudentLeaveQuery query, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        if (!current.HasAny(Permissions.Attendance.ManageLeave, Permissions.Attendance.View)) throw new ForbiddenException();
        var leave = Scoped(current)
            .WhereIf(query.Status is not null, l => l.Status == query.Status)
            .WhereIf(query.StudentId is not null, l => l.StudentId == query.StudentId)
            .WhereIf(query.CampusId is not null, l => db.Students.Any(s => s.Id == l.StudentId && s.CampusId == query.CampusId))
            .WhereIf(query.SectionId is not null, l => db.Students.Any(s => s.Id == l.StudentId && s.CurrentSectionId == query.SectionId))
            .WhereIf(query.From is not null, l => l.ToDate >= query.From)
            .WhereIf(query.To is not null, l => l.FromDate <= query.To);
        if (query.Term is { } term)
        {
            var pattern = $"%{term}%";
            leave = leave.Where(l => db.Students.Any(s => s.Id == l.StudentId
                                                          && (EF.Functions.ILike(s.FullName, pattern) || EF.Functions.ILike(s.AdmissionNumber, term + "%"))));
        }

        var total = await leave.CountAsync(ct);
        var page = leave.OrderBy(l => l.Status == LeaveStatus.Pending ? 0 : 1).ThenByDescending(l => l.FromDate).ThenBy(l => l.Id)
            .Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize);
        return new PagedResult<StudentLeaveDto>(await DtosAsync(page, ct), query.SafePage, query.SafePageSize, total);
    }

    public async Task<StudentLeaveDto> GetAsync(Guid id, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        if (!current.HasAny(Permissions.Attendance.ManageLeave, Permissions.Attendance.View)) throw new ForbiddenException();
        return (await DtosAsync(Scoped(current).Where(l => l.Id == id), ct)).SingleOrDefault() ?? throw new NotFoundException("Leave", id);
    }

    public async Task<LeaveDecisionResult<StudentLeaveDto>> CreateAsync(CreateStudentLeaveRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Attendance.ManageLeave, ct);
        var current = await access.GetAsync(ct);
        var student = await db.Students.Where(s => s.Id == request.StudentId && (current.CampusIds == null || current.CampusIds.Contains(s.CampusId)))
            .Select(s => new { s.Id, s.Status, s.FullName }).SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Student", request.StudentId);
        Guard.Against(student.Status is not (StudentStatus.Active or StudentStatus.Suspended), "leave.student_not_current",
            "Leave can only be recorded for current students.");
        var today = await calendar.TodayAsync(ct);
        Guard.Against(request.FromDate < today.AddDays(-90), "leave.too_old", "Leave can be recorded at most 90 days back.");
        Guard.Against(request.FromDate > today.AddDays(180), "leave.too_far", "Leave can be recorded at most six months ahead.");
        if (await db.StudentLeaveRequests.AnyAsync(l => l.StudentId == request.StudentId && l.FromDate <= request.ToDate && l.ToDate >= request.FromDate
                                                       && (l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.Approved), ct))
        {
            throw new ConflictException("leave.overlap", $"{student.FullName} already has leave recorded for some of these days.");
        }

        var leave = new StudentLeaveRequest(current.SchoolId, request.StudentId, request.FromDate, request.ToDate, request.Kind, request.Reason,
            request.RequestedBy);
        var changed = await db.InTransactionAsync(async innerCt =>
        {
            db.StudentLeaveRequests.Add(leave);
            var marks = 0;
            if (request.Approve)
            {
                leave.Approve(current.UserId, calendar.Now, null);
                await db.SaveChangesAsync(innerCt);
                marks = await ApplyApprovalAsync(leave, innerCt);
            }

            await db.SaveChangesAsync(innerCt);
            return marks;
        }, ct: ct);
        return new LeaveDecisionResult<StudentLeaveDto>(await GetAsync(leave.Id, ct), changed);
    }

    public async Task<LeaveDecisionResult<StudentLeaveDto>> DecideAsync(Guid id, DecideRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Attendance.ManageLeave, ct);
        var current = await access.GetAsync(ct);
        var leave = await Scoped(current).SingleOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException("Leave", id);
        var changed = await db.InTransactionAsync(async innerCt =>
        {
            var marks = 0;
            if (request.Approve)
            {
                leave.Approve(current.UserId, calendar.Now, request.Note);
                marks = await ApplyApprovalAsync(leave, innerCt);
            }
            else
            {
                leave.Reject(current.UserId, calendar.Now, request.Note);
            }

            await db.SaveChangesAsync(innerCt);
            return marks;
        }, ct: ct);
        return new LeaveDecisionResult<StudentLeaveDto>(await GetAsync(id, ct), changed);
    }

    /// <summary>Cancels a leave; days already marked Leave because of it go back to Absent (the student did not attend).</summary>
    public async Task<LeaveDecisionResult<StudentLeaveDto>> CancelAsync(Guid id, string? note, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Attendance.ManageLeave, ct);
        var current = await access.GetAsync(ct);
        var leave = await Scoped(current).SingleOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException("Leave", id);
        var changed = await db.InTransactionAsync(async innerCt =>
        {
            leave.Cancel(current.UserId, calendar.Now, note);
            var records = await db.AttendanceRecords.Where(r => r.LeaveRequestId == id && r.Status == AttendanceStatus.Leave).ToListAsync(innerCt);
            foreach (var record in records)
            {
                record.Status = AttendanceStatus.Absent;
                record.LeaveRequestId = null;
                record.Remark ??= "Leave cancelled";
            }

            await db.SaveChangesAsync(innerCt);
            return records.Count;
        }, ct: ct);
        return new LeaveDecisionResult<StudentLeaveDto>(await GetAsync(id, ct), changed);
    }

    private async Task<int> ApplyApprovalAsync(StudentLeaveRequest leave, CancellationToken ct)
    {
        var records = await db.AttendanceRecords.Include(r => r.Register)
            .Where(r => r.StudentId == leave.StudentId && r.Status == AttendanceStatus.Absent
                        && r.Register.Date >= leave.FromDate && r.Register.Date <= leave.ToDate)
            .ToListAsync(ct);
        foreach (var record in records)
        {
            record.Status = AttendanceStatus.Leave;
            record.LeaveRequestId = leave.Id;
        }

        var registerIds = records.Select(r => r.RegisterId).Distinct().ToList();
        if (registerIds.Count > 0)
        {
            var unsent = await db.OutboundMessages.Where(m => m.StudentId == leave.StudentId && m.RelatedId != null
                                                              && registerIds.Contains(m.RelatedId.Value) && m.Purpose == AbsenceNotifier.AbsentPurpose)
                .ToListAsync(ct);
            foreach (var message in unsent) message.Cancel("Leave was approved before the message was sent.", calendar.Now);
        }

        return records.Count;
    }

    private IQueryable<StudentLeaveRequest> Scoped(EffectiveAccess current) =>
        current.AllCampuses ? db.StudentLeaveRequests
            : db.StudentLeaveRequests.Where(l => db.Students.Any(s => s.Id == l.StudentId && current.CampusIds!.Contains(s.CampusId)));

    internal async Task<List<StudentLeaveDto>> DtosAsync(IQueryable<StudentLeaveRequest> query, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var rows = await query.Select(l => new
        {
            l.Id, l.StudentId, l.FromDate, l.ToDate, l.Kind, l.Reason, l.RequestedBy, l.Status, l.CreatedAt, l.CreatedBy, l.DecidedBy, l.DecidedAt,
            l.DecisionNote,
            Student = db.Students.Where(s => s.Id == l.StudentId).Select(s => new
            {
                s.FullName, s.AdmissionNumber, s.PhotoFileId, s.CampusId,
                ClassName = db.Sections.Where(x => x.Id == s.CurrentSectionId).Select(x => x.Grade.Name + " – " + x.Name).FirstOrDefault(),
            }).First(),
        }).ToListAsync(ct);
        if (rows.Count == 0) return [];

        var closed = await calendar.ClosedReasonsAsync(rows.Min(r => r.FromDate), rows.Max(r => r.ToDate), ct);
        var names = await registers.UserNamesAsync(rows.SelectMany(r => new[] { r.CreatedBy, r.DecidedBy }), ct);
        var canDecide = current.Has(Permissions.Attendance.ManageLeave);
        return rows.Select(r =>
        {
            var days = 0;
            for (var d = r.FromDate; d <= r.ToDate; d = d.AddDays(1))
            {
                if (closed(d, r.Student.CampusId) is null) days++;
            }

            return new StudentLeaveDto(r.Id, r.StudentId, r.Student.FullName, r.Student.AdmissionNumber, r.Student.ClassName, r.Student.PhotoFileId,
                r.FromDate, r.ToDate, days, r.Kind, r.Reason, r.RequestedBy, r.Status, r.CreatedAt, RegisterService.Name(names, r.CreatedBy),
                RegisterService.Name(names, r.DecidedBy), r.DecidedAt, r.DecisionNote, canDecide && r.Status == LeaveStatus.Pending);
        }).ToList();
    }
}
