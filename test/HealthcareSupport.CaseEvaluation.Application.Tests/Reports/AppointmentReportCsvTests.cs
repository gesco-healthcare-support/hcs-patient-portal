using System;
using System.Collections.Generic;
using System.Text;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Reports;

/// <summary>
/// G-08 (2026-06-15): the CSV export sibling of the PDF render. These assert the
/// header + a masked row, RFC-4180 escaping (so a comma/quote/newline in a name
/// cannot corrupt columns), and the UTF-8 BOM. Rows are already masked (the
/// builder never masks) -- synthetic data only.
/// </summary>
public class AppointmentReportCsvTests
{
    [Fact]
    public void Writes_header_and_a_masked_row()
    {
        var rows = new List<AppointmentReportRowDto>
        {
            new()
            {
                RequestConfirmationNumber = "A90001",
                AppointmentTypeName = "Panel QME",
                LocationName = "Downtown Clinic",
                AppointmentDate = new DateTime(2026, 6, 10, 9, 30, 0),
                AppointmentStatus = AppointmentStatusType.Approved,
                PatientName = "DOE, JANE",
                DateOfBirth = "1985",
                Email = "jane.doe@example.test",
                PhoneNumber = "555-0101",
                SocialSecurityNumber = "***-**-9012",
            },
        };

        var text = Decode(AppointmentReportCsv.Build(rows));
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        lines.Length.ShouldBe(2);
        lines[0].ShouldBe(
            "Confirmation No,Appointment Type,Location Name,Appointment Date Time,Status,Patient Name,Date Of Birth,Email,Phone Number,Social Security Number");
        // "DOE, JANE" carries a comma -> the field must be quoted so columns do not split.
        lines[1].ShouldContain("\"DOE, JANE\"");
        lines[1].ShouldContain("A90001");
        lines[1].ShouldContain("***-**-9012");
    }

    [Fact]
    public void Escapes_embedded_quotes_and_newlines()
    {
        var rows = new List<AppointmentReportRowDto>
        {
            new()
            {
                RequestConfirmationNumber = "A90002",
                AppointmentTypeName = "AME",
                LocationName = "Clinic \"North\"\nSuite 5",
                AppointmentDate = new DateTime(2026, 1, 2, 8, 0, 0),
                AppointmentStatus = AppointmentStatusType.Pending,
                PatientName = "SMITH, JOHN",
                DateOfBirth = "1990",
                Email = "j@x.test",
                PhoneNumber = "555-0202",
                SocialSecurityNumber = "***-**-1234",
            },
        };

        var text = Decode(AppointmentReportCsv.Build(rows));

        // Embedded quotes doubled, whole field wrapped; the newline stays inside the quotes.
        text.ShouldContain("\"Clinic \"\"North\"\"\nSuite 5\"");
    }

    [Fact]
    public void Emits_a_utf8_bom()
    {
        var bytes = AppointmentReportCsv.Build(new List<AppointmentReportRowDto>());

        bytes.Length.ShouldBeGreaterThanOrEqualTo(3);
        bytes[0].ShouldBe((byte)0xEF);
        bytes[1].ShouldBe((byte)0xBB);
        bytes[2].ShouldBe((byte)0xBF);
    }

    private static string Decode(byte[] bytes)
    {
        // Strip the 3-byte UTF-8 BOM for assertion convenience.
        return new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
    }
}
