using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Move_InsuranceAndCe_To_Appointment_Drop_Attention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppAppointmentClaimExaminers_AppAppointmentInjuryDetails_AppointmentInjuryDetailId",
                table: "AppAppointmentClaimExaminers");

            migrationBuilder.DropForeignKey(
                name: "FK_AppAppointmentPrimaryInsurances_AppAppointmentInjuryDetails_AppointmentInjuryDetailId",
                table: "AppAppointmentPrimaryInsurances");

            migrationBuilder.DropColumn(
                name: "Attention",
                table: "AppAppointmentPrimaryInsurances");

            migrationBuilder.RenameColumn(
                name: "AppointmentInjuryDetailId",
                table: "AppAppointmentPrimaryInsurances",
                newName: "AppointmentId");

            migrationBuilder.RenameIndex(
                name: "IX_AppAppointmentPrimaryInsurances_AppointmentInjuryDetailId",
                table: "AppAppointmentPrimaryInsurances",
                newName: "IX_AppAppointmentPrimaryInsurances_AppointmentId");

            migrationBuilder.RenameColumn(
                name: "AppointmentInjuryDetailId",
                table: "AppAppointmentClaimExaminers",
                newName: "AppointmentId");

            migrationBuilder.RenameIndex(
                name: "IX_AppAppointmentClaimExaminers_AppointmentInjuryDetailId",
                table: "AppAppointmentClaimExaminers",
                newName: "IX_AppAppointmentClaimExaminers_AppointmentId");

            // CI1 (2026-06-05): the renamed AppointmentId column still holds the
            // OLD AppointmentInjuryDetailId values. Remap each row to its injury's
            // owning AppointmentId BEFORE the new FK to AppAppointments is added,
            // so the constraint validates. Inner join: rows whose injury is
            // missing are left untouched (none expected -- deletes are soft).
            migrationBuilder.Sql(@"
                UPDATE ce SET AppointmentId = idet.AppointmentId
                FROM AppAppointmentClaimExaminers AS ce
                INNER JOIN AppAppointmentInjuryDetails AS idet ON ce.AppointmentId = idet.Id;");
            migrationBuilder.Sql(@"
                UPDATE pi SET AppointmentId = idet.AppointmentId
                FROM AppAppointmentPrimaryInsurances AS pi
                INNER JOIN AppAppointmentInjuryDetails AS idet ON pi.AppointmentId = idet.Id;");

            migrationBuilder.AddForeignKey(
                name: "FK_AppAppointmentClaimExaminers_AppAppointments_AppointmentId",
                table: "AppAppointmentClaimExaminers",
                column: "AppointmentId",
                principalTable: "AppAppointments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppAppointmentPrimaryInsurances_AppAppointments_AppointmentId",
                table: "AppAppointmentPrimaryInsurances",
                column: "AppointmentId",
                principalTable: "AppAppointments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppAppointmentClaimExaminers_AppAppointments_AppointmentId",
                table: "AppAppointmentClaimExaminers");

            migrationBuilder.DropForeignKey(
                name: "FK_AppAppointmentPrimaryInsurances_AppAppointments_AppointmentId",
                table: "AppAppointmentPrimaryInsurances");

            migrationBuilder.RenameColumn(
                name: "AppointmentId",
                table: "AppAppointmentPrimaryInsurances",
                newName: "AppointmentInjuryDetailId");

            migrationBuilder.RenameIndex(
                name: "IX_AppAppointmentPrimaryInsurances_AppointmentId",
                table: "AppAppointmentPrimaryInsurances",
                newName: "IX_AppAppointmentPrimaryInsurances_AppointmentInjuryDetailId");

            migrationBuilder.RenameColumn(
                name: "AppointmentId",
                table: "AppAppointmentClaimExaminers",
                newName: "AppointmentInjuryDetailId");

            migrationBuilder.RenameIndex(
                name: "IX_AppAppointmentClaimExaminers_AppointmentId",
                table: "AppAppointmentClaimExaminers",
                newName: "IX_AppAppointmentClaimExaminers_AppointmentInjuryDetailId");

            migrationBuilder.AddColumn<string>(
                name: "Attention",
                table: "AppAppointmentPrimaryInsurances",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            // CI1 rollback (best-effort): the column (renamed back to
            // AppointmentInjuryDetailId) now holds appointment ids; remap each to
            // a representative injury id for that appointment so the restored
            // per-injury FK validates. The exact original per-injury association
            // cannot be recovered (CI1 collapsed N injuries -> 1 CE/insurance).
            migrationBuilder.Sql(@"
                UPDATE ce SET AppointmentInjuryDetailId =
                    (SELECT TOP 1 idet.Id FROM AppAppointmentInjuryDetails AS idet
                     WHERE idet.AppointmentId = ce.AppointmentInjuryDetailId)
                FROM AppAppointmentClaimExaminers AS ce;");
            migrationBuilder.Sql(@"
                UPDATE pi SET AppointmentInjuryDetailId =
                    (SELECT TOP 1 idet.Id FROM AppAppointmentInjuryDetails AS idet
                     WHERE idet.AppointmentId = pi.AppointmentInjuryDetailId)
                FROM AppAppointmentPrimaryInsurances AS pi;");

            migrationBuilder.AddForeignKey(
                name: "FK_AppAppointmentClaimExaminers_AppAppointmentInjuryDetails_AppointmentInjuryDetailId",
                table: "AppAppointmentClaimExaminers",
                column: "AppointmentInjuryDetailId",
                principalTable: "AppAppointmentInjuryDetails",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppAppointmentPrimaryInsurances_AppAppointmentInjuryDetails_AppointmentInjuryDetailId",
                table: "AppAppointmentPrimaryInsurances",
                column: "AppointmentInjuryDetailId",
                principalTable: "AppAppointmentInjuryDetails",
                principalColumn: "Id");
        }
    }
}
