using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Scholar.Application.Abstractions;
using Scholar.Application.Access;
using Scholar.Application.Admissions;
using Scholar.Application.Documents;
using Scholar.Application.Students;
using Scholar.Application.Identity;
using Scholar.Application.Organization;
using Scholar.Application.Platform;

namespace Scholar.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        // Identity & access
        services.AddScoped<AuthService>();
        services.AddScoped<IAccessContext, AccessContext>();
        services.AddScoped<ISchoolModuleGate, SchoolModuleGate>();
        services.AddScoped<MeService>();
        services.AddScoped<RoleService>();
        services.AddScoped<MemberService>();
        services.AddScoped<AuditService>();

        // Organization
        services.AddScoped<ISchoolClock, SchoolClock>();
        services.AddScoped<OverviewService>();
        services.AddScoped<SchoolSettingsService>();
        services.AddScoped<CampusService>();
        services.AddScoped<AcademicYearService>();
        services.AddScoped<GradeService>();
        services.AddScoped<SectionService>();
        services.AddScoped<SubjectService>();
        services.AddScoped<RoomService>();
        services.AddScoped<CalendarService>();
        services.AddScoped<GradingService>();
        services.AddScoped<StaffStructureService>();

        // Students, guardians, documents, admissions
        services.AddScoped<StudentService>();
        services.AddScoped<StudentTransferService>();
        services.AddScoped<GuardianService>();
        services.AddScoped<SearchService>();
        services.AddScoped<FileService>();
        services.AddScoped<AdmissionService>();
        services.AddScoped<PublicAdmissionService>();

        // Platform
        services.AddScoped<SchoolProvisioningService>();
        services.AddScoped<ISchoolProvisioningStep, SystemRolesProvisioningStep>();
        services.AddScoped<ISchoolProvisioningStep, OrganizationDefaultsProvisioningStep>();

        return services;
    }
}
