using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaGestion.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResollicitationSurUnPoste : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_assignments_position_id_contractor_id",
                table: "assignments");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_position_id_contractor_id",
                table: "assignments",
                columns: new[] { "position_id", "contractor_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_assignments_position_id_contractor_id",
                table: "assignments");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_position_id_contractor_id",
                table: "assignments",
                columns: new[] { "position_id", "contractor_id" },
                unique: true);
        }
    }
}
