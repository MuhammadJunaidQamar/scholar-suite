using Scholar.Domain.Common;

namespace Scholar.Domain.Auditing;

/// <summary>Append-only record of a change or a security-relevant event.</summary>
public sealed class AuditLog : Entity
{
    private AuditLog() { }

    public AuditLog(Guid? schoolId, Guid? userId, string action, string entityType, string? entityId, string? changes,
        DateTimeOffset occurredAt, string? ipAddress, string? userAgent)
    {
        SchoolId = schoolId;
        UserId = userId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Changes = changes;
        OccurredAt = occurredAt;
        IpAddress = ipAddress;
        UserAgent = userAgent is { Length: > 256 } ? userAgent[..256] : userAgent;
    }

    public Guid? SchoolId { get; private set; }
    public Guid? UserId { get; private set; }
    /// <summary>Created / Updated / Deleted, or a named business event such as <c>auth.login_failed</c>.</summary>
    public string Action { get; private set; } = default!;
    public string EntityType { get; private set; } = default!;
    public string? EntityId { get; private set; }
    /// <summary>JSON object of changed properties: <c>{ "name": { "from": "A", "to": "B" } }</c>.</summary>
    public string? Changes { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
}
