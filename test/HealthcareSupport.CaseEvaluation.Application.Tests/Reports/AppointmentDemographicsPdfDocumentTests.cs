using System.Text;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Reports.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Reports;

/// <summary>
/// G-08-04: the per-appointment demographics PDF. Smoke tests -- assert a real,
/// non-empty PDF is produced (the `%PDF-` magic header). The document formats
/// already-masked rows; synthetic data only.
/// </summary>
public class AppointmentDemographicsPdfDocumentTests
{
    static AppointmentDemographicsPdfDocumentTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void Renders_a_valid_pdf_for_a_populated_appointment()
    {
        var dto = new AppointmentWithNavigationPropertiesDto
        {
            Appointment = new AppointmentDto
            {
                RequestConfirmationNumber = "A90001",
                AppointmentDate = new DateTime(2026, 6, 10, 9, 30, 0),
                AppointmentStatus = AppointmentStatusType.Approved,
                PanelNumber = "PN-1",
            },
            Patient = new PatientDto
            {
                FirstName = "Pat",
                LastName = "Tester",
                Email = "pat.tester@example.test",
                DateOfBirth = new DateTime(1985, 3, 12),
                SocialSecurityNumber = "***-**-9012",
                PhoneNumber = "555-0101",
                City = "Anytown",
            },
        };

        var bytes = new AppointmentDemographicsPdfDocument(dto).GeneratePdf();

        bytes.ShouldNotBeNull();
        bytes.Length.ShouldBeGreaterThan(0);
        Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
    }

    [Fact]
    public void Renders_when_only_the_appointment_is_present()
    {
        var dto = new AppointmentWithNavigationPropertiesDto
        {
            Appointment = new AppointmentDto
            {
                RequestConfirmationNumber = "A90002",
                AppointmentStatus = AppointmentStatusType.Pending,
            },
        };

        var bytes = new AppointmentDemographicsPdfDocument(dto).GeneratePdf();

        Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
    }
}
