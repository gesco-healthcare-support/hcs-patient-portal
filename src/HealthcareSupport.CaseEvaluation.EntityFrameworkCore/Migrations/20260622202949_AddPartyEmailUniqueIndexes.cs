using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyEmailUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AppDefenseAttorneys_TenantId_Email",
                table: "AppDefenseAttorneys",
                columns: new[] { "TenantId", "Email" },
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppClaimExaminers_TenantId_Email",
                table: "AppClaimExaminers",
                columns: new[] { "TenantId", "Email" },
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppApplicantAttorneys_TenantId_Email",
                table: "AppApplicantAttorneys",
                columns: new[] { "TenantId", "Email" },
                unique: true,
                filter: "[Email] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppDefenseAttorneys_TenantId_Email",
                table: "AppDefenseAttorneys");

            migrationBuilder.DropIndex(
                name: "IX_AppClaimExaminers_TenantId_Email",
                table: "AppClaimExaminers");

            migrationBuilder.DropIndex(
                name: "IX_AppApplicantAttorneys_TenantId_Email",
                table: "AppApplicantAttorneys");
        }
    }
}
