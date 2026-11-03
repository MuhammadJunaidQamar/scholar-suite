namespace Scholar.Domain.Common;

/// <summary>Base type for every persisted entity. Identifiers are time-ordered UUID v7 values.</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
}

/// <summary>Entities whose creation and last modification are stamped automatically.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}

/// <summary>Entities owned by a single school (tenant). Reads are filtered and writes are guarded by tenant.</summary>
public interface ITenantEntity
{
    Guid SchoolId { get; set; }
}

/// <summary>Entities that are hidden instead of physically deleted.</summary>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; set; }
    Guid? DeletedBy { get; set; }
}

/// <summary>Marker: field-level changes to this entity are written to the audit log.</summary>
public interface IHasAuditTrail;

/// <summary>
/// Marker for high-volume rows (attendance marks): only changes and deletions are audited, not creation, which the
/// parent record already captures (the register's own audit entry says who took it and when).
/// </summary>
public interface IAuditChangesOnly : IHasAuditTrail;

/// <summary>Entities that use PostgreSQL's <c>xmin</c> system column for optimistic concurrency.</summary>
public interface IConcurrencyVersioned
{
    uint Version { get; set; }
}

public abstract class AuditableEntity : Entity, IAuditable
{
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

public abstract class TenantEntity : AuditableEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
}
