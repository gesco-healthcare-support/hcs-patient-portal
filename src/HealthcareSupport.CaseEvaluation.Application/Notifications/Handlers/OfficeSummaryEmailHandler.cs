using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Sends one office's 08:00 / 16:00 summary (<see cref="OfficeSummaryEto"/>) as ONE email per run to the
/// office notification inbox plus the office's switched-on Intake Staff, each address once
/// (<see cref="OfficeNotificationRecipients"/>). Staff Supervisors are not recipients; they have their own
/// all-offices summary. The context tag names the half-day run, so the afternoon summary is not collapsed
/// onto the morning one by the outbox, while a retry of the same run is.
/// </summary>
public class OfficeSummaryEmailHandler :
    ILocalEventHandler<OfficeSummaryEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly OfficeNotificationRecipients _officeRecipients;
    private readonly ICurrentTenant _currentTenant;
    private readonly IAccountUrlBuilder _accountUrlBuilder;
    private readonly ILogger<OfficeSummaryEmailHandler> _logger;

    public OfficeSummaryEmailHandler(
        INotificationDispatcher dispatcher,
        OfficeNotificationRecipients officeRecipients,
        ICurrentTenant currentTenant,
        IAccountUrlBuilder accountUrlBuilder,
        ILogger<OfficeSummaryEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _officeRecipients = officeRecipients;
        _currentTenant = currentTenant;
        _accountUrlBuilder = accountUrlBuilder;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(OfficeSummaryEto eventData)
    {
        if (eventData == null || !eventData.Counts.HasOutstanding)
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var recipients = await _officeRecipients.GetOfficeInboxAndIntakeAsync("OfficeDailySummary");
            if (recipients.Count == 0)
            {
                _logger.LogWarning(
                    "OfficeSummaryEmailHandler: tenant {TenantId} has no notification inbox and no active Intake Staff; summary not sent.",
                    eventData.TenantId);
                return;
            }

            // Staff-only mail: link to the HOST portal (admin.<base>) where staff work.
            var portalUrl = await _accountUrlBuilder.BuildPortalRootUrlAsync(null);

            var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["SummaryLabel"] = StaffSummaryHtml.LabelFor(eventData.OccurredAt),
                ["SummaryContent"] = StaffSummaryHtml.BuildOffice(eventData),
                ["PortalUrl"] = portalUrl ?? string.Empty,
                // No ClinicName: NotificationTemplateRenderer fills it from the tenant store (#1014).
            };

            await _dispatcher.DispatchAsync(
                notificationCase: NotificationCase.None,
                templateCode: NotificationTemplateConsts.Codes.OfficeDailySummary,
                recipients: recipients,
                variables: variables,
                contextTag: NotificationOccurrence.PerPacificRun($"OfficeDailySummary/{eventData.TenantId}", eventData.OccurredAt));
        }
    }
}
