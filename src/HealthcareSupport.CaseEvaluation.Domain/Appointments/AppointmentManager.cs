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

    public AppointmentManager(
        IAppointmentRepository appointmentRepository,
        ILocalEventBus localEventBus)
    {
        _appointmentRepository = appointmentRepository;
        _localEventBus = localEventBus;
    }

    public virtual async Task<Appointment> CreateAsync(Guid patientId, Guid identityUserId, Guid appointmentTypeId, Guid locationId, Guid doctorAvailabilityId, DateTime appointmentDate, string requestConfirmationNumber, AppointmentStatusType appointmentStatus, string? panelNumber = null, DateTime? dueDate = null)
    {
        Check.NotNull(patientId, nameof(patientId));
        Check.NotNull(identityUserId, nameof(identityUserId));
        Check.NotNull(appointmentTypeId, nameof(appointmentTypeId));
        Check.NotNull(locationId, nameof(locationId));
        Check.NotNull(doctorAvailabilityId, nameof(doctorAvailabilityId));
        Check.NotNull(appointmentDate, nameof(appointmentDate));
        Check.NotNullOrWhiteSpace(requestConfirmationNumber, nameof(requestConfirmationNumber));
        Check.Length(requestConfirmationNumber, nameof(requestConfirmationNumber), AppointmentConsts.RequestConfirmationNumberMaxLength);
        Check.NotNull(appointmentStatus, nameof(appointmentStatus));
        Check.Length(panelNumber, nameof(panelNumber), AppointmentConsts.PanelNumberMaxLength);
        EnsureAppointmentDateNotInPast(appointmentDate);
        var appointment = new Appointment(GuidGenerator.Create(), patientId, identityUserId, appointmentTypeId, locationId, doctorAvailabilityId, appointmentDate, requestConfirmationNumber, appointmentStatus, panelNumber, dueDate);
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

    public virtual async Task<Appointment> UpdateAsync(Guid id, Guid patientId, Guid identityUserId, Guid appointmentTypeId, Guid locationId, Guid doctorAvailabilityId, DateTime appointmentDate, string? panelNumber = null, DateTime? dueDate = null, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNull(patientId, nameof(patientId));
        Check.NotNull(identityUserId, nameof(identityUserId));
        Check.NotNull(appointmentTypeId, nameof(appointmentTypeId));
        Check.NotNull(locationId, nameof(locationId));
        Check.NotNull(doctorAvailabilityId, nameof(doctorAvailabilityId));
        Check.NotNull(appointmentDate, nameof(appointmentDate));
        Check.Length(panelNumber, nameof(panelNumber), AppointmentConsts.PanelNumberMaxLength);
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
            throw new BusinessException("CaseEvaluation:AppointmentInvalidTransition")
                .WithData("from", fromStatus)
                .WithData("trigger", trigger);
        }

        machine.Fire(trigger);

        if (trigger == AppointmentTransitionTrigger.Approve)
        {
            appointment.AppointmentApproveDate = DateTime.UtcNow;
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
            .Permit(AppointmentTransitionTrigger.Reject, AppointmentStatusType.Rejected);

        machine.Configure(AppointmentStatusType.Approved)
            .Permit(AppointmentTransitionTrigger.RequestCancellation, AppointmentStatusType.CancellationRequested)
            .Permit(AppointmentTransitionTrigger.RequestReschedule, AppointmentStatusType.RescheduleRequested)
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
