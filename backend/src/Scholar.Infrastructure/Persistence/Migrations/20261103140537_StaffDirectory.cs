using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scholar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "class_teacher_id",
                table: "sections",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "staff",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    full_name = table.Column<string>(type: "character varying(210)", maxLength: 210, nullable: false, computedColumnSql: "trim(first_name || ' ' || last_name)", stored: true),
                    gender = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    national_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    photo_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    designation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    staff_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    employment_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    join_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    exit_date = table.Column<DateOnly>(type: "date", nullable: true),
                    exit_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_staff", x => x.id);
                    table.ForeignKey(
                        name: "fk_staff_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_staff_campuses_campus_id",
                        column: x => x.campus_id,
                        principalTable: "campuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_staff_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_staff_designations_designation_id",
                        column: x => x.designation_id,
                        principalTable: "designations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_staff_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_staff_stored_files_photo_file_id",
                        column: x => x.photo_file_id,
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "section_subject_teachers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_section_subject_teachers", x => x.id);
                    table.ForeignKey(
                        name: "fk_section_subject_teachers_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_section_subject_teachers_sections_section_id",
                        column: x => x.section_id,
                        principalTable: "sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_section_subject_teachers_staff_staff_id",
                        column: x => x.staff_id,
                        principalTable: "staff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_section_subject_teachers_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sections_class_teacher_id",
                table: "sections",
                column: "class_teacher_id");

            migrationBuilder.CreateIndex(
                name: "ix_section_subject_teachers_school_id",
                table: "section_subject_teachers",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_section_subject_teachers_section_id_subject_id",
                table: "section_subject_teachers",
                columns: new[] { "section_id", "subject_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_section_subject_teachers_staff_id",
                table: "section_subject_teachers",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "ix_section_subject_teachers_subject_id",
                table: "section_subject_teachers",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_campus_id",
                table: "staff",
                column: "campus_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_department_id",
                table: "staff",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_designation_id",
                table: "staff",
                column: "designation_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_full_name_trgm",
                table: "staff",
                column: "full_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_staff_photo_file_id",
                table: "staff",
                column: "photo_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_school_id_employee_number",
                table: "staff",
                columns: new[] { "school_id", "employee_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_staff_school_id_status_campus_id",
                table: "staff",
                columns: new[] { "school_id", "status", "campus_id" });

            migrationBuilder.CreateIndex(
                name: "ix_staff_school_id_user_id",
                table: "staff",
                columns: new[] { "school_id", "user_id" },
                unique: true,
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_staff_user_id",
                table: "staff",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_sections_staff_class_teacher_id",
                table: "sections",
                column: "class_teacher_id",
                principalTable: "staff",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // New permission: existing schools' built-in roles get it as new schools would (owners hold every permission).
            migrationBuilder.Sql("""
                INSERT INTO role_permissions (role_id, permission)
                SELECT r.id, 'staff.attendance.approve' FROM roles r
                WHERE r.key IN ('school_admin', 'principal', 'hr_manager')
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM role_permissions WHERE permission = 'staff.attendance.approve';");

            migrationBuilder.DropForeignKey(
                name: "fk_sections_staff_class_teacher_id",
                table: "sections");

            migrationBuilder.DropTable(
                name: "section_subject_teachers");

            migrationBuilder.DropTable(
                name: "staff");

            migrationBuilder.DropIndex(
                name: "ix_sections_class_teacher_id",
                table: "sections");

            migrationBuilder.DropColumn(
                name: "class_teacher_id",
                table: "sections");
        }
    }
}
