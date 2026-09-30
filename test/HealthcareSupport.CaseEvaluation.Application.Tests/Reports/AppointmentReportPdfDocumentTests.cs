using System.Text;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Reports.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Reports;

/// <summary>
/// G-08-03: the first QuestPDF render in the codebase. These are smoke tests --
/// they assert a real, non-empty PDF is produced (the `%PDF-` magic header) for
/// both populated and empty result sets. Layout fidelity is verified live, not here.
/// Rows are already masked (the document never masks) -- synthetic data only.
/// </summary>
public class AppointmentReportPdfDocumentTests
{
    static AppointmentReportPdfDocumentTests()
    {
        // Mirror CaseEvaluationDomainModule: the Community license must be set
        // before any QuestPDF render (the module is not loaded in a pure unit test).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void Renders_a_valid_pdf_for_masked_rows()
    {
        var rows = new List<AppointmentReportRowDto>
        {
            new()
            {
                AppointmentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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

        var bytes = new AppointmentReportPdfDocument(rows).GeneratePdf();

        bytes.ShouldNotBeNull();
        bytes.Length.ShouldBeGreaterThan(0);
        Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
    }

    [Fact]
    public void Renders_a_valid_pdf_for_an_empty_result_set()
    {
        var bytes = new AppointmentReportPdfDocument(new List<AppointmentReportRowDto>()).GeneratePdf();

        Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
    }
}
