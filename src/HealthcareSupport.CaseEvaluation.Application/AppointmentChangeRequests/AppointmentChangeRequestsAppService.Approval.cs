using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications;
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
    // Phase 4c (2026-08-05): confirming a date opens a consent round and tokens both sides.
    private readonly IChangeRequestConsentRoundRepository _consentRoundRepository;
    private readonly ChangeRequestConsentManager _consentManager;
    private readonly ChangeRequestSideResolver _sideResolver;
    private readonly IAccountUrlBuilder _accountUrlBuilder;
    // The reject-reschedule path reverts through the state machine.
    private readonly AppointmentManager _appointmentManager;
    // 2026-10-09: the decision bodies (split / cancel) live in the finalizers so the automatic
    // completion path shares them; the completion orchestrator is invoked here when nobody had
    // a representative to ask.
    private readonly RescheduleFinalizer _rescheduleFinalizer;
    private readonly CancellationFinalizer _cancellationFinalizer;
    private readonly ChangeRequestConsentCompletion _consentCompletion;

    public AppointmentChangeRequestsApprovalAppService(
        IAppointmentChangeRequestRepository changeRequestRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<DoctorAvailability, Guid> doctorAvailabilityRepository,
        ILocalEventBus localEventBus,
        ILogger<AppointmentChangeRequestsApprovalAppService> logger,
        BookingPolicyValidator bookingPolicyValidator,
        IChangeRequestConsentRoundRepository consentRoundRepository,
        ChangeRequestConsentManager consentManager,
        ChangeRequestSideResolver sideResolver,
        IAccountUrlBuilder accountUrlBuilder,
        AppointmentManager appointmentManager,
        RescheduleFinalizer rescheduleFinalizer,
        CancellationFinalizer cancellationFinalizer,
        ChangeRequestConsentCompletion consentCompletion)
    {
        _appointmentManager = appointmentManager;
        _rescheduleFinalizer = rescheduleFinalizer;
        _cancellationFinalizer = cancellationFinalizer;
        _consentCompletion = consentCompletion;
        _changeRequestRepository = changeRequestRepository;
        _appointmentRepository = appointmentRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _localEventBus = localEventBus;
        _logger = logger;
        _bookingPolicyValidator = bookingPolicyValidator;
        _consentRoundRepository = consentRoundRepository;
        _consentManager = consentManager;
        _sideResolver = sideResolver;
        _accountUrlBuilder = accountUrlBuilder;
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

        await _cancellationFinalizer.FinalizeAsync(
            changeRequest, input.CancellationOutcome, CurrentUser.Id);

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
        // Outcome, rejector and timestamp move together.
        changeRequest.MarkDecided(
            RequestStatusType.Rejected, CurrentUser.Id, Clock.Now.ToUniversalTime());
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
    public virtual async Task<AppointmentChangeRequestDto> ConfirmRescheduleDateAsync(
        Guid changeRequestId,
        ConfirmRescheduleDateInput input)
    {
        Check.NotNull(input, nameof(input));
        // 2026-10-09: staff pick the outcome HERE, because the reschedule completes on its own
        // once both sides agree and there is no later Approve to pick it at.
        ChangeRequestApprovalValidator.EnsureRescheduleOutcome(input.RescheduleOutcome);

        var changeRequest = await LoadAndStampStampAsync(changeRequestId, input.ConcurrencyStamp);
        ChangeRequestApprovalValidator.EnsurePending(changeRequest);
        EnsureRescheduleRequest(changeRequest);

        if (input.DoctorAvailabilityId == Guid.Empty)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestNewSlotRequired);
        }

        var appointment = await _appointmentRepository.GetAsync(changeRequest.AppointmentId);
        var slot = await _doctorAvailabilityRepository.GetAsync(input.DoctorAvailabilityId);

        // Same lead-time + horizon gates the booking and submit paths enforce, applied at the
        // moment staff COMMIT to the date rather than at finalize -- there is no point asking
        // two parties to consent to a date the policy will later reject. Approval is always an
        // internal actor, hence the internal (90-day) horizon.
        await _bookingPolicyValidator.ValidateAsync(
            slot.AvailableDate,
            appointment.AppointmentTypeId,
            isInternalCaller: true);

        // One read serves both questions: which round is current, and what the next round
        // number is. Deriving "next" from the CURRENT round alone would restart at 1 if every
        // round were somehow superseded, colliding with the unique
        // (TenantId, ChangeRequestId, RoundNumber) index.
        var rounds = await _consentRoundRepository.GetListAsync(
            x => x.AppointmentChangeRequestId == changeRequestId);
        var currentRound = rounds
            .Where(r => r.SupersededAt == null)
            .OrderByDescending(r => r.RoundNumber)
            .FirstOrDefault();

        if (currentRound != null
            && currentRound.ProposedDoctorAvailabilityId == input.DoctorAvailabilityId)
        {
            // Confirming the SAME date again is a RESEND, not a new proposal: the parties have
            // already been asked about this date and may simply not have acted yet.
            // A different No bill / Late choice on the same date is carried onto the round too.
            currentRound.ProposeOutcome(input.RescheduleOutcome);
            return await ResendForRoundAsync(changeRequest, currentRound);
        }

        // A genuinely new date needs a free slot. The slot this request itself already holds is
        // acceptable: an internal staff-filed reschedule reserves its proposed slot at submit,
        // so requiring Available here would reject staff confirming their own proposal.
        var isHeldByThisRequest = changeRequest.NewDoctorAvailabilityId == slot.Id;
        if (!isHeldByThisRequest && !RescheduleRequestValidators.IsSlotAvailable(slot.BookingStatusId))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestNewSlotNotAvailable);
        }

        var nowUtc = Clock.Now.ToUniversalTime();
        if (currentRound != null)
        {
            // The previous date is retired, but its row and its decisions stay -- that record of
            // who declined which date is the reason rounds are rows at all.
            currentRound.Supersede(nowUtc);
            await _consentRoundRepository.UpdateAsync(currentRound, autoSave: true);
        }

        var round = new ChangeRequestConsentRound(
            id: GuidGenerator.Create(),
            tenantId: changeRequest.TenantId,
            appointmentChangeRequestId: changeRequestId,
            roundNumber: rounds.Count == 0 ? 1 : rounds.Max(r => r.RoundNumber) + 1,
            proposedDoctorAvailabilityId: slot.Id,
            proposedByUserId: CurrentUser.Id,
            proposedReason: input.AdminReScheduleReason,
            proposedOutcome: input.RescheduleOutcome);

        // Both sides must agree to a staff-chosen date; a side with no representative stays
        // NotRequired and is auto-satisfied. Reuses the resolver the staff-initiated submit path
        // already uses -- do not write a second one.
        var bothSides = await _sideResolver.ResolveBothSidesAsync(changeRequest.AppointmentId);
        var toNotify = new List<(string Email, RecipientRole Role, string Token)>();
        if (!string.IsNullOrWhiteSpace(bothSides.SideARepEmail))
        {
            var token = _consentManager.IssueSideConsent(round, ChangeRequestSide.SideA);
            toNotify.Add((bothSides.SideARepEmail, bothSides.SideARepRole ?? RecipientRole.Patient, token));
        }
        if (!string.IsNullOrWhiteSpace(bothSides.SideBRepEmail))
        {
            var token = _consentManager.IssueSideConsent(round, ChangeRequestSide.SideB);
            toNotify.Add((bothSides.SideBRepEmail, bothSides.SideBRepRole ?? RecipientRole.ClaimExaminer, token));
        }

        await _consentRoundRepository.InsertAsync(round, autoSave: true);

        foreach (var (email, role, token) in toNotify)
        {
            await PublishConsentRequestedAsync(changeRequest, round, email, role, token);
        }

        if (toNotify.Count == 0)
        {
            // Nobody to ask: both sides stay NotRequired, so the round is immediately grantable
            // and staff can finalize. Logged because it is unexpected in practice -- a patient
            // and a claim examiner normally always exist.
            _logger.LogWarning(
                "ConfirmRescheduleDateAsync: change request {ChangeRequestId} round {RoundNumber} has no representative on either side; consent skipped.",
                changeRequestId,
                round.RoundNumber);
            // Nobody to ask, so the round is already fully granted: complete it now (after this
            // unit of work commits, so the round is visible to the completion's own unit).
            await _consentCompletion.RequestCompletionAsync(changeRequestId);
        }

        _logger.LogInformation(
            "ConfirmRescheduleDateAsync: change request {ChangeRequestId} round {RoundNumber} opened on slot {SlotId}; {SideCount} side(s) asked to consent.",
            changeRequestId,
            round.RoundNumber,
            slot.Id,
            toNotify.Count);

        return ObjectMapper.Map<AppointmentChangeRequest, AppointmentChangeRequestDto>(changeRequest);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentChangeRequests.Approve)]
    public virtual async Task<AppointmentChangeRequestDto> ResendConsentRequestAsync(Guid changeRequestId)
    {
        var changeRequest = await _changeRequestRepository.GetAsync(changeRequestId);
        ChangeRequestApprovalValidator.EnsurePending(changeRequest);
        EnsureRescheduleRequest(changeRequest);

        var currentRound = await _consentRoundRepository.GetCurrentAsync(changeRequestId);
        if (currentRound == null)
        {
            // Nothing has been proposed, so there is no consent request to resend.
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestNewSlotRequired);
        }

        return await ResendForRoundAsync(changeRequest, currentRound);
    }

    /// <summary>
    /// Re-asks the sides of <paramref name="round"/> that have not answered yet. Each still-Pending
    /// side gets a FRESH token (Adrian, 2026-08-05): only the token's SHA256 hash is stored, so the
    /// original raw token cannot be recovered to rebuild its URL. The link in the superseded email
    /// therefore stops working and the recipient must use the newest one -- accepted, because the
    /// alternative is holding a decryptable consent credential at rest.
    ///
    /// <para>Sides that already Approved / Rejected / Expired are deliberately left alone: re-asking
    /// a party who answered needs a NEW ROUND, which is what confirming a different date does.</para>
    /// </summary>
    private async Task<AppointmentChangeRequestDto> ResendForRoundAsync(
        AppointmentChangeRequest changeRequest,
        ChangeRequestConsentRound round)
    {
        var bothSides = await _sideResolver.ResolveBothSidesAsync(changeRequest.AppointmentId);
        var toNotify = new List<(string Email, RecipientRole Role, string Token)>();

        if (round.SideConsentStatus(ChangeRequestSide.SideA) == ChangeRequestConsentStatus.Pending
            && !string.IsNullOrWhiteSpace(bothSides.SideARepEmail))
        {
            var token = _consentManager.ReissueSideConsent(round, ChangeRequestSide.SideA);
            toNotify.Add((bothSides.SideARepEmail, bothSides.SideARepRole ?? RecipientRole.Patient, token));
        }
        if (round.SideConsentStatus(ChangeRequestSide.SideB) == ChangeRequestConsentStatus.Pending
            && !string.IsNullOrWhiteSpace(bothSides.SideBRepEmail))
        {
            var token = _consentManager.ReissueSideConsent(round, ChangeRequestSide.SideB);
            toNotify.Add((bothSides.SideBRepEmail, bothSides.SideBRepRole ?? RecipientRole.ClaimExaminer, token));
        }

        // The attempt counter is what makes the outbox idempotency key distinct. Bumping it is
        // NOT bookkeeping: without it EnqueueAsync silently returns the row from the previous
        // send and the resend never reaches anyone.
        round.RegisterResend();
        await _consentRoundRepository.UpdateAsync(round, autoSave: true);

        foreach (var (email, role, token) in toNotify)
        {
            await PublishConsentRequestedAsync(changeRequest, round, email, role, token);
        }

        _logger.LogInformation(
            "ResendForRoundAsync: change request {ChangeRequestId} round {RoundNumber} attempt {SendAttempt}; {SideCount} side(s) re-asked.",
            changeRequest.Id,
            round.RoundNumber,
            round.SendAttempts,
            toNotify.Count);

        return ObjectMapper.Map<AppointmentChangeRequest, AppointmentChangeRequestDto>(changeRequest);
    }

    private async Task PublishConsentRequestedAsync(
        AppointmentChangeRequest changeRequest,
        ChangeRequestConsentRound round,
        string recipientEmail,
        RecipientRole recipientRole,
        string rawToken)
    {
        var consentUrl = await _accountUrlBuilder.BuildChangeRequestConsentUrlAsync(
            changeRequest.TenantId!.Value, rawToken);

        await _localEventBus.PublishAsync(new NotificationsEvents.ChangeRequestConsentRequestedEto
        {
            AppointmentId = changeRequest.AppointmentId,
            ChangeRequestId = changeRequest.Id,
            TenantId = changeRequest.TenantId,
            ChangeRequestType = changeRequest.ChangeRequestType,
            OpposingRecipientEmail = recipientEmail,
            OpposingRecipientRole = recipientRole,
            ConsentUrl = consentUrl,
            RoundNumber = round.RoundNumber,
            SendAttempt = round.SendAttempts,
            ProposedDoctorAvailabilityId = round.ProposedDoctorAvailabilityId,
            OccurredAt = DateTime.UtcNow,
        });
    }

    private static void EnsureRescheduleRequest(AppointmentChangeRequest changeRequest)
    {
        if (changeRequest.ChangeRequestType != ChangeRequestType.Reschedule)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.ChangeRequestInvalidRescheduleOutcome);
        }
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
        EnsureRescheduleRequest(changeRequest);

        // Phase 4c (2026-08-05): the date is no longer chosen here. It comes from the consent
        // round staff confirmed -- the one both sides actually agreed to -- so the gate and the
        // slot are read from the same row and cannot disagree. A missing round is a hard block:
        // the parent's own consent columns are both NotRequired on a reschedule, and the cancel
        // gate reads both-NotRequired as "nothing to consent", which would wave this through
        // with no consent recorded at all.
        var currentRound = await _consentRoundRepository.GetCurrentAsync(changeRequestId);
        RescheduleConsentGate.EnsureRoundConsentGranted(
            currentRound, AppointmentChangeRequestConsts.ConsentGatingEnabled);

        // The decision itself -- the split, packets, slot release and events -- lives in
        // RescheduleFinalizer so the automatic path (both sides agreed) runs the same body.
        await _rescheduleFinalizer.FinalizeAsync(
            changeRequest, currentRound!, input.RescheduleOutcome, CurrentUser.Id);

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

        // #926: revert through the state machine. Only RescheduleRequested has an edge back to
        // Approved. A Pending source (internal staff may file a reschedule against one, B1) never
        // left Pending, so it needs no write and no transition -- forcing Approved would promote a
        // never-approved appointment past the approval gate.
        if (fromStatus == AppointmentStatusType.RescheduleRequested)
        {
            await _appointmentManager.RejectRescheduleAsync(
                sourceAppointment.Id, input.Reason.Trim(), CurrentUser.Id);
            sourceAppointment = await _appointmentRepository.GetAsync(sourceAppointment.Id);
        }

        // Mark change request Rejected. Outcome, rejector and timestamp move together.
        changeRequest.MarkDecided(
            RequestStatusType.Rejected, CurrentUser.Id, Clock.Now.ToUniversalTime());
        changeRequest.RejectionNotes = input.Reason.Trim();
        await PersistChangeRequestAsync(changeRequest);

        // The manager published the RescheduleRequested -> Approved transition. A Pending source
        // makes no transition, so there is nothing to publish for it (the old Pending -> Pending
        // event raised no notification).

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
        // input.Sorting is DELIBERATELY IGNORED: ABP's PagedAndSortedResultRequestDto sorting
        // parsing requires DynamicLinq, and the branch CLAUDE.md forbids string-LINQ in new code.
        // This was previously written as a ternary whose two branches were byte-identical, which
        // read as though a caller's sort order were honoured on one path. Collapsed 2026-09-09
        // (#649) so the single ordering is visible; results are always newest-first.
        var sorted = filtered.OrderByDescending(c => c.CreationTime);
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

        // Phase 4c (2026-08-05): the current consent round per request, in ONE query over the
        // whole page. Ordering by RoundNumber descending and keeping the first per request gives
        // the same answer as GetCurrentAsync would per row, without a query per row.
        var changeRequestIds = changeRequests.Select(c => c.Id).ToList();
        var roundQuery = await _consentRoundRepository.GetQueryableAsync();
        var roundRows = await AsyncExecuter.ToListAsync(
            roundQuery
                .Where(r => changeRequestIds.Contains(r.AppointmentChangeRequestId)
                    && r.SupersededAt == null)
                .Select(r => new
                {
                    r.AppointmentChangeRequestId,
                    r.RoundNumber,
                    r.ProposedDoctorAvailabilityId,
                    r.SideAConsentStatus,
                    r.SideBConsentStatus,
                    r.SendAttempts,
                    r.ProposedOutcome,
                }));
        var roundsByChangeRequestId = roundRows
            .GroupBy(r => r.AppointmentChangeRequestId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var current = g.OrderByDescending(r => r.RoundNumber).First();
                    return new ChangeRequestQueueContext.ConsentRoundContext(
                        current.RoundNumber,
                        current.ProposedDoctorAvailabilityId,
                        current.SideAConsentStatus,
                        current.SideBConsentStatus,
                        current.SendAttempts,
                        current.ProposedOutcome);
                });

        // Slots for BOTH the requestor's proposal (after 4b most rows have none) and each
        // current round's confirmed date, resolved together so one query serves both.
        var slotIds = changeRequests
            .Where(c => c.NewDoctorAvailabilityId.HasValue)
            .Select(c => c.NewDoctorAvailabilityId!.Value)
            .Concat(roundsByChangeRequestId.Values.Select(r => r.ProposedDoctorAvailabilityId))
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

        ChangeRequestQueueContext.Apply(dtos, appointmentsById, slotsById, roundsByChangeRequestId);
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

    /// <summary>
    /// Saves a DECIDED change request. All four decision paths -- cancel approve/reject and
    /// reschedule finalize/reject -- and nothing else reach this method.
    ///
    /// <para>Phase 6 (2026-08-08): the <c>DecidedAt</c> stamp that used to live here MOVED into
    /// <see cref="AppointmentChangeRequest.MarkDecided"/>. Stamping here was the flaw -- it put the
    /// timestamp in a different place from the status and actor, so the three could be written
    /// apart. The clock idiom is unchanged: <c>Clock.Now.ToUniversalTime()</c> at each caller, to
    /// match the consent timestamps. <c>AbpClockOptions.Kind</c> is pinned to <c>Utc</c> as of
    /// 2026-08-27, so that conversion is now a no-op rather than a correction; it is kept because
    /// every decision call site reads identically either way.</para>
    /// </summary>
    private async Task PersistChangeRequestAsync(AppointmentChangeRequest changeRequest)
    {
        await _changeRequestRepository.UpdateAsync(changeRequest, autoSave: true);
    }

}
