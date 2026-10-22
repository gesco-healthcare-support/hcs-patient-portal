using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Added_AppointmentPacketVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAppointmentPacketVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppointmentPacketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    BlobName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FileGeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReplacedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAppointmentPacketVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAppointmentPacketVersions_AppAppointmentPackets_AppointmentPacketId",
                        column: x => x.AppointmentPacketId,
                        principalTable: "AppAppointmentPackets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentPacketVersions_AppointmentId",
                table: "AppAppointmentPacketVersions",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentPacketVersions_AppointmentPacketId",
                table: "AppAppointmentPacketVersions",
                column: "AppointmentPacketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAppointmentPacketVersions");
        }
    }
}
