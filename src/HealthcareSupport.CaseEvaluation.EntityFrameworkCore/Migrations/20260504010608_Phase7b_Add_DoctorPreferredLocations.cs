using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Phase7b_Add_DoctorPreferredLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppDoctorPreferredLocations",
                columns: table => new
                {
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDoctorPreferredLocations", x => new { x.DoctorId, x.LocationId });
                    table.ForeignKey(
                        name: "FK_AppDoctorPreferredLocations_AppDoctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "AppDoctors",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppDoctorPreferredLocations_AppLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "AppLocations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppDoctorPreferredLocations_DoctorId_IsActive",
                table: "AppDoctorPreferredLocations",
                columns: new[] { "DoctorId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AppDoctorPreferredLocations_LocationId",
                table: "AppDoctorPreferredLocations",
                column: "LocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppDoctorPreferredLocations");
        }
    }
}
