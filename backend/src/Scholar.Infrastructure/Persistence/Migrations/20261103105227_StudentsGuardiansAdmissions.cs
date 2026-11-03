using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scholar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudentsGuardiansAdmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admission_inquiries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campus_id = table.Column<Guid>(type: "uuid", nullable: true),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: true),
                    grade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    student_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    guardian_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    follow_up_on = table.Column<DateOnly>(type: "date", nullable: true),
                    assigned_to_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    closed_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admission_inquiries", x => x.id);
                    table.ForeignKey(
                        name: "fk_admission_inquiries_academic_years_academic_year_id",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_admission_inquiries_campuses_campus_id",
                        column: x => x.campus_id,
                        principalTable: "campuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_admission_inquiries_grades_grade_id",
                        column: x => x.grade_id,
                        principalTable: "grades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_admission_inquiries_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "guardians",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    full_name = table.Column<string>(type: "character varying(210)", maxLength: 210, nullable: false, computedColumnSql: "trim(first_name || ' ' || last_name)", stored: true),
                    national_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    alternate_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    occupation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    employer = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    preferred_language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    notify_by_sms = table.Column<bool>(type: "boolean", nullable: false),
                    notify_by_email = table.Column<bool>(type: "boolean", nullable: false),
                    notify_by_whats_app = table.Column<bool>(type: "boolean", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    address_line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address_province = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guardians", x => x.id);
                    table.ForeignKey(
                        name: "fk_guardians_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_guardians_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stored_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sensitivity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stored_files", x => x.id);
                    table.CheckConstraint("ck_stored_files_size", "size_bytes >= 0");
                    table.ForeignKey(
                        name: "fk_stored_files_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "students",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    admission_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    registration_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    middle_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    full_name = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false, computedColumnSql: "first_name || coalesce(' ' || middle_name, '') || ' ' || last_name", stored: true),
                    gender = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    place_of_birth = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    national_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    nationality = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    religion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    blood_group = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    mother_tongue = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    photo_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    previous_school_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    previous_school_class = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    admission_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status_changed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    medical_alert = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_section_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_academic_year_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    address_line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address_province = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_students", x => x.id);
                    table.ForeignKey(
                        name: "fk_students_academic_years_current_academic_year_id",
                        column: x => x.current_academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_students_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_students_campuses_campus_id",
                        column: x => x.campus_id,
                        principalTable: "campuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_students_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_students_sections_current_section_id",
                        column: x => x.current_section_id,
                        principalTable: "sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_students_stored_files_photo_file_id",
                        column: x => x.photo_file_id,
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "admission_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    campus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inquiry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    middle_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    gender = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    national_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    previous_school_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    previous_school_class = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    previous_result = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    guardian_first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    guardian_last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    guardian_relationship = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    guardian_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    guardian_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    guardian_national_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    guardian_occupation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    waitlist_rank = table.Column<int>(type: "integer", nullable: true),
                    offer_expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    student_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    address_line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address_province = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admission_applications", x => x.id);
                    table.ForeignKey(
                        name: "fk_admission_applications_academic_years_academic_year_id",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_admission_applications_admission_inquiries_inquiry_id",
                        column: x => x.inquiry_id,
                        principalTable: "admission_inquiries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_admission_applications_campuses_campus_id",
                        column: x => x.campus_id,
                        principalTable: "campuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_admission_applications_grades_grade_id",
                        column: x => x.grade_id,
                        principalTable: "grades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_admission_applications_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_admission_applications_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "emergency_contacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    relationship = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    alternate_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_emergency_contacts", x => x.id);
                    table.ForeignKey(
                        name: "fk_emergency_contacts_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_emergency_contacts_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "enrollments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    campus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    roll_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enrollments", x => x.id);
                    table.CheckConstraint("ck_enrollments_dates", "end_date IS NULL OR end_date >= start_date");
                    table.ForeignKey(
                        name: "fk_enrollments_academic_years_academic_year_id",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_enrollments_campuses_campus_id",
                        column: x => x.campus_id,
                        principalTable: "campuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_enrollments_grades_grade_id",
                        column: x => x.grade_id,
                        principalTable: "grades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_enrollments_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_enrollments_sections_section_id",
                        column: x => x.section_id,
                        principalTable: "sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_enrollments_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_guardians",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    guardian_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relationship = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_emergency_contact = table.Column<bool>(type: "boolean", nullable: false),
                    lives_with_student = table.Column<bool>(type: "boolean", nullable: false),
                    can_pick_up = table.Column<bool>(type: "boolean", nullable: false),
                    is_fee_payer = table.Column<bool>(type: "boolean", nullable: false),
                    portal_access = table.Column<bool>(type: "boolean", nullable: false),
                    can_view_attendance = table.Column<bool>(type: "boolean", nullable: false),
                    can_view_results = table.Column<bool>(type: "boolean", nullable: false),
                    can_view_fees = table.Column<bool>(type: "boolean", nullable: false),
                    receives_notifications = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_guardians", x => x.id);
                    table.ForeignKey(
                        name: "fk_student_guardians_guardians_guardian_id",
                        column: x => x.guardian_id,
                        principalTable: "guardians",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_guardians_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_guardians_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_medical_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allergies = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    conditions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    medications = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    special_needs = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    doctor_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    doctor_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    emergency_instructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_medical_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_student_medical_profiles_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_medical_profiles_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_status_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    to_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    destination_school = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_status_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_student_status_changes_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_status_changes_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "application_assessments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    score = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: true),
                    max_score = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_assessments", x => x.id);
                    table.ForeignKey(
                        name: "fk_application_assessments_admission_applications_application_",
                        column: x => x.application_id,
                        principalTable: "admission_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_assessments_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "application_status_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    to_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_status_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_application_status_changes_admission_applications_applicati",
                        column: x => x.application_id,
                        principalTable: "admission_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_status_changes_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admission_applications_academic_year_id",
                table: "admission_applications",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "ix_admission_applications_campus_id",
                table: "admission_applications",
                column: "campus_id");

            migrationBuilder.CreateIndex(
                name: "ix_admission_applications_grade_id",
                table: "admission_applications",
                column: "grade_id");

            migrationBuilder.CreateIndex(
                name: "ix_admission_applications_inquiry_id",
                table: "admission_applications",
                column: "inquiry_id");

            migrationBuilder.CreateIndex(
                name: "ix_admission_applications_school_id_application_number",
                table: "admission_applications",
                columns: new[] { "school_id", "application_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_admission_applications_school_id_guardian_phone",
                table: "admission_applications",
                columns: new[] { "school_id", "guardian_phone" });

            migrationBuilder.CreateIndex(
                name: "ix_admission_applications_school_id_status_academic_year_id",
                table: "admission_applications",
                columns: new[] { "school_id", "status", "academic_year_id" });

            migrationBuilder.CreateIndex(
                name: "ix_admission_applications_student_id",
                table: "admission_applications",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_admission_inquiries_academic_year_id",
                table: "admission_inquiries",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "ix_admission_inquiries_campus_id",
                table: "admission_inquiries",
                column: "campus_id");

            migrationBuilder.CreateIndex(
                name: "ix_admission_inquiries_grade_id",
                table: "admission_inquiries",
                column: "grade_id");

            migrationBuilder.CreateIndex(
                name: "ix_admission_inquiries_school_id_phone",
                table: "admission_inquiries",
                columns: new[] { "school_id", "phone" });

            migrationBuilder.CreateIndex(
                name: "ix_admission_inquiries_school_id_status_follow_up_on",
                table: "admission_inquiries",
                columns: new[] { "school_id", "status", "follow_up_on" });

            migrationBuilder.CreateIndex(
                name: "ix_application_assessments_application_id",
                table: "application_assessments",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_application_assessments_school_id",
                table: "application_assessments",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_application_status_changes_application_id_changed_at",
                table: "application_status_changes",
                columns: new[] { "application_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_application_status_changes_school_id",
                table: "application_status_changes",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_emergency_contacts_school_id",
                table: "emergency_contacts",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_emergency_contacts_student_id",
                table: "emergency_contacts",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_academic_year_id",
                table: "enrollments",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_campus_id",
                table: "enrollments",
                column: "campus_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_grade_id",
                table: "enrollments",
                column: "grade_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_school_id",
                table: "enrollments",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_section_id_status",
                table: "enrollments",
                columns: new[] { "section_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_student_id_academic_year_id",
                table: "enrollments",
                columns: new[] { "student_id", "academic_year_id" },
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_guardians_full_name_trgm",
                table: "guardians",
                column: "full_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_guardians_school_id_national_id",
                table: "guardians",
                columns: new[] { "school_id", "national_id" });

            migrationBuilder.CreateIndex(
                name: "ix_guardians_school_id_phone",
                table: "guardians",
                columns: new[] { "school_id", "phone" });

            migrationBuilder.CreateIndex(
                name: "ix_guardians_school_id_user_id",
                table: "guardians",
                columns: new[] { "school_id", "user_id" },
                unique: true,
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_guardians_user_id",
                table: "guardians",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_stored_files_school_id_owner_type_owner_id",
                table: "stored_files",
                columns: new[] { "school_id", "owner_type", "owner_id" });

            migrationBuilder.CreateIndex(
                name: "ix_stored_files_storage_key",
                table: "stored_files",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_guardians_guardian_id",
                table: "student_guardians",
                column: "guardian_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_guardians_school_id",
                table: "student_guardians",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_guardians_student_id_guardian_id",
                table: "student_guardians",
                columns: new[] { "student_id", "guardian_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_medical_profiles_school_id",
                table: "student_medical_profiles",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_medical_profiles_student_id",
                table: "student_medical_profiles",
                column: "student_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_status_changes_school_id",
                table: "student_status_changes",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_status_changes_student_id",
                table: "student_status_changes",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_students_campus_id",
                table: "students",
                column: "campus_id");

            migrationBuilder.CreateIndex(
                name: "ix_students_current_academic_year_id",
                table: "students",
                column: "current_academic_year_id");

            migrationBuilder.CreateIndex(
                name: "ix_students_current_section_id",
                table: "students",
                column: "current_section_id");

            migrationBuilder.CreateIndex(
                name: "ix_students_full_name_trgm",
                table: "students",
                column: "full_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_students_photo_file_id",
                table: "students",
                column: "photo_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_students_school_id_admission_number",
                table: "students",
                columns: new[] { "school_id", "admission_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_students_school_id_national_id",
                table: "students",
                columns: new[] { "school_id", "national_id" });

            migrationBuilder.CreateIndex(
                name: "ix_students_school_id_status_campus_id",
                table: "students",
                columns: new[] { "school_id", "status", "campus_id" });

            migrationBuilder.CreateIndex(
                name: "ix_students_user_id",
                table: "students",
                column: "user_id",
                unique: true,
                filter: "user_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_assessments");

            migrationBuilder.DropTable(
                name: "application_status_changes");

            migrationBuilder.DropTable(
                name: "emergency_contacts");

            migrationBuilder.DropTable(
                name: "enrollments");

            migrationBuilder.DropTable(
                name: "student_guardians");

            migrationBuilder.DropTable(
                name: "student_medical_profiles");

            migrationBuilder.DropTable(
                name: "student_status_changes");

            migrationBuilder.DropTable(
                name: "admission_applications");

            migrationBuilder.DropTable(
                name: "guardians");

            migrationBuilder.DropTable(
                name: "admission_inquiries");

            migrationBuilder.DropTable(
                name: "students");

            migrationBuilder.DropTable(
                name: "stored_files");
        }
    }
}
