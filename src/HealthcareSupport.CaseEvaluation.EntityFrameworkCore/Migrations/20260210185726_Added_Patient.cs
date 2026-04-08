using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Added_Patient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppPatients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GenderId = table.Column<int>(type: "int", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SocialSecurityNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ZipCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    RefferedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CellPhoneNumber = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    PhoneNumberTypeId = table.Column<int>(type: "int", nullable: false),
                    Street = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    InterpreterVendorName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ApptNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OthersLanguageName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppointmentLanguageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdentityUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_AppPatients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppPatients_AbpUsers_IdentityUserId",
                        column: x => x.IdentityUserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppPatients_AppAppointmentLanguages_AppointmentLanguageId",
                        column: x => x.AppointmentLanguageId,
                        principalTable: "AppAppointmentLanguages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AppPatients_AppStates_StateId",
                        column: x => x.StateId,
                        principalTable: "AppStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AppPatients_SaasTenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "SaasTenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppPatients_AppointmentLanguageId",
                table: "AppPatients",
                column: "AppointmentLanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPatients_IdentityUserId",
                table: "AppPatients",
                column: "IdentityUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPatients_StateId",
                table: "AppPatients",
                column: "StateId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPatients_TenantId",
                table: "AppPatients",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppPatients");
        }
    }
}
