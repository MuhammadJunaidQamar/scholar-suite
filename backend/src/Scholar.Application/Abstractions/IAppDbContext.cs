using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Scholar.Domain.Access;
using Scholar.Domain.Auditing;
using Scholar.Domain.Identity;
using Scholar.Domain.Organization;
using Scholar.Domain.Platform;

namespace Scholar.Application.Abstractions;

/// <summary>
/// Persistence surface for application services. <see cref="DbSet{TEntity}"/> already is a repository and unit of
/// work, so it is exposed directly rather than wrapped. Tenant filtering is applied automatically.
/// </summary>
public interface IAppDbContext
{
    // Platform
    DbSet<School> Schools { get; }
    DbSet<Campus> Campuses { get; }
    DbSet<NumberSequence> NumberSequences { get; }

    // Identity & access
    DbSet<User> Users { get; }
    DbSet<SchoolMembership> SchoolMemberships { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Role> Roles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<AuditLog> AuditLogs { get; }

    // Organization
    DbSet<AcademicYear> AcademicYears { get; }
    DbSet<AcademicTerm> AcademicTerms { get; }
    DbSet<Grade> Grades { get; }
    DbSet<Section> Sections { get; }
    DbSet<Room> Rooms { get; }
    DbSet<Department> Departments { get; }
    DbSet<Designation> Designations { get; }
    DbSet<SubjectGroup> SubjectGroups { get; }
    DbSet<Subject> Subjects { get; }
    DbSet<GradeSubject> GradeSubjects { get; }
    DbSet<CalendarEvent> CalendarEvents { get; }
    DbSet<GradingScale> GradingScales { get; }
    DbSet<GradingBand> GradingBands { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> inside a database transaction and commits when it completes. Nested calls join
    /// the outer transaction. Use for every operation that must be all-or-nothing (money, enrollment, imports).
    /// </summary>
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken ct = default);
}
