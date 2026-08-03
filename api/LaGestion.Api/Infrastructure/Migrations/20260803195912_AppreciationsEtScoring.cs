using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaGestion.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AppreciationsEtScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "score",
                table: "contractors");

            migrationBuilder.CreateTable(
                name: "mission_ratings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    rated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mission_ratings", x => x.id);
                    table.UniqueConstraint("ak_mission_ratings_id_agency_id", x => new { x.id, x.agency_id });
                    table.ForeignKey(
                        name: "fk_mission_ratings_agencies_agency_id",
                        column: x => x.agency_id,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mission_ratings_assignments_assignment_id_agency_id",
                        columns: x => new { x.assignment_id, x.agency_id },
                        principalTable: "assignments",
                        principalColumns: new[] { "id", "agency_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mission_ratings_users_rated_by_user_id_agency_id",
                        columns: x => new { x.rated_by_user_id, x.agency_id },
                        principalTable: "users",
                        principalColumns: new[] { "id", "agency_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mission_ratings_agency_id",
                table: "mission_ratings",
                column: "agency_id");

            migrationBuilder.CreateIndex(
                name: "ix_mission_ratings_assignment_id",
                table: "mission_ratings",
                column: "assignment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mission_ratings_assignment_id_agency_id",
                table: "mission_ratings",
                columns: new[] { "assignment_id", "agency_id" });

            migrationBuilder.CreateIndex(
                name: "ix_mission_ratings_rated_by_user_id_agency_id",
                table: "mission_ratings",
                columns: new[] { "rated_by_user_id", "agency_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mission_ratings");

            migrationBuilder.AddColumn<decimal>(
                name: "score",
                table: "contractors",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: true);
        }
    }
}
