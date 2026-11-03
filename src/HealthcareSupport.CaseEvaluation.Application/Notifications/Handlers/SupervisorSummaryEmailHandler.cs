using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Sends the 08:00 / 16:00 all-offices summary (<see cref="SupervisorSummaryEto"/>): ONE email per
/// switched-on HOST user in the "Staff Supervisor" role, covering every office with something outstanding.
/// Runs in host scope, which is why <see cref="NotificationTemplateConsts.Codes.SupervisorDailySummary"/> is
/// in <see cref="NotificationTemplateConsts.Codes.HostScoped"/>. Nobody else is mailed: not the office
/// inboxes, not office Intake Staff, not a switched-off Supervisor.
/// </summary>
public class SupervisorSummaryEmailHandler :
    ILocalEventHandler<SupervisorSummaryEto>,
    ITransientDependency
{
    private const string SupervisorRoleName = "Staff Supervisor";

    private readonly INotificationDispatcher _dispatcher;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IAccountUrlBuilder _accountUrlBuilder;
    private readonly ILogger<SupervisorSummaryEmailHandler> _logger;

    public SupervisorSummaryEmailHandler(
        INotificationDispatcher dispatcher,
        IdentityUserManager userManager,
        ICurrentTenant currentTenant,
        IAccountUrlBuilder accountUrlBuilder,
        ILogger<SupervisorSummaryEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _userManager = userManager;
        _currentTenant = currentTenant;
        _accountUrlBuilder = accountUrlBuilder;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(SupervisorSummaryEto eventData)
    {
        if (eventData == null || eventData.Offices.Count == 0)
        {
            return;
        }

        using (_currentTenant.Change(null))
        {
            var users = await _userManager.GetActiveUsersInRoleAsync(SupervisorRoleName);
            var recipients = OfficeNotificationRecipients.Distinct(
                users
                    .Where(u => !string.IsNullOrWhiteSpace(u.Email))
                    .Select(u => new NotificationRecipient(
                        email: u.Email,
                        role: RecipientRole.OfficeAdmin,
                        isRegistered: true)));
            if (recipients.Count == 0)
            {
                _logger.LogWarning("SupervisorSummaryEmailHandler: no active host Staff Supervisor has an email; summary not sent.");
                return;
            }

            var portalUrl = await _accountUrlBuilder.BuildPortalRootUrlAsync(null);

            var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["SummaryLabel"] = StaffSummaryHtml.LabelFor(eventData.OccurredAt),
                ["SummaryContent"] = StaffSummaryHtml.BuildSupervisor(eventData),
                ["PortalUrl"] = portalUrl ?? string.Empty,
            };

            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.SupervisorDailySummary,
                recipients: recipients,
                variables: variables,
                contextTag: NotificationOccurrence.PerPacificRun("SupervisorDailySummary", eventData.OccurredAt));

            _logger.LogInformation(
                "SupervisorSummaryEmailHandler: sent the all-offices summary ({OfficeCount} office(s)) to {RecipientCount} supervisor(s).",
                eventData.Offices.Count, recipients.Count);
        }
    }
}
