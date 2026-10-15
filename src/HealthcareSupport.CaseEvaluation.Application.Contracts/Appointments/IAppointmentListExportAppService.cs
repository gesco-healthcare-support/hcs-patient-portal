using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-10-14 -- records an export of the internal appointment list in the PHI access log.
/// </summary>
public interface IAppointmentListExportAppService : IApplicationService
{
    /// <summary>
    /// Records one AppointmentListCsvExported row per appointment, after checking the caller may
    /// read each one. Call it BEFORE producing the file; produce the file only if it succeeds.
    /// </summary>
    Task RecordCsvExportAsync(RecordAppointmentListExportInput input);
}
