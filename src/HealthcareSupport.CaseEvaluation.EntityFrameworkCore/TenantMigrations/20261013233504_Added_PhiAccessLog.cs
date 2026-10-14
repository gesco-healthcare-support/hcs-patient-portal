using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.TenantMigrations
{
    /// <inheritdoc />
    public partial class Added_PhiAccessLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppPhiAccessLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ImpersonatorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<int>(type: "int", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccessedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPhiAccessLog", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppPhiAccessLog_AccessedAt",
                table: "AppPhiAccessLog",
                column: "AccessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppPhiAccessLog_AppointmentId_AccessedAt",
                table: "AppPhiAccessLog",
                columns: new[] { "AppointmentId", "AccessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppPhiAccessLog_PatientId_AccessedAt",
                table: "AppPhiAccessLog",
                columns: new[] { "PatientId", "AccessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppPhiAccessLog_UserId_AccessedAt",
                table: "AppPhiAccessLog",
                columns: new[] { "UserId", "AccessedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppPhiAccessLog");
        }
    }
}
