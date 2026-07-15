using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Notifications.Outbox;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

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
    private readonly NotificationOutboxManager _outboxManager;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationTemplateRenderer renderer,
        IBackgroundJobManager backgroundJobManager,
        ICurrentTenant currentTenant,
        NotificationOutboxManager outboxManager,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<NotificationDispatcher> logger)
    {
        _renderer = renderer;
        _backgroundJobManager = backgroundJobManager;
        _currentTenant = currentTenant;
        _outboxManager = outboxManager;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    public virtual async Task DispatchAsync(
        string templateCode,
        IReadOnlyCollection<NotificationRecipient> recipients,
        IReadOnlyDictionary<string, object?> variables,
        string contextTag,
        PacketAttachmentRef? packetRef = null,
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

        var wroteAny = false;
        foreach (var recipient in recipients)
        {
            wroteAny |= await WriteRecipientRowAsync(recipient, rendered, contextTag, templateCode, packetRef);
        }
        if (wroteAny)
        {
            await ScheduleDrainAsync(_currentTenant.Id);
        }
    }

    public virtual async Task DispatchToWithCcAsync(
        string templateCode,
        NotificationRecipient to,
        IReadOnlyCollection<NotificationRecipient> cc,
        IReadOnlyDictionary<string, object?> variables,
        string contextTag,
        PacketAttachmentRef? packetRef = null,
        CancellationToken cancellationToken = default)
    {
        Check.NotNullOrWhiteSpace(templateCode, nameof(templateCode));
        Check.NotNull(to, nameof(to));
        Check.NotNull(variables, nameof(variables));

        if (string.IsNullOrWhiteSpace(to.Email))
        {
            _logger.LogWarning(
                "NotificationDispatcher: DispatchToWithCc skipped -- empty To for template {TemplateCode} ({Context}).",
                templateCode,
                contextTag);
            return;
        }

        var rendered = await _renderer.RenderAsync(templateCode, variables, cancellationToken);

        // Drop empty + the To address itself (case-insensitive), then de-dup.
        var ccEmails = (cc ?? Array.Empty<NotificationRecipient>())
            .Where(r => r != null
                && !string.IsNullOrWhiteSpace(r.Email)
                && !string.Equals(r.Email, to.Email, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Email)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var tenantId = _currentTenant.Id;
        await _outboxManager.EnqueueAsync(
            tenantId,
            to.Email,
            ccEmails,
            rendered.Subject,
            rendered.BodyEmail,
            isBodyHtml: true,
            contextTag,
            SendAppointmentEmailArgs.BuildIdempotencyKey(tenantId, to.Email, contextTag, packetRef?.Kind),
            packetRef?.AppointmentId,
            packetRef?.PacketId,
            packetRef?.Kind);
        await ScheduleDrainAsync(tenantId);
    }

    // T10: writes one Pending outbox row (idempotent by key) in the current UoW
    // instead of enqueueing SendAppointmentEmailJob directly. Returns false when
    // the recipient has no email (nothing written). The originating tenant is
    // stamped on the row so the drain re-enters it before a packet-attachment
    // fetch (the 2026-05-11 Bug A fix, now carried by the row's TenantId).
    private async Task<bool> WriteRecipientRowAsync(
        NotificationRecipient recipient,
        RenderedNotification rendered,
        string contextTag,
        string templateCode,
        PacketAttachmentRef? packetRef)
    {
        if (string.IsNullOrWhiteSpace(recipient.Email))
        {
            _logger.LogWarning(
                "NotificationDispatcher: skipping recipient with empty email for template {TemplateCode} ({Context}).",
                templateCode,
                contextTag);
            return false;
        }
        var tenantId = _currentTenant.Id;
        await _outboxManager.EnqueueAsync(
            tenantId,
            recipient.Email,
            cc: null,
            rendered.Subject,
            rendered.BodyEmail,
            isBodyHtml: true,
            contextTag,
            SendAppointmentEmailArgs.BuildIdempotencyKey(tenantId, recipient.Email, contextTag, packetRef?.Kind),
            packetRef?.AppointmentId,
            packetRef?.PacketId,
            packetRef?.Kind);
        return true;
    }

    // Kicks a drain of this office's outbox on UoW commit so the rows just
    // written go out promptly. Deferred via OnCompleted because ABP's
    // Hangfire-backed IBackgroundJobManager enqueues immediately (not UoW-aware)
    // -- the same pattern PacketGenerationOnApprovedHandler uses. If the enqueue
    // is lost to a shutdown race, the rows are already committed and the T11
    // reconciliation sweep re-drives them, so nothing is lost.
    private async Task ScheduleDrainAsync(Guid? tenantId)
    {
        var drainArgs = new OutboxDrainArgs { TenantId = tenantId };
        var uow = _unitOfWorkManager.Current;
        if (uow == null)
        {
            // No ambient UoW: the row already auto-saved, so enqueue the drain now.
            await _backgroundJobManager.EnqueueAsync(drainArgs);
            return;
        }
        uow.OnCompleted(async () =>
        {
            try
            {
                await _backgroundJobManager.EnqueueAsync(drainArgs);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogWarning(
                    ex,
                    "NotificationDispatcher: outbox drain enqueue skipped for tenant {TenantId} -- DI scope disposed before OnCompleted (test/shutdown). The reconciliation sweep will drain it.",
                    tenantId);
            }
        });
    }
}
