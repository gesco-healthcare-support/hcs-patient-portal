using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 15 (2026-05-04) -- external-user cancel submit. AppService
/// composes the per-row edit-access policy
/// (<see cref="AppointmentAccessRules.CanEdit"/>) with the domain
/// service's
/// <see cref="AppointmentChangeRequestManager.SubmitCancellationAsync"/>
/// orchestrator.
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize]
public class AppointmentChangeRequestsAppService : CaseEvaluationAppService, IAppointmentChangeRequestsAppService
{
    private readonly AppointmentChangeRequestManager _manager;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<AppointmentAccessor, Guid> _appointmentAccessorRepository;

    public AppointmentChangeRequestsAppService(
        AppointmentChangeRequestManager manager,
        IAppointmentRepository appointmentRepository,
        IRepository<AppointmentAccessor, Guid> appointmentAccessorRepository)
    {
        _manager = manager;
        _appointmentRepository = appointmentRepository;
        _appointmentAccessorRepository = appointmentAccessorRepository;
    }

    [Authorize]
    public virtual async Task<AppointmentChangeRequestDto> RequestCancellationAsync(
        Guid appointmentId,
        RequestCancellationDto input)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }
        if (input == null || string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["CancellationReason"]]);
        }

        // Per-row edit-access policy. Internal users (admin / Clinic
        // Staff / Staff Supervisor / IT Admin / Doctor) bypass; external
        // users must be the creator OR hold an accessor row with
        // AccessType.Edit. View accessors are rejected.
        await EnsureCanEditAsync(appointmentId);

        var changeRequest = await _manager.SubmitCancellationAsync(
            appointmentId: appointmentId,
            cancellationReason: input.Reason,
            actingUserId: CurrentUser.Id);

        return ObjectMapper.Map<AppointmentChangeRequest, AppointmentChangeRequestDto>(changeRequest);
    }

    private async Task EnsureCanEditAsync(Guid appointmentId)
    {
        var appointment = await _appointmentRepository.FindAsync(appointmentId);
        if (appointment == null)
        {
            throw new EntityNotFoundException(typeof(Appointment), appointmentId);
        }

        var callerRoles = CurrentUser.Roles ?? Array.Empty<string>();
        var isInternal = BookingFlowRoles.IsInternalUserCaller(callerRoles);

        var accessorQuery = await _appointmentAccessorRepository.GetQueryableAsync();
        var entries = await AsyncExecuter.ToListAsync(
            accessorQuery
                .Where(a => a.AppointmentId == appointmentId)
                .Select(a => new AppointmentAccessRules.AccessorEntry(a.IdentityUserId, a.AccessTypeId)));

        var canEdit = AppointmentAccessRules.CanEdit(
            callerUserId: CurrentUser.Id,
            callerIsInternalUser: isInternal,
            appointmentCreatorId: appointment.CreatorId,
            accessorEntries: entries);

        if (!canEdit)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestEditAccessRequired)
                .WithData("appointmentId", appointmentId);
        }
    }
}
