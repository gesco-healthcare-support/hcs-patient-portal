using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Reports;

/// <summary>
/// G-08-04 (2026-06-07) -- the per-appointment Patient Demographics intake sheet
/// as a PDF. Internal-staff-only (gated by <c>CaseEvaluation.Reports</c>): the
/// sheet aggregates cross-party PHI, so it is stricter than the per-appointment
/// read guard alone. Rendered from the already-masked appointment nav graph
/// (SSN last-4, date of birth shown as the birth year only).
/// </summary>
public interface IAppointmentDemographicsAppService : IApplicationService
{
    Task<DownloadResult> GetPdfAsync(Guid appointmentId);
}
