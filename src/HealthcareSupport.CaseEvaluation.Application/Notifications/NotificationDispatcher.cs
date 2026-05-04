using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Phase 18 (2026-05-04) -- default impl of
/// <see cref="INotificationDispatcher"/>. One render call per dispatch
/// (template loads + variables substitute once); recipients fan out to
/// the existing <c>SendAppointmentEmailJob</c> Hangfire queue.
///
/// <para><b>SMS leg deferred (Phase 18 open item):</b> the
/// <see cref="RenderedNotification.BodySms"/> field is populated when
/// the template carries SMS content, but actual delivery is not wired
/// here. Reason: <c>Volo.Abp.Sms</c> + Twilio provider modules are not
/// yet referenced by any project in this solution; adding them is a
/// host-config change that belongs with the Twilio creds rollout (when
/// real Twilio creds land per master-plan section 18.3). Until then,
/// per-feature handlers can read <c>BodySms</c> from the rendered
/// output via <see cref="INotificationTemplateRenderer"/> directly if
/// they need to emit SMS through a future impl.</para>
///
/// <para>Fault tolerance:</para>
/// <list type="bullet">
///   <item>Empty recipient list -> early return without rendering. Zero
///     work, zero log noise.</item>
///   <item>Render throws (template missing) -> propagates. A missing
///     template is a seed bug, not a runtime fallback opportunity --
///     the unit of work rolls back so the gap surfaces in tests.</item>
///   <item>Email enqueue throws -> propagates. Hangfire pipeline
///     handles SMTP transport failure separately (see
///     <c>SendAppointmentEmailJob.ExecuteAsync</c>).</item>
/// </list>
/// </summary>
public class NotificationDispatcher : INotificationDispatcher, ITransientDependency
{
    private readonly INotificationTemplateRenderer _renderer;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationTemplateRenderer renderer,
        IBackgroundJobManager backgroundJobManager,
        ICurrentTenant currentTenant,
        ILogger<NotificationDispatcher> logger)
    {
        _renderer = renderer;
        _backgroundJobManager = backgroundJobManager;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public virtual async Task DispatchAsync(
        string templateCode,
        IReadOnlyCollection<NotificationRecipient> recipients,
        IReadOnlyDictionary<string, object?> variables,
        string contextTag,
        CancellationToken cancellationToken = default)
    {
        Check.NotNullOrWhiteSpace(templateCode, nameof(templateCode));
        Check.NotNull(recipients, nameof(recipients));
        Check.NotNull(variables, nameof(variables));

        if (recipients.Count == 0)
        {
            _logger.LogDebug(
                "NotificationDispatcher: zero recipients for template {TemplateCode} ({Context}); short-circuiting.",
                templateCode,
                contextTag);
            return;
        }

        var rendered = await _renderer.RenderAsync(templateCode, variables, cancellationToken);

        var tenantName = _currentTenant.Name;
        foreach (var recipient in recipients)
        {
            await EnqueueEmailAsync(recipient, rendered, contextTag, templateCode, tenantName);
        }
    }

    private async Task EnqueueEmailAsync(
        NotificationRecipient recipient,
        RenderedNotification rendered,
        string contextTag,
        string templateCode,
        string? tenantName)
    {
        if (string.IsNullOrWhiteSpace(recipient.Email))
        {
            _logger.LogWarning(
                "NotificationDispatcher: skipping recipient with empty email for template {TemplateCode} ({Context}).",
                templateCode,
                contextTag);
            return;
        }
        var args = new SendAppointmentEmailArgs
        {
            To = recipient.Email,
            Subject = rendered.Subject,
            Body = rendered.BodyEmail,
            IsBodyHtml = true,
            Context = contextTag,
            Role = recipient.Role,
            IsRegistered = recipient.IsRegistered,
            TenantName = tenantName,
        };
        await _backgroundJobManager.EnqueueAsync(args);
    }
}
