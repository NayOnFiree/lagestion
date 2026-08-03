using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaGestion.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Notifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "attempts",
                table: "notifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "dedup_key",
                table: "notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_error",
                table: "notifications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recipient",
                table: "notifications",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_agency_id_dedup_key",
                table: "notifications",
                columns: new[] { "agency_id", "dedup_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notifications_agency_id_dedup_key",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "attempts",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "dedup_key",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "last_error",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "recipient",
                table: "notifications");
        }
    }
}
