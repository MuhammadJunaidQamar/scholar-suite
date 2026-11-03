using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Scholar.Domain.Access;
using Scholar.Domain.Admissions;
using Scholar.Domain.Documents;
using Scholar.Domain.Auditing;
using Scholar.Domain.Identity;
using Scholar.Domain.Organization;
using Scholar.Domain.Platform;
using Scholar.Domain.Students;

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
    DbSet<NumberSequenceCounter> NumberSequenceCounters { get; }

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

    // Students & guardians
    DbSet<Student> Students { get; }
    DbSet<Enrollment> Enrollments { get; }
    DbSet<StudentStatusChange> StudentStatusChanges { get; }
    DbSet<StudentMedicalProfile> StudentMedicalProfiles { get; }
    DbSet<EmergencyContact> EmergencyContacts { get; }
    DbSet<Guardian> Guardians { get; }
    DbSet<StudentGuardian> StudentGuardians { get; }
    DbSet<StoredFile> StoredFiles { get; }

    // Admissions
    DbSet<AdmissionInquiry> AdmissionInquiries { get; }
    DbSet<AdmissionApplication> AdmissionApplications { get; }
    DbSet<ApplicationStatusChange> ApplicationStatusChanges { get; }
    DbSet<ApplicationAssessment> ApplicationAssessments { get; }

    DatabaseFacade Database { get; }

    /// <summary>Change-tracking entry, used to apply a client's row version for optimistic concurrency.</summary>
    Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> inside a database transaction and commits when it completes. Nested calls join
    /// the outer transaction. Use for every operation that must be all-or-nothing (money, enrollment, imports).
    /// </summary>
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken ct = default);
}
