using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Scholar.Application.Abstractions;
using Scholar.Domain.Access;
using Scholar.Domain.Admissions;
using Scholar.Domain.Attendance;
using Scholar.Domain.Communication;
using Scholar.Domain.Documents;
using Scholar.Domain.Auditing;
using Scholar.Domain.Common;
using Scholar.Domain.Identity;
using Scholar.Domain.Organization;
using Scholar.Domain.Platform;
using Scholar.Domain.Staffing;
using Scholar.Domain.Students;

namespace Scholar.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant)
    : IdentityUserContext<User, Guid>(options), IAppDbContext, IDataProtectionKeyContext
{
    public const string TenantFilter = "Tenant";
    public const string SoftDeleteFilter = "SoftDelete";

    /// <summary>Referenced by the tenant query filter; EF parameterises it per context instance.</summary>
    private Guid? TenantId => tenant.SchoolId;

    public DbSet<School> Schools => Set<School>();
    public DbSet<Campus> Campuses => Set<Campus>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<NumberSequenceCounter> NumberSequenceCounters => Set<NumberSequenceCounter>();
    public DbSet<SchoolMembership> SchoolMemberships => Set<SchoolMembership>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<AcademicTerm> AcademicTerms => Set<AcademicTerm>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<SubjectGroup> SubjectGroups => Set<SubjectGroup>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<GradeSubject> GradeSubjects => Set<GradeSubject>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<GradingScale> GradingScales => Set<GradingScale>();
    public DbSet<GradingBand> GradingBands => Set<GradingBand>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<StudentStatusChange> StudentStatusChanges => Set<StudentStatusChange>();
    public DbSet<StudentMedicalProfile> StudentMedicalProfiles => Set<StudentMedicalProfile>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<AdmissionInquiry> AdmissionInquiries => Set<AdmissionInquiry>();
    public DbSet<AdmissionApplication> AdmissionApplications => Set<AdmissionApplication>();
    public DbSet<ApplicationStatusChange> ApplicationStatusChanges => Set<ApplicationStatusChange>();
    public DbSet<ApplicationAssessment> ApplicationAssessments => Set<ApplicationAssessment>();
    public DbSet<StaffMember> Staff => Set<StaffMember>();
    public DbSet<SectionSubjectTeacher> SectionSubjectTeachers => Set<SectionSubjectTeacher>();
    public DbSet<AttendanceRegister> AttendanceRegisters => Set<AttendanceRegister>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<AttendanceCorrection> AttendanceCorrections => Set<AttendanceCorrection>();
    public DbSet<AttendanceCorrectionItem> AttendanceCorrectionItems => Set<AttendanceCorrectionItem>();
    public DbSet<StudentLeaveRequest> StudentLeaveRequests => Set<StudentLeaveRequest>();
    public DbSet<BellSchedule> BellSchedules => Set<BellSchedule>();
    public DbSet<BellPeriod> BellPeriods => Set<BellPeriod>();
    public DbSet<StaffAttendanceRecord> StaffAttendanceRecords => Set<StaffAttendanceRecord>();
    public DbSet<StaffAttendanceCorrection> StaffAttendanceCorrections => Set<StaffAttendanceCorrection>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<StaffLeaveRequest> StaffLeaveRequests => Set<StaffLeaveRequest>();
    public DbSet<OutboundMessage> OutboundMessages => Set<OutboundMessage>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken ct = default)
    {
        if (Database.CurrentTransaction is not null)
        {
            return await work(ct);
        }

        await using var transaction = await Database.BeginTransactionAsync(isolationLevel, ct);
        try
        {
            var result = await work(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            ChangeTracker.Clear();
            throw;
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums are stored as readable text; money defaults to numeric(18,2).
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("pg_trgm");
        builder.HasPostgresExtension("btree_gist");
        builder.HasPostgresExtension("citext");

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in builder.Model.GetEntityTypes().Where(e => !e.IsOwned() && e.BaseType is null).ToList())
        {
            var clr = entityType.ClrType;
            if (typeof(ITenantEntity).IsAssignableFrom(clr))
            {
                ConfigureTenantMethod.MakeGenericMethod(clr).Invoke(this, [builder]);
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(clr))
            {
                ConfigureSoftDeleteMethod.MakeGenericMethod(clr).Invoke(null, [builder]);
            }

            if (typeof(IConcurrencyVersioned).IsAssignableFrom(clr))
            {
                builder.Entity(clr).Property(nameof(IConcurrencyVersioned.Version))
                    .IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
            }
        }

        // Keys are generated by the application (UUID v7), never by the store. Declaring that is what lets EF treat a
        // new child added to an already-tracked aggregate (a term on a loaded year, a band on a scale) as an insert
        // rather than assuming that a set key means the row already exists.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (entityType.FindPrimaryKey() is { Properties: [{ ClrType: var keyType } key] } && keyType == typeof(Guid))
            {
                key.ValueGenerated = ValueGenerated.Never;
            }
        }

        // Deleting a parent must never silently remove history: restrict unless a configuration says otherwise.
        foreach (var foreignKey in builder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            if (((IConventionForeignKey)foreignKey).GetDeleteBehaviorConfigurationSource() != ConfigurationSource.Explicit)
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }

    private static readonly MethodInfo ConfigureTenantMethod =
        typeof(AppDbContext).GetMethod(nameof(ConfigureTenant), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly MethodInfo ConfigureSoftDeleteMethod =
        typeof(AppDbContext).GetMethod(nameof(ConfigureSoftDelete), BindingFlags.NonPublic | BindingFlags.Static)!;

    private void ConfigureTenant<T>(ModelBuilder builder) where T : class, ITenantEntity
    {
        var entity = builder.Entity<T>();
        entity.HasQueryFilter(TenantFilter, e => e.SchoolId == TenantId);

        // Every tenant row references its school. (EF's FK index convention adds the school_id index
        // unless a composite index already starts with it.)
        if (!entity.Metadata.GetForeignKeys().Any(fk => fk.PrincipalEntityType.ClrType == typeof(School)))
        {
            entity.HasOne<School>().WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    private static void ConfigureSoftDelete<T>(ModelBuilder builder) where T : class, ISoftDeletable
    {
        Expression<Func<T, bool>> notDeleted = e => e.DeletedAt == null;
        builder.Entity<T>().HasQueryFilter(SoftDeleteFilter, notDeleted);
    }
}
