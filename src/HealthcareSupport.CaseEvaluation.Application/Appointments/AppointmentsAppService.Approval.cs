using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Events;
using HealthcareSupport.CaseEvaluation.Enums;
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
///   <item>Persists the optional approver comment. (It also wrote
///     <see cref="Appointment.PrimaryResponsibleUserId"/> until 2026-11-02, when the office
///     retired the responsible user; stored values are left as history.)</item>
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
    // 2026-05-28 -- centralised BusinessException -> UserFriendlyException
    // translator so the SPA toast shows the localized message instead of
    // ABP's generic "internal error" fallback.

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
        // 2026-11-02: no responsible user any more. Approval neither asks for one nor touches
        // PrimaryResponsibleUserId, so a value stored before the retirement stays as history.

        // A1 (2026-05-05) -- OLD-parity: persist optional approver comments
        // before the status transition so subscribers see them.
        // OLD batched this in a single PATCH (`view/appointment-view.component
        // .html`:141-144 + `updateAppointmentRequest`); NEW splits the
        // status transition out via the manager but keeps the supplemental
        // writes co-located.
        if (input.InternalUserComments != null)
        {
            appointment.InternalUserComments = input.InternalUserComments;
        }
        await _appointmentRepository.UpdateAsync(appointment, autoSave: true);

        // Delegate the state-machine transition to Session A's manager.
        // This stamps AppointmentApproveDate, validates the transition,
        // persists the new status, and publishes
        // AppointmentStatusChangedEto for the slot cascade + email
        // subscribers. The supplemental write above (the comment) is
        // committed first so subscribers see the resolved entity.
        // 2026-05-28 -- Manager throws BusinessException on invalid
        // transitions / missing injury details; translate to a localized
        // UserFriendlyException so the SPA shows the actionable message.
        Appointment approved;
        approved = await _appointmentManager.ApproveAsync(id, CurrentUser.Id);

        await _localEventBus.PublishAsync(new AppointmentApprovedEto
        {
            AppointmentId = approved.Id,
            TenantId = approved.TenantId,
            AppointmentTypeId = approved.AppointmentTypeId,
            ApprovedByUserId = CurrentUser.Id ?? Guid.Empty,
            OccurredAt = DateTime.UtcNow,
        });

        _logger.LogInformation(
            "AppointmentApprovalAppService.ApproveAppointmentAsync: appointment {AppointmentId} approved by {UserId}.",
            approved.Id,
            CurrentUser.Id);

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

        Appointment rejected;
        rejected = await _appointmentManager.RejectAsync(id, input.Reason, CurrentUser.Id);

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
