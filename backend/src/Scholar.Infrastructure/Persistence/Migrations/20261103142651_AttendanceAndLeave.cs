using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scholar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceAndLeave : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attendance_registers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    campus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    period = table.Column<int>(type: "integer", nullable: true),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    taken_by_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_registers", x => x.id);
                    table.CheckConstraint("ck_attendance_registers_period", "period IS NULL OR period BETWEEN 1 AND 16");
                    table.ForeignKey(
                        name: "fk_attendance_registers_academic_years_academic_year_id",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_registers_campuses_campus_id",
                        column: x => x.campus_id,
                        principalTable: "campuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_registers_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_registers_sections_section_id",
                        column: x => x.section_id,
                        principalTable: "sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_registers_staff_taken_by_staff_id",
                        column: x => x.taken_by_staff_id,
                        principalTable: "staff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_attendance_registers_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bell_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bell_schedules", x => x.id);
                    table.ForeignKey(
                        name: "fk_bell_schedules_campuses_campus_id",
                        column: x => x.campus_id,
                        principalTable: "campuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bell_schedules_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "leave_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    annual_days = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    is_paid = table.Column<bool>(type: "boolean", nullable: false),
                    requires_document = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leave_types", x => x.id);
                    table.CheckConstraint("ck_leave_types_days", "annual_days IS NULL OR annual_days >= 0");
                    table.ForeignKey(
                        name: "fk_leave_types_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "outbound_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    recipient = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    recipient_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    purpose = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    dedup_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status_detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    provider = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    guardian_id = table.Column<Guid>(type: "uuid", nullable: true),
                    student_id = table.Column<Guid>(type: "uuid", nullable: true),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    related_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbound_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_outbound_messages_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_attendance_corrections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    from_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    to_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    check_in = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    check_out = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    requested_by_self = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_attendance_corrections", x => x.id);
                    table.ForeignKey(
                        name: "fk_staff_attendance_corrections_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_staff_attendance_corrections_staff_staff_id",
                        column: x => x.staff_id,
                        principalTable: "staff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_leave_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    to_date = table.Column<DateOnly>(type: "date", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    requested_by = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    document_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_leave_requests", x => x.id);
                    table.CheckConstraint("ck_student_leave_requests_dates", "to_date >= from_date");
                    table.ForeignKey(
                        name: "fk_student_leave_requests_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_leave_requests_stored_files_document_file_id",
                        column: x => x.document_file_id,
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_student_leave_requests_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance_corrections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_corrections", x => x.id);
                    table.ForeignKey(
                        name: "fk_attendance_corrections_attendance_registers_register_id",
                        column: x => x.register_id,
                        principalTable: "attendance_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attendance_corrections_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bell_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: true),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bell_periods", x => x.id);
                    table.CheckConstraint("ck_bell_periods_times", "end_time > start_time");
                    table.ForeignKey(
                        name: "fk_bell_periods_bell_schedules_schedule_id",
                        column: x => x.schedule_id,
                        principalTable: "bell_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bell_periods_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_leave_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leave_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    to_date = table.Column<DateOnly>(type: "date", nullable: false),
                    half_day = table.Column<bool>(type: "boolean", nullable: false),
                    days = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    requested_by_self = table.Column<bool>(type: "boolean", nullable: false),
                    document_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_leave_requests", x => x.id);
                    table.CheckConstraint("ck_staff_leave_requests_dates", "to_date >= from_date");
                    table.CheckConstraint("ck_staff_leave_requests_days", "days > 0");
                    table.ForeignKey(
                        name: "fk_staff_leave_requests_leave_types_leave_type_id",
                        column: x => x.leave_type_id,
                        principalTable: "leave_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_staff_leave_requests_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_staff_leave_requests_staff_staff_id",
                        column: x => x.staff_id,
                        principalTable: "staff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_staff_leave_requests_stored_files_document_file_id",
                        column: x => x.document_file_id,
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "attendance_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    remark = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    arrived_at = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    leave_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_attendance_records_attendance_registers_register_id",
                        column: x => x.register_id,
                        principalTable: "attendance_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attendance_records_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_records_student_leave_requests_leave_request_id",
                        column: x => x.leave_request_id,
                        principalTable: "student_leave_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_attendance_records_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance_correction_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    correction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    to_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    remark = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_correction_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_attendance_correction_items_attendance_corrections_correcti",
                        column: x => x.correction_id,
                        principalTable: "attendance_corrections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attendance_correction_items_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_correction_items_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_attendance_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    campus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    check_in = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    check_out = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    remark = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    leave_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_attendance_records", x => x.id);
                    table.CheckConstraint("ck_staff_attendance_records_times", "check_in IS NULL OR check_out IS NULL OR check_out > check_in");
                    table.ForeignKey(
                        name: "fk_staff_attendance_records_campuses_campus_id",
                        column: x => x.campus_id,
                        principalTable: "campuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_staff_attendance_records_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_staff_attendance_records_staff_leave_requests_leave_request",
                        column: x => x.leave_request_id,
                        principalTable: "staff_leave_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_staff_attendance_records_staff_staff_id",
                        column: x => x.staff_id,
                        principalTable: "staff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_correction_items_correction_id_student_id",
                table: "attendance_correction_items",
                columns: new[] { "correction_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attendance_correction_items_school_id",
                table: "attendance_correction_items",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_correction_items_student_id",
                table: "attendance_correction_items",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_corrections_register_id",
                table: "attendance_corrections",
                column: "register_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_corrections_school_id_status",
                table: "attendance_corrections",
                columns: new[] { "school_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_leave_request_id",
                table: "attendance_records",
                column: "leave_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_register_id_student_id",
                table: "attendance_records",
                columns: new[] { "register_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_school_id",
                table: "attendance_records",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_student_id_status",
                table: "attendance_records",
                columns: new[] { "student_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_registers_academic_year_id",
                table: "attendance_registers",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_registers_campus_id",
                table: "attendance_registers",
                column: "campus_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_registers_school_id_date_campus_id",
                table: "attendance_registers",
                columns: new[] { "school_id", "date", "campus_id" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_registers_section_id_date",
                table: "attendance_registers",
                columns: new[] { "section_id", "date" },
                unique: true,
                filter: "period IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_registers_section_id_date_period",
                table: "attendance_registers",
                columns: new[] { "section_id", "date", "period" },
                unique: true,
                filter: "period IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_registers_subject_id",
                table: "attendance_registers",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_registers_taken_by_staff_id",
                table: "attendance_registers",
                column: "taken_by_staff_id");

            migrationBuilder.CreateIndex(
                name: "ix_bell_periods_schedule_id_sequence",
                table: "bell_periods",
                columns: new[] { "schedule_id", "sequence" });

            migrationBuilder.CreateIndex(
                name: "ix_bell_periods_school_id",
                table: "bell_periods",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_bell_schedules_campus_id",
                table: "bell_schedules",
                column: "campus_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bell_schedules_school_id",
                table: "bell_schedules",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_leave_types_school_id_code",
                table: "leave_types",
                columns: new[] { "school_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbound_messages_pending",
                table: "outbound_messages",
                column: "status",
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_outbound_messages_school_id_dedup_key",
                table: "outbound_messages",
                columns: new[] { "school_id", "dedup_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbound_messages_school_id_related_id",
                table: "outbound_messages",
                columns: new[] { "school_id", "related_id" });

            migrationBuilder.CreateIndex(
                name: "ix_outbound_messages_school_id_student_id_created_at",
                table: "outbound_messages",
                columns: new[] { "school_id", "student_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_staff_attendance_corrections_school_id_status",
                table: "staff_attendance_corrections",
                columns: new[] { "school_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_staff_attendance_corrections_staff_id_date",
                table: "staff_attendance_corrections",
                columns: new[] { "staff_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_staff_attendance_records_campus_id",
                table: "staff_attendance_records",
                column: "campus_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_attendance_records_leave_request_id",
                table: "staff_attendance_records",
                column: "leave_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_attendance_records_school_id_date_campus_id",
                table: "staff_attendance_records",
                columns: new[] { "school_id", "date", "campus_id" });

            migrationBuilder.CreateIndex(
                name: "ix_staff_attendance_records_staff_id_date",
                table: "staff_attendance_records",
                columns: new[] { "staff_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_staff_leave_requests_document_file_id",
                table: "staff_leave_requests",
                column: "document_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_leave_requests_leave_type_id",
                table: "staff_leave_requests",
                column: "leave_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_leave_requests_school_id_status",
                table: "staff_leave_requests",
                columns: new[] { "school_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_staff_leave_requests_staff_id_from_date",
                table: "staff_leave_requests",
                columns: new[] { "staff_id", "from_date" });

            migrationBuilder.CreateIndex(
                name: "ix_student_leave_requests_document_file_id",
                table: "student_leave_requests",
                column: "document_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_leave_requests_school_id_status",
                table: "student_leave_requests",
                columns: new[] { "school_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_student_leave_requests_student_id_from_date",
                table: "student_leave_requests",
                columns: new[] { "student_id", "from_date" });

            // Existing schools get the same default leave types a new school is provisioned with.
            migrationBuilder.Sql("""
                INSERT INTO leave_types (id, school_id, code, name, annual_days, is_paid, requires_document, is_active, sort_order, created_at)
                SELECT gen_random_uuid(), s.id, t.code, t.name, t.days, t.paid, t.document, true, t.sort, now()
                FROM schools s
                CROSS JOIN (VALUES
                    ('CL', 'Casual leave', 10.0, true, false, 1),
                    ('SL', 'Sick leave', 8.0, true, false, 2),
                    ('ML', 'Maternity leave', 90.0, true, true, 3),
                    ('HL', 'Hajj / Umrah leave', 40.0, false, false, 4),
                    ('UL', 'Unpaid leave', NULL, false, false, 5)
                ) AS t(code, name, days, paid, document, sort)
                WHERE NOT EXISTS (SELECT 1 FROM leave_types l WHERE l.school_id = s.id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_correction_items");

            migrationBuilder.DropTable(
                name: "attendance_records");

            migrationBuilder.DropTable(
                name: "bell_periods");

            migrationBuilder.DropTable(
                name: "outbound_messages");

            migrationBuilder.DropTable(
                name: "staff_attendance_corrections");

            migrationBuilder.DropTable(
                name: "staff_attendance_records");

            migrationBuilder.DropTable(
                name: "attendance_corrections");

            migrationBuilder.DropTable(
                name: "student_leave_requests");

            migrationBuilder.DropTable(
                name: "bell_schedules");

            migrationBuilder.DropTable(
                name: "staff_leave_requests");

            migrationBuilder.DropTable(
                name: "attendance_registers");

            migrationBuilder.DropTable(
                name: "leave_types");
        }
    }
}
