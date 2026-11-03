using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scholar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerPeriodSequenceCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "number_sequence_counters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    period_key = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    next_value = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_number_sequence_counters", x => x.id);
                    table.CheckConstraint("ck_number_sequence_counters_next_value", "next_value >= 1");
                    table.ForeignKey(
                        name: "fk_number_sequence_counters_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_number_sequence_counters_school_id_key_period_key",
                table: "number_sequence_counters",
                columns: new[] { "school_id", "key", "period_key" },
                unique: true);

            // Carry each sequence's running counter over as the counter of the period it was last used in.
            migrationBuilder.Sql(
                """
                INSERT INTO number_sequence_counters (id, school_id, key, period_key, next_value, created_at)
                SELECT gen_random_uuid(), school_id, key, period_key, next_value, now()
                FROM number_sequences WHERE next_value > 1;
                """);

            // Payroll references gain a run number so several runs in a month (off-cycle, per campus) stay distinct.
            migrationBuilder.Sql(
                """
                UPDATE number_sequences SET pattern = '{prefix}-{yyyy}{mm}-{seq:2}', reset_policy = 'Monthly'
                WHERE key = 'payroll' AND pattern = '{prefix}-{yyyy}{mm}';
                """);

            migrationBuilder.DropColumn(
                name: "next_value",
                table: "number_sequences");

            migrationBuilder.DropColumn(
                name: "period_key",
                table: "number_sequences");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "next_value",
                table: "number_sequences",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<string>(
                name: "period_key",
                table: "number_sequences",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "");

            // Keep the most recent period's counter for each sequence.
            migrationBuilder.Sql(
                """
                UPDATE number_sequences s SET period_key = c.period_key, next_value = c.next_value
                FROM (SELECT DISTINCT ON (school_id, key) school_id, key, period_key, next_value
                      FROM number_sequence_counters ORDER BY school_id, key, period_key DESC) c
                WHERE s.school_id = c.school_id AND s.key = c.key;
                """);

            migrationBuilder.DropTable(
                name: "number_sequence_counters");
        }
    }
}
