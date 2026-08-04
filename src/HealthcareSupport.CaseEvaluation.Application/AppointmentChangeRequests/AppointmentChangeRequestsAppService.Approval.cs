using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using NotificationsEvents = HealthcareSupport.CaseEvaluation.Notifications.Events;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 17 (2026-05-04) -- supervisor-side approve / reject AppService
/// for the cancel + reschedule lifecycle.
///
/// <para>Class-naming deviation rationale (mirrors Phase 12 / Phase 14
/// pattern): the user's directive locks "DO NOT edit the main file"
/// for the existing
/// <see cref="AppointmentChangeRequestsAppService"/> (Phase 15+16
/// submit endpoints). Partial-class would force the <c>partial</c>
/// keyword onto that file. Resolution: ship a sibling class
/// <see cref="AppointmentChangeRequestsApprovalAppService"/> in the
/// user's requested file path. Functional outcome is identical (the
/// approve/reject endpoints land at
/// <c>api/app/appointment-change-request-approvals</c>); only class
/// layout differs. Sync 4 cleanup PR can converge if desired.</para>
///
/// <para>B2 (2026-07-01) reschedule redesign: approve moves the SAME
/// appointment to the new slot IN PLACE (see
/// <see cref="RescheduleInPlacePolicy"/>), keeping its confirmation
/// number, child entities and audit trail, instead of cloning a new
/// row. No child-entity cascade-copy is needed; the capacity model
/// tracks the move via the appointment's <c>DoctorAvailabilityId</c>.</para>
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize]
public class AppointmentChangeRequestsApprovalAppService :
    CaseEvaluationAppService,
    IAppointmentChangeRequestsApprovalAppService
{
    private readonly IAppointmentChangeRequestRepository _changeRequestRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<DoctorAvailability, Guid> _doctorAvailabilityRepository;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<AppointmentChangeRequestsApprovalAppService> _logger;
    // Phase 4b (2026-08-04): the approve path now chooses the date, so it must enforce the same
    // lead-time + horizon gates the booking and submit paths enforce.
    private readonly BookingPolicyValidator _bookingPolicyValidator;

    public AppointmentChangeRequestsApprovalAppService(
        IAppointmentChangeRequestRepository changeRequestRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<DoctorAvailability, Guid> doctorAvailabilityRepository,
        ILocalEventBus localEventBus,
        ILogger<AppointmentChangeRequestsApprovalAppService> logger,
        BookingPolicyValidator bookingPolicyValidator)
    {
        _changeRequestRepository = changeRequestRepository;
        _appointmentRepository = appointmentRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _localEventBus = localEventBus;
        _logger = logger;
        _bookingPolicyValidator = bookingPolicyValidator;
    }

    [Authorize(CaseEvaluationPermissions.AppointmentChangeRequests.Approve)]
    public virtual async Task<AppointmentChangeRequestDto> ApproveCancellationAsync(
        Guid changeRequestId,
        ApproveCancellationInput input)
    {
        Check.NotNull(input, nameof(input));
        ChangeRequestApprovalValidator.EnsureCancellationOutcome(input.CancellationOutcome);

        var changeRequest = await LoadAndStampStampAsync(changeRequestId, input.ConcurrencyStamp);
        ChangeRequestApprovalValidator.EnsurePending(changeRequest);
        if (changeRequest.ChangeRequestType != ChangeRequestType.Cancel)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.ChangeRequestInvalidCancellationOutcome);
        }

        // Group D (2026-06-09): block finalize until the opposing side consents.
        // A No/Expired consent stays blocked here and surfaces in the supervisor's
        // mediation bucket; staff reject it via the normal reject path.
        OpposingConsentValidator.EnsureConsentGranted(
            changeRequest, AppointmentChangeRequestConsts.ConsentGatingEnabled);

        var appointment = await _appointmentRepository.GetAsync(changeRequest.AppointmentId);
        var fromStatus = appointment.AppointmentStatus;

        // Apply terminal status to the parent appointment.
        appointment.AppointmentStatus = input.CancellationOutcome;
        if (input.CancellationOutcome == AppointmentStatusType.CancelledNoBill ||
            input.CancellationOutcome == AppointmentStatusType.CancelledLate)
        {
            appointment.CancelledById = CurrentUser.Id;
            // 2026-07-31 -- copy the reason onto the appointment. It was previously left on the
            // change-request row only, so this column was permanently null: the patient
            // PatientAppointmentCancelledNoBill email rendered a blank reason (it reads the
            // appointment, unlike the staff templates which read the request), and the Case
            // Tracker payload had nothing to send.
            appointment.CancellationReason = changeRequest.CancellationReason;
        }
        await _appointmentRepository.UpdateAsync(appointment, autoSave: true);

        // Mark the change request Accepted; persist outcome + approver.
        changeRequest.RequestStatus = RequestStatusType.Accepted;
        changeRequest.ApprovedById = CurrentUser.Id;
        changeRequest.CancellationOutcome = input.CancellationOutcome;
        await PersistChangeRequestAsync(changeRequest);

        // Drive the slot cascade -- SlotCascadeHandler maps
        // CancelledNoBill / CancelledLate -> Available.
        await _localEventBus.PublishAsync(new AppointmentStatusChangedEto(
            appointmentId: appointment.Id,
            tenantId: appointment.TenantId,
            fromStatus: fromStatus,
            toStatus: appointment.AppointmentStatus,
            actingUserId: CurrentUser.Id,
            reason: changeRequest.CancellationReason,
            occurredAt: DateTime.UtcNow,
            doctorAvailabilityId: appointment.DoctorAvailabilityId));

        // Phase-18-declared Eto for the per-feature email handler.
        await _localEventBus.PublishAsync(new NotificationsEvents.AppointmentChangeRequestApprovedEto
        {
            AppointmentId = appointment.Id,
            ChangeRequestId = changeRequest.Id,
            TenantId = appointment.TenantId,
            ChangeRequestType = ChangeRequestType.Cancel,
            Outcome = input.CancellationOutcome,
            IsAdminOverride = false,
            ApprovedByUserId = CurrentUser.Id ?? Guid.Empty,
            OccurredAt = DateTime.UtcNow,
        });

        _logger.LogInformation(
            "ApproveCancellationAsync: change request {ChangeRequestId} accepted; appointment {AppointmentId} -> {Outcome}.",
            changeRequest.Id,
            appointment.Id,
            input.CancellationOutcome);

        return ObjectMapper.Map<AppointmentChangeRequest, AppointmentChangeRequestDto>(changeRequest);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentChangeRequests.Reject)]
    public virtual async Task<AppointmentChangeRequestDto> RejectCancellationAsync(
        Guid changeRequestId,
        RejectChangeRequestInput input)
    {
        Check.NotNull(input, nameof(input));
        ChangeRequestApprovalValidator.EnsureRejectionNotes(input.Reason);

        var changeRequest = await LoadAndStampStampAsync(changeRequestId, input.ConcurrencyStamp);
        ChangeRequestApprovalValidator.EnsurePending(changeRequest);
        if (changeRequest.ChangeRequestType != ChangeRequestType.Cancel)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.ChangeRequestInvalidCancellationOutcome);
        }

        // Cancel-reject: parent appointment stayed at Approved during
        // the Pending lifecycle (Phase 15 design), so no parent-side
        // status revert needed. Just flip the change request and emit.
        changeRequest.RequestStatus = RequestStatusType.Rejected;
        changeRequest.RejectedById = CurrentUser.Id;
        changeRequest.RejectionNotes = input.Reason.Trim();
        await PersistChangeRequestAsync(changeRequest);

        await _localEventBus.PublishAsync(new NotificationsEvents.AppointmentChangeRequestRejectedEto
        {
            AppointmentId = changeRequest.AppointmentId,
            ChangeRequestId = changeRequest.Id,
            TenantId = changeRequest.TenantId,
            ChangeRequestType = ChangeRequestType.Cancel,
            RejectionNotes = input.Reason.Trim(),
            RejectedByUserId = CurrentUser.Id ?? Guid.Empty,
            OccurredAt = DateTime.UtcNow,
        });

        _logger.LogInformation(
            "RejectCancellationAsync: change request {ChangeRequestId} rejected.",
            changeRequest.Id);

        return ObjectMapper.Map<AppointmentChangeRequest, AppointmentChangeRequestDto>(changeRequest);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentChangeRequests.Approve)]
    public virtual async Task<AppointmentChangeRequestDto> ApproveRescheduleAsync(
        Guid changeRequestId,
        ApproveRescheduleInput input)
    {
        Check.NotNull(input, nameof(input));
        ChangeRequestApprovalValidator.EnsureRescheduleOutcome(input.RescheduleOutcome);

        var changeRequest = await LoadAndStampStampAsync(changeRequestId, input.ConcurrencyStamp);
        ChangeRequestApprovalValidator.EnsurePending(changeRequest);
        if (changeRequest.ChangeRequestType != ChangeRequestType.Reschedule)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.ChangeRequestInvalidRescheduleOutcome);
        }

        // Resolve the actual new slot (override or user-picked) +
        // enforce admin-reason gate.
        // Group D (2026-06-09): block finalize until the opposing side consents.
        OpposingConsentValidator.EnsureConsentGranted(
            changeRequest, AppointmentChangeRequestConsts.ConsentGatingEnabled);

        var newSlotId = ChangeRequestApprovalValidator.ResolveNewSlotAndEnsureAdminReason(
            userPickedSlotId: changeRequest.NewDoctorAvailabilityId,
            overrideSlotId: input.OverrideSlotId,
            adminReason: input.AdminReScheduleReason);

        // An "override" requires something to override -- see ChangeRequestApprovalValidator
        // .IsAdminOverride for why the null case matters after phase 4b.
        var isAdminOverride = ChangeRequestApprovalValidator.IsAdminOverride(
            proposedSlotId: changeRequest.NewDoctorAvailabilityId,
            staffSlotId: input.OverrideSlotId);

        var sourceAppointment = await _appointmentRepository.GetAsync(changeRequest.AppointmentId);
        var newSlot = await _doctorAvailabilityRepository.GetAsync(newSlotId);

        // Phase 4b (2026-08-04): enforce the SAME booking policy the submit path enforces, on
        // the slot actually being scheduled onto. Previously the approve path ran no policy
        // check at all, so a supervisor override could land inside the lead time or past the
        // horizon; 4b makes the staff pick the primary path, which would have turned that
        // pre-existing bypass into the normal one. Approval is always an internal actor, hence
        // the internal (90-day) horizon.
        await _bookingPolicyValidator.ValidateAsync(
            newSlot.AvailableDate,
            sourceAppointment.AppointmentTypeId,
            isInternalCaller: true);

        // B2 (2026-07-01) reschedule redesign -- move the SAME appointment to the
        // resolved slot instead of cloning a new row. The confirmation number,
        // party links, injuries, documents and audit trail all stay on the one
        // appointment; the capacity model reflects the move automatically once
        // DoctorAvailabilityId changes (active-count is evaluated per slot). The
        // RescheduledNoBill / RescheduledLate outcome is recorded on the
        // change-request row below, NOT on the appointment status.
        var fromStatus = sourceAppointment.AppointmentStatus;
        sourceAppointment.DoctorAvailabilityId = newSlotId;
        // F-017 (2026-06-23): AvailableDate is date-only (midnight); the slot's
        // start time lives in TimeOnly FromTime. Combine them so the moved
        // appointment carries the picked time (was showing 12:00 AM everywhere).
        sourceAppointment.AppointmentDate = newSlot.AvailableDate.Date + newSlot.FromTime.ToTimeSpan();
        sourceAppointment.ReScheduledById = CurrentUser.Id;
        // Approved source returns from RescheduleRequested to Approved; a Pending
        // source stays Pending (see RescheduleInPlacePolicy).
        sourceAppointment.AppointmentStatus =
            RescheduleInPlacePolicy.ResolveFinalizedStatus(sourceAppointment.AppointmentStatus);
        await _appointmentRepository.UpdateAsync(sourceAppointment, autoSave: true);

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

        // Mark change request Accepted; record outcome + the staff slot choice + approver.
        changeRequest.RequestStatus = RequestStatusType.Accepted;
        changeRequest.ApprovedById = CurrentUser.Id;
        changeRequest.CancellationOutcome = input.RescheduleOutcome;
        // Phase 4b (2026-08-04): record the staff-chosen slot whenever staff chose one, NOT only
        // when it overrode a requestor proposal. Since 4b the external path has no proposal to
        // override, so gating this on isAdminOverride left BOTH slot columns null on the row --
        // losing the audit trail and blanking the date in the approval email, which resolves the
        // slot from this row. The admin REASON stays gated on a genuine override: there is
        // nobody to justify a change to when the requestor proposed nothing.
        if (input.OverrideSlotId.HasValue)
        {
            changeRequest.AdminOverrideSlotId = input.OverrideSlotId;
        }
        if (isAdminOverride)
        {
            changeRequest.AdminReScheduleReason = input.AdminReScheduleReason;
        }
        await PersistChangeRequestAsync(changeRequest);

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
                actingUserId: CurrentUser.Id,
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
            Outcome = input.RescheduleOutcome,
            IsAdminOverride = isAdminOverride,
            ApprovedByUserId = CurrentUser.Id ?? Guid.Empty,
            OccurredAt = DateTime.UtcNow,
        });

        _logger.LogInformation(
            "ApproveRescheduleAsync: change request {ChangeRequestId} accepted; appointment {AppointmentId} moved in place to slot {SlotId} (override={Override}).",
            changeRequest.Id,
            sourceAppointment.Id,
            newSlotId,
            isAdminOverride);

        return ObjectMapper.Map<AppointmentChangeRequest, AppointmentChangeRequestDto>(changeRequest);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentChangeRequests.Reject)]
    public virtual async Task<AppointmentChangeRequestDto> RejectRescheduleAsync(
        Guid changeRequestId,
        RejectChangeRequestInput input)
    {
        Check.NotNull(input, nameof(input));
        ChangeRequestApprovalValidator.EnsureRejectionNotes(input.Reason);

        var changeRequest = await LoadAndStampStampAsync(changeRequestId, input.ConcurrencyStamp);
        ChangeRequestApprovalValidator.EnsurePending(changeRequest);
        if (changeRequest.ChangeRequestType != ChangeRequestType.Reschedule)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.ChangeRequestInvalidRescheduleOutcome);
        }

        var sourceAppointment = await _appointmentRepository.GetAsync(changeRequest.AppointmentId);
        var fromStatus = sourceAppointment.AppointmentStatus;

        // Revert parent appointment to Approved.
        sourceAppointment.AppointmentStatus = AppointmentStatusType.Approved;
        await _appointmentRepository.UpdateAsync(sourceAppointment, autoSave: true);

        // Mark change request Rejected.
        changeRequest.RequestStatus = RequestStatusType.Rejected;
        changeRequest.RejectedById = CurrentUser.Id;
        changeRequest.RejectionNotes = input.Reason.Trim();
        await PersistChangeRequestAsync(changeRequest);

        // Drive the slot cascade for the parent: RescheduleRequested -> Approved
        // means the source slot stays Booked (mapping says Approved -> Booked).
        await _localEventBus.PublishAsync(new AppointmentStatusChangedEto(
            appointmentId: sourceAppointment.Id,
            tenantId: sourceAppointment.TenantId,
            fromStatus: fromStatus,
            toStatus: sourceAppointment.AppointmentStatus,
            actingUserId: CurrentUser.Id,
            reason: input.Reason.Trim(),
            occurredAt: DateTime.UtcNow,
            doctorAvailabilityId: sourceAppointment.DoctorAvailabilityId));

        // Gate 2 (2026-06-01) / OLD parity (AppointmentChangeRequestDomain.cs:600):
        // a reschedule submit puts the user-picked slot into Reserved as a transient
        // hold; rejecting the request must release that hold so the slot rejoins the
        // bookable pool. Guarded and idempotent -- release ONLY the slot this request
        // reserved, and ONLY if it is still Reserved, so a slot a doctor's-admin
        // genuinely closed in the meantime is never reopened.
        if (changeRequest.NewDoctorAvailabilityId.HasValue)
        {
            var reservedSlot = await _doctorAvailabilityRepository.FindAsync(
                changeRequest.NewDoctorAvailabilityId.Value);
            if (reservedSlot != null && reservedSlot.BookingStatusId == BookingStatus.Reserved)
            {
                reservedSlot.BookingStatusId = BookingStatus.Available;
                await _doctorAvailabilityRepository.UpdateAsync(reservedSlot, autoSave: true);
            }
        }

        await _localEventBus.PublishAsync(new NotificationsEvents.AppointmentChangeRequestRejectedEto
        {
            AppointmentId = sourceAppointment.Id,
            ChangeRequestId = changeRequest.Id,
            TenantId = sourceAppointment.TenantId,
            ChangeRequestType = ChangeRequestType.Reschedule,
            RejectionNotes = input.Reason.Trim(),
            RejectedByUserId = CurrentUser.Id ?? Guid.Empty,
            OccurredAt = DateTime.UtcNow,
        });

        _logger.LogInformation(
            "RejectRescheduleAsync: change request {ChangeRequestId} rejected.",
            changeRequest.Id);

        return ObjectMapper.Map<AppointmentChangeRequest, AppointmentChangeRequestDto>(changeRequest);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentChangeRequests.Default)]
    public virtual async Task<PagedResultDto<AppointmentChangeRequestDto>> GetPendingChangeRequestsAsync(
        GetChangeRequestsInput input)
    {
        Check.NotNull(input, nameof(input));

        var queryable = await _changeRequestRepository.GetQueryableAsync();
        var filtered = ChangeRequestListFilter.Apply(
            source: queryable,
            requestStatus: input.RequestStatus ?? RequestStatusType.Pending,
            changeRequestType: input.ChangeRequestType,
            createdFromUtc: input.CreatedFromUtc,
            createdToUtc: input.CreatedToUtc);

        var totalCount = filtered.Count();
        var sorted = string.IsNullOrWhiteSpace(input.Sorting)
            ? filtered.OrderByDescending(c => c.CreationTime)
            : filtered.OrderByDescending(c => c.CreationTime); // SafeFallback: ABP's PagedAndSortedResultRequestDto sorting parsing requires DynamicLinq; per branch CLAUDE.md we avoid string-LINQ for new code.
        var paged = sorted
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var dtos = ObjectMapper.Map<List<AppointmentChangeRequest>, List<AppointmentChangeRequestDto>>(paged);
        await PopulateAppointmentConfirmationNumbersAsync(paged, dtos);
        await PopulateAppointmentContextAsync(paged, dtos);
        return new PagedResultDto<AppointmentChangeRequestDto>(totalCount, dtos);
    }

    /// <summary>
    /// Phase 4b (2026-08-04) -- copies the referenced appointment's location + appointment type,
    /// and the proposed slot's date + start time, onto the matching change-request DTOs. The
    /// approval queue needs the first pair to drive the availability calendar staff now choose
    /// the new date with, and the second to SHOW what was requested instead of a bare GUID
    /// (before this the queue told staff to go open the appointment to see the slot).
    ///
    /// <para>Set-based like <see cref="PopulateAppointmentConfirmationNumbersAsync"/>: one
    /// projection query per referenced table, never per row. Slots are looked up only for rows
    /// that actually proposed one -- after 4b most do not.</para>
    /// </summary>
    private async Task PopulateAppointmentContextAsync(
        IReadOnlyCollection<AppointmentChangeRequest> changeRequests,
        IReadOnlyCollection<AppointmentChangeRequestDto> dtos)
    {
        var appointmentIds = changeRequests.Select(c => c.AppointmentId).Distinct().ToList();
        if (appointmentIds.Count == 0)
        {
            return;
        }

        var appointmentQuery = await _appointmentRepository.GetQueryableAsync();
        var appointmentRows = await AsyncExecuter.ToListAsync(
            appointmentQuery
                .Where(a => appointmentIds.Contains(a.Id))
                .Select(a => new { a.Id, a.LocationId, a.AppointmentTypeId }));
        var appointmentsById = appointmentRows.ToDictionary(
            row => row.Id,
            row => new ChangeRequestQueueContext.AppointmentContext(row.LocationId, row.AppointmentTypeId));

        // Only rows that actually proposed a slot need one resolved -- after 4b most do not.
        var slotIds = changeRequests
            .Where(c => c.NewDoctorAvailabilityId.HasValue)
            .Select(c => c.NewDoctorAvailabilityId!.Value)
            .Distinct()
            .ToList();
        var slotsById = new Dictionary<Guid, ChangeRequestQueueContext.SlotContext>();
        if (slotIds.Count > 0)
        {
            var slotQuery = await _doctorAvailabilityRepository.GetQueryableAsync();
            var slotRows = await AsyncExecuter.ToListAsync(
                slotQuery
                    .Where(s => slotIds.Contains(s.Id))
                    .Select(s => new { s.Id, s.AvailableDate, s.FromTime }));
            slotsById = slotRows.ToDictionary(
                row => row.Id,
                row => new ChangeRequestQueueContext.SlotContext(row.AvailableDate, row.FromTime));
        }

        ChangeRequestQueueContext.Apply(dtos, appointmentsById, slotsById);
    }

    /// <summary>
    /// Copies each referenced appointment's <c>RequestConfirmationNumber</c>
    /// onto the matching change-request DTO so the supervisor reschedule/cancel
    /// queues can show the human-facing "A#####" instead of the raw appointment
    /// GUID. The change-request entity stores only <c>AppointmentId</c>, so the
    /// values are fetched here in a single set-based query, not per row.
    /// </summary>
    private async Task PopulateAppointmentConfirmationNumbersAsync(
        IReadOnlyCollection<AppointmentChangeRequest> changeRequests,
        IReadOnlyCollection<AppointmentChangeRequestDto> dtos)
    {
        var appointmentIds = changeRequests.Select(c => c.AppointmentId).Distinct().ToList();
        if (appointmentIds.Count == 0)
        {
            return;
        }

        var query = await _appointmentRepository.GetQueryableAsync();
        var confirmationRows = await AsyncExecuter.ToListAsync(
            query
                .Where(a => appointmentIds.Contains(a.Id))
                .Select(a => new { a.Id, a.RequestConfirmationNumber }));
        var confirmationByAppointmentId = confirmationRows
            .ToDictionary(row => row.Id, row => row.RequestConfirmationNumber);

        foreach (var dto in dtos)
        {
            if (confirmationByAppointmentId.TryGetValue(dto.AppointmentId, out var confirmationNumber))
            {
                dto.AppointmentConfirmationNumber = confirmationNumber;
            }
        }
    }

    private async Task<AppointmentChangeRequest> LoadAndStampStampAsync(Guid id, string? concurrencyStamp)
    {
        var changeRequest = await _changeRequestRepository.GetAsync(id);

        // Pre-flight optimistic-concurrency comparison. The Application
        // layer does not reference EF Core, so we compare client +
        // server stamps directly here; a mismatch raises the same
        // BusinessException(ChangeRequestAlreadyHandled) the
        // EF-side gate would have produced. EF Core's UPDATE-with-
        // WHERE-stamp still fires below as a defense-in-depth check
        // (any race between this read and the next write surfaces as
        // a different exception we let bubble).
        if (!string.IsNullOrEmpty(concurrencyStamp) &&
            !string.Equals(concurrencyStamp, changeRequest.ConcurrencyStamp, StringComparison.Ordinal))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestAlreadyHandled);
        }
        return changeRequest;
    }

    private async Task PersistChangeRequestAsync(AppointmentChangeRequest changeRequest)
    {
        await _changeRequestRepository.UpdateAsync(changeRequest, autoSave: true);
    }

}
