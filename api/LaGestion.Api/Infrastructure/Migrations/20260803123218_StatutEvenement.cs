using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaGestion.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StatutEvenement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancelled_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Draft");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "events");

            migrationBuilder.DropColumn(
                name: "status",
                table: "events");
        }
    }
}
