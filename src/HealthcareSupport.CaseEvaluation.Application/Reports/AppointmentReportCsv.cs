using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HealthcareSupport.CaseEvaluation.Reports;

/// <summary>
/// G-08 (2026-06-15) -- CSV serializer for the Appointment Request Report, the
/// sibling of <see cref="Pdf.AppointmentReportPdfDocument"/>. Emits the SAME ten
/// columns in the same order from the SAME pre-masked rows (SSN last-4, DOB
/// year-only are applied upstream by ReportRowRedactor); this builder only
/// formats and never masks. UTF-8 with a BOM + CRLF line endings so Excel opens
/// it cleanly; every field is RFC-4180 quoted/escaped so commas, quotes, and
/// newlines in patient/clinic names cannot corrupt the column layout.
/// </summary>
internal static class AppointmentReportCsv
{
    private static readonly string[] Headers =
    {
        "Confirmation No", "Appointment Type", "Location Name", "Appointment Date Time",
        "Status", "Patient Name", "Date Of Birth", "Email", "Phone Number",
        "Social Security Number",
    };

    public static byte[] Build(IReadOnlyList<AppointmentReportRowDto> rows)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(',', Headers.Select(Escape)));
        sb.Append("\r\n");

        foreach (var row in rows)
        {
            sb.Append(string.Join(',', CellValues(row).Select(Escape)));
            sb.Append("\r\n");
        }

        // Prepend a UTF-8 BOM so Excel detects the encoding for non-ASCII names.
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        preamble.CopyTo(bytes, 0);
        body.CopyTo(bytes, preamble.Length);
        return bytes;
    }

    private static string?[] CellValues(AppointmentReportRowDto row) => new[]
    {
        row.RequestConfirmationNumber,
        row.AppointmentTypeName,
        row.LocationName,
        row.AppointmentDate.ToString("g", CultureInfo.InvariantCulture),
        row.AppointmentStatus.ToString(),
        row.PatientName,
        row.DateOfBirth,
        row.Email,
        row.PhoneNumber,
        row.SocialSecurityNumber,
    };

    /// <summary>
    /// Characters that make a spreadsheet read a cell as a formula rather than text.
    ///
    /// <para>Quoting does not stop this. RFC-4180 quotes are removed on import, so
    /// <c>"=HYPERLINK(...)"</c> is still evaluated. The cells at risk here are
    /// caller-supplied: Patient Name, Email and Phone Number come from the patient record,
    /// and external users enter those during anonymous self-registration and in the booking
    /// wizard. The report lists many patients, so a formula can read neighbouring cells and
    /// carry another patient's data out on one click.</para>
    /// </summary>
    private static readonly char[] FormulaTriggers = { '=', '+', '-', '@', '\t', '\r' };

    private static string Escape(string? value)
    {
        return Quote(Neutralise(value ?? string.Empty));
    }

    /// <summary>
    /// Prefixes a formula-triggering cell with an apostrophe, which spreadsheets read as
    /// "treat the rest as text" and do not display in the cell.
    ///
    /// <para><c>+</c> and <c>-</c> are on the list even though a phone number may legitimately
    /// begin with <c>+</c>. The apostrophe is hidden in the cell and visible only in the formula
    /// bar, so the cost is a formula-bar oddity on international numbers; the cost of leaving
    /// them off is that <c>+</c> is one of the two characters Excel actually accepts as a formula
    /// start. Neutralising is applied BEFORE quoting, so a neutralised cell that also contains a
    /// comma still gets its RFC-4180 quotes.</para>
    /// </summary>
    private static string Neutralise(string value)
    {
        if (value.Length > 0 && FormulaTriggers.Contains(value[0]))
        {
            return "'" + value;
        }

        return value;
    }

    private static string Quote(string v)
    {
        if (v.Contains('"') || v.Contains(',') || v.Contains('\n') || v.Contains('\r'))
        {
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        }

        return v;
    }
}
