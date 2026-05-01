using System;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// W2-11: AppService for the per-appointment merged-PDF packet.
/// </summary>
public interface IAppointmentPacketsAppService
{
    /// <summary>
    /// Returns the (single) packet for an appointment. Null if the
    /// generation job has not run yet (e.g. appointment not yet Approved).
    /// </summary>
    Task<AppointmentPacketDto?> GetByAppointmentAsync(Guid appointmentId);

    /// <summary>
    /// Streams the packet PDF blob for download. Throws when the packet is
    /// not yet Generated. Tenant scoping is enforced through the underlying
    /// appointment's auto-filter.
    /// </summary>
    Task<DownloadResult> DownloadAsync(Guid appointmentId);
}
