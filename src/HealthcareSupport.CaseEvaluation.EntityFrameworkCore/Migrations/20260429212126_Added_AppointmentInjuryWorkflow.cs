using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Added_AppointmentInjuryWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAppointmentInjuryDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateOfInjury = table.Column<DateTime>(type: "date", nullable: false),
                    ToDateOfInjury = table.Column<DateTime>(type: "date", nullable: true),
                    ClaimNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsCumulativeInjury = table.Column<bool>(type: "bit", nullable: false),
                    WcabAdj = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BodyPartsSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    WcabOfficeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AppAppointmentInjuryDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAppointmentInjuryDetails_AppAppointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "AppAppointments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppAppointmentInjuryDetails_AppWcabOffices_WcabOfficeId",
                        column: x => x.WcabOfficeId,
                        principalTable: "AppWcabOffices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppAppointmentBodyParts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppointmentInjuryDetailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BodyPartDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_AppAppointmentBodyParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAppointmentBodyParts_AppAppointmentInjuryDetails_AppointmentInjuryDetailId",
                        column: x => x.AppointmentInjuryDetailId,
                        principalTable: "AppAppointmentInjuryDetails",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppAppointmentClaimExaminers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppointmentInjuryDetailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ClaimExaminerNumber = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Fax = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Street = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Zip = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    StateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AppAppointmentClaimExaminers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAppointmentClaimExaminers_AppAppointmentInjuryDetails_AppointmentInjuryDetailId",
                        column: x => x.AppointmentInjuryDetailId,
                        principalTable: "AppAppointmentInjuryDetails",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppAppointmentClaimExaminers_AppStates_StateId",
                        column: x => x.StateId,
                        principalTable: "AppStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AppAppointmentPrimaryInsurances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppointmentInjuryDetailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InsuranceNumber = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Attention = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    FaxNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Street = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    City = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Zip = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    StateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AppAppointmentPrimaryInsurances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAppointmentPrimaryInsurances_AppAppointmentInjuryDetails_AppointmentInjuryDetailId",
                        column: x => x.AppointmentInjuryDetailId,
                        principalTable: "AppAppointmentInjuryDetails",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppAppointmentPrimaryInsurances_AppStates_StateId",
                        column: x => x.StateId,
                        principalTable: "AppStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentBodyParts_AppointmentInjuryDetailId",
                table: "AppAppointmentBodyParts",
                column: "AppointmentInjuryDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentClaimExaminers_AppointmentInjuryDetailId",
                table: "AppAppointmentClaimExaminers",
                column: "AppointmentInjuryDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentClaimExaminers_StateId",
                table: "AppAppointmentClaimExaminers",
                column: "StateId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentInjuryDetails_AppointmentId",
                table: "AppAppointmentInjuryDetails",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentInjuryDetails_WcabOfficeId",
                table: "AppAppointmentInjuryDetails",
                column: "WcabOfficeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentPrimaryInsurances_AppointmentInjuryDetailId",
                table: "AppAppointmentPrimaryInsurances",
                column: "AppointmentInjuryDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentPrimaryInsurances_StateId",
                table: "AppAppointmentPrimaryInsurances",
                column: "StateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAppointmentBodyParts");

            migrationBuilder.DropTable(
                name: "AppAppointmentClaimExaminers");

            migrationBuilder.DropTable(
                name: "AppAppointmentPrimaryInsurances");

            migrationBuilder.DropTable(
                name: "AppAppointmentInjuryDetails");
        }
    }
}
