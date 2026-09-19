using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Fix_UniqueIndexesExcludeSoftDeleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppSystemParameters_TenantId",
                table: "AppSystemParameters");

            migrationBuilder.DropIndex(
                name: "IX_AppEntity_OfficeBrandings_Office",
                table: "AppOfficeBrandings");

            migrationBuilder.DropIndex(
                name: "IX_AppNotificationTemplates_TenantId_TemplateCode",
                table: "AppNotificationTemplates");

            migrationBuilder.DropIndex(
                name: "IX_AppLocations_FacilityId",
                table: "AppLocations");

            migrationBuilder.DropIndex(
                name: "IX_AppInvitations_TokenHash",
                table: "AppInvitations");

            migrationBuilder.DropIndex(
                name: "IX_AppEntity_IntakeOfficeAssignments_Operator_Office",
                table: "AppIntakeOfficeAssignments");

            migrationBuilder.DropIndex(
                name: "IX_AppDefenseAttorneys_TenantId_Email",
                table: "AppDefenseAttorneys");

            migrationBuilder.DropIndex(
                name: "IX_AppClaimExaminers_TenantId_Email",
                table: "AppClaimExaminers");

            migrationBuilder.DropIndex(
                name: "IX_AppAppointmentTypeFieldConfigs_TenantId_AppointmentTypeId_FieldName",
                table: "AppAppointmentTypeFieldConfigs");

            migrationBuilder.DropIndex(
                name: "IX_AppEntity_Appointments_TenantId_RequestConfirmationNumber",
                table: "AppAppointments");

            migrationBuilder.DropIndex(
                name: "IX_AppApplicantAttorneys_TenantId_Email",
                table: "AppApplicantAttorneys");

            migrationBuilder.CreateIndex(
                name: "IX_AppSystemParameters_TenantId",
                table: "AppSystemParameters",
                column: "TenantId",
                unique: true,
                filter: "[TenantId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AppEntity_OfficeBrandings_Office",
                table: "AppOfficeBrandings",
                column: "OfficeId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AppNotificationTemplates_TenantId_TemplateCode",
                table: "AppNotificationTemplates",
                columns: new[] { "TenantId", "TemplateCode" },
                unique: true,
                filter: "[TenantId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AppLocations_FacilityId",
                table: "AppLocations",
                column: "FacilityId",
                unique: true,
                filter: "[FacilityId] <> '' AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AppInvitations_TokenHash",
                table: "AppInvitations",
                column: "TokenHash",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AppEntity_IntakeOfficeAssignments_Operator_Office",
                table: "AppIntakeOfficeAssignments",
                columns: new[] { "OperatorUserId", "OfficeId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AppDefenseAttorneys_TenantId_Email",
                table: "AppDefenseAttorneys",
                columns: new[] { "TenantId", "Email" },
                unique: true,
                filter: "[Email] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AppClaimExaminers_TenantId_Email",
                table: "AppClaimExaminers",
                columns: new[] { "TenantId", "Email" },
                unique: true,
                filter: "[Email] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentTypeFieldConfigs_TenantId_AppointmentTypeId_FieldName",
                table: "AppAppointmentTypeFieldConfigs",
                columns: new[] { "TenantId", "AppointmentTypeId", "FieldName" },
                unique: true,
                filter: "[TenantId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AppEntity_Appointments_TenantId_RequestConfirmationNumber",
                table: "AppAppointments",
                columns: new[] { "TenantId", "RequestConfirmationNumber" },
                unique: true,
                filter: "[TenantId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AppApplicantAttorneys_TenantId_Email",
                table: "AppApplicantAttorneys",
                columns: new[] { "TenantId", "Email" },
                unique: true,
                filter: "[Email] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppSystemParameters_TenantId",
                table: "AppSystemParameters");

            migrationBuilder.DropIndex(
                name: "IX_AppEntity_OfficeBrandings_Office",
                table: "AppOfficeBrandings");

            migrationBuilder.DropIndex(
                name: "IX_AppNotificationTemplates_TenantId_TemplateCode",
                table: "AppNotificationTemplates");

            migrationBuilder.DropIndex(
                name: "IX_AppLocations_FacilityId",
                table: "AppLocations");

            migrationBuilder.DropIndex(
                name: "IX_AppInvitations_TokenHash",
                table: "AppInvitations");

            migrationBuilder.DropIndex(
                name: "IX_AppEntity_IntakeOfficeAssignments_Operator_Office",
                table: "AppIntakeOfficeAssignments");

            migrationBuilder.DropIndex(
                name: "IX_AppDefenseAttorneys_TenantId_Email",
                table: "AppDefenseAttorneys");

            migrationBuilder.DropIndex(
                name: "IX_AppClaimExaminers_TenantId_Email",
                table: "AppClaimExaminers");

            migrationBuilder.DropIndex(
                name: "IX_AppAppointmentTypeFieldConfigs_TenantId_AppointmentTypeId_FieldName",
                table: "AppAppointmentTypeFieldConfigs");

            migrationBuilder.DropIndex(
                name: "IX_AppEntity_Appointments_TenantId_RequestConfirmationNumber",
                table: "AppAppointments");

            migrationBuilder.DropIndex(
                name: "IX_AppApplicantAttorneys_TenantId_Email",
                table: "AppApplicantAttorneys");

            migrationBuilder.CreateIndex(
                name: "IX_AppSystemParameters_TenantId",
                table: "AppSystemParameters",
                column: "TenantId",
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppEntity_OfficeBrandings_Office",
                table: "AppOfficeBrandings",
                column: "OfficeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppNotificationTemplates_TenantId_TemplateCode",
                table: "AppNotificationTemplates",
                columns: new[] { "TenantId", "TemplateCode" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppLocations_FacilityId",
                table: "AppLocations",
                column: "FacilityId",
                unique: true,
                filter: "[FacilityId] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_AppInvitations_TokenHash",
                table: "AppInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppEntity_IntakeOfficeAssignments_Operator_Office",
                table: "AppIntakeOfficeAssignments",
                columns: new[] { "OperatorUserId", "OfficeId" },
                unique: true);

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
                name: "IX_AppAppointmentTypeFieldConfigs_TenantId_AppointmentTypeId_FieldName",
                table: "AppAppointmentTypeFieldConfigs",
                columns: new[] { "TenantId", "AppointmentTypeId", "FieldName" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppEntity_Appointments_TenantId_RequestConfirmationNumber",
                table: "AppAppointments",
                columns: new[] { "TenantId", "RequestConfirmationNumber" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppApplicantAttorneys_TenantId_Email",
                table: "AppApplicantAttorneys",
                columns: new[] { "TenantId", "Email" },
                unique: true,
                filter: "[Email] IS NOT NULL");
        }
    }
}
