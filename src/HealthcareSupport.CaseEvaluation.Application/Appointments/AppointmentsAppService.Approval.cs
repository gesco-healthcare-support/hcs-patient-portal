using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Events;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 12 (2026-05-04) -- richer Approve / Reject AppService surface.
///
/// <para>Class-naming deviation rationale: the user's Phase 12 directive
/// asked for a <c>partial class AppointmentsAppService</c> in this file
/// AND said "DO NOT edit the main `AppointmentsAppService.cs`". Partial
/// classes need the <c>partial</c> keyword on every fragment, including
/// the main file's declaration. To honor the no-edit rule strictly,
/// Phase 12 ships a sibling class <see cref="AppointmentApprovalAppService"/>
/// in the user's requested file path. Functional outcome is identical
/// (Approve / Reject endpoints under <c>api/app/appointment-approvals</c>);
/// only the class layout differs. Sync 3 (post-parity demo) is the
/// natural point to converge into a single AppService once Session A's
/// <c>AppointmentManager.CreateAsync</c> rewrite lands.</para>
///
/// <para>Sits alongside Session A's existing thin
/// <c>AppointmentsAppService.ApproveAsync(Guid)</c> /
/// <c>RejectAsync(Guid, RejectAppointmentInput)</c>. Both surfaces
/// delegate the actual state transition to
/// <c>AppointmentManager.ApproveAsync</c> / <c>RejectAsync</c> (Session
/// A territory) so we never duplicate the state-machine guard or the
/// <c>AppointmentStatusChangedEto</c> publish that drives the existing
/// slot cascade.</para>
///
/// <para>What Phase 12 adds on top of the manager's transition:</para>
/// <list type="bullet">
///   <item>Idempotency check at the AppService boundary surfacing OLD's
///     verbatim "Appointment Already Approved" / "Appointment Already
///     Rejected" strings before the state machine fires (cleaner UX
///     than the generic <c>InvalidTransition</c> error).</item>
///   <item>Persists <see cref="Appointment.PrimaryResponsibleUserId"/>
///     from the input DTO (audit B-row gap).</item>
///   <item>Persists <see cref="Appointment.RejectionNotes"/> +
///     <see cref="Appointment.RejectedById"/> on rejection.</item>
///   <item>Records the patient-match-override decision on
///     <see cref="Appointment.IsPatientAlreadyExist"/> (the actual
///     patient-row split is deferred to Session A's manager rewrite,
///     per the user's Phase 12 directive).</item>
///   <item>Publishes <see cref="AppointmentApprovedEto"/> /
///     <see cref="AppointmentRejectedEto"/> for the
///     <c>PackageDocumentQueueHandler</c> + future approval-email
///     handlers (Phase 14).</item>
/// </list>
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize]
public class AppointmentApprovalAppService : CaseEvaluationAppService, IAppointmentApprovalAppService
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly AppointmentManager _appointmentManager;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<AppointmentApprovalAppService> _logger;

    public AppointmentApprovalAppService(
        IAppointmentRepository appointmentRepository,
        AppointmentManager appointmentManager,
        ILocalEventBus localEventBus,
        ILogger<AppointmentApprovalAppService> logger)
    {
        _appointmentRepository = appointmentRepository;
        _appointmentManager = appointmentManager;
        _localEventBus = localEventBus;
        _logger = logger;
    }

    [Authorize(CaseEvaluationPermissions.Appointments.Approve)]
    public virtual async Task<AppointmentDto> ApproveAppointmentAsync(Guid id, ApproveAppointmentInput input)
    {
        Check.NotNull(input, nameof(input));

        var appointment = await _appointmentRepository.GetAsync(id);
        AppointmentApprovalValidator.EnsureApprovable(appointment, input);

        appointment.PrimaryResponsibleUserId = input.PrimaryResponsibleUserId;
        var overridden = AppointmentApprovalValidator.ShouldOverridePatientMatch(appointment, input);
        if (overridden)
        {
            // OLD parity (`AppointmentDomain.cs`:217): when the staff
            // approver overrides the dedup match, the appointment is
            // treated as a NEW patient. Phase 12 records the decision
            // by clearing the dedup flag; the actual patient-row split
            // (creating a new Patient row + relinking) is downstream
            // work in Session A's manager rewrite.
            appointment.IsPatientAlreadyExist = false;
        }
        await _appointmentRepository.UpdateAsync(appointment, autoSave: true);

        // Delegate the state-machine transition to Session A's manager.
        // This stamps AppointmentApproveDate, validates the transition,
        // persists the new status, and publishes
        // AppointmentStatusChangedEto for the slot cascade + email
        // subscribers. The supplemental write above (PrimaryResponsibleUserId)
        // is committed first so subscribers see the resolved entity.
        var approved = await _appointmentManager.ApproveAsync(id, CurrentUser.Id);

        await _localEventBus.PublishAsync(new AppointmentApprovedEto
        {
            AppointmentId = approved.Id,
            TenantId = approved.TenantId,
            AppointmentTypeId = approved.AppointmentTypeId,
            PrimaryResponsibleUserId = input.PrimaryResponsibleUserId,
            PatientMatchOverridden = overridden,
            ApprovedByUserId = CurrentUser.Id ?? Guid.Empty,
            OccurredAt = DateTime.UtcNow,
        });

        _logger.LogInformation(
            "AppointmentApprovalAppService.ApproveAppointmentAsync: appointment {AppointmentId} approved by {UserId} with responsible {ResponsibleUserId}, override={Override}.",
            approved.Id,
            CurrentUser.Id,
            input.PrimaryResponsibleUserId,
            overridden);

        return ObjectMapper.Map<Appointment, AppointmentDto>(approved);
    }

    [Authorize(CaseEvaluationPermissions.Appointments.Reject)]
    public virtual async Task<AppointmentDto> RejectAppointmentAsync(Guid id, RejectAppointmentInput input)
    {
        Check.NotNull(input, nameof(input));

        var appointment = await _appointmentRepository.GetAsync(id);
        AppointmentApprovalValidator.EnsureRejectable(appointment, input);

        appointment.RejectionNotes = input.Reason;
        appointment.RejectedById = CurrentUser.Id;
        await _appointmentRepository.UpdateAsync(appointment, autoSave: true);

        var rejected = await _appointmentManager.RejectAsync(id, input.Reason, CurrentUser.Id);

        await _localEventBus.PublishAsync(new AppointmentRejectedEto
        {
            AppointmentId = rejected.Id,
            TenantId = rejected.TenantId,
            RejectionNotes = input.Reason ?? string.Empty,
            RejectedByUserId = CurrentUser.Id ?? Guid.Empty,
            OccurredAt = DateTime.UtcNow,
        });

        _logger.LogInformation(
            "AppointmentApprovalAppService.RejectAppointmentAsync: appointment {AppointmentId} rejected by {UserId}.",
            rejected.Id,
            CurrentUser.Id);

        return ObjectMapper.Map<Appointment, AppointmentDto>(rejected);
    }
}
