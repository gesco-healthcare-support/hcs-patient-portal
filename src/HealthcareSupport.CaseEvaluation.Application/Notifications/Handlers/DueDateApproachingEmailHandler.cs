using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
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
/// <see cref="DueDateApproachingEto"/> and dispatches the OLD-parity
/// <c>AppointmentDueDateReminder</c> template to every stakeholder via
/// <see cref="IAppointmentRecipientResolver"/>.
///
/// <para>Mirrors OLD <c>SchedulerDomain.cs</c>:171. No CC per OLD's
/// 3-arg <c>SendSMTPMail</c> overload at that line.</para>
/// </summary>
public class DueDateApproachingEmailHandler :
    ILocalEventHandler<DueDateApproachingEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<DueDateApproachingEmailHandler> _logger;
    // BUG-029 v3 fix (2026-05-21).
    private readonly IAccountUrlBuilder _accountUrlBuilder;

    public DueDateApproachingEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        ICurrentTenant currentTenant,
        ILogger<DueDateApproachingEmailHandler> logger,
        IAccountUrlBuilder accountUrlBuilder)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _currentTenant = currentTenant;
        _logger = logger;
        _accountUrlBuilder = accountUrlBuilder;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(DueDateApproachingEto eventData)
    {
        if (eventData == null)
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var ctx = await _contextResolver.ResolveAsync(eventData.AppointmentId, appointmentDocumentId: null);
            if (ctx == null)
            {
                _logger.LogWarning(
                    "DueDateApproachingEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var resolverOutput = await _recipientResolver.ResolveAsync(
                eventData.AppointmentId,
                NotificationKind.DueDateApproachingReminder);
            var recipients = resolverOutput
                .Where(r => !string.IsNullOrWhiteSpace(r.To))
                .Select(r => new NotificationRecipient(
                    email: r.To,
                    role: r.Role,
                    isRegistered: r.IsRegistered))
                .ToList();
            if (recipients.Count == 0)
            {
                _logger.LogInformation(
                    "DueDateApproachingEmailHandler: no recipients for appointment {AppointmentId}; skipping.",
                    eventData.AppointmentId);
                return;
            }

            // BUG-029 v3 fix (2026-05-21).
            var portalUrl = await _accountUrlBuilder.BuildPortalRootUrlAsync(eventData.TenantId);

            var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["AppointmentRequestConfirmationNumber"] = ctx.RequestConfirmationNumber,
                ["DueDate"] = ctx.DueDate.HasValue
                    ? ctx.DueDate.Value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)
                    : string.Empty,
                ["DaysUntilDue"] = eventData.DaysUntilDue,
                ["PortalUrl"] = portalUrl ?? string.Empty,
                ["ClinicName"] = _currentTenant.Name ?? string.Empty,
            };

            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.AppointmentDueDateReminder,
                recipients: recipients,
                variables: variables,
                contextTag: $"DueDateApproaching/T-{eventData.DaysUntilDue}/{eventData.AppointmentId}");
        }
    }
}
