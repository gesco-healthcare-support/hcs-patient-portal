using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class I3_LocationAppointmentTypesM2M : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppLocations_AppAppointmentTypes_AppointmentTypeId",
                table: "AppLocations");

            migrationBuilder.DropIndex(
                name: "IX_AppLocations_AppointmentTypeId",
                table: "AppLocations");

            migrationBuilder.DropColumn(
                name: "AppointmentTypeId",
                table: "AppLocations");

            migrationBuilder.CreateTable(
                name: "AppLocationAppointmentType",
                columns: table => new
                {
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLocationAppointmentType", x => new { x.LocationId, x.AppointmentTypeId });
                    table.ForeignKey(
                        name: "FK_AppLocationAppointmentType_AppAppointmentTypes_AppointmentTypeId",
                        column: x => x.AppointmentTypeId,
                        principalTable: "AppAppointmentTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppLocationAppointmentType_AppLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "AppLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppLocationAppointmentType_AppointmentTypeId",
                table: "AppLocationAppointmentType",
                column: "AppointmentTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppLocationAppointmentType");

            migrationBuilder.AddColumn<Guid>(
                name: "AppointmentTypeId",
                table: "AppLocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLocations_AppointmentTypeId",
                table: "AppLocations",
                column: "AppointmentTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppLocations_AppAppointmentTypes_AppointmentTypeId",
                table: "AppLocations",
                column: "AppointmentTypeId",
                principalTable: "AppAppointmentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
