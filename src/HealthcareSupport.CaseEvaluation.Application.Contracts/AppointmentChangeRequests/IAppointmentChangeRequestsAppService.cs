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

    /// <summary>
    /// Phase 16 (2026-05-04) -- external user submits a reschedule
    /// request on an Approved appointment. Caller must be the creator
    /// OR an accessor with <c>AccessType.Edit</c>. The new slot must
    /// be currently <c>Available</c>; lead-time + per-AppointmentType
    /// max-time gates run upstream of the manager. On success the
    /// parent appointment transitions <c>Approved -&gt; RescheduleRequested</c>
    /// and the new slot transitions <c>Available -&gt; Reserved</c>.
    /// Mirrors OLD's <c>AppointmentChangeRequestDomain.Add</c>
    /// reschedule path (lines 96-122 validation + 197-223 action).
    /// </summary>
    Task<AppointmentChangeRequestDto> RequestRescheduleAsync(Guid appointmentId, RequestRescheduleDto input);
}
