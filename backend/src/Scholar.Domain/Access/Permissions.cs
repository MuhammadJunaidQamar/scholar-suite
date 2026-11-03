namespace Scholar.Domain.Access;

/// <summary>
/// The authoritative permission catalogue. Keys follow <c>module.action</c> or <c>module.resource.action</c>.
/// Keys are persisted in <c>role_permissions</c>: never rename one without a data migration.
/// "Own" variants (e.g. <see cref="Attendance.Record"/>) are limited to the caller's assigned classes;
/// "Manage"/"any" variants apply across the caller's campus scope.
/// </summary>
public static class Permissions
{
    public static class School
    {
        public const string View = "school.view";
        public const string Manage = "school.manage";
    }

    public static class Academics
    {
        public const string View = "academics.view";
        public const string Manage = "academics.manage";
    }

    public static class Users
    {
        public const string View = "access.users.view";
        public const string Manage = "access.users.manage";
    }

    public static class Roles
    {
        public const string View = "access.roles.view";
        public const string Manage = "access.roles.manage";
    }

    public static class Audit
    {
        public const string View = "audit.view";
    }

    public static class Students
    {
        public const string View = "students.view";
        public const string Create = "students.create";
        public const string Update = "students.update";
        public const string Delete = "students.delete";
        public const string Import = "students.import";
        public const string Export = "students.export";
        public const string ManageStatus = "students.status";
        public const string ViewDocuments = "students.documents.view";
        public const string ManageDocuments = "students.documents.manage";
        public const string ViewMedical = "students.medical.view";
        public const string ManageMedical = "students.medical.manage";
    }

    public static class Guardians
    {
        public const string View = "guardians.view";
        public const string Create = "guardians.create";
        public const string Update = "guardians.update";
        public const string Delete = "guardians.delete";
        public const string ManagePortal = "guardians.portal";
    }

    public static class Admissions
    {
        public const string View = "admissions.view";
        public const string Create = "admissions.create";
        public const string Update = "admissions.update";
        public const string Review = "admissions.review";
        public const string Decide = "admissions.decide";
        public const string Enroll = "admissions.enroll";
        public const string Export = "admissions.export";
    }

    public static class Attendance
    {
        public const string View = "attendance.view";
        public const string Record = "attendance.record";
        public const string Manage = "attendance.manage";
        public const string Approve = "attendance.approve";
        public const string Export = "attendance.export";
        public const string ManageLeave = "attendance.leave";
    }

    public static class Staff
    {
        public const string View = "staff.view";
        public const string Create = "staff.create";
        public const string Update = "staff.update";
        public const string Delete = "staff.delete";
        public const string Export = "staff.export";
        public const string ManageDocuments = "staff.documents";
        public const string ViewBank = "staff.bank.view";
        public const string ViewAttendance = "staff.attendance.view";
        public const string RecordAttendance = "staff.attendance.record";
        public const string ApproveAttendance = "staff.attendance.approve";
    }

    public static class Hr
    {
        public const string ViewLeave = "hr.leave.view";
        public const string ApproveLeave = "hr.leave.approve";
        public const string Recruitment = "hr.recruitment";
        public const string Performance = "hr.performance";
        public const string Configure = "hr.configure";
    }

    public static class Payroll
    {
        public const string View = "payroll.view";
        public const string Configure = "payroll.configure";
        public const string Process = "payroll.process";
        public const string Approve = "payroll.approve";
    }

    public static class Timetable
    {
        public const string View = "timetable.view";
        public const string Manage = "timetable.manage";
    }

    public static class Assignments
    {
        public const string View = "assignments.view";
        public const string Manage = "assignments.manage";
        public const string ManageAll = "assignments.manage_all";
    }

    public static class LessonPlans
    {
        public const string View = "lessonplans.view";
        public const string Manage = "lessonplans.manage";
        public const string Approve = "lessonplans.approve";
    }

    public static class Resources
    {
        public const string Manage = "resources.manage";
    }

    public static class Promotions
    {
        public const string Manage = "promotions.manage";
    }

    public static class Exams
    {
        public const string View = "exams.view";
        public const string Manage = "exams.manage";
        public const string EnterMarks = "exams.marks.enter";
        public const string ManageMarks = "exams.marks.manage";
        public const string ApproveResults = "exams.results.approve";
        public const string PublishResults = "exams.results.publish";
    }

    public static class Fees
    {
        public const string View = "fees.view";
        public const string Configure = "fees.configure";
        public const string Generate = "fees.invoices.generate";
        public const string Cancel = "fees.invoices.cancel";
        public const string Collect = "fees.payments.collect";
        public const string Reverse = "fees.payments.reverse";
        public const string RequestConcession = "fees.concessions.request";
        public const string ApproveConcession = "fees.concessions.approve";
        public const string RequestAdjustment = "fees.adjustments.request";
        public const string ApproveAdjustment = "fees.adjustments.approve";
        public const string RequestRefund = "fees.refunds.request";
        public const string ApproveRefund = "fees.refunds.approve";
        public const string Reports = "fees.reports";
        public const string Export = "fees.export";
    }

    public static class Finance
    {
        public const string ViewLedger = "finance.ledger.view";
        public const string ManageAccounts = "finance.accounts.manage";
        public const string CloseRegister = "finance.registers.close";
    }

    public static class Communication
    {
        public const string ViewAnnouncements = "comms.announcements.view";
        public const string ManageAnnouncements = "comms.announcements.manage";
        public const string SendMessages = "comms.messages.send";
        public const string ManageTemplates = "comms.templates.manage";
        public const string ViewLogs = "comms.logs.view";
    }

    public static class Library
    {
        public const string View = "library.view";
        public const string Manage = "library.manage";
        public const string Circulate = "library.circulate";
    }

    public static class Transport
    {
        public const string View = "transport.view";
        public const string Manage = "transport.manage";
    }

    public static class Inventory
    {
        public const string View = "inventory.view";
        public const string Manage = "inventory.manage";
        public const string Approve = "inventory.approve";
    }

    public static class Welfare
    {
        public const string ViewDiscipline = "welfare.discipline.view";
        public const string ManageDiscipline = "welfare.discipline.manage";
        public const string ViewCounseling = "welfare.counseling.view";
        public const string ManageCounseling = "welfare.counseling.manage";
        public const string ViewMedical = "welfare.medical.view";
        public const string ManageMedical = "welfare.medical.manage";
    }

    public static class Events
    {
        public const string View = "events.view";
        public const string Manage = "events.manage";
    }

    public static class Facilities
    {
        public const string Book = "facilities.book";
        public const string Manage = "facilities.manage";
    }

    public static class Documents
    {
        public const string IssueCertificates = "documents.certificates.issue";
        public const string ManageTemplates = "documents.templates.manage";
    }

    public static class Hostel
    {
        public const string View = "hostel.view";
        public const string Manage = "hostel.manage";
    }

    public static class Cafeteria
    {
        public const string View = "cafeteria.view";
        public const string Manage = "cafeteria.manage";
    }

    public static class Reports
    {
        public const string View = "reports.view";
    }

    public static class Portals
    {
        public const string Teacher = "portal.teacher";
        public const string Family = "portal.family";
        public const string Student = "portal.student";
    }
}

public sealed record PermissionDefinition(string Key, string Label, string Description, bool Sensitive = false);

public sealed record PermissionGroup(string Key, string Label, string? Module, IReadOnlyList<PermissionDefinition> Permissions);

/// <summary>Human-facing metadata for the role editor. Every constant in <see cref="Permissions"/> appears exactly once.</summary>
public static class PermissionCatalog
{
    private static PermissionDefinition P(string key, string label, string description, bool sensitive = false) =>
        new(key, label, description, sensitive);

    public static IReadOnlyList<PermissionGroup> Groups { get; } =
    [
        new("school", "School & configuration", null,
        [
            P(Permissions.School.View, "View school profile", "See school profile, campuses, numbering and module settings."),
            P(Permissions.School.Manage, "Manage school settings", "Edit profile, branding, campuses, numbering, modules and policies."),
            P(Permissions.Academics.View, "View academic structure", "See years, terms, grades, sections, subjects, rooms and calendar."),
            P(Permissions.Academics.Manage, "Manage academic structure", "Create and change years, grades, sections, subjects, rooms, calendar and grading."),
        ]),
        new("access", "Users & access", null,
        [
            P(Permissions.Users.View, "View users", "See user accounts and their role assignments."),
            P(Permissions.Users.Manage, "Manage users", "Invite, disable and assign roles to users; revoke sessions."),
            P(Permissions.Roles.View, "View roles", "See roles and their permissions."),
            P(Permissions.Roles.Manage, "Manage roles", "Create roles and change their permissions."),
            P(Permissions.Audit.View, "View audit log", "See who changed what and when.", true),
        ]),
        new("students", "Students", null,
        [
            P(Permissions.Students.View, "View students", "Search and open student records."),
            P(Permissions.Students.Create, "Add students", "Create student records directly."),
            P(Permissions.Students.Update, "Edit students", "Change student details, guardians and enrollment."),
            P(Permissions.Students.Delete, "Delete students", "Delete student records that were created in error."),
            P(Permissions.Students.Import, "Import students", "Bulk import students from spreadsheets."),
            P(Permissions.Students.Export, "Export students", "Export student lists."),
            P(Permissions.Students.ManageStatus, "Change student status", "Suspend, withdraw, transfer out and graduate students."),
            P(Permissions.Students.ViewDocuments, "View student documents", "Open uploaded student documents."),
            P(Permissions.Students.ManageDocuments, "Manage student documents", "Upload and remove student documents."),
            P(Permissions.Students.ViewMedical, "View medical profile", "See allergies, conditions and medications.", true),
            P(Permissions.Students.ManageMedical, "Edit medical profile", "Change medical information.", true),
        ]),
        new("guardians", "Guardians", null,
        [
            P(Permissions.Guardians.View, "View guardians", "See guardian records and their children."),
            P(Permissions.Guardians.Create, "Add guardians", "Create guardian records."),
            P(Permissions.Guardians.Update, "Edit guardians", "Change guardian details and links."),
            P(Permissions.Guardians.Delete, "Delete guardians", "Delete guardian records without links."),
            P(Permissions.Guardians.ManagePortal, "Manage family portal access", "Create guardian logins and set what they can see."),
        ]),
        new("admissions", "Admissions", "admissions",
        [
            P(Permissions.Admissions.View, "View admissions", "See inquiries and applications."),
            P(Permissions.Admissions.Create, "Record inquiries & applications", "Create inquiries and applications."),
            P(Permissions.Admissions.Update, "Edit applications", "Change application details and documents."),
            P(Permissions.Admissions.Review, "Review applications", "Move applications through review, tests and interviews."),
            P(Permissions.Admissions.Decide, "Decide admissions", "Offer, waitlist or reject applicants."),
            P(Permissions.Admissions.Enroll, "Enroll applicants", "Convert accepted applicants into students."),
            P(Permissions.Admissions.Export, "Export admissions", "Export inquiry and application lists."),
        ]),
        new("attendance", "Student attendance", null,
        [
            P(Permissions.Attendance.View, "View attendance", "See registers, histories and reports."),
            P(Permissions.Attendance.Record, "Take attendance (own classes)", "Take registers for sections the user teaches."),
            P(Permissions.Attendance.Manage, "Take attendance (any class)", "Take and edit registers for any section in scope."),
            P(Permissions.Attendance.Approve, "Approve corrections", "Approve changes to locked registers."),
            P(Permissions.Attendance.Export, "Export attendance", "Export attendance data."),
            P(Permissions.Attendance.ManageLeave, "Manage student leave", "Approve or reject student leave requests."),
        ]),
        // The staff directory, staff attendance and leave are core (every school has teachers and registers);
        // contracts, recruitment and reviews belong to the optional HR module.
        new("staff", "Staff & staff attendance", null,
        [
            P(Permissions.Staff.View, "View staff", "See staff directory and profiles."),
            P(Permissions.Staff.Create, "Add staff", "Create staff records."),
            P(Permissions.Staff.Update, "Edit staff", "Change staff details, assignments and sign-in access."),
            P(Permissions.Staff.Delete, "Delete staff", "Delete staff records created in error."),
            P(Permissions.Staff.Export, "Export staff", "Export staff lists."),
            P(Permissions.Staff.ManageDocuments, "Manage staff documents", "Upload and remove staff documents."),
            P(Permissions.Staff.ViewAttendance, "View staff attendance", "See staff registers and attendance reports."),
            P(Permissions.Staff.RecordAttendance, "Record staff attendance", "Mark the daily staff register."),
            P(Permissions.Staff.ApproveAttendance, "Approve staff attendance corrections", "Approve changes to locked staff attendance."),
            P(Permissions.Hr.ViewLeave, "View staff leave", "See staff leave requests and balances."),
            P(Permissions.Hr.ApproveLeave, "Approve staff leave", "Approve or reject staff leave."),
            P(Permissions.Hr.Configure, "Configure leave & HR", "Leave types, departments and designations."),
        ]),
        new("hr", "HR", "hr",
        [
            P(Permissions.Staff.ViewBank, "View bank details", "See staff bank account details.", true),
            P(Permissions.Hr.Recruitment, "Manage recruitment", "Manage openings and applicants."),
            P(Permissions.Hr.Performance, "Manage performance reviews", "Record and view performance reviews.", true),
        ]),
        new("payroll", "Payroll", "payroll",
        [
            P(Permissions.Payroll.View, "View payroll", "See salary structures, payroll runs and payslips.", true),
            P(Permissions.Payroll.Configure, "Configure payroll", "Salary components, structures and loans.", true),
            P(Permissions.Payroll.Process, "Process payroll", "Create and process payroll runs.", true),
            P(Permissions.Payroll.Approve, "Approve payroll", "Approve and mark payroll runs as paid.", true),
        ]),
        new("academics", "Teaching & learning", null,
        [
            P(Permissions.Timetable.View, "View timetables", "See section and teacher timetables."),
            P(Permissions.Timetable.Manage, "Manage timetables", "Edit timetables, bell schedules and substitutions."),
            P(Permissions.Assignments.View, "View assignments", "See homework and assignments."),
            P(Permissions.Assignments.Manage, "Manage own assignments", "Create and grade assignments for own classes."),
            P(Permissions.Assignments.ManageAll, "Manage all assignments", "Manage assignments for any class."),
            P(Permissions.LessonPlans.View, "View lesson plans", "See lesson plans."),
            P(Permissions.LessonPlans.Manage, "Write lesson plans", "Create and submit own lesson plans."),
            P(Permissions.LessonPlans.Approve, "Approve lesson plans", "Review and approve lesson plans."),
            P(Permissions.Resources.Manage, "Manage learning resources", "Upload and publish learning materials."),
            P(Permissions.Promotions.Manage, "Manage promotions", "Promote, retain and graduate students at year end."),
        ]),
        new("exams", "Examinations", "exams",
        [
            P(Permissions.Exams.View, "View examinations", "See exam sessions, schedules and results."),
            P(Permissions.Exams.Manage, "Manage examinations", "Create exam sessions, papers and grading schemes."),
            P(Permissions.Exams.EnterMarks, "Enter marks (own subjects)", "Enter marks for subjects the user teaches."),
            P(Permissions.Exams.ManageMarks, "Enter marks (any subject)", "Enter and correct any marks."),
            P(Permissions.Exams.ApproveResults, "Approve results", "Approve computed results."),
            P(Permissions.Exams.PublishResults, "Publish results", "Publish results to families and students."),
        ]),
        new("fees", "Fees", "fees",
        [
            P(Permissions.Fees.View, "View fees", "See fee structures, challans, payments and balances."),
            P(Permissions.Fees.Configure, "Configure fees", "Fee heads, structures, concession types and student fee items."),
            P(Permissions.Fees.Generate, "Generate challans", "Run billing and issue challans."),
            P(Permissions.Fees.Cancel, "Cancel challans", "Cancel unpaid challans."),
            P(Permissions.Fees.Collect, "Collect payments", "Record payments and issue receipts."),
            P(Permissions.Fees.Reverse, "Reverse payments", "Reverse recorded payments (with reason)."),
            P(Permissions.Fees.RequestConcession, "Request concessions", "Propose scholarships and concessions."),
            P(Permissions.Fees.ApproveConcession, "Approve concessions", "Approve or reject concessions."),
            P(Permissions.Fees.RequestAdjustment, "Request waivers", "Propose waivers and write-offs."),
            P(Permissions.Fees.ApproveAdjustment, "Approve waivers", "Approve or reject waivers and write-offs."),
            P(Permissions.Fees.RequestRefund, "Request refunds", "Propose refunds of student credit."),
            P(Permissions.Fees.ApproveRefund, "Approve refunds", "Approve and pay refunds."),
            P(Permissions.Fees.Reports, "View fee reports", "Collections, defaulters, statements and revenue."),
            P(Permissions.Fees.Export, "Export fee data", "Export fee lists and reports."),
            P(Permissions.Finance.ViewLedger, "View ledger", "See journal entries, accounts and trial balance.", true),
            P(Permissions.Finance.ManageAccounts, "Manage accounts", "Chart of accounts, bank accounts and cash registers."),
            P(Permissions.Finance.CloseRegister, "Close cash registers", "Count and close daily cash registers."),
        ]),
        new("communication", "Communication", null,
        [
            P(Permissions.Communication.ViewAnnouncements, "View announcements", "See school announcements and notices."),
            P(Permissions.Communication.ManageAnnouncements, "Publish announcements", "Create, schedule and publish announcements."),
            P(Permissions.Communication.SendMessages, "Message families", "Send messages to guardians and staff."),
            P(Permissions.Communication.ManageTemplates, "Manage templates", "Edit notification templates."),
            P(Permissions.Communication.ViewLogs, "View delivery logs", "See outbound message delivery status."),
        ]),
        new("library", "Library", "library",
        [
            P(Permissions.Library.View, "View library", "Search the catalogue and loans."),
            P(Permissions.Library.Manage, "Manage catalogue", "Add titles and copies; manage fines."),
            P(Permissions.Library.Circulate, "Issue & return books", "Issue, renew and return books."),
        ]),
        new("transport", "Transport", "transport",
        [
            P(Permissions.Transport.View, "View transport", "See vehicles, routes and allocations."),
            P(Permissions.Transport.Manage, "Manage transport", "Manage vehicles, routes, stops, allocations and logs."),
        ]),
        new("inventory", "Inventory & assets", "inventory",
        [
            P(Permissions.Inventory.View, "View inventory", "See items, stock and assets."),
            P(Permissions.Inventory.Manage, "Manage inventory", "Record receipts, issues, returns and assets."),
            P(Permissions.Inventory.Approve, "Approve purchases", "Approve purchase orders."),
        ]),
        new("welfare", "Welfare & discipline", "welfare",
        [
            P(Permissions.Welfare.ViewDiscipline, "View discipline records", "See incidents and actions.", true),
            P(Permissions.Welfare.ManageDiscipline, "Manage discipline", "Record incidents and actions.", true),
            P(Permissions.Welfare.ViewCounseling, "View counselling", "See confidential counselling records.", true),
            P(Permissions.Welfare.ManageCounseling, "Manage counselling", "Record counselling sessions.", true),
            P(Permissions.Welfare.ViewMedical, "View medical visits", "See clinic visits and health records.", true),
            P(Permissions.Welfare.ManageMedical, "Manage medical visits", "Record clinic visits.", true),
        ]),
        new("events", "Events & facilities", "events",
        [
            P(Permissions.Events.View, "View events", "See the school calendar and events."),
            P(Permissions.Events.Manage, "Manage events", "Create events, PTMs and holidays."),
            P(Permissions.Facilities.Book, "Book rooms", "Reserve rooms and facilities."),
            P(Permissions.Facilities.Manage, "Manage facilities", "Manage bookings and maintenance requests."),
        ]),
        new("documents", "Documents & certificates", null,
        [
            P(Permissions.Documents.IssueCertificates, "Issue certificates", "Generate and revoke certificates."),
            P(Permissions.Documents.ManageTemplates, "Manage templates", "Edit certificate and document templates."),
        ]),
        new("hostel", "Hostel", "hostel",
        [
            P(Permissions.Hostel.View, "View hostel", "See hostels, rooms and residents."),
            P(Permissions.Hostel.Manage, "Manage hostel", "Allocations, check-in/out, attendance and incidents."),
        ]),
        new("cafeteria", "Cafeteria", "cafeteria",
        [
            P(Permissions.Cafeteria.View, "View cafeteria", "See menus, plans and orders."),
            P(Permissions.Cafeteria.Manage, "Manage cafeteria", "Manage menus, plans, subscriptions and orders."),
        ]),
        new("reports", "Reports", null,
        [
            P(Permissions.Reports.View, "View dashboards & reports", "See operational dashboards and reports."),
        ]),
        new("portals", "Portals", null,
        [
            P(Permissions.Portals.Teacher, "Teacher portal", "Use the teacher portal."),
            P(Permissions.Portals.Family, "Family portal", "Use the family portal."),
            P(Permissions.Portals.Student, "Student portal", "Use the student portal."),
        ]),
    ];

    public static IReadOnlyList<PermissionDefinition> All { get; } = Groups.SelectMany(g => g.Permissions).ToList();

    public static IReadOnlySet<string> Keys { get; } = All.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);

    public static bool Exists(string key) => Keys.Contains(key);
}
