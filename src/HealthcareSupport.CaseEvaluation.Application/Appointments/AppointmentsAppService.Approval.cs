using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Events;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;

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
    // A1 (2026-05-05) -- internal-user dropdown source for the Approve modal.
    private readonly IdentityUserManager _identityUserManager;

    public AppointmentApprovalAppService(
        IAppointmentRepository appointmentRepository,
        AppointmentManager appointmentManager,
        ILocalEventBus localEventBus,
        ILogger<AppointmentApprovalAppService> logger,
        IdentityUserManager identityUserManager)
    {
        _appointmentRepository = appointmentRepository;
        _appointmentManager = appointmentManager;
        _localEventBus = localEventBus;
        _logger = logger;
        _identityUserManager = identityUserManager;
    }

    [Authorize(CaseEvaluationPermissions.Appointments.Approve)]
    public virtual async Task<AppointmentDto> ApproveAppointmentAsync(Guid id, ApproveAppointmentInput input)
    {
        Check.NotNull(input, nameof(input));

        var appointment = await _appointmentRepository.GetAsync(id);
        AppointmentApprovalValidator.EnsureApprovable(appointment, input);

        appointment.PrimaryResponsibleUserId = input.PrimaryResponsibleUserId;
        // A1 (2026-05-05) -- OLD-parity: persist optional approver comments
        // alongside the responsible-user write so the same UoW commits both.
        // OLD batched this in a single PATCH (`view/appointment-view.component
        // .html`:141-144 + `updateAppointmentRequest`); NEW splits the
        // status transition out via the manager but keeps the supplemental
        // writes co-located.
        if (input.InternalUserComments != null)
        {
            appointment.InternalUserComments = input.InternalUserComments;
        }
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

    /// <summary>
    /// A1 (2026-05-05) -- backs the Responsible-User dropdown on the Approve
    /// modal. Returns identity users in the current tenant whose role is one
    /// of <see cref="BookingFlowRoles.InternalUserRoles"/>. Mirrors OLD's
    /// <c>internalUserNameLookUps</c> source. Tenant scoping is enforced
    /// transparently by ABP's IMultiTenant filter on IdentityUser; no manual
    /// where-clause needed.
    /// </summary>
    [Authorize(CaseEvaluationPermissions.Appointments.Default)]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetInternalUserLookupAsync(
        LookupRequestDto input)
    {
        // Aggregate users across the 5 internal roles. UserManager's
        // GetUsersInRoleAsync uses ABP's identity stack (tenant-scoped + role-
        // scoped). Dedupe on Id because admin-tier users may hold multiple
        // internal roles.
        var byId = new Dictionary<Guid, IdentityUser>();
        foreach (var roleName in BookingFlowRoles.InternalUserRoles)
        {
            var roleUsers = await _identityUserManager.GetUsersInRoleAsync(roleName);
            foreach (var user in roleUsers)
            {
                byId[user.Id] = user;
            }
        }

        IEnumerable<IdentityUser> filtered = byId.Values;
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var needle = input.Filter.Trim();
            filtered = filtered.Where(u =>
                (u.Email != null && u.Email.Contains(needle, StringComparison.OrdinalIgnoreCase))
                || (u.UserName != null && u.UserName.Contains(needle, StringComparison.OrdinalIgnoreCase))
                || (u.Name != null && u.Name.Contains(needle, StringComparison.OrdinalIgnoreCase))
                || (u.Surname != null && u.Surname.Contains(needle, StringComparison.OrdinalIgnoreCase)));
        }

        var ordered = filtered
            .OrderBy(u => u.Surname ?? string.Empty)
            .ThenBy(u => u.Name ?? string.Empty)
            .ThenBy(u => u.Email ?? string.Empty)
            .ToList();

        var page = ordered.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = ordered.Count,
            Items = ObjectMapper.Map<List<IdentityUser>, List<LookupDto<Guid>>>(page),
        };
    }
}
