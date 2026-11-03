using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Scholar.Application.Abstractions;
using Scholar.Domain.Auditing;
using Scholar.Domain.Common;

namespace Scholar.Infrastructure.Persistence;

/// <summary>Raised when code attempts to write another tenant's data. Always a programming error.</summary>
public sealed class TenantViolationException(string message) : InvalidOperationException(message);

/// <summary>
/// Cross-cutting write rules: stamps audit columns, binds new rows to the current tenant, refuses cross-tenant writes,
/// and records field-level history for <see cref="IHasAuditTrail"/> entities.
/// </summary>
public sealed class AuditSaveChangesInterceptor(ICurrentUser currentUser, ITenantContext tenant, TimeProvider clock)
    : SaveChangesInterceptor
{
    private static readonly HashSet<string> IgnoredProperties =
        [nameof(IAuditable.CreatedAt), nameof(IAuditable.CreatedBy), nameof(IAuditable.UpdatedAt), nameof(IAuditable.UpdatedBy),
         nameof(IConcurrencyVersioned.Version)];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var userId = currentUser.UserId;
        var schoolId = tenant.SchoolId;
        var audits = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            if (entry.Entity is ITenantEntity tenantEntity)
            {
                GuardTenant(entry, tenantEntity, schoolId);
            }

            if (entry is { State: EntityState.Added, Entity: Domain.Identity.User { CreatedAt.Ticks: 0 } user })
            {
                user.CreatedAt = now;
            }

            if (entry.Entity is IAuditable auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAt = now;
                    auditable.CreatedBy ??= userId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.UpdatedAt = now;
                    auditable.UpdatedBy = userId;
                    entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
                }
            }

            if (entry.Entity is IHasAuditTrail && !(entry is { Entity: IAuditChangesOnly, State: EntityState.Added })
                && BuildAudit(entry, now, userId) is { } audit)
            {
                audits.Add(audit);
            }
        }

        if (audits.Count > 0)
        {
            context.Set<AuditLog>().AddRange(audits);
        }
    }

    private static void GuardTenant(EntityEntry entry, ITenantEntity entity, Guid? schoolId)
    {
        switch (entry.State)
        {
            case EntityState.Added when entity.SchoolId == Guid.Empty:
                entity.SchoolId = schoolId ?? throw new TenantViolationException(
                    $"Cannot create {entry.Metadata.ClrType.Name} without a school context.");
                break;
            case EntityState.Added when schoolId is not null && entity.SchoolId != schoolId:
                throw new TenantViolationException($"Attempted to create {entry.Metadata.ClrType.Name} for another school.");
            case EntityState.Modified or EntityState.Deleted:
                if (entry.Property(nameof(ITenantEntity.SchoolId)).IsModified)
                {
                    throw new TenantViolationException($"The school of {entry.Metadata.ClrType.Name} cannot be changed.");
                }

                if (schoolId is not null && entity.SchoolId != schoolId)
                {
                    throw new TenantViolationException($"Attempted to change {entry.Metadata.ClrType.Name} of another school.");
                }

                break;
        }
    }

    private AuditLog? BuildAudit(EntityEntry entry, DateTimeOffset now, Guid? userId)
    {
        var changes = new Dictionary<string, object?>();
        var action = entry.State switch
        {
            EntityState.Added => "Created",
            EntityState.Deleted => "Deleted",
            _ => "Updated",
        };

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (IgnoredProperties.Contains(name) || property.Metadata.IsShadowProperty())
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added when property.CurrentValue is not null:
                    changes[Camel(name)] = new { to = Trim(property.CurrentValue) };
                    break;
                case EntityState.Modified when property.IsModified && !Equals(property.OriginalValue, property.CurrentValue):
                    changes[Camel(name)] = new { from = Trim(property.OriginalValue), to = Trim(property.CurrentValue) };
                    break;
            }
        }

        foreach (var complex in entry.ComplexProperties)
        {
            foreach (var property in complex.Properties)
            {
                var name = $"{Camel(complex.Metadata.Name)}.{Camel(property.Metadata.Name)}";
                if (entry.State == EntityState.Added && property.CurrentValue is not null)
                {
                    changes[name] = new { to = Trim(property.CurrentValue) };
                }
                else if (entry.State == EntityState.Modified && property.IsModified &&
                         !Equals(property.OriginalValue, property.CurrentValue))
                {
                    changes[name] = new { from = Trim(property.OriginalValue), to = Trim(property.CurrentValue) };
                }
            }
        }

        if (entry.State == EntityState.Modified && changes.Count == 0)
        {
            return null;
        }

        var schoolId = entry.Entity is ITenantEntity t ? t.SchoolId : tenant.SchoolId;
        var id = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString();
        return new AuditLog(schoolId, userId, action, entry.Metadata.ClrType.Name, id,
            changes.Count == 0 ? null : JsonSerializer.Serialize(changes), now, currentUser.IpAddress, currentUser.UserAgent);
    }

    private static string Camel(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];

    private static object? Trim(object? value) => value switch
    {
        string { Length: > 500 } s => s[..500] + "…",
        Enum e => e.ToString(),
        _ => value,
    };
}
