using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Reports;

/// <summary>
/// G-08-01 (2026-06-06) -- the read-only Appointment Request Report for internal
/// staff. Gated by <c>CaseEvaluation.Reports</c>; every returned row is
/// PHI-redacted server-side (SSN masked to last 4, date of birth to the birth
/// year) before it leaves this service.
/// </summary>
public interface IReportsAppService : IApplicationService
{
    /// <summary>Paged, filtered, sorted appointment-request rows (the report grid).</summary>
    Task<PagedResultDto<AppointmentReportRowDto>> GetListAsync(GetAppointmentReportInput input);
}
