using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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
/// Emails the weekly missing-intake report (#944) to the technical list in
/// <see cref="CaseTrackerFeedConsts.AlertRecipientsConfigurationKey"/>, as ONE email covering every office, sent
/// from HOST scope (decided 2026-09-24). That is why its template is in
/// <see cref="NotificationTemplateConsts.Codes.HostScoped"/>.
///
/// <para>The context tag carries the run date. The notification outbox dedupes on it, so a fixed tag would
/// swallow next week's email about the same unresolved appointments -- and repeating it is the point.</para>
///
/// <para>An empty recipient list is an ERROR in the log, never a silent drop, as for the feed alerts.</para>
/// </summary>
public class CaseTrackerMissingIntakesEmailHandler :
    ILocalEventHandler<CaseTrackerMissingIntakesEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly ICurrentTenant _currentTenant;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CaseTrackerMissingIntakesEmailHandler> _logger;

    public CaseTrackerMissingIntakesEmailHandler(
        INotificationDispatcher dispatcher,
        ICurrentTenant currentTenant,
        IConfiguration configuration,
        ILogger<CaseTrackerMissingIntakesEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _currentTenant = currentTenant;
        _configuration = configuration;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(CaseTrackerMissingIntakesEto eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        var count = eventData.Offices.Sum(o => o.Items.Count);
        var addresses = CaseTrackerFeedAlertEmailHandler.ParseRecipients(
            _configuration[CaseTrackerFeedConsts.AlertRecipientsConfigurationKey]);
        if (addresses.Count == 0)
        {
            _logger.LogError(
                "CaseTrackerMissingIntakesEmailHandler: no recipients are configured under {Key}; the report of {Count} appointment(s) with no intake row was NOT sent.",
                CaseTrackerFeedConsts.AlertRecipientsConfigurationKey, count);
            return;
        }

        using (_currentTenant.Change(null))
        {
            var recipients = addresses
                .Select(address => new NotificationRecipient(
                    email: address,
                    role: Appointments.Notifications.RecipientRole.OfficeAdmin,
                    isRegistered: false))
                .ToList();

            var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["MissingIntakeCount"] = count.ToString(CultureInfo.InvariantCulture),
                ["MissingIntakeDetail"] = DetailFor(eventData),
            };

            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.CaseTrackerMissingIntakes,
                recipients: recipients,
                variables: variables,
                contextTag: ContextTagFor(eventData));

            _logger.LogInformation(
                "CaseTrackerMissingIntakesEmailHandler: sent the missing-intake report ({Count} appointment(s), {OfficeCount} office(s)) to {RecipientCount} recipient(s).",
                count, eventData.Offices.Count, recipients.Count);
        }
    }

    /// <summary>Dated, so each week's email is its own outbox row.</summary>
    public static string ContextTagFor(CaseTrackerMissingIntakesEto e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{NotificationTemplateConsts.Codes.CaseTrackerMissingIntakes}/{e.RunAt:yyyy-MM-dd}");
    }

    /// <summary>Plain text for a <c>pre</c> block: no markup, so escaping by the dispatcher cannot matter.</summary>
    public static string DetailFor(CaseTrackerMissingIntakesEto e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture,
            $"Checked {e.RunAt.ToString("u", CultureInfo.InvariantCulture)}. Each appointment below was approved after its office's first Case Tracker intake row, its packets have settled, and no intake row exists for it, so the Case Tracker has not received it.\n");
        text.Append("Nothing has been queued. Find out why no row was written, then use Push to Case Tracker on the appointment inside its office.\n");

        foreach (var office in e.Offices)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"\n{office.OfficeName} (first intake row {office.FirstIntakeRowAt.ToString("u", CultureInfo.InvariantCulture)}):\n");
            foreach (var item in office.Items)
            {
                text.Append(CultureInfo.InvariantCulture,
                    $"  {item.ConfirmationNumber}  {item.Status}  approved {item.ApprovedAt.ToString("u", CultureInfo.InvariantCulture)}  appointment {item.AppointmentId:D}\n");
            }
        }

        if (e.FailedOfficeNames.Count > 0)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"\nNot checked, because they could not be read this run: {string.Join(", ", e.FailedOfficeNames)}.\n");
        }

        return text.ToString();
    }
}
