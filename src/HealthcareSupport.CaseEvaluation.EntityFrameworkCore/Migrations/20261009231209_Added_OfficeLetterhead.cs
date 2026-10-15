using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Added_OfficeLetterhead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Fax",
                table: "AppOfficeBrandings",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LetterheadName",
                table: "AppOfficeBrandings",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LetterheadTagline",
                table: "AppOfficeBrandings",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MailingCity",
                table: "AppOfficeBrandings",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MailingState",
                table: "AppOfficeBrandings",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MailingStreet",
                table: "AppOfficeBrandings",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MailingZip",
                table: "AppOfficeBrandings",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MissedAppointmentFee",
                table: "AppOfficeBrandings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "AppOfficeBrandings",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhysicianName",
                table: "AppOfficeBrandings",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PracticeName",
                table: "AppOfficeBrandings",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordsDeliveryAddress",
                table: "AppOfficeBrandings",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordsReleaseAddress",
                table: "AppOfficeBrandings",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            // Falkinstein's packets printed these values as literals in tools/packet-templates
            // until this change moved them onto the office. Writing them here keeps that one
            // practice's packets saying exactly what they said before the deploy, rather than
            // falling back to the derived "Dr. Yuri Falkinstein" letterhead until someone types
            // them in. They are the practice's public letterhead, already in this public repo's
            // history, not patient data. The two records addresses differ (Suite 510 vs STE. 130)
            // because the two documents differed; both were kept as printed, by decision.
            //
            // An UPDATE only: an office with no branding row resolves the derived defaults, which
            // is correct everywhere this migration runs except the one server that has the row.
            migrationBuilder.Sql(
                "UPDATE b SET "
                + "LetterheadName = N'Yuri Falkinstein, M.D., FAAOS', "
                + "LetterheadTagline = N'FELLOW, AMERICAN ACADEMY OF ORTHOPAEDIC SURGEONS', "
                + "PhysicianName = N'Yuri Falkinstein, M.D.', "
                + "PracticeName = N'West Coast Spine Institute', "
                + "MailingStreet = N'P.O. Box 261656', MailingCity = N'Encino', "
                + "MailingState = N'CA', MailingZip = N'91426', "
                + "Phone = N'(818) 582-2600', Fax = N'(818) 855-2466', "
                + "RecordsDeliveryAddress = N'16530 Ventura Blvd., Suite 510, Encino, CA 91436', "
                + "RecordsReleaseAddress = N'16530 VENTURA BLVD.,' + CHAR(10) + N'STE. 130' + CHAR(10) + N'ENCINO, CA 91436', "
                + "MissedAppointmentFee = 503.75 "
                + "FROM AppOfficeBrandings b INNER JOIN SaasTenants t ON t.Id = b.OfficeId "
                + "WHERE LOWER(t.Name) = N'falkinstein' AND b.IsDeleted = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Fax",
                table: "AppOfficeBrandings");

            migrationBuilder.DropColumn(
                name: "LetterheadName",
                table: "AppOfficeBrandings");

            migrationBuilder.DropColumn(
                name: "LetterheadTagline",
                table: "AppOfficeBrandings");

            migrationBuilder.DropColumn(
                name: "MailingCity",
                table: "AppOfficeBrandings");

            migrationBuilder.DropColumn(
                name: "MailingState",
                table: "AppOfficeBrandings");

            migrationBuilder.DropColumn(
                name: "MailingStreet",
                table: "AppOfficeBrandings");

            migrationBuilder.DropColumn(
                name: "MailingZip",
                table: "AppOfficeBrandings");

            migrationBuilder.DropColumn(
                name: "MissedAppointmentFee",
                table: "AppOfficeBrandings");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "AppOfficeBrandings");

            migrationBuilder.DropColumn(
                name: "PhysicianName",
                table: "AppOfficeBrandings");

            migrationBuilder.DropColumn(
                name: "PracticeName",
                table: "AppOfficeBrandings");

            migrationBuilder.DropColumn(
                name: "RecordsDeliveryAddress",
                table: "AppOfficeBrandings");

            migrationBuilder.DropColumn(
                name: "RecordsReleaseAddress",
                table: "AppOfficeBrandings");
        }
    }
}
