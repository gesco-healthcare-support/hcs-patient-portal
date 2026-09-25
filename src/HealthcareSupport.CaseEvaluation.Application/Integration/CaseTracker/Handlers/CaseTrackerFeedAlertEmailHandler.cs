using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;

/// <summary>
/// Emails a Case Tracker feed alert (#927) to the technical list in
/// <see cref="CaseTrackerFeedConsts.AlertRecipientsConfigurationKey"/> (decided 2026-09-24): a dead or stuck
/// consumer can only be fixed by whoever runs the two systems, so intake staff are not mailed.
///
/// <para>An empty list is an ERROR in the log on every alert, never a silent drop -- an alert nobody can
/// receive is the failure this exists to prevent.</para>
///
/// <para>The body carries the office, times, counts, a cursor and at most an appointment id. Never a payload
/// and no patient field.</para>
/// </summary>
public class CaseTrackerFeedAlertEmailHandler :
    ILocalEventHandler<CaseTrackerFeedAlertEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly ICurrentTenant _currentTenant;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CaseTrackerFeedAlertEmailHandler> _logger;

    public CaseTrackerFeedAlertEmailHandler(
        INotificationDispatcher dispatcher,
        ICurrentTenant currentTenant,
        IConfiguration configuration,
        ILogger<CaseTrackerFeedAlertEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _currentTenant = currentTenant;
        _configuration = configuration;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(CaseTrackerFeedAlertEto eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        var addresses = ParseRecipients(_configuration[CaseTrackerFeedConsts.AlertRecipientsConfigurationKey]);
        if (addresses.Count == 0)
        {
            _logger.LogError(
                "CaseTrackerFeedAlertEmailHandler: no recipients are configured under {Key}; the {Kind} alert for office {OfficeId} was NOT sent.",
                CaseTrackerFeedConsts.AlertRecipientsConfigurationKey, eventData.Kind, eventData.TenantId);
            return;
        }

        // In the office's scope, as the other Case Tracker alert does: templates resolve per office.
        using (_currentTenant.Change(eventData.TenantId))
        {
            var recipients = addresses
                .Select(address => new NotificationRecipient(
                    email: address,
                    role: Appointments.Notifications.RecipientRole.OfficeAdmin,
                    isRegistered: false))
                .ToList();

            var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["OfficeName"] = eventData.OfficeName,
                ["FeedAlertTitle"] = TitleFor(eventData.Kind),
                ["FeedAlertDetail"] = DetailFor(eventData),
            };

            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.CaseTrackerFeedAlert,
                recipients: recipients,
                variables: variables,
                contextTag: $"{NotificationTemplateConsts.Codes.CaseTrackerFeedAlert}/{eventData.Kind}/{eventData.TenantId}");

            _logger.LogInformation(
                "CaseTrackerFeedAlertEmailHandler: sent the {Kind} feed alert for office {OfficeId} to {Count} recipient(s).",
                eventData.Kind, eventData.TenantId, recipients.Count);
        }
    }

    /// <summary>Addresses separated by <c>;</c> or <c>,</c>, trimmed, blanks and repeats dropped.</summary>
    public static IReadOnlyList<string> ParseRecipients(string? configured) =>
        (configured ?? string.Empty)
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>The subject's headline. An unknown kind throws rather than mailing a misleading one.</summary>
    public static string TitleFor(CaseTrackerFeedAlertKind kind) => kind switch
    {
        CaseTrackerFeedAlertKind.SilenceStarted => "no requests",
        CaseTrackerFeedAlertKind.SilenceCleared => "requests arriving again",
        CaseTrackerFeedAlertKind.StallStarted => "position not advancing",
        CaseTrackerFeedAlertKind.StallCleared => "position advancing again",
        CaseTrackerFeedAlertKind.CursorAhead => "cursor refused",
        CaseTrackerFeedAlertKind.SkipReported => "row skipped",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Case Tracker feed alert kind."),
    };

    /// <summary>Plain text for a <c>pre</c> block: no markup, so escaping by the dispatcher cannot matter.</summary>
    public static string DetailFor(CaseTrackerFeedAlertEto e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var at = e.OccurredAt.ToString("u", CultureInfo.InvariantCulture);
        return e.Kind switch
        {
            CaseTrackerFeedAlertKind.SilenceStarted => string.Create(CultureInfo.InvariantCulture,
                $"No feed request has arrived from this office for {CaseTrackerFeedConsts.SilenceMinutes} minutes (checked {at}). Last request: {Describe(e.LastRequestAt)}. The Case Tracker's feed reader may be stopped or unable to reach the portal."),
            CaseTrackerFeedAlertKind.SilenceCleared => string.Create(CultureInfo.InvariantCulture,
                $"Feed requests from this office are arriving again (checked {at})."),
            CaseTrackerFeedAlertKind.StallStarted => string.Create(CultureInfo.InvariantCulture,
                $"{e.OutstandingCount ?? 0} change(s) are waiting and the office's feed position has not advanced for {CaseTrackerFeedConsts.StallMinutes} minutes (checked {at}). Possible causes: the Case Tracker cannot process a row and has stopped on it, or a long-running or abandoned database transaction on the portal is holding the feed back."),
            CaseTrackerFeedAlertKind.StallCleared => string.Create(CultureInfo.InvariantCulture,
                $"The office's feed position is advancing again, or nothing is waiting (checked {at})."),
            CaseTrackerFeedAlertKind.CursorAhead => string.Create(CultureInfo.InvariantCulture,
                $"A feed request was refused because its cursor ({e.Cursor}) is beyond anything the portal has issued ({at}). Further refusals are logged but not emailed until a request succeeds."),
            CaseTrackerFeedAlertKind.SkipReported => string.Create(CultureInfo.InvariantCulture,
                $"The Case Tracker reported that it abandoned the {e.MessageType} change at cursor {e.Cursor} for appointment {e.AppointmentId:D} ({at}). That change will not be delivered by the feed."),
            _ => throw new ArgumentOutOfRangeException(nameof(e), e.Kind, "Unknown Case Tracker feed alert kind."),
        };
    }

    private static string Describe(DateTime? value) =>
        value.HasValue ? value.Value.ToString("u", CultureInfo.InvariantCulture) : "none since the feed started";
}
