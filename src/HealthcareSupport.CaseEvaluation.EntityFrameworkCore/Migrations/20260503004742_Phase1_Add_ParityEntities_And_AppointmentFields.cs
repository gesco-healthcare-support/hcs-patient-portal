using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_Add_ParityEntities_And_AppointmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "AppAppointments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledById",
                table: "AppAppointments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBeyondLimit",
                table: "AppAppointments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalAppointmentId",
                table: "AppAppointments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryResponsibleUserId",
                table: "AppAppointments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReScheduleReason",
                table: "AppAppointments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReScheduledById",
                table: "AppAppointments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedById",
                table: "AppAppointments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionNotes",
                table: "AppAppointments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdHoc",
                table: "AppAppointmentDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsJointDeclaration",
                table: "AppAppointmentDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "VerificationCode",
                table: "AppAppointmentDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppAppointmentChangeRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeRequestType = table.Column<int>(type: "int", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReScheduleReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NewDoctorAvailabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestStatus = table.Column<int>(type: "int", nullable: false),
                    RejectionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RejectedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdminReScheduleReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AdminOverrideSlotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsBeyondLimit = table.Column<bool>(type: "bit", nullable: false),
                    CancellationOutcome = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_AppAppointmentChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAppointmentChangeRequests_AppAppointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "AppAppointments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BlobName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_AppDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppNotificationTemplateTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_AppNotificationTemplateTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPackageDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PackageName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AppointmentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AppPackageDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSystemParameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppointmentLeadTime = table.Column<int>(type: "int", nullable: false),
                    AppointmentMaxTimePQME = table.Column<int>(type: "int", nullable: false),
                    AppointmentMaxTimeAME = table.Column<int>(type: "int", nullable: false),
                    AppointmentMaxTimeOTHER = table.Column<int>(type: "int", nullable: false),
                    AppointmentCancelTime = table.Column<int>(type: "int", nullable: false),
                    AppointmentDueDays = table.Column<int>(type: "int", nullable: false),
                    AppointmentDurationTime = table.Column<int>(type: "int", nullable: false),
                    AutoCancelCutoffTime = table.Column<int>(type: "int", nullable: false),
                    JointDeclarationUploadCutoffDays = table.Column<int>(type: "int", nullable: false),
                    PendingAppointmentOverDueNotificationDays = table.Column<int>(type: "int", nullable: false),
                    ReminderCutoffTime = table.Column<int>(type: "int", nullable: false),
                    IsCustomField = table.Column<bool>(type: "bit", nullable: false),
                    CcEmailIds = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AppSystemParameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAppointmentChangeRequestDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppointmentChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BlobName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_AppAppointmentChangeRequestDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAppointmentChangeRequestDocuments_AppAppointmentChangeRequests_AppointmentChangeRequestId",
                        column: x => x.AppointmentChangeRequestId,
                        principalTable: "AppAppointmentChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppNotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TemplateCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TemplateTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BodyEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodySms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_AppNotificationTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppNotificationTemplates_AppNotificationTemplateTypes_TemplateTypeId",
                        column: x => x.TemplateTypeId,
                        principalTable: "AppNotificationTemplateTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppDocumentPackages",
                columns: table => new
                {
                    PackageDetailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDocumentPackages", x => new { x.PackageDetailId, x.DocumentId });
                    table.ForeignKey(
                        name: "FK_AppDocumentPackages_AppDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "AppDocuments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppDocumentPackages_AppPackageDetails_PackageDetailId",
                        column: x => x.PackageDetailId,
                        principalTable: "AppPackageDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentDocuments_VerificationCode",
                table: "AppAppointmentDocuments",
                column: "VerificationCode");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentChangeRequestDocuments_AppointmentChangeRequestId",
                table: "AppAppointmentChangeRequestDocuments",
                column: "AppointmentChangeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentChangeRequests_AppointmentId",
                table: "AppAppointmentChangeRequests",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentChangeRequests_AppointmentId_RequestStatus",
                table: "AppAppointmentChangeRequests",
                columns: new[] { "AppointmentId", "RequestStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentPackages_DocumentId",
                table: "AppDocumentPackages",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppNotificationTemplates_TemplateTypeId",
                table: "AppNotificationTemplates",
                column: "TemplateTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppNotificationTemplates_TenantId_TemplateCode",
                table: "AppNotificationTemplates",
                columns: new[] { "TenantId", "TemplateCode" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppSystemParameters_TenantId",
                table: "AppSystemParameters",
                column: "TenantId",
                unique: true,
                filter: "[TenantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAppointmentChangeRequestDocuments");

            migrationBuilder.DropTable(
                name: "AppDocumentPackages");

            migrationBuilder.DropTable(
                name: "AppNotificationTemplates");

            migrationBuilder.DropTable(
                name: "AppSystemParameters");

            migrationBuilder.DropTable(
                name: "AppAppointmentChangeRequests");

            migrationBuilder.DropTable(
                name: "AppDocuments");

            migrationBuilder.DropTable(
                name: "AppPackageDetails");

            migrationBuilder.DropTable(
                name: "AppNotificationTemplateTypes");

            migrationBuilder.DropIndex(
                name: "IX_AppAppointmentDocuments_VerificationCode",
                table: "AppAppointmentDocuments");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "CancelledById",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "IsBeyondLimit",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "OriginalAppointmentId",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "PrimaryResponsibleUserId",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "ReScheduleReason",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "ReScheduledById",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "RejectedById",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "RejectionNotes",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "IsAdHoc",
                table: "AppAppointmentDocuments");

            migrationBuilder.DropColumn(
                name: "IsJointDeclaration",
                table: "AppAppointmentDocuments");

            migrationBuilder.DropColumn(
                name: "VerificationCode",
                table: "AppAppointmentDocuments");
        }
    }
}
