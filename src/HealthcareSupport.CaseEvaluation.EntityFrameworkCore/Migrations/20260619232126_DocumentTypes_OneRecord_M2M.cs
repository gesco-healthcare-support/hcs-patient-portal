using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <summary>
    /// #4 (2026-06-19): inverts document types from one row per
    /// (name, appointment-type) to ONE record per name with a M2M join to
    /// appointment types, plus an explicit AppliesToAll flag.
    ///
    /// The Up runs a one-way DATA step (dedupe + repoint) while the old
    /// AppointmentTypeId column still exists: it picks a survivor per
    /// (TenantId, Name), rebuilds the offered-type set into the join, repoints
    /// every AppointmentDocument that referenced a now-collapsed duplicate to the
    /// survivor, then deletes the duplicates and drops the column. Down restores
    /// the SCHEMA only -- it cannot reconstruct which duplicate each document
    /// came from, so the dedupe/repoint is intentionally NOT reversed (back up
    /// the DB before applying).
    /// </summary>
    public partial class DocumentTypes_OneRecord_M2M : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. New AppliesToAll flag (default false) and the M2M join table.
            migrationBuilder.AddColumn<bool>(
                name: "AppliesToAll",
                table: "AppAppointmentDocumentTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AppAppointmentDocumentTypeAppointmentType",
                columns: table => new
                {
                    AppointmentDocumentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAppointmentDocumentTypeAppointmentType", x => new { x.AppointmentDocumentTypeId, x.AppointmentTypeId });
                    table.ForeignKey(
                        name: "FK_AppAppointmentDocumentTypeAppointmentType_AppAppointmentDocumentTypes_AppointmentDocumentTypeId",
                        column: x => x.AppointmentDocumentTypeId,
                        principalTable: "AppAppointmentDocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentDocumentTypeAppointmentType_AppointmentTypeId",
                table: "AppAppointmentDocumentTypeAppointmentType",
                column: "AppointmentTypeId");

            // 2. DATA STEP (one-way). Runs while AppointmentTypeId still exists.

            // 2a. Reserved system rows ("Generated Packet") had a null type
            // meaning "applies to all" -- make that explicit.
            migrationBuilder.Sql(@"
                UPDATE AppAppointmentDocumentTypes SET AppliesToAll = 1 WHERE IsSystem = 1;");

            // 2b. Rebuild the offered-type set: for every non-system row map it to
            // its (TenantId, Name) survivor and insert (survivor, type) into the
            // join (deduplicated). Survivor = earliest CreationTime, then lowest Id.
            migrationBuilder.Sql(@"
                WITH Ranked AS (
                    SELECT Id, AppointmentTypeId,
                        FIRST_VALUE(Id) OVER (
                            PARTITION BY TenantId, [Name]
                            ORDER BY CreationTime, Id
                            ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING) AS SurvivorId
                    FROM AppAppointmentDocumentTypes
                    WHERE IsSystem = 0 AND IsDeleted = 0
                )
                INSERT INTO AppAppointmentDocumentTypeAppointmentType (AppointmentDocumentTypeId, AppointmentTypeId)
                SELECT DISTINCT r.SurvivorId, r.AppointmentTypeId
                FROM Ranked r
                WHERE r.AppointmentTypeId IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM AppAppointmentDocumentTypeAppointmentType j
                      WHERE j.AppointmentDocumentTypeId = r.SurvivorId
                        AND j.AppointmentTypeId = r.AppointmentTypeId);");

            // 2c. Repoint every document that referenced a now-collapsed duplicate
            // to its survivor, so historical document labels are preserved.
            migrationBuilder.Sql(@"
                WITH Ranked AS (
                    SELECT Id,
                        FIRST_VALUE(Id) OVER (
                            PARTITION BY TenantId, [Name]
                            ORDER BY CreationTime, Id
                            ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING) AS SurvivorId
                    FROM AppAppointmentDocumentTypes
                    WHERE IsSystem = 0 AND IsDeleted = 0
                )
                UPDATE d
                SET d.AppointmentDocumentTypeId = r.SurvivorId
                FROM AppAppointmentDocuments d
                INNER JOIN Ranked r ON r.Id = d.AppointmentDocumentTypeId
                WHERE d.AppointmentDocumentTypeId <> r.SurvivorId;");

            // 2d. Delete the non-survivor duplicate rows (they hold no join rows
            // and are no longer referenced by any document).
            migrationBuilder.Sql(@"
                WITH Ranked AS (
                    SELECT Id,
                        ROW_NUMBER() OVER (
                            PARTITION BY TenantId, [Name]
                            ORDER BY CreationTime, Id) AS rn
                    FROM AppAppointmentDocumentTypes
                    WHERE IsSystem = 0 AND IsDeleted = 0
                )
                DELETE FROM AppAppointmentDocumentTypes
                WHERE Id IN (SELECT Id FROM Ranked WHERE rn > 1);");

            // 3. Retire the old per-type column + index; add the per-tenant-name index.
            migrationBuilder.DropIndex(
                name: "IX_AppAppointmentDocumentTypes_TenantId_AppointmentTypeId",
                table: "AppAppointmentDocumentTypes");

            migrationBuilder.DropColumn(
                name: "AppointmentTypeId",
                table: "AppAppointmentDocumentTypes");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentDocumentTypes_TenantId_Name",
                table: "AppAppointmentDocumentTypes",
                columns: new[] { "TenantId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Schema-only revert. The Up dedupe/repoint is one-way and is NOT
            // reconstructed here: the restored AppointmentTypeId column is left
            // null and the collapsed duplicate rows are not recreated.
            migrationBuilder.DropTable(
                name: "AppAppointmentDocumentTypeAppointmentType");

            migrationBuilder.DropIndex(
                name: "IX_AppAppointmentDocumentTypes_TenantId_Name",
                table: "AppAppointmentDocumentTypes");

            migrationBuilder.DropColumn(
                name: "AppliesToAll",
                table: "AppAppointmentDocumentTypes");

            migrationBuilder.AddColumn<Guid>(
                name: "AppointmentTypeId",
                table: "AppAppointmentDocumentTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentDocumentTypes_TenantId_AppointmentTypeId",
                table: "AppAppointmentDocumentTypes",
                columns: new[] { "TenantId", "AppointmentTypeId" });
        }
    }
}
