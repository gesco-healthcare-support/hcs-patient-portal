using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
/// <see cref="PendingDailyDigestEto"/> and dispatches the OLD-parity
/// <c>PendingAppointmentDailyNotification</c> template to the per-tenant
/// clinic-staff inbox (resolved from
/// <c>NotificationsPolicy.OfficeEmail</c>).
///
/// <para>OLD <c>SchedulerDomain.cs</c>:82 received the digest body
/// pre-rendered as the proc's <c>Result</c> column. NEW renders the row
/// list as an HTML table in this handler so the dispatcher's flat
/// variable surface stays uniform across reminder templates.</para>
/// </summary>
public class PendingDailyDigestEmailHandler :
    ILocalEventHandler<PendingDailyDigestEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly ISettingProvider _settingProvider;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<PendingDailyDigestEmailHandler> _logger;

    public PendingDailyDigestEmailHandler(
        INotificationDispatcher dispatcher,
        ISettingProvider settingProvider,
        ICurrentTenant currentTenant,
        ILogger<PendingDailyDigestEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _settingProvider = settingProvider;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(PendingDailyDigestEto eventData)
    {
        if (eventData == null || eventData.Rows.Count == 0)
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var officeEmail = await _settingProvider.GetOrNullAsync(
                CaseEvaluationSettings.NotificationsPolicy.OfficeEmail);
            if (string.IsNullOrWhiteSpace(officeEmail))
            {
                _logger.LogInformation(
                    "PendingDailyDigestEmailHandler: tenant {TenantId} has no OfficeEmail configured; skipping digest.",
                    eventData.TenantId);
                return;
            }

            var portalUrl = await _settingProvider.GetOrNullAsync(
                CaseEvaluationSettings.NotificationsPolicy.PortalBaseUrl);

            var recipients = new List<NotificationRecipient>
            {
                new NotificationRecipient(
                    email: officeEmail!,
                    role: Appointments.Notifications.RecipientRole.OfficeAdmin,
                    isRegistered: false),
            };

            var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["DailyNotificationContent"] = BuildDigestHtml(eventData.Rows),
                ["PortalUrl"] = portalUrl ?? string.Empty,
                ["ClinicName"] = _currentTenant.Name ?? string.Empty,
            };

            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.PendingAppointmentDailyNotification,
                recipients: recipients,
                variables: variables,
                contextTag: $"PendingDailyDigest/{eventData.TenantId}");
        }
    }

    private static string BuildDigestHtml(List<PendingDailyDigestRow> rows)
    {
        var sb = new StringBuilder();
        sb.Append("<table style=\"border-collapse:collapse;width:100%;font-size:13px;margin:12px 0;\">");
        sb.Append("<thead><tr style=\"background:#f3f4f6;\">");
        sb.Append("<th style=\"text-align:left;padding:6px;border:1px solid #d1d5db;\">Confirmation #</th>");
        sb.Append("<th style=\"text-align:left;padding:6px;border:1px solid #d1d5db;\">Patient</th>");
        sb.Append("<th style=\"text-align:left;padding:6px;border:1px solid #d1d5db;\">Appointment date</th>");
        sb.Append("<th style=\"text-align:left;padding:6px;border:1px solid #d1d5db;\">Due date</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (var row in rows)
        {
            sb.Append("<tr>");
            sb.Append("<td style=\"padding:6px;border:1px solid #d1d5db;\">").Append(System.Net.WebUtility.HtmlEncode(row.RequestConfirmationNumber ?? string.Empty)).Append("</td>");
            sb.Append("<td style=\"padding:6px;border:1px solid #d1d5db;\">").Append(System.Net.WebUtility.HtmlEncode(row.PatientName ?? string.Empty)).Append("</td>");
            sb.Append("<td style=\"padding:6px;border:1px solid #d1d5db;\">").Append(row.AppointmentDate.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)).Append("</td>");
            sb.Append("<td style=\"padding:6px;border:1px solid #d1d5db;\">").Append(row.DueDate.HasValue ? row.DueDate.Value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) : "&mdash;").Append("</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table>");
        return sb.ToString();
    }
}
