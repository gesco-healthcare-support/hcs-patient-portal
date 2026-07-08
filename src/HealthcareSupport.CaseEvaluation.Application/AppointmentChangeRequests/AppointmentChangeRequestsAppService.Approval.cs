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

    public AppointmentChangeRequestsApprovalAppService(
        IAppointmentChangeRequestRepository changeRequestRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<DoctorAvailability, Guid> doctorAvailabilityRepository,
        ILocalEventBus localEventBus,
        ILogger<AppointmentChangeRequestsApprovalAppService> logger)
    {
        _changeRequestRepository = changeRequestRepository;
        _appointmentRepository = appointmentRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _localEventBus = localEventBus;
        _logger = logger;
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

        var isAdminOverride = input.OverrideSlotId.HasValue &&
            input.OverrideSlotId.Value != changeRequest.NewDoctorAvailabilityId;

        var sourceAppointment = await _appointmentRepository.GetAsync(changeRequest.AppointmentId);
        var newSlot = await _doctorAvailabilityRepository.GetAsync(newSlotId);

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

        // Mark change request Accepted; record outcome + override fields + approver.
        changeRequest.RequestStatus = RequestStatusType.Accepted;
        changeRequest.ApprovedById = CurrentUser.Id;
        changeRequest.CancellationOutcome = input.RescheduleOutcome;
        if (isAdminOverride)
        {
            changeRequest.AdminOverrideSlotId = input.OverrideSlotId;
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
        return new PagedResultDto<AppointmentChangeRequestDto>(totalCount, dtos);
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
