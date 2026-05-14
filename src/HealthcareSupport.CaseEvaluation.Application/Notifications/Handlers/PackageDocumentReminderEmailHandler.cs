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
/// Subscribes to <see cref="PackageDocumentReminderEto"/> and dispatches
/// the OLD-parity <c>UploadPendingDocuments</c> reminder email.
///
/// <para>Phase 7 (Category 7, 2026-05-10) refactor (closes Category 7.D
/// + Reminders #3 + #6, resolves PARITY-FLAG PF-001):</para>
/// <list type="bullet">
///   <item>Template is now <c>UploadPendingDocuments</c> for BOTH
///         package docs and JDF. Mirrors OLD <c>SchedulerDomain.cs</c>
///         :146 (package, 4-arg SendSMTPMail with CC) and :229 (JDF,
///         reuses the same template). PF-001's
///         <c>AppointmentDueDateUploadDocumentLeft</c> mapping is
///         retired; OLD never used that template for either reminder.</item>
///   <item>Recipients are now the full stakeholder fan-out via
///         <see cref="IAppointmentRecipientResolver"/> -- mirrors OLD
///         :146's per-row <c>item.EmailList</c>. The earlier
///         uploader-only addressee was a narrower seam that dropped AAs,
///         DAs, claim examiners, and the office mailbox.</item>
///   <item>CC is added via <see cref="CcRecipientAppender"/> (per-tenant
///         <c>SystemParameter.CcEmailIds</c>). OLD's :146 uses the
///         proc's per-row <c>item.PrimaryEmailList</c> -- semantically
///         the office's known important email -- which is unavailable
///         in NEW because the proc has no analogue. The tenant-level
///         CC list is the closest standing seam.</item>
/// </list>
/// </summary>
public class PackageDocumentReminderEmailHandler :
    ILocalEventHandler<PackageDocumentReminderEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly CcRecipientAppender _ccAppender;
    private readonly ISettingProvider _settingProvider;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<PackageDocumentReminderEmailHandler> _logger;

    public PackageDocumentReminderEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        CcRecipientAppender ccAppender,
        ISettingProvider settingProvider,
        ICurrentTenant currentTenant,
        ILogger<PackageDocumentReminderEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _ccAppender = ccAppender;
        _settingProvider = settingProvider;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(PackageDocumentReminderEto eventData)
    {
        if (eventData == null)
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var ctx = await _contextResolver.ResolveAsync(eventData.AppointmentId, eventData.AppointmentDocumentId);
            if (ctx == null)
            {
                _logger.LogWarning(
                    "PackageDocumentReminderEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var resolverOutput = await _recipientResolver.ResolveAsync(
                eventData.AppointmentId,
                NotificationKind.PackageDocumentReminder);
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
                    "PackageDocumentReminderEmailHandler: no recipients for appointment {AppointmentId}; skipping.",
                    eventData.AppointmentId);
                return;
            }

            // OLD :146 CC the per-row PrimaryEmailList; NEW uses
            // SystemParameter.CcEmailIds for the same intent (see class doc).
            await _ccAppender.AppendAsync(
                recipients,
                contextTagForLogging: $"PackageDocumentReminder/{eventData.AppointmentId}");

            var portalUrl = ctx.PortalBaseUrl ?? await _settingProvider.GetOrNullAsync(
                CaseEvaluationSettings.NotificationsPolicy.PortalBaseUrl);

            var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["AppointmentRequestConfirmationNumber"] = ctx.RequestConfirmationNumber,
                ["DueDate"] = ctx.DueDate.HasValue
                    ? ctx.DueDate.Value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)
                    : string.Empty,
                ["PendingDocList"] = ctx.DocumentName ?? string.Empty,
                ["IsJointDeclaration"] = eventData.IsJointDeclaration,
                ["PortalUrl"] = portalUrl ?? string.Empty,
                ["ClinicName"] = _currentTenant.Name ?? string.Empty,
            };

            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.UploadPendingDocuments,
                recipients: recipients,
                variables: variables,
                contextTag: $"PackageDocumentReminder/{(eventData.IsJointDeclaration ? "JDF" : "Package")}/{eventData.AppointmentDocumentId}");
        }
    }
}
