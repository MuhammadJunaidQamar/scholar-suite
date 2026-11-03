using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Scholar.Application.Abstractions;
using Scholar.Application.Common;
using Scholar.Domain.Access;

namespace Scholar.Application.Access;

public sealed record AuditQuery : PageQuery
{
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public Guid? UserId { get; init; }
    /// <summary>Only record changes (Created/Updated/Deleted), leaving out sign-ins and other events.</summary>
    public bool? ChangesOnly { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed record AuditEntryDto(Guid Id, DateTimeOffset OccurredAt, Guid? UserId, string? UserName, string Action,
    string EntityType, string EntityLabel, string? EntityId, string? Changes, string? IpAddress);

/// <summary>Read side of the audit trail. The log itself is written by the persistence interceptor and auth flows.</summary>
public sealed partial class AuditService(IAppDbContext db, IAccessContext access, ITenantContext tenant)
{
    private static readonly string[] ChangeActions = ["Created", "Updated", "Deleted"];

    public async Task<PagedResult<AuditEntryDto>> ListAsync(AuditQuery query, CancellationToken ct)
    {
        await access.RequireAsync(Permissions.Audit.View, ct);
        var schoolId = tenant.SchoolId ?? throw new ForbiddenException();

        var logs = db.AuditLogs.Where(a => a.SchoolId == schoolId)
            .WhereIf(query.EntityType is not null, a => a.EntityType == query.EntityType)
            .WhereIf(query.EntityId is not null, a => a.EntityId == query.EntityId)
            .WhereIf(query.UserId is not null, a => a.UserId == query.UserId)
            .WhereIf(query.ChangesOnly == true, a => ChangeActions.Contains(a.Action))
            .WhereIf(query.From is not null, a => a.OccurredAt >= Utc(query.From!.Value))
            .WhereIf(query.To is not null, a => a.OccurredAt < Utc(query.To!.Value.AddDays(1)));

        var page = await logs.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).ToPagedAsync(query, a => new
        {
            a.Id, a.OccurredAt, a.UserId,
            UserName = db.Users.Where(u => u.Id == a.UserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
            a.Action, a.EntityType, a.EntityId, a.Changes, a.IpAddress,
        }, ct);

        return new PagedResult<AuditEntryDto>(
            page.Items.Select(a => new AuditEntryDto(a.Id, a.OccurredAt, a.UserId, a.UserName, a.Action, a.EntityType,
                Humanize(a.EntityType), a.EntityId, a.Changes, a.IpAddress)).ToList(),
            page.Page, page.PageSize, page.Total);
    }

    private static DateTimeOffset Utc(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    /// <summary>"AcademicYear" → "academic year".</summary>
    internal static string Humanize(string typeName) => WordBoundary().Replace(typeName, " $1").Trim().ToLowerInvariant();

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex WordBoundary();
}
