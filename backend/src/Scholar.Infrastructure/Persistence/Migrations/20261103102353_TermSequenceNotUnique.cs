using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scholar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TermSequenceNotUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_academic_terms_academic_year_id_sequence",
                table: "academic_terms");

            migrationBuilder.CreateIndex(
                name: "ix_academic_terms_academic_year_id_sequence",
                table: "academic_terms",
                columns: new[] { "academic_year_id", "sequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_academic_terms_academic_year_id_sequence",
                table: "academic_terms");

            migrationBuilder.CreateIndex(
                name: "ix_academic_terms_academic_year_id_sequence",
                table: "academic_terms",
                columns: new[] { "academic_year_id", "sequence" },
                unique: true);
        }
    }
}
