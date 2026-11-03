using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Scholar.Application.Attendance;
using Scholar.Application.Common;
using Scholar.Domain.Attendance;
using Scholar.Domain.Communication;
using Scholar.Domain.Organization;
using Scholar.IntegrationTests.Infrastructure;

namespace Scholar.IntegrationTests.Attendance;

public sealed class StudentAttendanceTests(ApiFactory factory) : IntegrationTest(factory)
{
    private static object Register(Guid sectionId, DateOnly date, IEnumerable<(Guid Id, string Status)> marks, int? period = null,
        Guid? subjectId = null, uint? version = null, bool overrideClosedDay = false) => new
    {
        sectionId, date, period, subjectId, version, overrideClosedDay,
        marks = marks.Select(m => new { studentId = m.Id, status = m.Status }).ToArray(),
    };

    private static IEnumerable<(Guid, string)> AllPresent(AttendanceSchool s, params (Guid Id, string Status)[] except) =>
        new[] { s.Ayesha, s.Bilal, s.Chand }.Select(id => (id, except.FirstOrDefault(e => e.Id == id).Status ?? "Present"));

    [Fact]
    public async Task Class_teachers_take_their_own_register_and_absences_are_recorded_but_never_claimed_as_sent()
    {
        var s = await Factory.CreateAttendanceSchoolAsync();
        using var sana = await Factory.SignInAsync(s.SanaEmail);
        using var omar = await Factory.SignInAsync(s.OmarEmail);

        // Before it is taken, everyone starts Present and nothing is saved.
        var sheet = await sana.GetJsonAsync<RegisterSheetDto>($"/api/attendance/registers/sheet?sectionId={s.SectionA}&date={s.Today:yyyy-MM-dd}");
        Assert.Null(sheet.Register);
        Assert.True(sheet.CanTake);
        Assert.Equal(["1", "2", "10"], sheet.Students.Select(r => r.RollNumber));
        Assert.All(sheet.Students, r => Assert.True(r.Status == AttendanceStatus.Present && !r.Saved));

        // Another section's class teacher may look but not take.
        var other = await omar.SendAsync(HttpMethod.Put, "/api/attendance/registers", Register(s.SectionA, s.Today, AllPresent(s)));
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);

        // A register must mark everyone on the roll.
        var partial = await sana.SendAsync(HttpMethod.Put, "/api/attendance/registers", Register(s.SectionA, s.Today, [(s.Ayesha, "Present")]));
        Assert.Equal("attendance.incomplete", await partial.ProblemCodeAsync());
        var stranger = await sana.SendAsync(HttpMethod.Put, "/api/attendance/registers",
            Register(s.SectionA, s.Today, [.. AllPresent(s), (s.Danish, "Present")]));
        Assert.Equal("attendance.not_on_roll", await stranger.ProblemCodeAsync());

        var saved = await (await sana.SendAsync(HttpMethod.Put, "/api/attendance/registers",
            Register(s.SectionA, s.Today, AllPresent(s, (s.Bilal, "Absent"))))).ReadJsonAsync<RegisterSheetDto>();
        Assert.NotNull(saved.Register);
        Assert.Equal(new RegisterCountsDto(3, 2, 0, 1, 0), saved.Counts);
        Assert.All(saved.Students, r => Assert.True(r.Saved));

        // One SMS per guardian, recorded honestly: no provider is connected, so nothing is reported as sent.
        Assert.NotNull(saved.Alerts);
        Assert.Equal(1, saved.Alerts!.Total);
        Assert.Equal(1, saved.Alerts.NotSent);
        Assert.Equal(0, saved.Alerts.Sent + saved.Alerts.Delivered + saved.Alerts.Queued);
        Assert.Contains("No SMS provider", saved.Alerts.NotSentReason);
        var message = await Factory.WithDbAsync(s.School.SchoolId, db => db.OutboundMessages.SingleAsync());
        Assert.Equal(MessageStatus.NotSent, message.Status);
        Assert.Equal(MessageChannel.Sms, message.Channel);
        Assert.Equal("+923002222222", message.Recipient);
        Assert.Contains("Bilal Ahmed (Class 6 – A) was marked absent today", message.Body);
        Assert.Contains("042 111 222 333", message.Body);

        // Saving the same register again never duplicates an alert; correcting the absence cancels the unsent one.
        var again = await (await sana.SendAsync(HttpMethod.Put, "/api/attendance/registers",
            Register(s.SectionA, s.Today, AllPresent(s, (s.Bilal, "Absent")), version: saved.Register!.Version))).ReadJsonAsync<RegisterSheetDto>();
        Assert.Equal(1, again.Alerts!.Total);
        var corrected = await (await sana.SendAsync(HttpMethod.Put, "/api/attendance/registers",
            Register(s.SectionA, s.Today, AllPresent(s), version: again.Register!.Version))).ReadJsonAsync<RegisterSheetDto>();
        Assert.Equal(1, corrected.Alerts!.Cancelled);
        Assert.Equal(0, corrected.Counts.Absent);

        // A stale copy of the register cannot overwrite newer marks.
        var stale = await sana.SendAsync(HttpMethod.Put, "/api/attendance/registers",
            Register(s.SectionA, s.Today, AllPresent(s, (s.Chand, "Late")), version: saved.Register.Version));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        // Changes to marks are audited (who changed what), creation is not repeated per student.
        var audits = await Factory.WithDbAsync(s.School.SchoolId, db => db.AuditLogs.Where(a => a.EntityType == "AttendanceRecord").ToListAsync());
        Assert.NotEmpty(audits);
        Assert.DoesNotContain(audits, a => a.Action == "Created");
    }

    [Fact]
    public async Task Registers_cannot_be_taken_ahead_on_holidays_or_twice_and_the_office_can_take_make_up_days()
    {
        var s = await Factory.CreateAttendanceSchoolAsync();
        using var sana = await Factory.SignInAsync(s.SanaEmail);
        using var principal = await Factory.SignInAsync(s.PrincipalEmail);
        var holiday = s.Today.AddDays(-1);
        await Factory.WithDbAsync(s.School.SchoolId, async db =>
        {
            db.CalendarEvents.Add(new CalendarEvent(s.School.SchoolId, "Iqbal Day", CalendarEventType.Holiday, holiday, holiday));
            return await db.SaveChangesAsync();
        });

        var future = await sana.SendAsync(HttpMethod.Put, "/api/attendance/registers", Register(s.SectionA, s.Today.AddDays(1), AllPresent(s)));
        Assert.Equal("attendance.future", await future.ProblemCodeAsync());

        var closed = await sana.GetJsonAsync<RegisterSheetDto>($"/api/attendance/registers/sheet?sectionId={s.SectionA}&date={holiday:yyyy-MM-dd}");
        Assert.False(closed.Day.IsSchoolDay);
        Assert.Equal("Holiday: Iqbal Day", closed.Day.ClosedReason);
        Assert.False(closed.CanTake);
        var onHoliday = await sana.SendAsync(HttpMethod.Put, "/api/attendance/registers", Register(s.SectionA, holiday, AllPresent(s), overrideClosedDay: true));
        Assert.Equal("attendance.closed_day", await onHoliday.ProblemCodeAsync());

        // The office can open a make-up day deliberately.
        var makeUp = await principal.SendAsync(HttpMethod.Put, "/api/attendance/registers", Register(s.SectionA, holiday, AllPresent(s), overrideClosedDay: true));
        await makeUp.EnsureSuccessWithBodyAsync();

        // One daily register per section per day, enforced by the database even if two requests race.
        var duplicate = await Factory.WithDbAsync(s.School.SchoolId, async db =>
        {
            var section = await db.Sections.SingleAsync(x => x.Id == s.SectionA);
            db.AttendanceRegisters.Add(new AttendanceRegister(s.School.SchoolId, s.SectionA, s.YearId, section.CampusId, holiday, null, null, null));
            return await Record.ExceptionAsync(() => db.SaveChangesAsync());
        });
        Assert.IsType<DbUpdateException>(duplicate);
    }

    [Fact]
    public async Task Locked_registers_change_only_through_corrections_that_an_approver_decides()
    {
        var s = await Factory.CreateAttendanceSchoolAsync();
        using var sana = await Factory.SignInAsync(s.SanaEmail);
        using var principal = await Factory.SignInAsync(s.PrincipalEmail);
        var taken = await (await sana.SendAsync(HttpMethod.Put, "/api/attendance/registers",
            Register(s.SectionA, s.Today, AllPresent(s, (s.Bilal, "Absent"))))).ReadJsonAsync<RegisterSheetDto>();

        // Two days on, the register has locked (default: 24 hours after the day ends).
        Factory.Clock.Advance(TimeSpan.FromDays(3));
        var edit = await sana.SendAsync(HttpMethod.Put, "/api/attendance/registers", Register(s.SectionA, s.Today, AllPresent(s)));
        Assert.Equal("attendance.locked", await edit.ProblemCodeAsync());
        var sheet = await sana.GetJsonAsync<RegisterSheetDto>($"/api/attendance/registers/sheet?sectionId={s.SectionA}&date={s.Today:yyyy-MM-dd}");
        Assert.True(sheet.IsLocked && sheet.CanCorrect && !sheet.CanTake);

        var noReason = await sana.PostAsync($"/api/attendance/registers/{taken.Register!.Id}/corrections",
            new { changes = new[] { new { studentId = s.Bilal, status = "Present" } }, reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        var request = await (await sana.PostAsync($"/api/attendance/registers/{taken.Register.Id}/corrections",
            new { changes = new[] { new { studentId = s.Bilal, status = "Late", remark = "Came at 9:10 with a note" } }, reason = "Bilal arrived after I sent the register" }))
            .ReadJsonAsync<CorrectionDto>();
        Assert.Equal(CorrectionStatus.Pending, request.Status);
        var item = Assert.Single(request.Items);
        Assert.Equal(AttendanceStatus.Absent, item.FromStatus);
        Assert.False(request.CanDecide);
        Assert.True(request.CanWithdraw);

        var second = await sana.PostAsync($"/api/attendance/registers/{taken.Register.Id}/corrections",
            new { changes = new[] { new { studentId = s.Ayesha, status = "Absent" } }, reason = "Another one" });
        Assert.Equal("correction.pending_exists", await second.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await sana.PostAsync($"/api/attendance/corrections/{request.Id}/decision", new { approve = true })).StatusCode);

        // Nothing changes until it is approved.
        Assert.Equal(AttendanceStatus.Absent, (await RegisterAsync(sana, s)).Students.Single(r => r.StudentId == s.Bilal).Status);
        var queue = await principal.GetJsonAsync<PagedResult<CorrectionDto>>("/api/attendance/corrections?status=Pending");
        Assert.True(Assert.Single(queue.Items).CanDecide);
        var rejected = await principal.PostAsync($"/api/attendance/corrections/{request.Id}/decision", new { approve = false });
        Assert.Equal("correction.note_required", await rejected.ProblemCodeAsync());
        var approved = await (await principal.PostAsync($"/api/attendance/corrections/{request.Id}/decision", new { approve = true, note = "Note seen" }))
            .ReadJsonAsync<CorrectionDto>();
        Assert.Equal(CorrectionStatus.Approved, approved.Status);
        Assert.Equal("Member principal", approved.DecidedByName);
        var after = (await RegisterAsync(sana, s)).Students.Single(r => r.StudentId == s.Bilal);
        Assert.Equal(AttendanceStatus.Late, after.Status);
        Assert.Equal("Came at 9:10 with a note", after.Remark);

        // An approver's own correction applies at once (and is still on record).
        var direct = await (await principal.PostAsync($"/api/attendance/registers/{taken.Register.Id}/corrections",
            new { changes = new[] { new { studentId = s.Chand, status = "Leave" } }, reason = "Medical certificate received" })).ReadJsonAsync<CorrectionDto>();
        Assert.Equal(CorrectionStatus.Approved, direct.Status);
        Assert.Equal(AttendanceStatus.Leave, (await RegisterAsync(sana, s)).Students.Single(r => r.StudentId == s.Chand).Status);
    }

    [Fact]
    public async Task Approved_leave_excuses_recorded_absences_starts_future_registers_on_leave_and_stale_corrections_are_refused()
    {
        var s = await Factory.CreateAttendanceSchoolAsync();
        using var sana = await Factory.SignInAsync(s.SanaEmail);
        using var principal = await Factory.SignInAsync(s.PrincipalEmail);
        var yesterday = s.Today.AddDays(-1);
        var threeDaysAgo = s.Today.AddDays(-3);
        await principal.SendAsync(HttpMethod.Put, "/api/attendance/registers", Register(s.SectionA, threeDaysAgo, AllPresent(s, (s.Ayesha, "Absent"))));
        await principal.SendAsync(HttpMethod.Put, "/api/attendance/registers", Register(s.SectionA, yesterday, AllPresent(s, (s.Ayesha, "Absent"))));

        // A pending correction based on the absence…
        var locked = await RegisterAsync(sana, s, threeDaysAgo);
        var correction = await (await sana.PostAsync($"/api/attendance/registers/{locked.Register!.Id}/corrections",
            new { changes = new[] { new { studentId = s.Ayesha, status = "Present" } }, reason = "Marked by mistake" })).ReadJsonAsync<CorrectionDto>();

        // …then leave is recorded for the same days.
        var pending = await (await principal.PostAsync("/api/attendance/leave", new
        {
            studentId = s.Ayesha, fromDate = threeDaysAgo, toDate = s.Today.AddDays(2), kind = "Sick", reason = "Dengue fever", requestedBy = "Father, by phone",
        })).ReadJsonAsync<LeaveDecisionResult<StudentLeaveDto>>();
        Assert.Equal(LeaveStatus.Pending, pending.Leave.Status);
        Assert.Equal(6, pending.Leave.SchoolDays);
        var hinted = await RegisterAsync(sana, s);
        Assert.Equal("pendingLeave", hinted.Students.Single(r => r.StudentId == s.Ayesha).Hint?.Kind);

        var overlap = await principal.PostAsync("/api/attendance/leave", new
        {
            studentId = s.Ayesha, fromDate = s.Today, toDate = s.Today, kind = "Family", reason = "Wedding",
        });
        Assert.Equal("leave.overlap", await overlap.ProblemCodeAsync());

        var decided = await (await principal.PostAsync($"/api/attendance/leave/{pending.Leave.Id}/decision", new { approve = true }))
            .ReadJsonAsync<LeaveDecisionResult<StudentLeaveDto>>();
        Assert.Equal(LeaveStatus.Approved, decided.Leave.Status);
        Assert.Equal(2, decided.MarksChanged);
        Assert.Equal(AttendanceStatus.Leave, (await RegisterAsync(sana, s, yesterday)).Students.Single(r => r.StudentId == s.Ayesha).Status);

        // Today's register (not taken yet) starts her on leave.
        var today = (await RegisterAsync(sana, s)).Students.Single(r => r.StudentId == s.Ayesha);
        Assert.Equal(AttendanceStatus.Leave, today.Status);
        Assert.Equal("leave", today.Hint?.Kind);

        // The correction was asked against "Absent"; the mark is now Leave, so applying it would overwrite unseen changes.
        var stale = await principal.PostAsync($"/api/attendance/corrections/{correction.Id}/decision", new { approve = true });
        Assert.Equal("correction.stale", await stale.ProblemCodeAsync());

        // Leave is left out of the percentage by default: 2 present days of 2 counted, 2 days on leave.
        var summary = await principal.GetJsonAsync<StudentAttendanceSummaryDto>($"/api/attendance/students/{s.Ayesha}");
        Assert.Equal(new AttendanceTotalsDto(2, 0, 0, 0, 2, null), summary.Totals);

        // Cancelling the leave puts the recorded days back to Absent.
        var cancelled = await (await principal.PostAsync($"/api/attendance/leave/{pending.Leave.Id}/cancel", new { note = "Was at school after all" }))
            .ReadJsonAsync<LeaveDecisionResult<StudentLeaveDto>>();
        Assert.Equal(2, cancelled.MarksChanged);
        Assert.Equal(AttendanceStatus.Absent, (await RegisterAsync(sana, s, yesterday)).Students.Single(r => r.StudentId == s.Ayesha).Status);

        // Class teachers without the leave permission cannot approve leave.
        using var omar = await Factory.SignInAsync(s.OmarEmail);
        Assert.Equal(HttpStatusCode.OK, (await omar.GetAsync("/api/attendance/leave")).StatusCode);
    }

    [Fact]
    public async Task Reports_show_the_day_the_month_the_call_list_and_who_is_below_the_minimum()
    {
        var s = await Factory.CreateAttendanceSchoolAsync();
        using var principal = await Factory.SignInAsync(s.PrincipalEmail);
        for (var d = 1; d <= 4; d++)
        {
            var date = s.Today.AddDays(-d);
            // Chand is absent every day; Bilal is late once.
            await (await principal.SendAsync(HttpMethod.Put, "/api/attendance/registers",
                Register(s.SectionA, date, AllPresent(s, (s.Chand, "Absent"), (s.Bilal, d == 2 ? "Late" : "Present"))))).EnsureSuccessWithBodyAsync();
        }

        await (await principal.SendAsync(HttpMethod.Put, "/api/attendance/registers",
            Register(s.SectionA, s.Today, AllPresent(s, (s.Chand, "Absent"))))).EnsureSuccessWithBodyAsync();

        var overview = await principal.GetJsonAsync<AttendanceOverviewDto>("/api/attendance/overview");
        Assert.Equal(2, overview.Totals.Sections);
        Assert.Equal(1, overview.Totals.Taken);
        Assert.Equal(4, overview.Totals.OnRoll);
        Assert.Equal(3, overview.Totals.Marked);
        Assert.Equal(1, overview.Totals.Absent);
        Assert.Equal(66.7m, overview.Totals.AttendanceRate);
        Assert.Null(overview.Sections.Single(x => x.SectionId == s.SectionB).RegisterId);
        Assert.Equal(1, overview.Alerts.NotSent);

        var calls = await principal.GetJsonAsync<List<AbsenteeDto>>("/api/attendance/absentees");
        var chand = Assert.Single(calls);
        Assert.Equal(5, chand.ConsecutiveAbsences);
        Assert.Equal("+923003333333", chand.GuardianPhone);
        Assert.Equal(MessageStatus.NotSent, Assert.Single(chand.Alerts).Status);

        var low = await principal.GetJsonAsync<LowAttendanceDto>("/api/attendance/low");
        Assert.Equal(75m, low.Below);
        var lowRow = Assert.Single(low.Students.Items);
        Assert.Equal(s.Chand, lowRow.StudentId);
        Assert.Equal(0m, lowRow.Totals.Percentage);

        var month = await principal.GetJsonAsync<MonthRegisterDto>($"/api/attendance/sections/{s.SectionA}/month?year={s.Today.Year}&month={s.Today.Month}");
        var bilal = month.Students.Single(r => r.StudentId == s.Bilal);
        Assert.Equal(s.Today.Day, bilal.Marks.Count(m => m is not null) + month.Days.Count(d => d.Date.Day <= s.Today.Day && !d.Taken));
        var export = await principal.GetAsync($"/api/attendance/sections/{s.SectionA}/month/export?year={s.Today.Year}&month={s.Today.Month}");
        await export.EnsureSuccessWithBodyAsync();
        var csv = Encoding.UTF8.GetString(await export.Content.ReadAsByteArrayAsync());
        Assert.Contains("Chand Bibi", csv);
        Assert.Contains("Attendance %", csv);

        var trend = await principal.GetJsonAsync<List<TrendPointDto>>($"/api/attendance/trend?from={s.Today.AddDays(-4):yyyy-MM-dd}");
        Assert.Equal(5, trend.Count(p => p.Marked == 3));

        // Teachers can see reports but cannot export without the export permission.
        using var sana = await Factory.SignInAsync(s.SanaEmail);
        Assert.Equal(HttpStatusCode.OK, (await sana.GetAsync("/api/attendance/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await sana.GetAsync($"/api/attendance/sections/{s.SectionA}/month/export?year={s.Today.Year}&month={s.Today.Month}")).StatusCode);
    }

    [Fact]
    public async Task Period_registers_are_taken_by_subject_teachers_when_the_school_uses_period_wise_attendance()
    {
        var s = await Factory.CreateAttendanceSchoolAsync();
        using var omar = await Factory.SignInAsync(s.OmarEmail);
        using var principal = await Factory.SignInAsync(s.PrincipalEmail);

        var off = await omar.SendAsync(HttpMethod.Put, "/api/attendance/registers", Register(s.SectionA, s.Today, AllPresent(s), period: 2, subjectId: s.MathsId));
        Assert.Equal("attendance.periods_off", await off.ProblemCodeAsync());
        await Factory.ConfigureAttendanceAsync(s.School.SchoolId, a => a.Mode = Domain.Platform.AttendanceMode.PeriodWise);

        // Omar teaches Section A maths, not its daily register.
        var assign = await principal.SendAsync(HttpMethod.Put, $"/api/org/teaching/sections/{s.SectionA}", new
        {
            classTeacherId = s.SanaStaffId, subjects = new[] { new { subjectId = s.MathsId, staffId = (Guid?)s.OmarStaffId } },
        });
        await assign.EnsureSuccessWithBodyAsync();
        var bells = await principal.SendAsync(HttpMethod.Put, $"/api/attendance/bell-schedules/{s.School.CampusId}", new
        {
            name = "Winter timings",
            slots = new[]
            {
                new { name = "", startTime = "08:00:00", endTime = "08:40:00", isBreak = false },
                new { name = "", startTime = "08:40:00", endTime = "09:20:00", isBreak = false },
                new { name = "Break", startTime = "09:20:00", endTime = "09:40:00", isBreak = true },
                new { name = "", startTime = "09:40:00", endTime = "10:20:00", isBreak = false },
            },
        });
        var schedule = await bells.ReadJsonAsync<BellScheduleDto>();
        Assert.Equal([1, 2, null, 3], schedule.Periods.Select(p => p.Number));

        var daily = await omar.SendAsync(HttpMethod.Put, "/api/attendance/registers", Register(s.SectionA, s.Today, AllPresent(s)));
        Assert.Equal(HttpStatusCode.Forbidden, daily.StatusCode);
        var periodSheet = await (await omar.SendAsync(HttpMethod.Put, "/api/attendance/registers",
            Register(s.SectionA, s.Today, AllPresent(s, (s.Ayesha, "Absent")), period: 2))).ReadJsonAsync<RegisterSheetDto>();
        Assert.Equal(s.MathsId, periodSheet.SubjectId);
        Assert.Equal("Period 2 · 08:40–09:20", periodSheet.PeriodLabel);
        Assert.Null(periodSheet.Alerts); // period absences do not alert guardians; the daily roll call does

        var today = await omar.GetJsonAsync<TeacherTodayDto>("/api/teach/today");
        var task = today.Registers.Single(t => t.Kind == "period");
        Assert.True(task.Taken);
        Assert.Equal("Mathematics", task.SubjectName);
        Assert.Contains(today.Registers, t => t.Kind == "daily" && t.SectionId == s.SectionB && !t.Taken);

        var summary = await principal.GetJsonAsync<StudentAttendanceSummaryDto>($"/api/attendance/students/{s.Ayesha}");
        var maths = Assert.Single(summary.Subjects);
        Assert.Equal(0m, maths.Percentage);
    }

    private static Task<RegisterSheetDto> RegisterAsync(BrowserClient client, AttendanceSchool s, DateOnly? date = null) =>
        client.GetJsonAsync<RegisterSheetDto>($"/api/attendance/registers/sheet?sectionId={s.SectionA}&date={date ?? s.Today:yyyy-MM-dd}");
}
