using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaGestion.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClesEtrangeresParAgence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_assignments_contractors_contractor_id",
                table: "assignments");

            migrationBuilder.DropForeignKey(
                name: "fk_assignments_positions_position_id",
                table: "assignments");

            migrationBuilder.DropForeignKey(
                name: "fk_availabilities_contractors_contractor_id",
                table: "availabilities");

            migrationBuilder.DropForeignKey(
                name: "fk_contractor_skills_contractors_contractor_id",
                table: "contractor_skills");

            migrationBuilder.DropForeignKey(
                name: "fk_contractor_skills_skills_skill_id",
                table: "contractor_skills");

            migrationBuilder.DropForeignKey(
                name: "fk_contractors_users_user_id",
                table: "contractors");

            migrationBuilder.DropForeignKey(
                name: "fk_documents_contractors_contractor_id",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "fk_documents_users_reviewed_by_user_id",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "fk_invoice_lines_assignments_assignment_id",
                table: "invoice_lines");

            migrationBuilder.DropForeignKey(
                name: "fk_invoice_lines_invoices_invoice_id",
                table: "invoice_lines");

            migrationBuilder.DropForeignKey(
                name: "fk_invoices_contractors_contractor_id",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "fk_notifications_users_user_id",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "fk_positions_events_event_id",
                table: "positions");

            migrationBuilder.DropForeignKey(
                name: "fk_timesheets_assignments_assignment_id",
                table: "timesheets");

            migrationBuilder.DropForeignKey(
                name: "fk_timesheets_users_validated_by_user_id",
                table: "timesheets");

            migrationBuilder.DropIndex(
                name: "ix_timesheets_validated_by_user_id",
                table: "timesheets");

            migrationBuilder.DropIndex(
                name: "ix_notifications_user_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_invoice_lines_assignment_id",
                table: "invoice_lines");

            migrationBuilder.DropIndex(
                name: "ix_documents_contractor_id",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "ix_documents_reviewed_by_user_id",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "ix_contractor_skills_skill_id",
                table: "contractor_skills");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_users_id_agency_id",
                table: "users",
                columns: new[] { "id", "agency_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_timesheets_id_agency_id",
                table: "timesheets",
                columns: new[] { "id", "agency_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_skills_id_agency_id",
                table: "skills",
                columns: new[] { "id", "agency_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_positions_id_agency_id",
                table: "positions",
                columns: new[] { "id", "agency_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_notifications_id_agency_id",
                table: "notifications",
                columns: new[] { "id", "agency_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_invoices_id_agency_id",
                table: "invoices",
                columns: new[] { "id", "agency_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_invoice_lines_id_agency_id",
                table: "invoice_lines",
                columns: new[] { "id", "agency_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_events_id_agency_id",
                table: "events",
                columns: new[] { "id", "agency_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_documents_id_agency_id",
                table: "documents",
                columns: new[] { "id", "agency_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_contractors_id_agency_id",
                table: "contractors",
                columns: new[] { "id", "agency_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_contractor_skills_id_agency_id",
                table: "contractor_skills",
                columns: new[] { "id", "agency_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_availabilities_id_agency_id",
                table: "availabilities",
                columns: new[] { "id", "agency_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_assignments_id_agency_id",
                table: "assignments",
                columns: new[] { "id", "agency_id" });

            migrationBuilder.CreateIndex(
                name: "ix_timesheets_assignment_id_agency_id",
                table: "timesheets",
                columns: new[] { "assignment_id", "agency_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_timesheets_validated_by_user_id_agency_id",
                table: "timesheets",
                columns: new[] { "validated_by_user_id", "agency_id" });

            migrationBuilder.CreateIndex(
                name: "ix_positions_event_id_agency_id",
                table: "positions",
                columns: new[] { "event_id", "agency_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_agency_id",
                table: "notifications",
                columns: new[] { "user_id", "agency_id" });

            migrationBuilder.CreateIndex(
                name: "ix_invoices_contractor_id_agency_id",
                table: "invoices",
                columns: new[] { "contractor_id", "agency_id" });

            migrationBuilder.CreateIndex(
                name: "ix_invoice_lines_assignment_id_agency_id",
                table: "invoice_lines",
                columns: new[] { "assignment_id", "agency_id" });

            migrationBuilder.CreateIndex(
                name: "ix_invoice_lines_invoice_id_agency_id",
                table: "invoice_lines",
                columns: new[] { "invoice_id", "agency_id" });

            migrationBuilder.CreateIndex(
                name: "ix_documents_contractor_id_agency_id",
                table: "documents",
                columns: new[] { "contractor_id", "agency_id" });

            migrationBuilder.CreateIndex(
                name: "ix_documents_reviewed_by_user_id_agency_id",
                table: "documents",
                columns: new[] { "reviewed_by_user_id", "agency_id" });

            migrationBuilder.CreateIndex(
                name: "ix_contractors_user_id_agency_id",
                table: "contractors",
                columns: new[] { "user_id", "agency_id" });

            migrationBuilder.CreateIndex(
                name: "ix_contractor_skills_contractor_id_agency_id",
                table: "contractor_skills",
                columns: new[] { "contractor_id", "agency_id" });

            migrationBuilder.CreateIndex(
                name: "ix_contractor_skills_skill_id_agency_id",
                table: "contractor_skills",
                columns: new[] { "skill_id", "agency_id" });

            migrationBuilder.CreateIndex(
                name: "ix_availabilities_contractor_id_agency_id",
                table: "availabilities",
                columns: new[] { "contractor_id", "agency_id" });

            migrationBuilder.CreateIndex(
                name: "ix_assignments_contractor_id_agency_id",
                table: "assignments",
                columns: new[] { "contractor_id", "agency_id" });

            migrationBuilder.CreateIndex(
                name: "ix_assignments_position_id_agency_id",
                table: "assignments",
                columns: new[] { "position_id", "agency_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_assignments_contractors_contractor_id_agency_id",
                table: "assignments",
                columns: new[] { "contractor_id", "agency_id" },
                principalTable: "contractors",
                principalColumns: new[] { "id", "agency_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_assignments_positions_position_id_agency_id",
                table: "assignments",
                columns: new[] { "position_id", "agency_id" },
                principalTable: "positions",
                principalColumns: new[] { "id", "agency_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_availabilities_contractors_contractor_id_agency_id",
                table: "availabilities",
                columns: new[] { "contractor_id", "agency_id" },
                principalTable: "contractors",
                principalColumns: new[] { "id", "agency_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_contractor_skills_contractors_contractor_id_agency_id",
                table: "contractor_skills",
                columns: new[] { "contractor_id", "agency_id" },
                principalTable: "contractors",
                principalColumns: new[] { "id", "agency_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_contractor_skills_skills_skill_id_agency_id",
                table: "contractor_skills",
                columns: new[] { "skill_id", "agency_id" },
                principalTable: "skills",
                principalColumns: new[] { "id", "agency_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_contractors_users_user_id_agency_id",
                table: "contractors",
                columns: new[] { "user_id", "agency_id" },
                principalTable: "users",
                principalColumns: new[] { "id", "agency_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_documents_contractors_contractor_id_agency_id",
                table: "documents",
                columns: new[] { "contractor_id", "agency_id" },
                principalTable: "contractors",
                principalColumns: new[] { "id", "agency_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_documents_users_reviewed_by_user_id_agency_id",
                table: "documents",
                columns: new[] { "reviewed_by_user_id", "agency_id" },
                principalTable: "users",
                principalColumns: new[] { "id", "agency_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_invoice_lines_assignments_assignment_id_agency_id",
                table: "invoice_lines",
                columns: new[] { "assignment_id", "agency_id" },
                principalTable: "assignments",
                principalColumns: new[] { "id", "agency_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_invoice_lines_invoices_invoice_id_agency_id",
                table: "invoice_lines",
                columns: new[] { "invoice_id", "agency_id" },
                principalTable: "invoices",
                principalColumns: new[] { "id", "agency_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_invoices_contractors_contractor_id_agency_id",
                table: "invoices",
                columns: new[] { "contractor_id", "agency_id" },
                principalTable: "contractors",
                principalColumns: new[] { "id", "agency_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_users_user_id_agency_id",
                table: "notifications",
                columns: new[] { "user_id", "agency_id" },
                principalTable: "users",
                principalColumns: new[] { "id", "agency_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_positions_events_event_id_agency_id",
                table: "positions",
                columns: new[] { "event_id", "agency_id" },
                principalTable: "events",
                principalColumns: new[] { "id", "agency_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_timesheets_assignments_assignment_id_agency_id",
                table: "timesheets",
                columns: new[] { "assignment_id", "agency_id" },
                principalTable: "assignments",
                principalColumns: new[] { "id", "agency_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_timesheets_users_validated_by_user_id_agency_id",
                table: "timesheets",
                columns: new[] { "validated_by_user_id", "agency_id" },
                principalTable: "users",
                principalColumns: new[] { "id", "agency_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_assignments_contractors_contractor_id_agency_id",
                table: "assignments");

            migrationBuilder.DropForeignKey(
                name: "fk_assignments_positions_position_id_agency_id",
                table: "assignments");

            migrationBuilder.DropForeignKey(
                name: "fk_availabilities_contractors_contractor_id_agency_id",
                table: "availabilities");

            migrationBuilder.DropForeignKey(
                name: "fk_contractor_skills_contractors_contractor_id_agency_id",
                table: "contractor_skills");

            migrationBuilder.DropForeignKey(
                name: "fk_contractor_skills_skills_skill_id_agency_id",
                table: "contractor_skills");

            migrationBuilder.DropForeignKey(
                name: "fk_contractors_users_user_id_agency_id",
                table: "contractors");

            migrationBuilder.DropForeignKey(
                name: "fk_documents_contractors_contractor_id_agency_id",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "fk_documents_users_reviewed_by_user_id_agency_id",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "fk_invoice_lines_assignments_assignment_id_agency_id",
                table: "invoice_lines");

            migrationBuilder.DropForeignKey(
                name: "fk_invoice_lines_invoices_invoice_id_agency_id",
                table: "invoice_lines");

            migrationBuilder.DropForeignKey(
                name: "fk_invoices_contractors_contractor_id_agency_id",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "fk_notifications_users_user_id_agency_id",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "fk_positions_events_event_id_agency_id",
                table: "positions");

            migrationBuilder.DropForeignKey(
                name: "fk_timesheets_assignments_assignment_id_agency_id",
                table: "timesheets");

            migrationBuilder.DropForeignKey(
                name: "fk_timesheets_users_validated_by_user_id_agency_id",
                table: "timesheets");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_users_id_agency_id",
                table: "users");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_timesheets_id_agency_id",
                table: "timesheets");

            migrationBuilder.DropIndex(
                name: "ix_timesheets_assignment_id_agency_id",
                table: "timesheets");

            migrationBuilder.DropIndex(
                name: "ix_timesheets_validated_by_user_id_agency_id",
                table: "timesheets");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_skills_id_agency_id",
                table: "skills");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_positions_id_agency_id",
                table: "positions");

            migrationBuilder.DropIndex(
                name: "ix_positions_event_id_agency_id",
                table: "positions");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_notifications_id_agency_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_user_id_agency_id",
                table: "notifications");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_invoices_id_agency_id",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "ix_invoices_contractor_id_agency_id",
                table: "invoices");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_invoice_lines_id_agency_id",
                table: "invoice_lines");

            migrationBuilder.DropIndex(
                name: "ix_invoice_lines_assignment_id_agency_id",
                table: "invoice_lines");

            migrationBuilder.DropIndex(
                name: "ix_invoice_lines_invoice_id_agency_id",
                table: "invoice_lines");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_events_id_agency_id",
                table: "events");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_documents_id_agency_id",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "ix_documents_contractor_id_agency_id",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "ix_documents_reviewed_by_user_id_agency_id",
                table: "documents");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_contractors_id_agency_id",
                table: "contractors");

            migrationBuilder.DropIndex(
                name: "ix_contractors_user_id_agency_id",
                table: "contractors");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_contractor_skills_id_agency_id",
                table: "contractor_skills");

            migrationBuilder.DropIndex(
                name: "ix_contractor_skills_contractor_id_agency_id",
                table: "contractor_skills");

            migrationBuilder.DropIndex(
                name: "ix_contractor_skills_skill_id_agency_id",
                table: "contractor_skills");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_availabilities_id_agency_id",
                table: "availabilities");

            migrationBuilder.DropIndex(
                name: "ix_availabilities_contractor_id_agency_id",
                table: "availabilities");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_assignments_id_agency_id",
                table: "assignments");

            migrationBuilder.DropIndex(
                name: "ix_assignments_contractor_id_agency_id",
                table: "assignments");

            migrationBuilder.DropIndex(
                name: "ix_assignments_position_id_agency_id",
                table: "assignments");

            migrationBuilder.CreateIndex(
                name: "ix_timesheets_validated_by_user_id",
                table: "timesheets",
                column: "validated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id",
                table: "notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_invoice_lines_assignment_id",
                table: "invoice_lines",
                column: "assignment_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_contractor_id",
                table: "documents",
                column: "contractor_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_reviewed_by_user_id",
                table: "documents",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_contractor_skills_skill_id",
                table: "contractor_skills",
                column: "skill_id");

            migrationBuilder.AddForeignKey(
                name: "fk_assignments_contractors_contractor_id",
                table: "assignments",
                column: "contractor_id",
                principalTable: "contractors",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_assignments_positions_position_id",
                table: "assignments",
                column: "position_id",
                principalTable: "positions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_availabilities_contractors_contractor_id",
                table: "availabilities",
                column: "contractor_id",
                principalTable: "contractors",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_contractor_skills_contractors_contractor_id",
                table: "contractor_skills",
                column: "contractor_id",
                principalTable: "contractors",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_contractor_skills_skills_skill_id",
                table: "contractor_skills",
                column: "skill_id",
                principalTable: "skills",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_contractors_users_user_id",
                table: "contractors",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_documents_contractors_contractor_id",
                table: "documents",
                column: "contractor_id",
                principalTable: "contractors",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_documents_users_reviewed_by_user_id",
                table: "documents",
                column: "reviewed_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_invoice_lines_assignments_assignment_id",
                table: "invoice_lines",
                column: "assignment_id",
                principalTable: "assignments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_invoice_lines_invoices_invoice_id",
                table: "invoice_lines",
                column: "invoice_id",
                principalTable: "invoices",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_invoices_contractors_contractor_id",
                table: "invoices",
                column: "contractor_id",
                principalTable: "contractors",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_users_user_id",
                table: "notifications",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_positions_events_event_id",
                table: "positions",
                column: "event_id",
                principalTable: "events",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_timesheets_assignments_assignment_id",
                table: "timesheets",
                column: "assignment_id",
                principalTable: "assignments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_timesheets_users_validated_by_user_id",
                table: "timesheets",
                column: "validated_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
