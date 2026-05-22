using System;
using System.Collections.Generic;
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
/// <see cref="DueDateDocumentIncompleteEto"/> and dispatches the
/// OLD-parity <c>AppointmentDocumentIncomplete</c> template to every
/// stakeholder via <see cref="IAppointmentRecipientResolver"/>.
///
/// <para>Mirrors OLD <c>SchedulerDomain.cs</c>:199. No CC per OLD's
/// 3-arg <c>SendSMTPMail</c> overload at that line. Distinct from the
/// <c>PackageDocumentReminderEmailHandler</c> (Reminder #3) trigger:
/// this one is date-driven (T-7 days from DueDate) where #3 is
/// status-driven (Pending/Rejected docs at cutoff window).</para>
/// </summary>
public class DueDateDocumentIncompleteEmailHandler :
    ILocalEventHandler<DueDateDocumentIncompleteEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<DueDateDocumentIncompleteEmailHandler> _logger;
    // BUG-029 v3 fix (2026-05-21).
    private readonly IAccountUrlBuilder _accountUrlBuilder;

    public DueDateDocumentIncompleteEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        ICurrentTenant currentTenant,
        ILogger<DueDateDocumentIncompleteEmailHandler> logger,
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
    public virtual async Task HandleEventAsync(DueDateDocumentIncompleteEto eventData)
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
                    "DueDateDocumentIncompleteEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var resolverOutput = await _recipientResolver.ResolveAsync(
                eventData.AppointmentId,
                NotificationKind.DueDateDocumentIncompleteReminder);
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
                    "DueDateDocumentIncompleteEmailHandler: no recipients for appointment {AppointmentId}; skipping.",
                    eventData.AppointmentId);
                return;
            }

            // BUG-029 v3 fix (2026-05-21).
            var portalUrl = await _accountUrlBuilder.BuildPortalRootUrlAsync(eventData.TenantId);

            var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["AppointmentRequestConfirmationNumber"] = ctx.RequestConfirmationNumber,
                ["PendingDocList"] = eventData.PendingDocList ?? string.Empty,
                ["DaysUntilDue"] = eventData.DaysUntilDue,
                ["PortalUrl"] = portalUrl ?? string.Empty,
                ["ClinicName"] = _currentTenant.Name ?? string.Empty,
            };

            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.AppointmentDocumentIncomplete,
                recipients: recipients,
                variables: variables,
                contextTag: $"DueDateDocumentIncomplete/{eventData.AppointmentId}");
        }
    }
}
