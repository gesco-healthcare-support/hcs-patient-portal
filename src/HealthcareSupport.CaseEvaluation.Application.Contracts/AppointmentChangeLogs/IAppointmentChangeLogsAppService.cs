using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

/// <summary>
/// Read-only change-log over ABP audit data for appointment intake entities.
/// Gated by <c>CaseEvaluation.AppointmentChangeLogs</c> (internal only); all values
/// are PHI-redacted server-side before they leave this service.
/// </summary>
public interface IAppointmentChangeLogsAppService : IApplicationService
{
    /// <summary>Every redacted field change for one appointment (incl. its child entities).</summary>
    Task<List<AppointmentChangeLogDto>> GetByAppointmentAsync(Guid appointmentId);

    /// <summary>Filtered, paged global change-log list across audited intake entities.</summary>
    Task<PagedResultDto<AppointmentChangeLogDto>> GetListAsync(GetAppointmentChangeLogsInput input);
}
