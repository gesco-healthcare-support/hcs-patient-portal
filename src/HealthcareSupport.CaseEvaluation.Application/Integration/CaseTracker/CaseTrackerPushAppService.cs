using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Manual (re-)push of an approved appointment to the Case Tracker. Reuses the exact same
/// <see cref="CaseTrackerIntakeQueue"/> path as the automatic approval trigger, so a manual retry
/// cannot diverge from what the automatic push would have sent.
/// </summary>
[Authorize]
public class CaseTrackerPushAppService : CaseEvaluationAppService, ICaseTrackerPushAppService
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly CaseTrackerIntakeQueue _intakeQueue;
    private readonly ISettingProvider _settingProvider;
    private readonly ILogger<CaseTrackerPushAppService> _logger;

    public CaseTrackerPushAppService(
        IRepository<Appointment, Guid> appointmentRepository,
        CaseTrackerIntakeQueue intakeQueue,
        ISettingProvider settingProvider,
        ILogger<CaseTrackerPushAppService> logger)
    {
        _appointmentRepository = appointmentRepository;
        _intakeQueue = intakeQueue;
        _settingProvider = settingProvider;
        _logger = logger;
    }

    [Authorize(CaseEvaluationPermissions.Appointments.PushToCaseTracker)]
    public virtual async Task<CaseTrackerPushQueuedDto> PushAppointmentAsync(Guid appointmentId)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", "AppointmentId"]);
        }

        var appointment = await _appointmentRepository.GetAsync(appointmentId);

        // Only an approved appointment is a case. Pushing anything earlier would create a case for
        // work the office has not accepted yet.
        if (appointment.AppointmentStatus == AppointmentStatusType.Pending)
        {
            throw new UserFriendlyException(
                L["Only an approved appointment can be pushed to the Case Tracker."]);
        }

        var row = await _intakeQueue.EnqueueIntakeAsync(appointmentId, appointment.TenantId);

        var pushEnabled = await _settingProvider.IsTrueAsync(
            CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled);

        _logger.LogInformation(
            "CaseTrackerPushAppService: appointment {AppointmentId} queued manually by {UserId} (row {RowId}, pushEnabled={PushEnabled}).",
            appointmentId, CurrentUser.Id, row.Id, pushEnabled);

        return new CaseTrackerPushQueuedDto
        {
            AppointmentId = appointmentId,
            OutboxItemId = row.Id,
            Status = row.Status.ToString(),
            PushEnabled = pushEnabled,
        };
    }
}
