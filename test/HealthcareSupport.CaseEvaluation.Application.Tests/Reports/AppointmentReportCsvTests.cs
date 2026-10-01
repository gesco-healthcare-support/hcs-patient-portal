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

    /// <summary>
    /// A cell that begins with a formula trigger is prefixed with an apostrophe, so a
    /// spreadsheet reads it as text. Quoting alone does not do this: the quotes are removed
    /// on import and the formula still runs.
    ///
    /// <para>Patient Name, Email and Phone Number all come from the patient record, which
    /// external users populate during anonymous self-registration and in the booking wizard.
    /// The export lists many patients, so a formula in one row can read its neighbours.</para>
    /// </summary>
    [Theory]
    [InlineData("=HYPERLINK(\"http://x.test\")")]
    [InlineData("+1234")]
    [InlineData("-1+2")]
    [InlineData("@SUM(A1)")]
    [InlineData("\tlead-tab")]
    public void Neutralises_a_cell_that_opens_with_a_formula_trigger(string hostile)
    {
        var text = Decode(AppointmentReportCsv.Build(RowWithPatientName(hostile)));
        var cells = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)[1];

        // Assert on the boundary, not the whole value: a cell carrying quotes is also
        // RFC-4180 quoted, which doubles them, so the raw payload is not in the output
        // verbatim. What must hold is that the trigger is preceded by the apostrophe...
        cells.ShouldContain("'" + hostile[0]);

        // ...and never sits directly against a field delimiter, which is where a
        // spreadsheet would read it as the start of a formula.
        cells.ShouldNotContain("," + hostile[0]);
    }

    /// <summary>
    /// The trade-off, stated rather than left to be discovered: an international phone number
    /// legitimately begins with <c>+</c>, and <c>+</c> is a formula start, so it is neutralised
    /// too. The apostrophe does not show in the cell, only in the formula bar.
    /// </summary>
    [Fact]
    public void Neutralises_an_international_phone_number_and_that_is_deliberate()
    {
        var text = Decode(AppointmentReportCsv.Build(
            RowWithPatientName("DOE JANE", phoneNumber: "+15550100")));

        text.ShouldContain("'+15550100");
    }

    /// <summary>
    /// Neutralising runs BEFORE quoting, so a hostile cell that also carries a comma still
    /// gets its RFC-4180 quotes and cannot split the columns.
    /// </summary>
    [Fact]
    public void Still_quotes_a_neutralised_cell_that_contains_a_comma()
    {
        var text = Decode(AppointmentReportCsv.Build(RowWithPatientName("=A1,B1")));

        text.ShouldContain("\"'=A1,B1\"");
    }

    [Fact]
    public void Leaves_an_ordinary_cell_untouched()
    {
        var text = Decode(AppointmentReportCsv.Build(RowWithPatientName("DOE JANE")));

        text.ShouldContain(",DOE JANE,");
        text.ShouldNotContain("'DOE JANE");
    }

    private static List<AppointmentReportRowDto> RowWithPatientName(
        string patientName,
        string phoneNumber = "555-0101") => new()
    {
        new()
        {
            RequestConfirmationNumber = "A90002",
            AppointmentTypeName = "Panel QME",
            LocationName = "Downtown Clinic",
            AppointmentDate = new DateTime(2026, 6, 10, 9, 30, 0),
            AppointmentStatus = AppointmentStatusType.Approved,
            PatientName = patientName,
            DateOfBirth = "1985",
            Email = "jane.doe@example.test",
            PhoneNumber = phoneNumber,
            SocialSecurityNumber = "***-**-9012",
        },
    };

    private static string Decode(byte[] bytes)
    {
        // Strip the 3-byte UTF-8 BOM for assertion convenience.
        return new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
    }
}
