using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;

/// <summary>
/// Emails one internal staff member about a batch of failed Case Tracker pushes: dead letters, or since
/// #917 pushes still retrying (<see cref="CaseTrackerPushFailedEto.Kind"/> picks the template).
///
/// <para>Lives in Application because the notification dispatcher does; the job that decides WHEN to
/// alert lives in Domain. Mirrors <c>InternalStaffQueueDigestEmailHandler</c>, including linking to the
/// HOST portal rather than an office subdomain, because internal staff work there.</para>
///
/// <para>The body carries appointment ids, confirmation numbers and the receiver's own error text, and
/// nothing else. No patient name, date of birth or document content -- per contract section I2, an alert
/// must never be the thing that leaks PHI into an inbox.</para>
/// </summary>
public class CaseTrackerPushFailedEmailHandler :
    ILocalEventHandler<CaseTrackerPushFailedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly ICurrentTenant _currentTenant;
    private readonly IAccountUrlBuilder _accountUrlBuilder;
    private readonly ILogger<CaseTrackerPushFailedEmailHandler> _logger;

    public CaseTrackerPushFailedEmailHandler(
        INotificationDispatcher dispatcher,
        ICurrentTenant currentTenant,
        IAccountUrlBuilder accountUrlBuilder,
        ILogger<CaseTrackerPushFailedEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _currentTenant = currentTenant;
        _accountUrlBuilder = accountUrlBuilder;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(CaseTrackerPushFailedEto eventData)
    {
        if (eventData == null || string.IsNullOrWhiteSpace(eventData.StaffEmail))
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            // null = the HOST surface (admin.<base>), where internal staff work.
            var portalUrl = await _accountUrlBuilder.BuildPortalRootUrlAsync(null);

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
                ["OfficeName"] = eventData.OfficeName,
                ["FailureCount"] = eventData.FailureCount,
                ["FailureList"] = BuildFailureList(eventData),
                ["PortalUrl"] = portalUrl ?? string.Empty,
            };

            var templateCode = TemplateCodeFor(eventData.Kind);

            await _dispatcher.DispatchAsync(
                templateCode: templateCode,
                recipients: recipients,
                variables: variables,
                contextTag: $"{templateCode}/{eventData.TenantId}/{eventData.StaffUserId}");

            _logger.LogInformation(
                "CaseTrackerPushFailedEmailHandler: alerted {Email} about {Count} push(es) ({Kind}) in office {TenantId}.",
                eventData.StaffEmail, eventData.FailureCount, eventData.Kind, eventData.TenantId);
        }
    }

    /// <summary>
    /// The template for each alert kind. An unknown kind throws rather than falling back to either email:
    /// sending "retrying" for a dead letter would tell staff no action is needed when it is.
    /// </summary>
    public static string TemplateCodeFor(CaseTrackerPushAlertKind kind) => kind switch
    {
        CaseTrackerPushAlertKind.DeadLettered => NotificationTemplateConsts.Codes.CaseTrackerPushFailed,
        CaseTrackerPushAlertKind.StillRetrying => NotificationTemplateConsts.Codes.CaseTrackerPushRetrying,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Case Tracker alert kind."),
    };

    /// <summary>
    /// One line per failure, plain text. Rendered inside a <c>pre</c> block, so this deliberately emits
    /// no markup -- the template must not depend on whether the dispatcher escapes substituted values.
    /// </summary>
    private static string BuildFailureList(CaseTrackerPushFailedEto eventData)
    {
        if (eventData.Failures.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var failure in eventData.Failures)
        {
            var reference = string.IsNullOrWhiteSpace(failure.ConfirmationNumber)
                ? failure.AppointmentId.ToString("D")
                : failure.ConfirmationNumber;

            builder.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{reference}  ({failure.MessageType}, attempt {failure.AttemptCount})  {failure.LastError}"));
        }

        var undisclosed = eventData.FailureCount - eventData.Failures.Count;
        if (undisclosed > 0)
        {
            // Never let the list imply it is the whole story. Only dead letters point at the portal: the
            // failures screen lists nothing that is still retrying.
            builder.AppendLine(eventData.Kind == CaseTrackerPushAlertKind.DeadLettered
                ? string.Create(CultureInfo.InvariantCulture, $"... and {undisclosed} more. Open the portal to see all of them.")
                : string.Create(CultureInfo.InvariantCulture, $"... and {undisclosed} more."));
        }

        return builder.ToString().TrimEnd();
    }
}
