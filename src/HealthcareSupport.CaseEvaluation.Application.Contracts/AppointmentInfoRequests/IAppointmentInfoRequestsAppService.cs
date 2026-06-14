using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Send Back / Request-more-information flow. Staff send an appointment back to
/// the external user with flagged fields + a note (Pending -&gt; InfoRequested);
/// the external user resubmits their corrections (InfoRequested -&gt; Pending).
/// </summary>
public interface IAppointmentInfoRequestsAppService : IApplicationService
{
    /// <summary>Staff-only. Open an info request + move the appointment to InfoRequested.</summary>
    Task<AppointmentInfoRequestDto> SendBackAsync(Guid appointmentId, SendBackAppointmentInput input);

    /// <summary>External party. Resolve the open request + move the appointment back to Pending.</summary>
    Task ResubmitAsync(Guid appointmentId);

    /// <summary>The open (unresolved) info request for the appointment, or null when none is open.</summary>
    Task<AppointmentInfoRequestDto?> GetOpenAsync(Guid appointmentId);
}
