using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HealthcareSupport.CaseEvaluation.Reports.Pdf;

/// <summary>
/// G-08-03 (2026-06-06) -- the FIRST QuestPDF render in the codebase. Lays the
/// already-masked report rows out as a landscape A4 table mirroring the legacy
/// report's ten columns, with a green header that repeats on every page. Rows are
/// redacted upstream by <see cref="ReportRowRedactor"/> (SSN last-4, DOB
/// year-only); this document only formats -- it never masks. The QuestPDF
/// Community license is set process-globally in CaseEvaluationDomainModule.
/// </summary>
internal sealed class AppointmentReportPdfDocument : IDocument
{
    // QuestPDF's Material Green 500 == the legacy report header (#4CAF50).
    private static readonly Color HeaderColor = Colors.Green.Medium;

    private static readonly string[] Headers =
    {
        "Confirmation No", "Appointment Type", "Location Name", "Appointment Date Time",
        "Status", "Patient Name", "Date Of Birth", "Email", "Phone Number",
        "Social Security Number",
    };

    // Relative widths roughly proportional to expected content length.
    private static readonly float[] ColumnWidths =
    {
        1.2f, 1.4f, 1.4f, 1.3f, 1f, 1.5f, 0.8f, 1.8f, 1.2f, 1.3f,
    };

    private readonly IReadOnlyList<AppointmentReportRowDto> _rows;

    public AppointmentReportPdfDocument(IReadOnlyList<AppointmentReportRowDto> rows)
    {
        _rows = rows;
    }

    public DocumentMetadata GetMetadata() => new() { Title = "Appointment Request Report" };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.DefaultTextStyle(text => text.FontSize(8));

            page.Header().Text("Appointment Request Report").FontSize(14).SemiBold();

            page.Content().PaddingVertical(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    foreach (var width in ColumnWidths)
                    {
                        columns.RelativeColumn(width);
                    }
                });

                table.Header(header =>
                {
                    foreach (var label in Headers)
                    {
                        header.Cell().Background(HeaderColor).Padding(4)
                            .Text(label).FontColor(Colors.White).Bold();
                    }
                });

                var rowIndex = 0;
                foreach (var row in _rows)
                {
                    var background = rowIndex % 2 == 1 ? Colors.Grey.Lighten4 : Colors.White;
                    foreach (var value in CellValues(row))
                    {
                        table.Cell().Background(background)
                            .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Padding(4).Text(value ?? string.Empty);
                    }

                    rowIndex++;
                }
            });

            page.Footer().AlignRight().Text(text =>
            {
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
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
}
