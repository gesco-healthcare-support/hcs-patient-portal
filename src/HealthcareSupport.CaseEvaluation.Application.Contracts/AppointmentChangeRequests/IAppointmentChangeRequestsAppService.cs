using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 15 (2026-05-04) -- external-user cancel + reschedule submit.
/// Phase 16 will add <c>RequestRescheduleAsync</c>; Phase 17 (Session B)
/// will add the supervisor-side approve / reject methods on a partial
/// of this same interface (or a sibling interface, per Session B's
/// preference). The 2-session-split memory pre-locks file ownership
/// for the AppService implementation files.
/// </summary>
public interface IAppointmentChangeRequestsAppService : IApplicationService
{
    /// <summary>
    /// External user submits a cancellation request on an Approved
    /// appointment. Caller must be the appointment creator OR an
    /// accessor with <c>AccessType.Edit</c>; the per-row policy is
    /// composed from <c>AppointmentAccessRules.CanEdit</c>.
    /// Mirrors OLD's <c>AppointmentChangeRequestDomain.Add</c> cancel
    /// path (lines 73-95 + 197-224).
    /// </summary>
    Task<AppointmentChangeRequestDto> RequestCancellationAsync(Guid appointmentId, RequestCancellationDto input);
}
