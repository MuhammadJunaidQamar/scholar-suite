using System.Net;
using Scholar.Application.Attendance;
using Scholar.Application.Common;
using Scholar.Domain.Access;
using Scholar.Domain.Attendance;
using Scholar.IntegrationTests.Infrastructure;

namespace Scholar.IntegrationTests.Attendance;

public sealed class StaffAttendanceTests(ApiFactory factory) : IntegrationTest(factory)
{
    private static object StaffRegister(AttendanceSchool s, DateOnly date, params (Guid Id, string Status)[] marks) => new
    {
        campusId = s.School.CampusId, date,
        marks = marks.Select(m => new { staffId = m.Id, status = m.Status, checkIn = m.Status == "Late" ? "08:35:00" : null }).ToArray(),
    };

    [Fact]
    public async Task The_office_keeps_the_staff_register_and_staff_correct_their_own_days_with_someone_else_approving()
    {
        var s = await Factory.CreateAttendanceSchoolAsync();
        var (_, hrEmail) = await Factory.AddMemberAsync(s.School, SystemRoles.HrManager, email: "hr@greenfield.test");
        using var hr = await Factory.SignInAsync(hrEmail);
        using var sana = await Factory.SignInAsync(s.SanaEmail);
        using var principal = await Factory.SignInAsync(s.PrincipalEmail);

        var blank = await hr.GetJsonAsync<StaffRegisterDto>($"/api/staff-attendance/register?campusId={s.School.CampusId}");
        Assert.Equal(2, blank.Staff.Count);
        Assert.False(blank.Taken);
        Assert.True(blank.CanRecord);
        Assert.All(blank.Staff, r => Assert.Equal(StaffAttendanceStatus.Present, r.Status));

        var saved = await (await hr.SendAsync(HttpMethod.Put, "/api/staff-attendance/register",
            StaffRegister(s, s.Today, (s.SanaStaffId, "Late"), (s.OmarStaffId, "Absent")))).ReadJsonAsync<StaffRegisterDto>();
        Assert.True(saved.Taken);
        Assert.Equal(new TimeOnly(8, 35), saved.Staff.Single(r => r.StaffId == s.SanaStaffId).CheckIn);
        Assert.Equal(1, saved.Counts.Absent);

        // Teachers cannot keep the staff register.
        var teacher = await sana.SendAsync(HttpMethod.Put, "/api/staff-attendance/register", StaffRegister(s, s.Today, (s.SanaStaffId, "Present")));
        Assert.Equal(HttpStatusCode.Forbidden, teacher.StatusCode);

        // Sana asks for her late mark to be corrected (she was on exam duty); she cannot approve it herself.
        var request = await (await sana.PostAsync("/api/staff-attendance/corrections", new
        {
            date = s.Today, status = "OnDuty", reason = "Invigilating the board practical at the DHA centre",
        })).ReadJsonAsync<StaffCorrectionDto>();
        Assert.True(request.RequestedBySelf);
        Assert.Equal(StaffAttendanceStatus.Late, request.FromStatus);
        var mine = await sana.GetJsonAsync<PagedResult<StaffCorrectionDto>>("/api/staff-attendance/corrections");
        Assert.Equal(request.Id, Assert.Single(mine.Items).Id);

        var approved = await (await principal.PostAsync($"/api/staff-attendance/corrections/{request.Id}/decision", new { approve = true }))
            .ReadJsonAsync<StaffCorrectionDto>();
        Assert.Equal(CorrectionStatus.Approved, approved.Status);
        var after = await hr.GetJsonAsync<StaffRegisterDto>($"/api/staff-attendance/register?campusId={s.School.CampusId}");
        Assert.Equal(StaffAttendanceStatus.OnDuty, after.Staff.Single(r => r.StaffId == s.SanaStaffId).Status);

        // The HR manager's own correction would need someone else too.
        var (_, otherHr) = await Factory.AddMemberAsync(s.School, SystemRoles.HrManager);
        var hrStaff = await (await hr.PostAsync("/api/staff", new
        {
            firstName = "Hina", lastName = "Rauf", gender = "Female", campusId = s.School.CampusId, staffType = "Administrative",
            employmentType = "Permanent", joinDate = s.Today.AddYears(-1),
        })).ReadJsonAsync<Application.Staffing.StaffDetailDto>();
        await Factory.WithDbAsync(s.School.SchoolId, async db =>
        {
            var row = db.Staff.Single(x => x.Id == hrStaff.Id);
            row.UserId = db.Users.Single(u => u.Email == hrEmail).Id;
            return await db.SaveChangesAsync();
        });
        var own = await (await hr.PostAsync("/api/staff-attendance/corrections", new { date = s.Today, status = "Present", reason = "I was here" }))
            .ReadJsonAsync<StaffCorrectionDto>();
        var selfDecision = await hr.PostAsync($"/api/staff-attendance/corrections/{own.Id}/decision", new { approve = true });
        Assert.Equal("correction.own", await selfDecision.ProblemCodeAsync());
        using var second = await Factory.SignInAsync(otherHr);
        await (await second.PostAsync($"/api/staff-attendance/corrections/{own.Id}/decision", new { approve = false, note = "Not on the gate log" }))
            .EnsureSuccessWithBodyAsync();

        var month = await hr.GetJsonAsync<StaffMonthDto>($"/api/staff-attendance/month?campusId={s.School.CampusId}&year={s.Today.Year}&month={s.Today.Month}");
        Assert.Equal(StaffAttendanceStatus.Absent, month.Staff.Single(r => r.StaffId == s.OmarStaffId).Marks[s.Today.Day - 1]);
    }

    [Fact]
    public async Task Staff_request_leave_within_their_allowance_and_approval_marks_the_register()
    {
        var s = await Factory.CreateAttendanceSchoolAsync();
        using var sana = await Factory.SignInAsync(s.SanaEmail);
        using var principal = await Factory.SignInAsync(s.PrincipalEmail);
        var types = await sana.GetJsonAsync<List<LeaveTypeDto>>("/api/staff-leave/types");
        var casual = types.Single(t => t.Code == "CL");
        Assert.Equal(10m, casual.AnnualDays);
        Assert.Contains(types, t => t.Code == "UL" && t.AnnualDays is null);

        var tooLong = await sana.PostAsync("/api/staff-leave", new
        {
            leaveTypeId = casual.Id, fromDate = s.Today.AddDays(5), toDate = s.Today.AddDays(15), halfDay = false, reason = "Family wedding in Swat",
        });
        Assert.Equal("leave.exceeds_balance", await tooLong.ProblemCodeAsync());

        var selfApprove = await sana.PostAsync("/api/staff-leave", new
        {
            leaveTypeId = casual.Id, fromDate = s.Today.AddDays(-1), toDate = s.Today, halfDay = false, reason = "Fever", approve = true,
        });
        Assert.Equal("leave.own", await selfApprove.ProblemCodeAsync());

        var request = await (await sana.PostAsync("/api/staff-leave", new
        {
            leaveTypeId = casual.Id, fromDate = s.Today.AddDays(-1), toDate = s.Today, halfDay = false, reason = "Fever",
        })).ReadJsonAsync<StaffLeaveDto>();
        Assert.Equal(2m, request.Days);
        Assert.Equal(LeaveStatus.Pending, request.Status);
        var balance = await sana.GetJsonAsync<List<LeaveBalanceDto>>($"/api/staff-leave/balances?staffId={s.SanaStaffId}");
        Assert.Equal(8m, balance.Single(b => b.Code == "CL").Remaining);

        // Others' leave is private to HR and approvers.
        using var omar = await Factory.SignInAsync(s.OmarEmail);
        Assert.Equal(0, (await omar.GetJsonAsync<PagedResult<StaffLeaveDto>>("/api/staff-leave")).Total);
        Assert.Equal(HttpStatusCode.NotFound, (await omar.GetAsync($"/api/staff-leave/{request.Id}")).StatusCode);

        var approved = await (await principal.PostAsync($"/api/staff-leave/{request.Id}/decision", new { approve = true })).ReadJsonAsync<StaffLeaveDto>();
        Assert.Equal(LeaveStatus.Approved, approved.Status);
        var register = await principal.GetJsonAsync<StaffRegisterDto>($"/api/staff-attendance/register?campusId={s.School.CampusId}");
        var row = register.Staff.Single(r => r.StaffId == s.SanaStaffId);
        Assert.Equal(StaffAttendanceStatus.OnLeave, row.Status);
        Assert.True(row.Saved);
        Assert.Equal("leave", row.Hint?.Kind);

        var mine = await sana.GetJsonAsync<MyAttendanceDto>("/api/teach/my-attendance");
        Assert.Equal(StaffAttendanceStatus.OnLeave, mine.Marks[s.Today.Day - 1]);
        Assert.Equal(2m, mine.Balances.Single(b => b.Code == "CL").Taken);
    }
}
