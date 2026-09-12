using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.Reports.Pdf;
using Microsoft.AspNetCore.Authorization;
using QuestPDF.Fluent;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.Reports;

/// <summary>
/// G-08-04 (2026-06-07) -- renders the per-appointment Patient Demographics
/// intake sheet as a PDF. Internal-staff-only via the Reports permission (the
/// sheet aggregates cross-party PHI, so it is stricter than the per-appointment
/// read guard alone). It reuses <see cref="IAppointmentsAppService.GetWithNavigationPropertiesAsync"/>,
/// which enforces the per-appointment read guard and returns the SSN-masked nav
/// DTO; the document additionally renders the date of birth as the birth year
/// only. The full SSN is never emitted here -- only via the audited reveal
/// endpoint.
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.Reports.Default)]
public class AppointmentDemographicsAppService : CaseEvaluationAppService, IAppointmentDemographicsAppService
{
    private readonly IAppointmentsAppService _appointmentsAppService;

    public AppointmentDemographicsAppService(IAppointmentsAppService appointmentsAppService)
    {
        _appointmentsAppService = appointmentsAppService;
    }

    public virtual async Task<DownloadResult> GetPdfAsync(Guid appointmentId)
    {
        var appointment = await _appointmentsAppService.GetWithNavigationPropertiesAsync(appointmentId);
        var bytes = new AppointmentDemographicsPdfDocument(appointment).GeneratePdf();

        var confirmation = appointment.Appointment?.RequestConfirmationNumber;
        var fileName = string.IsNullOrWhiteSpace(confirmation)
            ? "appointment-demographics.pdf"
            : $"appointment-demographics-{confirmation}.pdf";

        return new DownloadResult
        {
            Content = new MemoryStream(bytes),
            ContentType = "application/pdf",
            FileName = fileName,
        };
    }
}
