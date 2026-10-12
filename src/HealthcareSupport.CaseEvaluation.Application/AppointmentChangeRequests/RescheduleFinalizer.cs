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
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Jobs;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Applies a reschedule DECISION -- the SPLIT: a new appointment on the consented slot, the old one
/// closed as Rescheduled through the state machine, packets regenerated, the held slot released and
/// the approved event published (2026-10-09 -- extracted from <c>ApproveRescheduleAsync</c> so the
/// same body also runs when the reschedule completes on its own once both sides agree).
///
/// <para>Deliberately NOT an application service, so ABP publishes no route for it. It takes the
/// acting user explicitly rather than reading <c>CurrentUser</c>: the automatic path runs inside the
/// ANONYMOUS public consent request, where <c>CurrentUser.Id</c> is null. Consent, state and
/// authorization are checked by the caller; this type assumes they passed.</para>
/// </summary>
public class RescheduleFinalizer : ITransientDependency
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<DoctorAvailability, Guid> _doctorAvailabilityRepository;
    private readonly IAppointmentChangeRequestRepository _changeRequestRepository;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<RescheduleFinalizer> _logger;
    private readonly IAppointmentChildCascadeCopier _childCascadeCopier;
    private readonly RequestConfirmationNumberGenerator _confirmationNumberGenerator;
    private readonly AppointmentManager _appointmentManager;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IClock _clock;
    private readonly ICurrentTenant _currentTenant;

    public RescheduleFinalizer(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<DoctorAvailability, Guid> doctorAvailabilityRepository,
        IAppointmentChangeRequestRepository changeRequestRepository,
        ILocalEventBus localEventBus,
        ILogger<RescheduleFinalizer> logger,
        IAppointmentChildCascadeCopier childCascadeCopier,
        RequestConfirmationNumberGenerator confirmationNumberGenerator,
        AppointmentManager appointmentManager,
        IBackgroundJobManager backgroundJobManager,
        IGuidGenerator guidGenerator,
        IClock clock,
        ICurrentTenant currentTenant)
    {
        _appointmentRepository = appointmentRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _changeRequestRepository = changeRequestRepository;
        _localEventBus = localEventBus;
        _logger = logger;
        _childCascadeCopier = childCascadeCopier;
        _confirmationNumberGenerator = confirmationNumberGenerator;
        _appointmentManager = appointmentManager;
        _backgroundJobManager = backgroundJobManager;
        _guidGenerator = guidGenerator;
        _clock = clock;
        _currentTenant = currentTenant;
    }

    /// <param name="currentRound">The consent round whose date both sides agreed to.</param>
    /// <param name="completedOnConsentNotifyUserId">
    /// Set only on the automatic path: the staff member who set the date, who is also emailed
    /// because they never clicked Approve.
    /// </param>
    public virtual async Task FinalizeAsync(
        AppointmentChangeRequest changeRequest,
        ChangeRequestConsentRound currentRound,
        AppointmentStatusType outcome,
        Guid? actingUserId,
        Guid? completedOnConsentNotifyUserId = null)
    {
        var newSlotId = currentRound.ProposedDoctorAvailabilityId;

        var sourceAppointment = await _appointmentRepository.GetAsync(changeRequest.AppointmentId);
        var newSlot = await _doctorAvailabilityRepository.GetAsync(newSlotId);

        // An "override" requires something to override -- see ChangeRequestApprovalValidator
        // .IsAdminOverride for why the null case matters after phase 4b. The staff slot is now
        // the round's, not an input field.
        var isAdminOverride = ChangeRequestApprovalValidator.IsAdminOverride(
            proposedSlotId: changeRequest.NewDoctorAvailabilityId,
            staffSlotId: newSlotId);

        // Phase 4d (2026-08-05) -- THE SPLIT. Until now finalize moved the SAME appointment to the
        // new slot; source item R3 asks instead for the old appointment to close into a Rescheduled
        // status and a NEW appointment to be created in its place, so Case Tracker can bill or
        // close the old date while tracking the new one separately (phase 4e).
        //
        // The new row is NOT built through the create pipeline: AppointmentCreateDto carries 16
        // scalars and no child collections, because the child cascade is client-side in the booking
        // wizard. It is created directly here and its children are copied by
        // IAppointmentChildCascadeCopier.
        var fromStatus = sourceAppointment.AppointmentStatus;

        // F-017 (2026-06-23): AvailableDate is date-only (midnight); the slot's start time lives in
        // TimeOnly FromTime. Combine them or the new appointment shows 12:00 AM everywhere.
        var newAppointmentDate = newSlot.AvailableDate.Date + newSlot.FromTime.ToTimeSpan();

        // (TenantId, RequestConfirmationNumber) is a hard unique index and the generator reads
        // max+1, so two concurrent finalizes can pick the same number -- the retry policy is what
        // resolves that, exactly as the booking path does.
        var newAppointment = await ConfirmationNumberRetryPolicy.RunWithRetryAsync(async () =>
        {
            var confirmationNumber = await _confirmationNumberGenerator.GenerateAsync();

            // Decision 3: the new appointment INHERITS THE SOURCE'S STATUS -- no re-approval,
            // because both sides already consented to this exact date in 4c. Normally that is
            // Approved (an external reschedule requires an Approved source), but B1 lets staff
            // reschedule a PENDING appointment, and hardcoding Approved there would turn an
            // unapproved appointment into an approved one just by rescheduling it, bypassing the
            // approval gate and its claim-information check.
            var newAppointmentStatus = RescheduleSplitPolicy.ResolveNewAppointmentStatus(fromStatus);

            var created = new Appointment(
                id: _guidGenerator.Create(),
                patientId: sourceAppointment.PatientId,
                identityUserId: sourceAppointment.IdentityUserId,
                appointmentTypeId: sourceAppointment.AppointmentTypeId,
                locationId: sourceAppointment.LocationId,
                doctorAvailabilityId: newSlotId,
                appointmentDate: newAppointmentDate,
                requestConfirmationNumber: confirmationNumber,
                appointmentStatus: newAppointmentStatus);

            // The reschedule chain. OriginalAppointmentId and EvaluationKind are carried across
            // UNCHANGED: they describe whether this case is a re-evaluation, which a reschedule
            // does not alter. Conflating the two links is what mislabels a Case Tracker folder.
            created.RescheduledFromAppointmentId = sourceAppointment.Id;
            created.OriginalAppointmentId = sourceAppointment.OriginalAppointmentId;
            created.EvaluationKind = sourceAppointment.EvaluationKind;
            created.PanelNumber = sourceAppointment.PanelNumber;
            created.DueDate = sourceAppointment.DueDate;
            created.RefferedBy = sourceAppointment.RefferedBy;

            // 2026-08-26: carry the party snapshot and the ORIGINAL booker. Without these the
            // replacement appointment locked its own external party out with a 403 -- the read
            // guard admits an external caller by booker/patient/accessor id or by the email+role
            // rule, and this row had a staff CreatorId, no BookedByUserId, and all four party-email
            // columns null. The child rows are copied below by the cascade copier, so notifications
            // still went out and hid it; found only by signing in as the attorney. See
            // Appointment.CopyPartySnapshotFrom.
            created.CopyPartySnapshotFrom(sourceAppointment);
            // RecordBookedBy rejects Guid.Empty, so assign through the property -- a source with no
            // booker (pre-D-R2-B rows) must stay null rather than throw mid-approval. This is the
            // "reschedule clone copies the value directly" case RecordBookedBy's own doc describes.
            created.BookedByUserId = sourceAppointment.BookedByUserId;

            return await _appointmentRepository.InsertAsync(created, autoSave: true);
        });

        var copied = await _childCascadeCopier.CopyAllAsync(
            sourceAppointment.Id, newAppointment.Id, sourceAppointment.TenantId);

        // Close the OLD appointment through the state machine rather than by assignment, so the
        // transition is validated and audited like every other one. These two triggers had been
        // unreachable since the machine was written.
        var oldTrigger = RescheduleSplitPolicy.ResolveOldAppointmentTrigger(outcome);
        sourceAppointment.ReScheduledById = actingUserId;
        await _appointmentRepository.UpdateAsync(sourceAppointment, autoSave: true);
        await _appointmentManager.CloseForRescheduleAsync(
            sourceAppointment.Id, oldTrigger, changeRequest.ReScheduleReason, actingUserId);
        sourceAppointment = await _appointmentRepository.GetAsync(sourceAppointment.Id);

        // Packets embed the appointment date, so the NEW appointment needs its own set. The OLD
        // appointment's packets are deliberately left intact (decision 6): they document what WAS
        // scheduled and are already in recipients' inboxes.
        //
        // Enqueues the SAME job AppointmentDocumentsAppService.RegeneratePacketAsync enqueues,
        // rather than calling that method: its first act is a READ-ACCESS GUARD written for a
        // user-facing HTTP caller, and staff finalizing a reschedule are not a party to the
        // appointment they just created, so the guard rejects them. Same generator, no borrowed
        // authorization.
        await _backgroundJobManager.EnqueueAsync(new GenerateAppointmentPacketArgs
        {
            AppointmentId = newAppointment.Id,
            TenantId = _currentTenant.Id,
        });

        // Release the transient Reserved hold the submit placed on the user-picked
        // slot (Phase 16 submit sets it Reserved). Idempotent and guarded -- release
        // ONLY that slot and ONLY if it is still Reserved, so a slot a doctor's-admin
        // genuinely closed is never reopened. Also covers the admin-override case:
        // the user-picked (held) slot is freed while the appointment lands on the
        // override slot. Mirrors the reject path's release. Under the capacity model
        // Booked == Available, so releasing the hold lets the slot rejoin the
        // bookable pool with this appointment now counted against its capacity.
        if (changeRequest.NewDoctorAvailabilityId.HasValue)
        {
            var heldSlot = await _doctorAvailabilityRepository.FindAsync(
                changeRequest.NewDoctorAvailabilityId.Value);
            if (heldSlot != null && heldSlot.BookingStatusId == BookingStatus.Reserved)
            {
                heldSlot.BookingStatusId = BookingStatus.Available;
                await _doctorAvailabilityRepository.UpdateAsync(heldSlot, autoSave: true);
            }
        }

        // Mark change request Accepted. Outcome, approver and timestamp move together; the
        // staff slot choice is recorded separately below.
        changeRequest.MarkDecided(
            RequestStatusType.Accepted, actingUserId, _clock.Now.ToUniversalTime());
        changeRequest.CancellationOutcome = outcome;
        // Phase 4b (2026-08-04): record the staff-chosen slot whenever staff chose one, NOT only
        // when it overrode a requestor proposal. Since 4b the external path has no proposal to
        // override, so gating this on isAdminOverride left BOTH slot columns null on the row --
        // losing the audit trail and blanking the date in the approval email, which resolves the
        // slot from this row. Phase 4c: the slot comes from the consented round, and this column
        // must still be written because ChangeRequestApprovedEmailHandler resolves the approval
        // email's date from it. The admin REASON stays gated on a genuine override: there is
        // nobody to justify a change to when the requestor proposed nothing.
        changeRequest.AdminOverrideSlotId = newSlotId;
        if (isAdminOverride)
        {
            changeRequest.AdminReScheduleReason = currentRound.ProposedReason;
        }
        await _changeRequestRepository.UpdateAsync(changeRequest, autoSave: true);

        // Notify audit / downstream subscribers of the status change only when it
        // actually changed (Approved source: RescheduleRequested -> Approved; a
        // Pending source stays Pending, so there is nothing to publish). The
        // date/slot move itself is captured by the appointment's own audit trail.
        if (sourceAppointment.AppointmentStatus != fromStatus)
        {
            await _localEventBus.PublishAsync(new AppointmentStatusChangedEto(
                appointmentId: sourceAppointment.Id,
                tenantId: sourceAppointment.TenantId,
                fromStatus: fromStatus,
                toStatus: sourceAppointment.AppointmentStatus,
                actingUserId: actingUserId,
                reason: changeRequest.ReScheduleReason,
                occurredAt: DateTime.UtcNow,
                doctorAvailabilityId: sourceAppointment.DoctorAvailabilityId));
        }

        await _localEventBus.PublishAsync(new NotificationsEvents.AppointmentChangeRequestApprovedEto
        {
            AppointmentId = sourceAppointment.Id,
            ChangeRequestId = changeRequest.Id,
            TenantId = sourceAppointment.TenantId,
            ChangeRequestType = ChangeRequestType.Reschedule,
            Outcome = outcome,
            IsAdminOverride = isAdminOverride,
            ApprovedByUserId = actingUserId ?? Guid.Empty,
            CompletedOnConsentNotifyUserId = completedOnConsentNotifyUserId,
            OccurredAt = DateTime.UtcNow,
        });

        _logger.LogInformation(
            "ApproveRescheduleAsync: change request {ChangeRequestId} accepted; appointment {OldAppointmentId} closed as {OldStatus} and replaced by {NewAppointmentId} ({NewConfirmationNumber}) on slot {SlotId}. Children copied: {CopiedCounts} (total {CopiedTotal}). Override={Override}.",
            changeRequest.Id,
            sourceAppointment.Id,
            sourceAppointment.AppointmentStatus,
            newAppointment.Id,
            newAppointment.RequestConfirmationNumber,
            newSlotId,
            copied,
            copied.Total,
            isAdminOverride);
    }
}
