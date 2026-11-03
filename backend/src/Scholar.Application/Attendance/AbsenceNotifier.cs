using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Communication;
using Scholar.Domain.Attendance;
using Scholar.Domain.Communication;
using Scholar.Domain.Students;

namespace Scholar.Application.Attendance;

/// <summary>
/// Turns a same-day roll call into guardian alerts: one message per guardian, per channel they accept, per student and
/// day (the outbox's dedup key makes "once" a database guarantee). Corrected absences cancel alerts that have not
/// left the school. Registers for earlier days never alert ("absent today" would be false).
/// </summary>
public sealed class AbsenceNotifier(IAppDbContext db, AttendanceCalendar calendar, MessageDispatcher dispatcher)
{
    public const string AbsentPurpose = "attendance.absent";
    public const string LatePurpose = "attendance.late";

    /// <summary>
    /// Adds and cancels messages for the register's changes (within the caller's transaction, before SaveChanges).
    /// Returns the messages to dispatch once the transaction has committed.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> PrepareAsync(AttendanceRegister register, IReadOnlyDictionary<Guid, AttendanceStatus?> previous,
        CancellationToken ct)
    {
        if (!register.IsDaily) return [];
        var settings = (await calendar.SettingsAsync(ct)).Attendance;
        bool Alerts(AttendanceStatus s) => (s == AttendanceStatus.Absent && settings.NotifyGuardiansOnAbsence)
                                           || (s == AttendanceStatus.Late && settings.NotifyGuardiansOnLate);

        var changed = register.Records.Where(r => !previous.TryGetValue(r.StudentId, out var before) || before != r.Status).ToList();
        if (changed.Count == 0) return [];
        var now = calendar.Now;
        var changedIds = changed.Select(r => r.StudentId).ToList();
        var existing = await db.OutboundMessages
            .Where(m => m.RelatedId == register.Id && m.StudentId != null && changedIds.Contains(m.StudentId.Value))
            .ToListAsync(ct);

        // A mark that no longer warrants an alert (or now warrants a different one) stops anything not yet sent.
        foreach (var record in changed)
        {
            var purpose = PurposeFor(record.Status);
            foreach (var message in existing.Where(m => m.StudentId == record.StudentId && m.Purpose != purpose))
            {
                message.Cancel($"Attendance changed to {Describe(record.Status)} before the message was sent.", now);
            }
        }

        if (register.Date != await calendar.TodayAsync(ct)) return [];
        var alerting = changed.Where(r => Alerts(r.Status)).ToList();
        if (alerting.Count == 0) return [];

        var studentIds = alerting.Select(r => r.StudentId).ToList();
        var students = await db.Students.Where(s => studentIds.Contains(s.Id) && s.Status == StudentStatus.Active)
            .Select(s => new { s.Id, s.FirstName, s.LastName }).ToDictionaryAsync(s => s.Id, ct);
        var guardians = await db.StudentGuardians
            .Where(l => studentIds.Contains(l.StudentId) && l.ReceivesNotifications)
            .Select(l => new
            {
                l.StudentId, l.GuardianId, l.Guardian.FullName, l.Guardian.Phone, l.Guardian.Email,
                l.Guardian.NotifyBySms, l.Guardian.NotifyByWhatsApp, l.Guardian.NotifyByEmail,
            })
            .ToListAsync(ct);
        var school = await db.Schools.Where(s => s.Id == register.SchoolId).Select(s => new { Name = s.ShortName ?? s.Name, s.Phone }).SingleAsync(ct);
        var className = await db.Sections.Where(s => s.Id == register.SectionId).Select(s => s.Grade.Name + " – " + s.Name).SingleAsync(ct);
        var keys = guardians.SelectMany(g => Channels(g.NotifyBySms, g.NotifyByWhatsApp, g.NotifyByEmail && g.Email != null)
                .Select(c => Key(register.Date, g.StudentId, g.GuardianId, c)))
            .ToList();
        var byKey = await db.OutboundMessages.Where(m => keys.Contains(m.DedupKey)).ToDictionaryAsync(m => m.DedupKey, ct);

        var created = new List<Guid>();
        foreach (var record in alerting)
        {
            if (!students.TryGetValue(record.StudentId, out var student)) continue; // suspended/inactive: no routine alerts
            var purpose = PurposeFor(record.Status)!;
            var body = Body(school.Name, school.Phone, $"{student.FirstName} {student.LastName}".Trim(), className, register.Date, record);
            foreach (var guardian in guardians.Where(g => g.StudentId == record.StudentId))
            {
                foreach (var channel in Channels(guardian.NotifyBySms, guardian.NotifyByWhatsApp, guardian.NotifyByEmail && guardian.Email != null))
                {
                    var key = Key(register.Date, record.StudentId, guardian.GuardianId, channel);
                    if (byKey.TryGetValue(key, out var earlier))
                    {
                        // Same student, guardian, channel and day: at most one message, ever. A cancelled one comes back.
                        if (earlier.Purpose == purpose && earlier.Reopen(body, now)) created.Add(earlier.Id);
                        continue;
                    }

                    var message = new OutboundMessage(register.SchoolId, channel, channel == MessageChannel.Email ? guardian.Email! : guardian.Phone,
                        guardian.FullName, purpose, body, key)
                    {
                        Subject = channel == MessageChannel.Email ? $"{student.FirstName} was {Describe(record.Status)} today" : null,
                        GuardianId = guardian.GuardianId,
                        StudentId = record.StudentId,
                        RelatedId = register.Id,
                    };
                    db.OutboundMessages.Add(message);
                    byKey[key] = message;
                    created.Add(message.Id);
                }
            }
        }

        return created;
    }

    /// <summary>Runs after commit; failures leave messages pending for the outbox sweeper.</summary>
    public async Task DispatchAsync(IReadOnlyList<Guid> messageIds, CancellationToken ct)
    {
        try
        {
            await dispatcher.DispatchAsync(messageIds, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The register is saved; the sweeper retries pending messages within a minute.
        }
    }

    public static async Task<MessageCountsDto> CountsAsync(IQueryable<OutboundMessage> messages, MessageDispatcher dispatcher, CancellationToken ct)
    {
        var counts = await messages.GroupBy(m => m.Status).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        int C(MessageStatus s) => counts.GetValueOrDefault(s);
        string? reason = null;
        if (C(MessageStatus.NotSent) > 0)
        {
            reason = await messages.Where(m => m.Status == MessageStatus.NotSent).Select(m => m.StatusDetail).FirstOrDefaultAsync(ct);
        }

        return new MessageCountsDto(counts.Values.Sum(), C(MessageStatus.Pending), C(MessageStatus.NotSent), C(MessageStatus.Queued),
            C(MessageStatus.Sent), C(MessageStatus.Delivered), C(MessageStatus.Failed), C(MessageStatus.Cancelled), reason);
    }

    private static string? PurposeFor(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Absent => AbsentPurpose,
        AttendanceStatus.Late => LatePurpose,
        _ => null,
    };

    private static IEnumerable<MessageChannel> Channels(bool sms, bool whatsApp, bool email)
    {
        if (sms) yield return MessageChannel.Sms;
        if (whatsApp) yield return MessageChannel.WhatsApp;
        if (email) yield return MessageChannel.Email;
    }

    private static string Key(DateOnly date, Guid studentId, Guid guardianId, MessageChannel channel) =>
        $"attendance:{date:yyyyMMdd}:{studentId:N}:{guardianId:N}:{channel}".ToLowerInvariant();

    private static string Describe(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Absent => "absent",
        AttendanceStatus.Late => "late",
        AttendanceStatus.Leave => "on leave",
        _ => "present",
    };

    private static string Body(string school, string? phone, string student, string className, DateOnly date, AttendanceRecord record)
    {
        var day = date.ToString("ddd d MMM", CultureInfo.InvariantCulture);
        var contact = string.IsNullOrWhiteSpace(phone) ? "please contact the school office" : $"please call the school office on {phone}";
        return record.Status == AttendanceStatus.Late
            ? $"{school}: {student} ({className}) arrived late today, {day}{(record.ArrivedAt is { } at ? ", at " + at.ToString("h:mm tt", CultureInfo.InvariantCulture) : "")}. If you have questions, {contact}."
            : $"{school}: {student} ({className}) was marked absent today, {day}. If this is unexpected, {contact}.";
    }
}
