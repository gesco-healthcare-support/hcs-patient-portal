using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Phase 7 (Category 7, 2026-05-10) -- subscribes to
/// <see cref="InternalStaffQueueDigestEto"/> and dispatches the
/// OLD-parity <c>AppointmentApproveRejectInternal</c> template to one
/// internal-staff recipient with their tenant-wide pending + approved
/// counts.
///
/// <para>Mirrors OLD <c>SchedulerDomain.cs</c>:113 (email leg only).
/// SMS at OLD :105 is intentionally dropped per Phase 1 scope.</para>
/// </summary>
public class InternalStaffQueueDigestEmailHandler :
    ILocalEventHandler<InternalStaffQueueDigestEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly ISettingProvider _settingProvider;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<InternalStaffQueueDigestEmailHandler> _logger;

    public InternalStaffQueueDigestEmailHandler(
        INotificationDispatcher dispatcher,
        ISettingProvider settingProvider,
        ICurrentTenant currentTenant,
        ILogger<InternalStaffQueueDigestEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _settingProvider = settingProvider;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(InternalStaffQueueDigestEto eventData)
    {
        if (eventData == null || string.IsNullOrWhiteSpace(eventData.StaffEmail))
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var portalUrl = await _settingProvider.GetOrNullAsync(
                CaseEvaluationSettings.NotificationsPolicy.PortalBaseUrl);

            var recipients = new List<NotificationRecipient>
            {
                new NotificationRecipient(
                    email: eventData.StaffEmail,
                    role: Appointments.Notifications.RecipientRole.OfficeAdmin,
                    isRegistered: true),
            };

            var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["StaffFirstName"] = eventData.StaffFirstName ?? string.Empty,
                ["PendingAppointmentCount"] = eventData.PendingAppointmentCount,
                ["ApprovedAppointmentCount"] = eventData.ApprovedAppointmentCount,
                ["PortalUrl"] = portalUrl ?? string.Empty,
                ["ClinicName"] = _currentTenant.Name ?? string.Empty,
            };

            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.AppointmentApproveRejectInternal,
                recipients: recipients,
                variables: variables,
                contextTag: $"InternalStaffQueueDigest/{eventData.StaffUserId}");

            _logger.LogDebug(
                "InternalStaffQueueDigestEmailHandler: dispatched to {Email} (pending={Pending}, approved={Approved}).",
                eventData.StaffEmail,
                eventData.PendingAppointmentCount,
                eventData.ApprovedAppointmentCount);
        }
    }
}
