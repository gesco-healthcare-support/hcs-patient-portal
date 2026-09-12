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

    private static IEnumerable<string?> CellValues(AppointmentReportRowDto row) => new[]
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

    private static string Escape(string? value)
    {
        var v = value ?? string.Empty;
        if (v.Contains('"') || v.Contains(',') || v.Contains('\n') || v.Contains('\r'))
        {
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        }

        return v;
    }
}
