using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaGestion.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoffreADocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "content_type",
                table: "documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "original_file_name",
                table: "documents",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "review_note",
                table: "documents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "size_bytes",
                table: "documents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "content_type",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "original_file_name",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "review_note",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "size_bytes",
                table: "documents");
        }
    }
}
