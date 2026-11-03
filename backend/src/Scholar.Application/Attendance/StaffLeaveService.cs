using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Domain.Access;
using Scholar.Domain.Attendance;
using Scholar.Domain.Common;
using Scholar.Domain.Staffing;

namespace Scholar.Application.Attendance;

/// <summary>
/// Staff leave: types and yearly allowances, requests (self-service or recorded by HR), balances and approval.
/// Nobody approves their own leave. Approved leave marks the staff register (On leave) for the days it covers.
/// The leave year is the academic year containing the leave (calendar year when no academic year covers it).
/// </summary>
public sealed class StaffLeaveService(IAppDbContext db, IAccessContext access, AttendanceCalendar calendar, RegisterService registers)
{
    // ───────────────────────────── Leave types ─────────────────────────────

    public async Task<IReadOnlyList<LeaveTypeDto>> ListTypesAsync(bool includeInactive, CancellationToken ct)
    {
        await access.GetAsync(ct);
        return await db.LeaveTypes.WhereIf(!includeInactive, t => t.IsActive).OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .Select(t => new LeaveTypeDto(t.Id, t.Code, t.Name, t.AnnualDays, t.IsPaid, t.RequiresDocument, t.IsActive, t.SortOrder))
            .ToListAsync(ct);
    }

    public async Task<LeaveTypeDto> SaveTypeAsync(Guid? id, SaveLeaveTypeRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Hr.Configure, ct);
        var current = await access.GetAsync(ct);
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.LeaveTypes.AnyAsync(t => t.Code == code && t.Id != id, ct))
        {
            throw new ConflictException("leave_type.code_taken", $"The code {code} is already used.");
        }

        LeaveType type;
        if (id is null)
        {
            type = new LeaveType(current.SchoolId, code, request.Name, request.AnnualDays, request.IsPaid);
            db.LeaveTypes.Add(type);
        }
        else
        {
            type = await db.LeaveTypes.SingleOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException("Leave type", id);
            type.Code = code;
            type.Name = request.Name.Trim();
            type.AnnualDays = request.AnnualDays;
            type.IsPaid = request.IsPaid;
        }

        type.RequiresDocument = request.RequiresDocument;
        type.IsActive = request.IsActive;
        type.SortOrder = request.SortOrder;
        await db.SaveChangesAsync(ct);
        return new LeaveTypeDto(type.Id, type.Code, type.Name, type.AnnualDays, type.IsPaid, type.RequiresDocument, type.IsActive, type.SortOrder);
    }

    public async Task DeleteTypeAsync(Guid id, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Hr.Configure, ct);
        var type = await db.LeaveTypes.SingleOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException("Leave type", id);
        if (await db.StaffLeaveRequests.AnyAsync(l => l.LeaveTypeId == id, ct))
        {
            throw new ConflictException("leave_type.in_use", "Leave has been taken under this type. Switch it off instead of deleting it.");
        }

        db.LeaveTypes.Remove(type);
        await db.SaveChangesAsync(ct);
    }

    // ───────────────────────────── Balances ─────────────────────────────

    public async Task<IReadOnlyList<LeaveBalanceDto>> BalancesAsync(Guid staffId, DateOnly? on, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var staff = await LoadStaffAsync(staffId, current, ct);
        if (staff.UserId != current.UserId && !current.HasAny(Permissions.Hr.ViewLeave, Permissions.Hr.ApproveLeave)) throw new ForbiddenException();
        return await BalancesCoreAsync(staffId, on ?? await calendar.TodayAsync(ct), ct);
    }

    internal async Task<IReadOnlyList<LeaveBalanceDto>> BalancesCoreAsync(Guid staffId, DateOnly on, CancellationToken ct)
    {
        var (start, end) = await LeaveYearAsync(on, ct);
        var types = await db.LeaveTypes.Where(t => t.IsActive).OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync(ct);
        var used = await db.StaffLeaveRequests
            .Where(l => l.StaffId == staffId && l.FromDate >= start && l.FromDate <= end
                        && (l.Status == LeaveStatus.Approved || l.Status == LeaveStatus.Pending))
            .GroupBy(l => new { l.LeaveTypeId, l.Status }).Select(g => new { g.Key.LeaveTypeId, g.Key.Status, Days = g.Sum(l => l.Days) })
            .ToListAsync(ct);
        return types.Select(t =>
        {
            var taken = used.Where(u => u.LeaveTypeId == t.Id && u.Status == LeaveStatus.Approved).Sum(u => u.Days);
            var pending = used.Where(u => u.LeaveTypeId == t.Id && u.Status == LeaveStatus.Pending).Sum(u => u.Days);
            return new LeaveBalanceDto(t.Id, t.Code, t.Name, t.AnnualDays, taken, pending, t.AnnualDays is { } days ? days - taken - pending : null);
        }).ToList();
    }

    // ───────────────────────────── Requests ─────────────────────────────

    public async Task<PagedResult<StaffLeaveDto>> ListAsync(StaffLeaveQuery query, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var leave = db.StaffLeaveRequests.AsQueryable();
        if (!current.HasAny(Permissions.Hr.ViewLeave, Permissions.Hr.ApproveLeave))
        {
            leave = leave.Where(l => db.Staff.Any(s => s.Id == l.StaffId && s.UserId == current.UserId));
        }
        else if (!current.AllCampuses)
        {
            leave = leave.Where(l => db.Staff.Any(s => s.Id == l.StaffId && current.CampusIds!.Contains(s.CampusId)));
        }

        leave = leave
            .WhereIf(query.Status is not null, l => l.Status == query.Status)
            .WhereIf(query.StaffId is not null, l => l.StaffId == query.StaffId)
            .WhereIf(query.LeaveTypeId is not null, l => l.LeaveTypeId == query.LeaveTypeId)
            .WhereIf(query.CampusId is not null, l => db.Staff.Any(s => s.Id == l.StaffId && s.CampusId == query.CampusId));
        if (query.Term is { } term)
        {
            var pattern = $"%{term}%";
            leave = leave.Where(l => db.Staff.Any(s => s.Id == l.StaffId && (EF.Functions.ILike(s.FullName, pattern) || EF.Functions.ILike(s.EmployeeNumber, term + "%"))));
        }

        var total = await leave.CountAsync(ct);
        var page = leave.OrderBy(l => l.Status == LeaveStatus.Pending ? 0 : 1).ThenByDescending(l => l.FromDate).ThenBy(l => l.Id)
            .Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize);
        return new PagedResult<StaffLeaveDto>(await DtosAsync(page, ct), query.SafePage, query.SafePageSize, total);
    }

    public async Task<StaffLeaveDto> GetAsync(Guid id, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var leave = await db.StaffLeaveRequests.SingleOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException("Leave", id);
        var staff = await LoadStaffAsync(leave.StaffId, current, ct);
        if (staff.UserId != current.UserId && !current.HasAny(Permissions.Hr.ViewLeave, Permissions.Hr.ApproveLeave)) throw new NotFoundException("Leave", id);
        return (await DtosAsync(db.StaffLeaveRequests.Where(l => l.Id == id), ct)).Single();
    }

    public async Task<StaffLeaveDto> CreateAsync(CreateStaffLeaveRequest request, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        StaffMember staff;
        if (request.StaffId is { } staffId)
        {
            staff = await LoadStaffAsync(staffId, current, ct);
            if (staff.UserId != current.UserId) await access.RequireAsync(Permissions.Hr.ApproveLeave, ct);
        }
        else
        {
            staff = await db.Staff.SingleOrDefaultAsync(s => s.UserId == current.UserId, ct)
                    ?? throw new DomainException("leave.no_staff_record", "Your account is not linked to a staff record. Ask the office to link it.");
        }

        var self = staff.UserId == current.UserId;
        Guard.Against(!staff.IsCurrent, "leave.staff_not_current", "Leave can only be requested for current staff.");
        Guard.Against(request.Approve && self, "leave.own", "Someone else has to approve your own leave.");
        if (request.Approve) await access.RequireAsync(Permissions.Hr.ApproveLeave, ct);
        var type = await db.LeaveTypes.SingleOrDefaultAsync(t => t.Id == request.LeaveTypeId && t.IsActive, ct)
                   ?? throw new NotFoundException("Leave type", request.LeaveTypeId);
        var today = await calendar.TodayAsync(ct);
        Guard.Against(request.FromDate < today.AddDays(-60), "leave.too_old", "Leave can be recorded at most 60 days back.");
        Guard.Against(request.FromDate > today.AddDays(365), "leave.too_far", "Leave can be requested at most a year ahead.");

        var days = request.HalfDay ? 0.5m : (await calendar.SchoolDaysAsync(request.FromDate, request.ToDate, staff.CampusId, ct)).Count;
        if (await db.StaffLeaveRequests.AnyAsync(l => l.StaffId == staff.Id && l.FromDate <= request.ToDate && l.ToDate >= request.FromDate
                                                     && (l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.Approved), ct))
        {
            throw new ConflictException("leave.overlap", "There is already leave requested for some of these days.");
        }

        var leave = new StaffLeaveRequest(current.SchoolId, staff.Id, type.Id, request.FromDate, request.ToDate, request.HalfDay, days,
            request.Reason, self);
        await EnsureBalanceAsync(staff.Id, type, leave, ct);
        await db.InTransactionAsync(async innerCt =>
        {
            db.StaffLeaveRequests.Add(leave);
            if (request.Approve)
            {
                leave.Approve(current.UserId, calendar.Now, null);
                await ApplyApprovalAsync(leave, staff, innerCt);
            }

            await db.SaveChangesAsync(innerCt);
            return true;
        }, ct: ct);
        return (await DtosAsync(db.StaffLeaveRequests.Where(l => l.Id == leave.Id), ct)).Single();
    }

    public async Task<StaffLeaveDto> DecideAsync(Guid id, DecideRequest request, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Hr.ApproveLeave, ct);
        var current = await access.GetAsync(ct);
        var leave = await db.StaffLeaveRequests.SingleOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException("Leave", id);
        var staff = await LoadStaffAsync(leave.StaffId, current, ct);
        Guard.Against(staff.UserId == current.UserId, "leave.own", "Someone else has to decide your own leave.");
        await db.InTransactionAsync(async innerCt =>
        {
            if (request.Approve)
            {
                var type = await db.LeaveTypes.SingleAsync(t => t.Id == leave.LeaveTypeId, innerCt);
                await EnsureBalanceAsync(staff.Id, type, leave, innerCt);
                leave.Approve(current.UserId, calendar.Now, request.Note);
                await ApplyApprovalAsync(leave, staff, innerCt);
            }
            else
            {
                leave.Reject(current.UserId, calendar.Now, request.Note);
            }

            await db.SaveChangesAsync(innerCt);
            return true;
        }, ct: ct);
        return (await DtosAsync(db.StaffLeaveRequests.Where(l => l.Id == id), ct)).Single();
    }

    /// <summary>Withdraws a request (its owner) or cancels approved leave (approvers); register days it set go back to Absent.</summary>
    public async Task<StaffLeaveDto> CancelAsync(Guid id, string? note, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var leave = await db.StaffLeaveRequests.SingleOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException("Leave", id);
        var staff = await LoadStaffAsync(leave.StaffId, current, ct);
        var own = staff.UserId == current.UserId;
        if (!own || leave.Status == LeaveStatus.Approved) await access.RequireAsync(Permissions.Hr.ApproveLeave, ct);
        await db.InTransactionAsync(async innerCt =>
        {
            leave.Cancel(current.UserId, calendar.Now, note);
            foreach (var record in await db.StaffAttendanceRecords.Where(r => r.LeaveRequestId == id).ToListAsync(innerCt))
            {
                // A cancelled day off was still not worked; a half day keeps its hours but loses the leave behind it.
                if (record.Status == StaffAttendanceStatus.OnLeave) record.Set(StaffAttendanceStatus.Absent, null, null, "Leave cancelled");
                else record.Set(record.Status, record.CheckIn, record.CheckOut, record.Remark ?? "Leave cancelled");
            }

            await db.SaveChangesAsync(innerCt);
            return true;
        }, ct: ct);
        return (await DtosAsync(db.StaffLeaveRequests.Where(l => l.Id == id), ct)).Single();
    }

    // ───────────────────────────── Helpers ─────────────────────────────

    private async Task EnsureBalanceAsync(Guid staffId, LeaveType type, StaffLeaveRequest leave, CancellationToken ct)
    {
        if (type.AnnualDays is not { } allowance) return;
        var (start, end) = await LeaveYearAsync(leave.FromDate, ct);
        var committed = await db.StaffLeaveRequests
            .Where(l => l.StaffId == staffId && l.LeaveTypeId == type.Id && l.Id != leave.Id && l.FromDate >= start && l.FromDate <= end
                        && (l.Status == LeaveStatus.Approved || l.Status == LeaveStatus.Pending))
            .SumAsync(l => l.Days, ct);
        var left = allowance - committed;
        if (leave.Days > left)
        {
            throw new DomainException("leave.exceeds_balance",
                $"{type.Name} has {Math.Max(left, 0):0.#} day{(left == 1 ? "" : "s")} left this year; this request needs {leave.Days:0.#}. " +
                "Shorten it or use another leave type (such as unpaid leave) for the rest.");
        }
    }

    /// <summary>
    /// Marks the staff register for covered school days up to today: unmarked and absent days become On leave (a half
    /// day's leave becomes Half day). Days the register shows the person working are left as recorded.
    /// </summary>
    private async Task ApplyApprovalAsync(StaffLeaveRequest leave, StaffMember staff, CancellationToken ct)
    {
        var today = await calendar.TodayAsync(ct);
        var last = leave.ToDate < today ? leave.ToDate : today;
        if (last < leave.FromDate) return;
        var days = await calendar.SchoolDaysAsync(leave.FromDate, last, staff.CampusId, ct);
        var existing = await db.StaffAttendanceRecords.Where(r => r.StaffId == staff.Id && r.Date >= leave.FromDate && r.Date <= last).ToListAsync(ct);
        var target = leave.HalfDay ? StaffAttendanceStatus.HalfDay : StaffAttendanceStatus.OnLeave;
        foreach (var day in days)
        {
            var record = existing.SingleOrDefault(r => r.Date == day);
            if (record is null)
            {
                record = new StaffAttendanceRecord(staff.SchoolId, staff.Id, staff.CampusId, day, target);
                db.StaffAttendanceRecords.Add(record);
                record.Set(target, null, null, null, leave.Id);
                continue;
            }

            var worked = record.Status is StaffAttendanceStatus.Present or StaffAttendanceStatus.Late or StaffAttendanceStatus.OnDuty
                or StaffAttendanceStatus.HalfDay;
            if (leave.HalfDay && record.Status is StaffAttendanceStatus.Present or StaffAttendanceStatus.Late)
            {
                record.Set(StaffAttendanceStatus.HalfDay, record.CheckIn, record.CheckOut, record.Remark, leave.Id);
            }
            else if (!worked)
            {
                record.Set(target, null, null, record.Remark, leave.Id);
            }
        }
    }

    internal async Task<(DateOnly Start, DateOnly End)> LeaveYearAsync(DateOnly on, CancellationToken ct)
    {
        var year = await db.AcademicYears.Where(y => y.StartDate <= on && y.EndDate >= on)
            .Select(y => new { y.StartDate, y.EndDate }).FirstOrDefaultAsync(ct);
        return year is null ? (new DateOnly(on.Year, 1, 1), new DateOnly(on.Year, 12, 31)) : (year.StartDate, year.EndDate);
    }

    private async Task<StaffMember> LoadStaffAsync(Guid staffId, EffectiveAccess current, CancellationToken ct)
    {
        var staff = await db.Staff.SingleOrDefaultAsync(s => s.Id == staffId, ct);
        if (staff is null || (staff.UserId != current.UserId && !current.CanAccessCampus(staff.CampusId))) throw new NotFoundException("Staff member", staffId);
        return staff;
    }

    internal async Task<List<StaffLeaveDto>> DtosAsync(IQueryable<StaffLeaveRequest> query, CancellationToken ct)
    {
        var current = await access.GetAsync(ct);
        var rows = await query.Select(l => new
        {
            l.Id, l.StaffId, l.LeaveTypeId, l.FromDate, l.ToDate, l.HalfDay, l.Days, l.Reason, l.Status, l.RequestedBySelf, l.CreatedAt,
            l.CreatedBy, l.DecidedBy, l.DecidedAt, l.DecisionNote,
            Staff = db.Staff.Where(s => s.Id == l.StaffId).Select(s => new
            {
                s.FullName, s.EmployeeNumber, s.UserId,
                Designation = db.Designations.Where(d => d.Id == s.DesignationId).Select(d => d.Name).FirstOrDefault(),
            }).First(),
            Type = db.LeaveTypes.Where(t => t.Id == l.LeaveTypeId).Select(t => new { t.Code, t.Name }).First(),
        }).ToListAsync(ct);
        var names = await registers.UserNamesAsync(rows.SelectMany(r => new[] { r.CreatedBy, r.DecidedBy }), ct);
        var approver = current.Has(Permissions.Hr.ApproveLeave);
        var today = await calendar.TodayAsync(ct);
        return rows.Select(r =>
        {
            var own = r.Staff.UserId == current.UserId;
            // A pending request is withdrawn by its owner (approvers reject it instead); approved leave is cancelled by approvers.
            var canCancel = r.Status == LeaveStatus.Pending ? own : r.Status == LeaveStatus.Approved && approver && r.ToDate >= today.AddDays(-60);
            return new StaffLeaveDto(r.Id, r.StaffId, r.Staff.FullName, r.Staff.EmployeeNumber, r.Staff.Designation, r.LeaveTypeId, r.Type.Code,
                r.Type.Name, r.FromDate, r.ToDate, r.HalfDay, r.Days, r.Reason, r.Status, r.RequestedBySelf, r.CreatedAt,
                RegisterService.Name(names, r.CreatedBy), RegisterService.Name(names, r.DecidedBy), r.DecidedAt, r.DecisionNote,
                approver && !own && r.Status == LeaveStatus.Pending, canCancel);
        }).ToList();
    }
}
