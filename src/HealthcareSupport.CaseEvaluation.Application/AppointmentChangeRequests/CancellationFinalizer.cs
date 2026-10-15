using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Timing;
using NotificationsEvents = HealthcareSupport.CaseEvaluation.Notifications.Events;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Applies a cancellation DECISION: moves the appointment to the chosen terminal status through the
/// state machine, marks the request Accepted and publishes the approved event (2026-10-09 -- extracted
/// from <c>ApproveCancellationAsync</c> so the same body also runs when a staff-filed cancellation
/// completes on its own once both sides agree).
///
/// <para>Deliberately NOT an application service, so ABP publishes no route for it. It takes the
/// acting user explicitly rather than reading <c>CurrentUser</c>: the automatic path runs inside the
/// ANONYMOUS public consent request, where <c>CurrentUser.Id</c> is null. Consent, state and
/// authorization are checked by the caller; this type assumes they passed.</para>
/// </summary>
public class CancellationFinalizer : ITransientDependency
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IAppointmentChangeRequestRepository _changeRequestRepository;
    private readonly AppointmentManager _appointmentManager;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<CancellationFinalizer> _logger;
    private readonly IClock _clock;

    public CancellationFinalizer(
        IRepository<Appointment, Guid> appointmentRepository,
        IAppointmentChangeRequestRepository changeRequestRepository,
        AppointmentManager appointmentManager,
        ILocalEventBus localEventBus,
        ILogger<CancellationFinalizer> logger,
        IClock clock)
    {
        _appointmentRepository = appointmentRepository;
        _changeRequestRepository = changeRequestRepository;
        _appointmentManager = appointmentManager;
        _localEventBus = localEventBus;
        _logger = logger;
        _clock = clock;
    }

    /// <param name="completedOnConsentNotifyUserId">
    /// Set only on the automatic path: the staff member who filed the cancellation, who is also
    /// emailed because they never clicked Approve.
    /// </param>
    public virtual async Task FinalizeAsync(
        AppointmentChangeRequest changeRequest,
        AppointmentStatusType outcome,
        Guid? actingUserId,
        Guid? completedOnConsentNotifyUserId = null)
    {
        var appointment = await _appointmentRepository.GetAsync(changeRequest.AppointmentId);
        var fromStatus = appointment.AppointmentStatus;

        // Companion fields first; the status itself moves through the state machine below.
        if (outcome == AppointmentStatusType.CancelledNoBill ||
            outcome == AppointmentStatusType.CancelledLate)
        {
            appointment.CancelledById = actingUserId;
            // 2026-07-31 -- copy the reason onto the appointment. It was previously left on the
            // change-request row only, so this column was permanently null: the patient
            // PatientAppointmentCancelledNoBill email rendered a blank reason (it reads the
            // appointment, unlike the staff templates which read the request), and the Case
            // Tracker payload had nothing to send.
            appointment.CancellationReason = changeRequest.CancellationReason;
        }
        await _appointmentRepository.UpdateAsync(appointment, autoSave: true);

        // #926: apply the terminal status through the manager so the transition is validated. It
        // also publishes AppointmentStatusChangedEto (slot cascade, notifications, audit).
        await _appointmentManager.ConfirmCancellationAsync(
            appointment.Id, outcome, changeRequest.CancellationReason, actingUserId);
        appointment = await _appointmentRepository.GetAsync(appointment.Id);

        // Mark the change request Accepted. Outcome, approver and timestamp move together.
        changeRequest.MarkDecided(
            RequestStatusType.Accepted, actingUserId, _clock.Now.ToUniversalTime());
        changeRequest.CancellationOutcome = outcome;
        await _changeRequestRepository.UpdateAsync(changeRequest, autoSave: true);

        // Phase-18-declared Eto for the per-feature email handler.
        await _localEventBus.PublishAsync(new NotificationsEvents.AppointmentChangeRequestApprovedEto
        {
            AppointmentId = appointment.Id,
            ChangeRequestId = changeRequest.Id,
            TenantId = appointment.TenantId,
            ChangeRequestType = ChangeRequestType.Cancel,
            Outcome = outcome,
            IsAdminOverride = false,
            ApprovedByUserId = actingUserId ?? Guid.Empty,
            CompletedOnConsentNotifyUserId = completedOnConsentNotifyUserId,
            OccurredAt = DateTime.UtcNow,
        });

        _logger.LogInformation(
            "ApproveCancellationAsync: change request {ChangeRequestId} accepted; appointment {AppointmentId} -> {Outcome}.",
            changeRequest.Id,
            appointment.Id,
            outcome);
    }
}
