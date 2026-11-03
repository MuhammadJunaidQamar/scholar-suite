using P = Scholar.Domain.Access.Permissions;

namespace Scholar.Domain.Access;

public sealed record SystemRoleDefinition(string Key, string Name, string Description, Portal Portal, bool Locked,
    IReadOnlyList<string> Permissions);

/// <summary>Roles created for every new school. Schools may edit unlocked ones or add their own.</summary>
public static class SystemRoles
{
    public const string Owner = "school_owner";
    public const string Administrator = "school_admin";
    public const string Principal = "principal";
    public const string VicePrincipal = "vice_principal";
    public const string AdmissionsOfficer = "admissions_officer";
    public const string Registrar = "registrar";
    public const string Accountant = "accountant";
    public const string HrManager = "hr_manager";
    public const string Teacher = "teacher";
    public const string ClassTeacher = "class_teacher";
    public const string Librarian = "librarian";
    public const string TransportManager = "transport_manager";
    public const string Nurse = "nurse";
    public const string Guardian = "guardian";
    public const string Student = "student";

    private static readonly string[] Everything = [.. PermissionCatalog.Keys];

    private static string[] AllExcept(params string[] excluded) =>
        Everything.Where(p => !excluded.Contains(p) && !p.StartsWith("portal.", StringComparison.Ordinal)).ToArray();

    private static readonly string[] AcademicLeadership =
    [
        P.School.View, P.Academics.View, P.Academics.Manage, P.Students.View, P.Students.Update, P.Students.Export,
        P.Students.ViewDocuments, P.Guardians.View, P.Admissions.View, P.Attendance.View, P.Attendance.Manage,
        P.Attendance.Approve, P.Attendance.Export, P.Attendance.ManageLeave, P.Staff.View, P.Staff.ViewAttendance,
        P.Staff.RecordAttendance, P.Hr.ViewLeave, P.Timetable.View, P.Timetable.Manage, P.Assignments.View,
        P.Assignments.ManageAll, P.LessonPlans.View, P.LessonPlans.Approve, P.Resources.Manage, P.Promotions.Manage,
        P.Exams.View, P.Exams.Manage, P.Exams.ManageMarks, P.Exams.ApproveResults, P.Communication.ViewAnnouncements,
        P.Communication.ManageAnnouncements, P.Communication.SendMessages, P.Welfare.ViewDiscipline,
        P.Welfare.ManageDiscipline, P.Events.View, P.Events.Manage, P.Facilities.Book, P.Facilities.Manage,
        P.Documents.IssueCertificates, P.Reports.View, P.Fees.View, P.Fees.Reports, P.Library.View, P.Transport.View,
        P.Portals.Teacher,
    ];

    private static readonly string[] TeacherBase =
    [
        P.Portals.Teacher, P.Academics.View, P.Attendance.Record, P.Attendance.View, P.Timetable.View,
        P.Assignments.View, P.Assignments.Manage, P.LessonPlans.View, P.LessonPlans.Manage, P.Resources.Manage,
        P.Exams.View, P.Exams.EnterMarks, P.Communication.ViewAnnouncements, P.Communication.SendMessages,
        P.Events.View, P.Facilities.Book, P.Library.View,
    ];

    public static IReadOnlyList<SystemRoleDefinition> All { get; } =
    [
        new(Owner, "School Owner", "Full, permanent access to everything in the school.", Portal.Admin, true, Everything),
        new(Administrator, "School Administrator", "Runs day-to-day administration across all modules.", Portal.Admin, false,
            AllExcept(P.Payroll.Approve, P.Welfare.ViewCounseling, P.Welfare.ManageCounseling)),
        new(Principal, "Principal", "Academic and operational head of the school.", Portal.Admin, false,
            [.. AcademicLeadership, P.Exams.PublishResults, P.Hr.ApproveLeave, P.Hr.Performance, P.Staff.Update,
             P.Staff.ApproveAttendance,
             P.Payroll.View, P.Payroll.Approve, P.Fees.ApproveConcession, P.Fees.ApproveAdjustment, P.Welfare.ViewCounseling,
             P.Admissions.Review, P.Admissions.Decide, P.Audit.View, P.Users.View]),
        new(VicePrincipal, "Vice Principal", "Supports the principal on academics, attendance and discipline.", Portal.Admin,
            false, AcademicLeadership),
        new(AdmissionsOfficer, "Admissions Officer", "Handles inquiries, applications, tests and enrollment.", Portal.Admin,
            false,
            [P.School.View, P.Academics.View, P.Admissions.View, P.Admissions.Create, P.Admissions.Update,
             P.Admissions.Review, P.Admissions.Enroll, P.Admissions.Export, P.Students.View, P.Students.Create,
             P.Guardians.View, P.Guardians.Create, P.Guardians.Update, P.Fees.View, P.Fees.Collect,
             P.Communication.ViewAnnouncements, P.Communication.SendMessages, P.Events.View, P.Reports.View]),
        new(Registrar, "Registrar", "Maintains student records, transfers and certificates.", Portal.Admin, false,
            [P.School.View, P.Academics.View, P.Students.View, P.Students.Create, P.Students.Update, P.Students.Import,
             P.Students.Export, P.Students.ManageStatus, P.Students.ViewDocuments, P.Students.ManageDocuments,
             P.Guardians.View, P.Guardians.Create, P.Guardians.Update, P.Guardians.ManagePortal, P.Admissions.View,
             P.Attendance.View, P.Exams.View, P.Documents.IssueCertificates, P.Documents.ManageTemplates,
             P.Communication.ViewAnnouncements, P.Events.View, P.Reports.View]),
        new(Accountant, "Accountant", "Runs billing, collections, refunds and financial reporting.", Portal.Admin, false,
            [P.School.View, P.Academics.View, P.Students.View, P.Guardians.View, P.Fees.View, P.Fees.Configure,
             P.Fees.Generate, P.Fees.Cancel, P.Fees.Collect, P.Fees.Reverse, P.Fees.RequestConcession,
             P.Fees.RequestAdjustment, P.Fees.RequestRefund, P.Fees.ApproveRefund, P.Fees.Reports, P.Fees.Export,
             P.Finance.ViewLedger, P.Finance.ManageAccounts, P.Finance.CloseRegister, P.Payroll.View,
             P.Communication.ViewAnnouncements, P.Communication.SendMessages, P.Reports.View, P.Inventory.View]),
        new(HrManager, "HR Manager", "Manages staff records, leave, recruitment and payroll.", Portal.Admin, false,
            [P.School.View, P.Academics.View, P.Staff.View, P.Staff.Create, P.Staff.Update, P.Staff.Export,
             P.Staff.ManageDocuments, P.Staff.ViewBank, P.Staff.ViewAttendance, P.Staff.RecordAttendance,
             P.Staff.ApproveAttendance, P.Hr.ViewLeave,
             P.Hr.ApproveLeave, P.Hr.Recruitment, P.Hr.Performance, P.Hr.Configure, P.Payroll.View, P.Payroll.Configure,
             P.Payroll.Process, P.Users.View, P.Communication.ViewAnnouncements, P.Reports.View]),
        new(Teacher, "Teacher", "Teaches assigned sections and subjects.", Portal.Teacher, false, TeacherBase),
        new(ClassTeacher, "Class Teacher", "Teacher responsible for a section's register and pastoral care.", Portal.Teacher,
            false, [.. TeacherBase, P.Attendance.ManageLeave, P.Students.View, P.Guardians.View]),
        new(Librarian, "Librarian", "Runs the library catalogue and circulation.", Portal.Admin, false,
            [P.Library.View, P.Library.Manage, P.Library.Circulate, P.Students.View, P.Staff.View,
             P.Communication.ViewAnnouncements, P.Reports.View]),
        new(TransportManager, "Transport Manager", "Runs fleet, routes and student transport.", Portal.Admin, false,
            [P.Transport.View, P.Transport.Manage, P.Students.View, P.Guardians.View, P.Staff.View,
             P.Communication.ViewAnnouncements, P.Communication.SendMessages, P.Reports.View]),
        new(Nurse, "Nurse / Medical Officer", "Keeps health records and clinic visits.", Portal.Admin, false,
            [P.Students.View, P.Students.ViewMedical, P.Students.ManageMedical, P.Welfare.ViewMedical,
             P.Welfare.ManageMedical, P.Guardians.View, P.Communication.ViewAnnouncements]),
        new(Guardian, "Parent / Guardian", "Family portal access to linked children.", Portal.Family, false,
            [P.Portals.Family]),
        new(Student, "Student", "Student portal access.", Portal.Student, false, [P.Portals.Student]),
    ];

    public static SystemRoleDefinition Get(string key) => All.Single(r => r.Key == key);
}
