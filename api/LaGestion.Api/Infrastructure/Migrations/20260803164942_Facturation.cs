using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaGestion.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Facturation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "sequence_index",
                table: "invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "number",
                table: "invoices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "issued_at",
                table: "invoices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "client_address",
                table: "invoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "client_name",
                table: "invoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "client_siret",
                table: "invoices",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "issuer_address",
                table: "invoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "issuer_name",
                table: "invoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "issuer_siret",
                table: "invoices",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "paid_at",
                table: "invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "submitted_at",
                table: "invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_prefix",
                table: "contractors",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "next_invoice_sequence",
                table: "contractors",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "ix_invoices_contractor_id_number",
                table: "invoices",
                columns: new[] { "contractor_id", "number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_invoices_contractor_id_number",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "client_address",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "client_name",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "client_siret",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "issuer_address",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "issuer_name",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "issuer_siret",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "paid_at",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "submitted_at",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "invoice_prefix",
                table: "contractors");

            migrationBuilder.DropColumn(
                name: "next_invoice_sequence",
                table: "contractors");

            migrationBuilder.AlterColumn<int>(
                name: "sequence_index",
                table: "invoices",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "number",
                table: "invoices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "issued_at",
                table: "invoices",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");
        }
    }
}
