using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaGestion.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DeclarationDHeures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "check_in_at",
                table: "timesheets");

            migrationBuilder.DropColumn(
                name: "check_out_at",
                table: "timesheets");

            migrationBuilder.AlterColumn<decimal>(
                name: "actual_hours",
                table: "timesheets",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,2)",
                oldPrecision: 6,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contractor_note",
                table: "timesheets",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_note",
                table: "timesheets",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contractor_note",
                table: "timesheets");

            migrationBuilder.DropColumn(
                name: "review_note",
                table: "timesheets");

            migrationBuilder.AlterColumn<decimal>(
                name: "actual_hours",
                table: "timesheets",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,2)",
                oldPrecision: 6,
                oldScale: 2);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "check_in_at",
                table: "timesheets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "check_out_at",
                table: "timesheets",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
