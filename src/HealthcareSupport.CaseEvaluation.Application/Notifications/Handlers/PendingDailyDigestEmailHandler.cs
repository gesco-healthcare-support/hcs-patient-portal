using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using HealthcareSupport.CaseEvaluation.SystemParameters;
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
/// intake-staff inbox (resolved from
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
    private readonly ISettingProvider _settingProvider;  // for OfficeEmail (not a URL)
    private readonly ISystemParameterRepository _systemParameterRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<PendingDailyDigestEmailHandler> _logger;
    // BUG-029 v3 fix (2026-05-21).
    private readonly IAccountUrlBuilder _accountUrlBuilder;

    public PendingDailyDigestEmailHandler(
        INotificationDispatcher dispatcher,
        ISettingProvider settingProvider,
        ISystemParameterRepository systemParameterRepository,
        ICurrentTenant currentTenant,
        ILogger<PendingDailyDigestEmailHandler> logger,
        IAccountUrlBuilder accountUrlBuilder)
    {
        _dispatcher = dispatcher;
        _settingProvider = settingProvider;
        _systemParameterRepository = systemParameterRepository;
        _currentTenant = currentTenant;
        _logger = logger;
        _accountUrlBuilder = accountUrlBuilder;
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
                // Item 13 audit (2026-07-01): raised Info -> Warning. OfficeEmail defaults
                // to empty and has no admin UI, so this office digest silently drops until
                // it is set per tenant; surface it in logs/monitoring.
                _logger.LogWarning(
                    "PendingDailyDigestEmailHandler: tenant {TenantId} has no OfficeEmail configured; skipping digest.",
                    eventData.TenantId);
                return;
            }

            // BUG-029 v3 fix (2026-05-21).
            var portalUrl = await _accountUrlBuilder.BuildPortalRootUrlAsync(eventData.TenantId);

            // 2026-06-11: the decision window is the per-tenant
            // PendingAppointmentOverDueNotificationDays (default 3 -- below the
            // legal 5-day limit for safety). The digest's "Decision due" column
            // and the OVERDUE highlight both read this single value, so the
            // server stays the source of truth (no hardcoded 5).
            var systemParameter = await _systemParameterRepository.GetCurrentTenantAsync();
            var decisionDueDays = systemParameter?.PendingAppointmentOverDueNotificationDays
                ?? SystemParameterConsts.DefaultPendingAppointmentOverDueNotificationDays;

            var recipients = new List<NotificationRecipient>
            {
                new NotificationRecipient(
                    email: officeEmail!,
                    role: Appointments.Notifications.RecipientRole.OfficeAdmin,
                    isRegistered: false),
            };

            var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["DailyNotificationContent"] = BuildDigestHtml(eventData.Rows, decisionDueDays, eventData.OccurredAt),
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

    /// <summary>
    /// Renders the digest table. The "Decision due" column = request date +
    /// <paramref name="decisionDueDays"/>. Rows whose deadline has passed
    /// (per <see cref="DecisionSlaPolicy.IsDecisionOverdue"/>, anchored on the
    /// run time <paramref name="nowForOverdue"/>) are highlighted and labelled
    /// OVERDUE; a banner above the table summarizes the overdue count so intake
    /// staff can escalate. No status change is made -- escalate / notify only.
    /// Internal (not private) so unit tests can verify the overdue banner +
    /// highlight without standing up the full ABP event-handler harness.
    /// </summary>
    internal static string BuildDigestHtml(List<PendingDailyDigestRow> rows, int decisionDueDays, DateTime nowForOverdue)
    {
        var overdueCount = 0;
        foreach (var row in rows)
        {
            if (DecisionSlaPolicy.IsDecisionOverdue(row.RequestedAt, nowForOverdue, decisionDueDays))
            {
                overdueCount++;
            }
        }

        var sb = new StringBuilder();

        if (overdueCount > 0)
        {
            sb.Append("<p style=\"margin:12px 0;padding:8px 12px;border-left:4px solid #b91c1c;background:#fef2f2;color:#991b1b;font-size:14px;font-weight:600;\">");
            sb.Append(overdueCount).Append(overdueCount == 1 ? " pending request is " : " pending requests are ");
            sb.Append("past the ").Append(decisionDueDays).Append("-day decision deadline and need a decision now.");
            sb.Append("</p>");
        }

        sb.Append("<table style=\"border-collapse:collapse;width:100%;font-size:13px;margin:12px 0;\">");
        sb.Append("<thead><tr style=\"background:#f3f4f6;\">");
        sb.Append("<th style=\"text-align:left;padding:6px;border:1px solid #d1d5db;\">Confirmation #</th>");
        sb.Append("<th style=\"text-align:left;padding:6px;border:1px solid #d1d5db;\">Patient</th>");
        sb.Append("<th style=\"text-align:left;padding:6px;border:1px solid #d1d5db;\">Appointment date</th>");
        sb.Append("<th style=\"text-align:left;padding:6px;border:1px solid #d1d5db;\">Due date</th>");
        sb.Append("<th style=\"text-align:left;padding:6px;border:1px solid #d1d5db;\">Decision due</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (var row in rows)
        {
            var isOverdue = DecisionSlaPolicy.IsDecisionOverdue(row.RequestedAt, nowForOverdue, decisionDueDays);
            var decisionDue = DecisionSlaPolicy.DecisionDueDate(row.RequestedAt, decisionDueDays);
            var rowStyle = isOverdue ? " style=\"background:#fef2f2;\"" : string.Empty;

            sb.Append("<tr").Append(rowStyle).Append('>');
            sb.Append("<td style=\"padding:6px;border:1px solid #d1d5db;\">").Append(System.Net.WebUtility.HtmlEncode(row.RequestConfirmationNumber ?? string.Empty)).Append("</td>");
            sb.Append("<td style=\"padding:6px;border:1px solid #d1d5db;\">").Append(System.Net.WebUtility.HtmlEncode(row.PatientName ?? string.Empty)).Append("</td>");
            sb.Append("<td style=\"padding:6px;border:1px solid #d1d5db;\">").Append(row.AppointmentDate.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)).Append("</td>");
            sb.Append("<td style=\"padding:6px;border:1px solid #d1d5db;\">").Append(row.DueDate.HasValue ? row.DueDate.Value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) : "&mdash;").Append("</td>");
            sb.Append("<td style=\"padding:6px;border:1px solid #d1d5db;\">").Append(decisionDue.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture));
            if (isOverdue)
            {
                sb.Append(" <strong style=\"color:#b91c1c;\">(OVERDUE)</strong>");
            }
            sb.Append("</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table>");
        return sb.ToString();
    }
}
