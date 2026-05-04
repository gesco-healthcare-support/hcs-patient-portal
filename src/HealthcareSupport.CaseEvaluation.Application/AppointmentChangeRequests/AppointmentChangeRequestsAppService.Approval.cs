using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
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
/// <para>Reschedule cascade-copy reuses Session A's Phase 11j
/// <see cref="AppointmentRescheduleCloner"/> for both the scalar
/// clone and every child-entity helper. The orchestration
/// (read source children, clone, persist) lives here.</para>
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
    private readonly IRepository<AppointmentInjuryDetail, Guid> _injuryDetailRepository;
    private readonly IRepository<AppointmentBodyPart, Guid> _bodyPartRepository;
    private readonly IRepository<AppointmentClaimExaminer, Guid> _claimExaminerRepository;
    private readonly IRepository<AppointmentPrimaryInsurance, Guid> _primaryInsuranceRepository;
    private readonly IRepository<AppointmentEmployerDetail, Guid> _employerDetailRepository;
    private readonly IRepository<AppointmentApplicantAttorney, Guid> _applicantAttorneyLinkRepository;
    private readonly IRepository<AppointmentDefenseAttorney, Guid> _defenseAttorneyLinkRepository;
    private readonly IRepository<AppointmentAccessor, Guid> _accessorRepository;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<AppointmentChangeRequestsApprovalAppService> _logger;

    public AppointmentChangeRequestsApprovalAppService(
        IAppointmentChangeRequestRepository changeRequestRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<DoctorAvailability, Guid> doctorAvailabilityRepository,
        IRepository<AppointmentInjuryDetail, Guid> injuryDetailRepository,
        IRepository<AppointmentBodyPart, Guid> bodyPartRepository,
        IRepository<AppointmentClaimExaminer, Guid> claimExaminerRepository,
        IRepository<AppointmentPrimaryInsurance, Guid> primaryInsuranceRepository,
        IRepository<AppointmentEmployerDetail, Guid> employerDetailRepository,
        IRepository<AppointmentApplicantAttorney, Guid> applicantAttorneyLinkRepository,
        IRepository<AppointmentDefenseAttorney, Guid> defenseAttorneyLinkRepository,
        IRepository<AppointmentAccessor, Guid> accessorRepository,
        ILocalEventBus localEventBus,
        ILogger<AppointmentChangeRequestsApprovalAppService> logger)
    {
        _changeRequestRepository = changeRequestRepository;
        _appointmentRepository = appointmentRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _injuryDetailRepository = injuryDetailRepository;
        _bodyPartRepository = bodyPartRepository;
        _claimExaminerRepository = claimExaminerRepository;
        _primaryInsuranceRepository = primaryInsuranceRepository;
        _employerDetailRepository = employerDetailRepository;
        _applicantAttorneyLinkRepository = applicantAttorneyLinkRepository;
        _defenseAttorneyLinkRepository = defenseAttorneyLinkRepository;
        _accessorRepository = accessorRepository;
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
        var newSlotId = ChangeRequestApprovalValidator.ResolveNewSlotAndEnsureAdminReason(
            userPickedSlotId: changeRequest.NewDoctorAvailabilityId,
            overrideSlotId: input.OverrideSlotId,
            adminReason: input.AdminReScheduleReason);

        var isAdminOverride = input.OverrideSlotId.HasValue &&
            input.OverrideSlotId.Value != changeRequest.NewDoctorAvailabilityId;

        var sourceAppointment = await _appointmentRepository.GetAsync(changeRequest.AppointmentId);
        var newSlot = await _doctorAvailabilityRepository.GetAsync(newSlotId);

        // Build the new appointment via Session A's scalar-clone helper.
        var newAppointmentId = GuidGenerator.Create();
        var newAppointment = AppointmentRescheduleCloner.BuildScalarClone(
            source: sourceAppointment,
            newAppointmentId: newAppointmentId,
            newTenantId: sourceAppointment.TenantId,
            newDoctorAvailabilityId: newSlotId,
            newAppointmentDate: newSlot.AvailableDate,
            sameConfirmationNumber: true,
            overrideConfirmationNumber: null,
            approveDate: DateTime.UtcNow,
            isBeyondLimit: changeRequest.IsBeyondLimit);

        await _appointmentRepository.InsertAsync(newAppointment, autoSave: true);

        // Cascade-clone every child entity. Done in dependency order:
        // injury details first (parents of body-parts / claim-examiners
        // / primary-insurances), then siblings.
        await CloneChildEntitiesAsync(sourceAppointment.Id, newAppointment.Id, newAppointment.TenantId);

        // Stamp the source appointment as Rescheduled* + record reschedule actor.
        var fromSourceStatus = sourceAppointment.AppointmentStatus;
        sourceAppointment.AppointmentStatus = input.RescheduleOutcome;
        sourceAppointment.ReScheduledById = CurrentUser.Id;
        await _appointmentRepository.UpdateAsync(sourceAppointment, autoSave: true);

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

        // Slot transitions:
        //  - source slot (Booked) -> Available via the Rescheduled* status cascade.
        //  - new slot (was Reserved by Phase 16 submit OR Available if override) -> Booked
        //    via initial-create publish on the new appointment.
        await _localEventBus.PublishAsync(new AppointmentStatusChangedEto(
            appointmentId: sourceAppointment.Id,
            tenantId: sourceAppointment.TenantId,
            fromStatus: fromSourceStatus,
            toStatus: sourceAppointment.AppointmentStatus,
            actingUserId: CurrentUser.Id,
            reason: changeRequest.ReScheduleReason,
            occurredAt: DateTime.UtcNow,
            doctorAvailabilityId: sourceAppointment.DoctorAvailabilityId));

        await _localEventBus.PublishAsync(new AppointmentStatusChangedEto(
            appointmentId: newAppointment.Id,
            tenantId: newAppointment.TenantId,
            fromStatus: null,
            toStatus: newAppointment.AppointmentStatus,
            actingUserId: CurrentUser.Id,
            reason: null,
            occurredAt: DateTime.UtcNow,
            doctorAvailabilityId: newSlotId));

        // Admin-override case: the user-picked slot was held in
        // Reserved by Phase 16's submit. The supervisor abandoned it
        // -- release back to Available. The cascade handler does not
        // know about an abandoned reserved slot so we flip directly.
        if (isAdminOverride && changeRequest.NewDoctorAvailabilityId.HasValue)
        {
            await ReleaseSlotIfReservedAsync(changeRequest.NewDoctorAvailabilityId.Value);
        }

        await _localEventBus.PublishAsync(new NotificationsEvents.AppointmentChangeRequestApprovedEto
        {
            AppointmentId = newAppointment.Id,
            ChangeRequestId = changeRequest.Id,
            TenantId = newAppointment.TenantId,
            ChangeRequestType = ChangeRequestType.Reschedule,
            Outcome = input.RescheduleOutcome,
            IsAdminOverride = isAdminOverride,
            ApprovedByUserId = CurrentUser.Id ?? Guid.Empty,
            OccurredAt = DateTime.UtcNow,
        });

        _logger.LogInformation(
            "ApproveRescheduleAsync: change request {ChangeRequestId} accepted; new appointment {NewAppointmentId} created at slot {SlotId} (override={Override}).",
            changeRequest.Id,
            newAppointment.Id,
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

        // Release the user-picked Reserved slot back to Available.
        if (changeRequest.NewDoctorAvailabilityId.HasValue)
        {
            await ReleaseSlotIfReservedAsync(changeRequest.NewDoctorAvailabilityId.Value);
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
        return new PagedResultDto<AppointmentChangeRequestDto>(totalCount, dtos);
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

    private async Task ReleaseSlotIfReservedAsync(Guid slotId)
    {
        var slot = await _doctorAvailabilityRepository.FindAsync(slotId);
        if (slot == null)
        {
            return;
        }
        if (slot.BookingStatusId == BookingStatus.Reserved)
        {
            slot.BookingStatusId = BookingStatus.Available;
            await _doctorAvailabilityRepository.UpdateAsync(slot, autoSave: true);
        }
    }

    /// <summary>
    /// Cascade-clones every child entity from <paramref name="sourceAppointmentId"/>
    /// onto <paramref name="newAppointmentId"/>. Reads via repositories,
    /// reuses Session A's per-child clone helpers in
    /// <see cref="AppointmentRescheduleCloner"/>, persists via repos.
    /// Strict-parity intent: the new appointment row is structurally
    /// identical to the source for purposes of party-fan-out, billing,
    /// document workflows.
    /// </summary>
    private async Task CloneChildEntitiesAsync(Guid sourceAppointmentId, Guid newAppointmentId, Guid? newTenantId)
    {
        // Injury details (parents of body parts / claim examiners / primary insurances).
        var injuryQueryable = await _injuryDetailRepository.GetQueryableAsync();
        var sourceInjuries = injuryQueryable.Where(i => i.AppointmentId == sourceAppointmentId).ToList();
        foreach (var sourceInjury in sourceInjuries)
        {
            var newInjuryId = GuidGenerator.Create();
            var clonedInjury = AppointmentRescheduleCloner.CloneInjuryDetailFor(
                sourceInjury, newInjuryId, newAppointmentId, newTenantId);
            await _injuryDetailRepository.InsertAsync(clonedInjury, autoSave: true);

            var bodyPartQueryable = await _bodyPartRepository.GetQueryableAsync();
            var sourceBodyParts = bodyPartQueryable
                .Where(b => b.AppointmentInjuryDetailId == sourceInjury.Id).ToList();
            foreach (var bp in sourceBodyParts)
            {
                var clonedBp = AppointmentRescheduleCloner.CloneBodyPartFor(
                    bp, GuidGenerator.Create(), newInjuryId, newTenantId);
                await _bodyPartRepository.InsertAsync(clonedBp);
            }

            var claimExaminerQueryable = await _claimExaminerRepository.GetQueryableAsync();
            var sourceClaimExaminers = claimExaminerQueryable
                .Where(c => c.AppointmentInjuryDetailId == sourceInjury.Id).ToList();
            foreach (var ce in sourceClaimExaminers)
            {
                var clonedCe = AppointmentRescheduleCloner.CloneClaimExaminerFor(
                    ce, GuidGenerator.Create(), newInjuryId, newTenantId);
                await _claimExaminerRepository.InsertAsync(clonedCe);
            }

            var primaryInsuranceQueryable = await _primaryInsuranceRepository.GetQueryableAsync();
            var sourcePrimaryInsurances = primaryInsuranceQueryable
                .Where(p => p.AppointmentInjuryDetailId == sourceInjury.Id).ToList();
            foreach (var pi in sourcePrimaryInsurances)
            {
                var clonedPi = AppointmentRescheduleCloner.ClonePrimaryInsuranceFor(
                    pi, GuidGenerator.Create(), newInjuryId, newTenantId);
                await _primaryInsuranceRepository.InsertAsync(clonedPi);
            }
        }

        // Employer details (1:N with appointment per Phase 1.6).
        var employerQueryable = await _employerDetailRepository.GetQueryableAsync();
        var sourceEmployers = employerQueryable.Where(e => e.AppointmentId == sourceAppointmentId).ToList();
        foreach (var emp in sourceEmployers)
        {
            var clonedEmp = AppointmentRescheduleCloner.CloneEmployerDetailFor(
                emp, GuidGenerator.Create(), newAppointmentId, newTenantId);
            await _employerDetailRepository.InsertAsync(clonedEmp);
        }

        // Applicant attorney link rows.
        var applicantQueryable = await _applicantAttorneyLinkRepository.GetQueryableAsync();
        var sourceApplicantLinks = applicantQueryable.Where(a => a.AppointmentId == sourceAppointmentId).ToList();
        foreach (var aa in sourceApplicantLinks)
        {
            var clonedAa = AppointmentRescheduleCloner.CloneApplicantAttorneyFor(
                aa, GuidGenerator.Create(), newAppointmentId, newTenantId);
            await _applicantAttorneyLinkRepository.InsertAsync(clonedAa);
        }

        // Defense attorney link rows.
        var defenseQueryable = await _defenseAttorneyLinkRepository.GetQueryableAsync();
        var sourceDefenseLinks = defenseQueryable.Where(d => d.AppointmentId == sourceAppointmentId).ToList();
        foreach (var da in sourceDefenseLinks)
        {
            var clonedDa = AppointmentRescheduleCloner.CloneDefenseAttorneyFor(
                da, GuidGenerator.Create(), newAppointmentId, newTenantId);
            await _defenseAttorneyLinkRepository.InsertAsync(clonedDa);
        }

        // Accessor grants.
        var accessorQueryable = await _accessorRepository.GetQueryableAsync();
        var sourceAccessors = accessorQueryable.Where(a => a.AppointmentId == sourceAppointmentId).ToList();
        foreach (var ac in sourceAccessors)
        {
            var clonedAc = AppointmentRescheduleCloner.CloneAccessorFor(
                ac, GuidGenerator.Create(), newAppointmentId, newTenantId);
            await _accessorRepository.InsertAsync(clonedAc);
        }
    }
}
