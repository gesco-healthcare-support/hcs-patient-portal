using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.Enums;
using Stateless;
using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.Appointments;

public class AppointmentManager : DomainService
{
    protected IAppointmentRepository _appointmentRepository;
    protected ILocalEventBus _localEventBus;
    // BUG-043 / T8 (2026-05-27) -- counts Claim Information rows to gate the
    // Pending->Approved transition (see ApplyTransitionAsync).
    protected IAppointmentInjuryDetailRepository _appointmentInjuryDetailRepository;
    // CI1 (2026-06-05) -- counts active Claim Examiner rows to gate the
    // Pending->Approved transition (CE is a required party; see ApplyTransitionAsync).
    protected IRepository<AppointmentClaimExaminer, Guid> _appointmentClaimExaminerRepository;
    // I15/I16 (2026-06-08) -- counts panel-strike-list documents to gate the
    // Pending->Approved transition for PQME appointments (see ApplyTransitionAsync).
    protected IRepository<AppointmentDocument, Guid> _appointmentDocumentRepository;

    public AppointmentManager(
        IAppointmentRepository appointmentRepository,
        ILocalEventBus localEventBus,
        IAppointmentInjuryDetailRepository appointmentInjuryDetailRepository,
        IRepository<AppointmentClaimExaminer, Guid> appointmentClaimExaminerRepository,
        IRepository<AppointmentDocument, Guid> appointmentDocumentRepository)
    {
        _appointmentRepository = appointmentRepository;
        _localEventBus = localEventBus;
        _appointmentInjuryDetailRepository = appointmentInjuryDetailRepository;
        _appointmentClaimExaminerRepository = appointmentClaimExaminerRepository;
        _appointmentDocumentRepository = appointmentDocumentRepository;
    }

    public virtual async Task<Appointment> CreateAsync(Guid patientId, Guid? identityUserId, Guid appointmentTypeId, Guid locationId, Guid doctorAvailabilityId, DateTime appointmentDate, string requestConfirmationNumber, AppointmentStatusType appointmentStatus, string? panelNumber = null, DateTime? dueDate = null, Guid? bookedByUserId = null)
    {
        Check.NotNull(patientId, nameof(patientId));
        Check.NotNull(appointmentTypeId, nameof(appointmentTypeId));
        Check.NotNull(locationId, nameof(locationId));
        Check.NotNull(doctorAvailabilityId, nameof(doctorAvailabilityId));
        Check.NotNull(appointmentDate, nameof(appointmentDate));
        Check.NotNullOrWhiteSpace(requestConfirmationNumber, nameof(requestConfirmationNumber));
        Check.Length(requestConfirmationNumber, nameof(requestConfirmationNumber), AppointmentConsts.RequestConfirmationNumberMaxLength);
        Check.NotNull(appointmentStatus, nameof(appointmentStatus));
        Check.Length(panelNumber, nameof(panelNumber), AppointmentConsts.PanelNumberMaxLength);
        EnsurePanelNumberMatchesType(appointmentTypeId, panelNumber);
        EnsureAppointmentDateNotInPast(appointmentDate);
        var appointment = new Appointment(GuidGenerator.Create(), patientId, identityUserId, appointmentTypeId, locationId, doctorAvailabilityId, appointmentDate, requestConfirmationNumber, appointmentStatus, panelNumber, dueDate);
        if (bookedByUserId.HasValue)
        {
            // R2-2: stamp the logged-in booker so the appointment is always
            // visible to whoever booked it, even when the audit CreatorId is null.
            appointment.RecordBookedBy(bookedByUserId.Value);
        }
        if (appointmentStatus == AppointmentStatusType.Approved)
        {
            // Companion-field stamp when an aggregate is created already
            // in Approved state (internal-user fast-path -- office-side
            // booker is implicitly the approver). Symmetric with the
            // transition-time stamp in ApplyTransitionAsync's Approve
            // branch.
            appointment.AppointmentApproveDate = DateTime.UtcNow;
        }
        return await _appointmentRepository.InsertAsync(appointment);
    }

    /// <summary>
    /// Issue #115 (2026-05-13) -- domain-layer invariant. Rejects any
    /// AppointmentDate strictly earlier than today (date-only compare).
    /// Defense-in-depth: BookingPolicyValidator already runs this check
    /// on the Create path at the AppService layer, but the Update path
    /// previously skipped it entirely. Putting the guard here closes
    /// every path through the domain regardless of which AppService
    /// happens to call it.
    ///
    /// Re-uses <see cref="CaseEvaluationDomainErrorCodes.AppointmentBookingDateInsideLeadTime"/>
    /// with <c>leadTimeDays=0</c> so the existing localized message
    /// chain renders. "Today" is local server time per
    /// <see cref="DateTime.Today"/>; this matches the Create-path
    /// comparison anchor in BookingPolicyValidator.
    /// </summary>
    private static void EnsureAppointmentDateNotInPast(DateTime appointmentDate)
    {
        if (appointmentDate.Date < DateTime.Today)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentBookingDateInsideLeadTime)
                .WithData("leadTimeDays", 0);
        }
    }

    /// <summary>
    /// AF3 + AF4 (2026-06-04) -- couples Panel Number to the appointment type.
    /// Only a PQME carries a state-issued panel number, so:
    ///   - PQME with a blank panel number is rejected (the number is required).
    ///   - any non-PQME type (AME / IME) with a panel number present is rejected
    ///     -- a value there means the wrong type was chosen or the number was
    ///     fabricated, so the submission is blocked rather than silently cleared.
    /// The Angular add + view/edit forms disable + clear the field for non-PQME
    /// and require it for PQME, so legitimate submissions never violate this;
    /// this domain check is the authoritative guard (and the defense-in-depth
    /// backstop for a tampered/bypassed client -- closes the OBS-24 gap for this
    /// field). Keyed off the seeded PQME identity, not a type-name substring, so
    /// it survives the AF1 label renames.
    /// </summary>
    private static void EnsurePanelNumberMatchesType(Guid appointmentTypeId, string? panelNumber)
    {
        var isPqme = appointmentTypeId == CaseEvaluationSeedIds.AppointmentTypes.PanelQme;
        var hasPanelNumber = !string.IsNullOrWhiteSpace(panelNumber);

        if (isPqme && !hasPanelNumber)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentPanelNumberRequiredForPqme);
        }

        if (!isPqme && hasPanelNumber)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentPanelNumberNotAllowedForType)
                .WithData("appointmentTypeId", appointmentTypeId);
        }
    }

    /// <summary>
    /// Phase 11g (2026-05-04) -- Re-Submit (OLD <c>IsReRequestForm</c>) gate.
    /// Looks up the source by confirmation number, validates it is in
    /// status <see cref="AppointmentStatusType.Rejected"/>, and returns
    /// the source for the caller to thread through the standard create
    /// pipeline. The new appointment must reuse the source's confirmation
    /// number (per OLD <c>AppointmentDomain.cs:262-266</c>); the caller
    /// is responsible for that copy because the conf# is part of the
    /// <see cref="Appointment"/> ctor signature.
    /// </summary>
    /// <exception cref="EntityNotFoundException">When no source row matches.</exception>
    /// <exception cref="BusinessException">
    /// With code <c>AppointmentReSubmitSourceNotRejected</c> when the
    /// source is in any status other than <c>Rejected</c>. Carries the
    /// source <c>RequestConfirmationNumber</c> + <c>AppointmentStatus</c>
    /// as <c>WithData</c>.
    /// </exception>
    public virtual async Task<Appointment> LoadResubmitSourceAsync(string sourceConfirmationNumber)
    {
        Check.NotNullOrWhiteSpace(sourceConfirmationNumber, nameof(sourceConfirmationNumber));

        var source = await _appointmentRepository.FindByConfirmationNumberAsync(sourceConfirmationNumber);
        if (source == null)
        {
            throw new EntityNotFoundException(typeof(Appointment), sourceConfirmationNumber);
        }

        if (!AppointmentLifecycleValidators.CanResubmit(source.AppointmentStatus))
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.AppointmentReSubmitSourceNotRejected)
                .WithData("confirmationNumber", sourceConfirmationNumber)
                .WithData("status", source.AppointmentStatus);
        }

        return source;
    }

    /// <summary>
    /// Phase 11g (2026-05-04) -- Reval (OLD <c>IsRevolutionForm</c>) gate.
    /// Looks up the source by confirmation number; validates it is in
    /// status <see cref="AppointmentStatusType.Approved"/>; surfaces a
    /// distinct error code when the caller is IT Admin (verbatim OLD
    /// hint message) versus a non-admin caller (verbatim OLD
    /// patient-facing message). Per OLD <c>AppointmentDomain.cs:171-173</c>
    /// the admin override is hint-only -- it does NOT bypass the
    /// "must be Approved" gate. Returns the source for the caller to
    /// thread through the standard create pipeline; the new appointment
    /// gets a freshly generated confirmation number (OLD line 268).
    /// </summary>
    public virtual async Task<Appointment> LoadRevalSourceAsync(string sourceConfirmationNumber, bool callerIsItAdmin)
    {
        Check.NotNullOrWhiteSpace(sourceConfirmationNumber, nameof(sourceConfirmationNumber));

        var source = await _appointmentRepository.FindByConfirmationNumberAsync(sourceConfirmationNumber);
        if (source == null)
        {
            throw new EntityNotFoundException(typeof(Appointment), sourceConfirmationNumber);
        }

        if (!AppointmentLifecycleValidators.CanCreateReval(source.AppointmentStatus, callerIsItAdmin))
        {
            var errorCode = AppointmentLifecycleValidators.ResolveRevalRejectionCode(callerIsItAdmin);
            throw new BusinessException(errorCode)
                .WithData("confirmationNumber", sourceConfirmationNumber)
                .WithData("status", source.AppointmentStatus);
        }

        return source;
    }

    public virtual async Task<Appointment> UpdateAsync(Guid id, Guid patientId, Guid? identityUserId, Guid appointmentTypeId, Guid locationId, Guid doctorAvailabilityId, DateTime appointmentDate, string? panelNumber = null, DateTime? dueDate = null, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNull(patientId, nameof(patientId));
        Check.NotNull(appointmentTypeId, nameof(appointmentTypeId));
        Check.NotNull(locationId, nameof(locationId));
        Check.NotNull(doctorAvailabilityId, nameof(doctorAvailabilityId));
        Check.NotNull(appointmentDate, nameof(appointmentDate));
        Check.Length(panelNumber, nameof(panelNumber), AppointmentConsts.PanelNumberMaxLength);
        EnsurePanelNumberMatchesType(appointmentTypeId, panelNumber);
        var appointment = await _appointmentRepository.GetAsync(id);
        // Issue #115 (2026-05-13): only enforce the not-in-past rule
        // when the date is actually changing. Completed appointments
        // (CheckedIn / CheckedOut / Billed) legitimately have past
        // dates; editing PanelNumber or PatientId on them must still
        // work. Moving an appointment TO a past date is the attack.
        if (appointment.AppointmentDate.Date != appointmentDate.Date)
        {
            EnsureAppointmentDateNotInPast(appointmentDate);
        }
        appointment.PatientId = patientId;
        appointment.IdentityUserId = identityUserId;
        appointment.AppointmentTypeId = appointmentTypeId;
        appointment.LocationId = locationId;
        appointment.DoctorAvailabilityId = doctorAvailabilityId;
        appointment.AppointmentDate = appointmentDate;
        appointment.PanelNumber = panelNumber;
        appointment.DueDate = dueDate;
        appointment.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _appointmentRepository.UpdateAsync(appointment);
    }

    /// <summary>
    /// Pending -> Approved. Stamps <c>AppointmentApproveDate</c> on entry; publishes
    /// <see cref="AppointmentStatusChangedEto"/> for slot-cascade + email subscribers.
    /// </summary>
    public virtual Task<Appointment> ApproveAsync(Guid id, Guid? actingUserId)
        => TransitionAsync(id, AppointmentTransitionTrigger.Approve, reason: null, actingUserId);

    /// <summary>Pending -> Rejected.</summary>
    public virtual Task<Appointment> RejectAsync(Guid id, string? reason, Guid? actingUserId)
        => TransitionAsync(id, AppointmentTransitionTrigger.Reject, reason, actingUserId);

    /// <summary>
    /// Phase 16 (2026-05-04) -- Approved -> RescheduleRequested. Used
    /// when an external user submits a reschedule request through
    /// <see cref="AppointmentChangeRequests.AppointmentChangeRequestManager.SubmitRescheduleAsync"/>.
    /// The state machine permits the trigger from Approved only (per
    /// <see cref="BuildMachine"/>); other source states surface the
    /// generic invalid-transition error.
    /// </summary>
    public virtual Task<Appointment> RequestRescheduleAsync(Guid id, string? reason, Guid? actingUserId)
        => TransitionAsync(id, AppointmentTransitionTrigger.RequestReschedule, reason, actingUserId);

    /// <summary>
    /// G-02-05 (2026-06-01) -- one-step internal-staff cancel of an Approved
    /// appointment. Maps the chosen NoBill/Late outcome to the matching
    /// direct-cancel trigger; the transition stamps CancellationReason +
    /// CancelledById and frees the slot via the capacity model. Mirrors OLD's
    /// internal-user CancelledNoBill path (AppointmentDomain.Update), which did
    /// not require a patient/attorney change request. Permitted from Approved only.
    /// </summary>
    public virtual Task<Appointment> DirectCancelAsync(Guid id, AppointmentStatusType outcome, string? reason, Guid? actingUserId)
    {
        var trigger = outcome == AppointmentStatusType.CancelledLate
            ? AppointmentTransitionTrigger.DirectCancelLate
            : AppointmentTransitionTrigger.DirectCancel;
        return TransitionAsync(id, trigger, reason, actingUserId);
    }

    /// <summary>
    /// Send Back (2026-06-14) -- Pending -> InfoRequested. Fired when staff
    /// request more information; the AppointmentInfoRequest row (note + flagged
    /// fields) is created by the Application layer alongside this transition.
    /// Permitted from Pending only.
    /// </summary>
    public virtual Task<Appointment> SendBackAsync(Guid id, Guid? actingUserId)
        => TransitionAsync(id, AppointmentTransitionTrigger.SendBack, reason: null, actingUserId);

    /// <summary>
    /// Resubmit (2026-06-14) -- InfoRequested -> Pending. Fired when the external
    /// user resubmits their corrections; the open AppointmentInfoRequest row is
    /// marked Resolved by the Application layer. Permitted from InfoRequested only.
    /// </summary>
    public virtual Task<Appointment> ResubmitInfoAsync(Guid id, Guid? actingUserId)
        => TransitionAsync(id, AppointmentTransitionTrigger.SaveAndResubmit, reason: null, actingUserId);

    private async Task<Appointment> TransitionAsync(Guid id, AppointmentTransitionTrigger trigger, string? reason, Guid? actingUserId)
    {
        var appointment = await _appointmentRepository.GetAsync(id);
        return await ApplyTransitionAsync(appointment, trigger, reason, actingUserId);
    }

    private async Task<Appointment> ApplyTransitionAsync(Appointment appointment, AppointmentTransitionTrigger trigger, string? reason, Guid? actingUserId)
    {
        var fromStatus = appointment.AppointmentStatus;
        var machine = BuildMachine(appointment);

        if (!machine.CanFire(trigger))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentInvalidTransition)
                .WithData("from", fromStatus)
                .WithData("trigger", trigger);
        }

        if (trigger == AppointmentTransitionTrigger.Approve)
        {
            // BUG-043 / T8 (2026-05-27) -- defense-in-depth behind the
            // client-side guard (T7): an appointment cannot be approved
            // without at least one Claim Information (injury detail) row.
            // Checked BEFORE the state machine fires so a failed gate
            // leaves the status unchanged. Only the Pending->Approved
            // transition is gated; the create-as-Approved internal
            // fast-path attaches injuries after creation and is out of
            // scope (see CreateAsync above + the T8 plan).
            var injuryCount = await _appointmentInjuryDetailRepository.GetCountAsync(appointmentId: appointment.Id);
            if (injuryCount < 1)
            {
                throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentApprovalRequiresInjuryDetail)
                    .WithData("appointmentId", appointment.Id);
            }

            // CI1 (2026-06-05) -- CE became a required first-class party. Mirror
            // the injury-detail gate: Pending->Approved requires at least one
            // active Claim Examiner. Server backstop behind the client-side
            // CE-section gate; the create-as-Approved fast-path is out of scope
            // (attaches parties after creation), same as the injury guard.
            var claimExaminerCount = await _appointmentClaimExaminerRepository.CountAsync(
                ce => ce.AppointmentId == appointment.Id && ce.IsActive);
            if (claimExaminerCount < 1)
            {
                throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentApprovalRequiresClaimExaminer)
                    .WithData("appointmentId", appointment.Id);
            }

            // I15/I16 (2026-06-08) -- a PQME cannot be approved until a panel
            // strike list document is on file (a doc flagged IsPanelStrikeList,
            // set when uploaded under the "Panel Strike List" category). A PQME
            // may be BOOKED without it (uploaded later); only approval is gated.
            // Same defense-in-depth pattern as the injury + CE gates above.
            if (appointment.AppointmentTypeId == CaseEvaluationSeedIds.AppointmentTypes.PanelQme)
            {
                var strikeListCount = await _appointmentDocumentRepository.CountAsync(
                    d => d.AppointmentId == appointment.Id && d.IsPanelStrikeList);
                if (strikeListCount < 1)
                {
                    throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentApprovalRequiresPanelStrikeList)
                        .WithData("appointmentId", appointment.Id);
                }
            }
        }

        machine.Fire(trigger);

        if (trigger == AppointmentTransitionTrigger.Approve)
        {
            appointment.AppointmentApproveDate = DateTime.UtcNow;
        }
        else if (trigger == AppointmentTransitionTrigger.Reject)
        {
            // Symmetric companion-field write for Reject -- mirrors the
            // Approve branch's stamp. Without this the reason flows in
            // via the parameter and emits on the StatusChangedEto but
            // never reaches the entity, leaving RejectionNotes NULL in
            // the database after a /reject call.
            appointment.RejectionNotes = reason;
            appointment.RejectedById = actingUserId;
        }
        else if (trigger == AppointmentTransitionTrigger.DirectCancel
            || trigger == AppointmentTransitionTrigger.DirectCancelLate)
        {
            // G-02-05 (OLD AppointmentDomain.Update:537-550): a one-step staff
            // cancel stamps the reason + actor. The terminal CancelledNoBill/Late
            // status drops the appointment from the slot's active count, freeing
            // capacity (Business Rule 4) -- no slot write needed.
            appointment.CancellationReason = reason;
            appointment.CancelledById = actingUserId;
        }

        await _appointmentRepository.UpdateAsync(appointment, autoSave: true);

        await _localEventBus.PublishAsync(new AppointmentStatusChangedEto(
            appointment.Id,
            appointment.TenantId,
            fromStatus,
            appointment.AppointmentStatus,
            actingUserId,
            reason,
            DateTime.UtcNow,
            doctorAvailabilityId: appointment.DoctorAvailabilityId));

        return appointment;
    }

    /// <summary>
    /// Builds the appointment status state machine. Per OLD spec
    /// (Phase 0.2, 2026-05-01) Pending transitions only to Approved or Rejected;
    /// there is no SendBack / AwaitingMoreInfo / SaveAndResubmit path. Cancel /
    /// Reschedule / day-of-exam triggers are reachable in the graph but
    /// unreachable through the API surface until Wave 3
    /// (appointment-change-requests).
    /// </summary>
    private static StateMachine<AppointmentStatusType, AppointmentTransitionTrigger> BuildMachine(Appointment appointment)
    {
        var machine = new StateMachine<AppointmentStatusType, AppointmentTransitionTrigger>(
            () => appointment.AppointmentStatus,
            s => appointment.AppointmentStatus = s);

        machine.Configure(AppointmentStatusType.Pending)
            .Permit(AppointmentTransitionTrigger.Approve, AppointmentStatusType.Approved)
            .Permit(AppointmentTransitionTrigger.Reject, AppointmentStatusType.Rejected)
            // Send Back (2026-06-14): staff request more information.
            .Permit(AppointmentTransitionTrigger.SendBack, AppointmentStatusType.InfoRequested);

        // Info Requested is transient: the external user resubmits their fixes
        // and the appointment returns to Pending for staff review. The slot
        // stays Reserved throughout (InfoRequested is not a terminal status).
        machine.Configure(AppointmentStatusType.InfoRequested)
            .Permit(AppointmentTransitionTrigger.SaveAndResubmit, AppointmentStatusType.Pending);

        machine.Configure(AppointmentStatusType.Approved)
            .Permit(AppointmentTransitionTrigger.RequestCancellation, AppointmentStatusType.CancellationRequested)
            .Permit(AppointmentTransitionTrigger.RequestReschedule, AppointmentStatusType.RescheduleRequested)
            .Permit(AppointmentTransitionTrigger.DirectCancel, AppointmentStatusType.CancelledNoBill)
            .Permit(AppointmentTransitionTrigger.DirectCancelLate, AppointmentStatusType.CancelledLate)
            .Permit(AppointmentTransitionTrigger.MarkNoShow, AppointmentStatusType.NoShow)
            .Permit(AppointmentTransitionTrigger.CheckIn, AppointmentStatusType.CheckedIn);

        machine.Configure(AppointmentStatusType.CancellationRequested)
            .Permit(AppointmentTransitionTrigger.ConfirmCancellation, AppointmentStatusType.CancelledNoBill)
            .Permit(AppointmentTransitionTrigger.ConfirmCancellationLate, AppointmentStatusType.CancelledLate);

        machine.Configure(AppointmentStatusType.RescheduleRequested)
            .Permit(AppointmentTransitionTrigger.ConfirmReschedule, AppointmentStatusType.RescheduledNoBill)
            .Permit(AppointmentTransitionTrigger.ConfirmRescheduleLate, AppointmentStatusType.RescheduledLate);

        machine.Configure(AppointmentStatusType.CheckedIn)
            .Permit(AppointmentTransitionTrigger.CheckOut, AppointmentStatusType.CheckedOut);

        machine.Configure(AppointmentStatusType.CheckedOut)
            .Permit(AppointmentTransitionTrigger.Bill, AppointmentStatusType.Billed);

        return machine;
    }
}
