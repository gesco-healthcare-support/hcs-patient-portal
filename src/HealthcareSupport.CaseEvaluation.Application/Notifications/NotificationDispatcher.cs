using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.EmailStops;
using HealthcareSupport.CaseEvaluation.Validation;
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
/// <para>2026-11-05 email stops: every dispatch first asks <see cref="EmailStopPolicy"/> -- ONE lookup per
/// call, over every address it would write -- and leaves out anyone staff stopped for this case or for all
/// cases, CC lines included. A stopped To hands the email to the first remaining CC; when nobody is left,
/// nothing is written. Each row also records whether the outbox drain must re-check it, so a stop saved
/// after the row was written still holds. Self-service account templates
/// (<see cref="EmailStopPolicy.NeverStoppedTemplateCodes"/>) are never stopped.</para>
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
[ExposeServices(typeof(INotificationDispatcher), typeof(IPacketCopyDispatcher), IncludeDefaults = true, IncludeSelf = true)]
public class NotificationDispatcher : INotificationDispatcher, IPacketCopyDispatcher, ITransientDependency
{
    private readonly INotificationTemplateRenderer _renderer;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly NotificationOutboxManager _outboxManager;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<NotificationDispatcher> _logger;
    private readonly IEmailStopPolicy _emailStopPolicy;

    public NotificationDispatcher(
        INotificationTemplateRenderer renderer,
        IBackgroundJobManager backgroundJobManager,
        ICurrentTenant currentTenant,
        NotificationOutboxManager outboxManager,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<NotificationDispatcher> logger,
        IEmailStopPolicy emailStopPolicy)
    {
        _renderer = renderer;
        _backgroundJobManager = backgroundJobManager;
        _currentTenant = currentTenant;
        _outboxManager = outboxManager;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
        _emailStopPolicy = emailStopPolicy;
    }

    public virtual async Task DispatchAsync(
        NotificationCase notificationCase,
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
                "NotificationDispatcher: zero recipients ({Context}); short-circuiting.",
                contextTag);
            return;
        }

        recipients = WithoutPlaceholders(recipients, templateCode, contextTag);
        if (recipients.Count == 0)
        {
            return;
        }

        var remaining = await WithoutStoppedAsync(notificationCase, templateCode, recipients, contextTag);
        if (remaining.Count == 0)
        {
            return;
        }

        var rendered = await _renderer.RenderAsync(templateCode, variables, cancellationToken);

        var wroteAny = false;
        foreach (var recipient in remaining)
        {
            wroteAny |= await WriteRecipientRowAsync(
                recipient, rendered, contextTag, templateCode, packetRef, notificationCase);
        }
        if (wroteAny)
        {
            await ScheduleDrainAsync(_currentTenant.Id);
        }
    }

    public virtual async Task DispatchWithPacketsAsync(
        NotificationCase notificationCase,
        string templateCode,
        IReadOnlyCollection<NotificationRecipient> recipients,
        IReadOnlyDictionary<string, object?> variables,
        string contextTag,
        IReadOnlyList<PacketAttachmentRef> packetRefs,
        CancellationToken cancellationToken = default)
    {
        Check.NotNullOrWhiteSpace(templateCode, nameof(templateCode));
        Check.NotNull(recipients, nameof(recipients));
        Check.NotNull(variables, nameof(variables));
        Check.NotNull(packetRefs, nameof(packetRefs));

        if (recipients.Count == 0 || packetRefs.Count == 0)
        {
            return;
        }

        recipients = WithoutPlaceholders(recipients, templateCode, contextTag);
        if (recipients.Count == 0)
        {
            return;
        }

        var remaining = await WithoutStoppedAsync(notificationCase, templateCode, recipients, contextTag);
        if (remaining.Count == 0)
        {
            return;
        }

        var rendered = await _renderer.RenderAsync(templateCode, variables, cancellationToken);
        var primary = packetRefs[0];
        var extras = packetRefs.Skip(1).ToList();

        var wroteAny = false;
        var tenantId = _currentTenant.Id;
        foreach (var recipient in remaining)
        {
            if (string.IsNullOrWhiteSpace(recipient.Email))
            {
                _logger.LogWarning(
                    "NotificationDispatcher: skipping a recipient with an empty email ({Context}).",
                    contextTag);
                continue;
            }
            await _outboxManager.EnqueueAsync(
                tenantId,
                recipient.Email,
                cc: null,
                rendered.Subject,
                rendered.BodyEmail,
                isBodyHtml: true,
                contextTag,
                SendAppointmentEmailArgs.BuildIdempotencyKey(tenantId, recipient.Email, contextTag, primary.Kind),
                primary.AppointmentId,
                primary.PacketId,
                primary.Kind,
                extraPacketRefs: extras,
                emailStopCheck: !EmailStopPolicy.IsNeverStopped(templateCode),
                emailStopCaseId: notificationCase.AppointmentId);
            wroteAny = true;
        }
        if (wroteAny)
        {
            await ScheduleDrainAsync(tenantId);
        }
    }

    public virtual async Task DispatchToWithCcAsync(
        NotificationCase notificationCase,
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
                "NotificationDispatcher: DispatchToWithCc skipped -- empty To ({Context}).",
                contextTag);
            return;
        }

        // Drop empty + the To address itself (case-insensitive), then de-dup.
        var ccEmails = (cc ?? Array.Empty<NotificationRecipient>())
            .Where(r => r != null
                && !string.IsNullOrWhiteSpace(r.Email)
                && !string.Equals(r.Email, to.Email, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Email)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 2026-11-13: a placeholder address (unknown@..., na@...) is never mailed. A placeholder To hands the
        // email to the first real CC; when nobody real is left nothing is written.
        var realCc = WithoutPlaceholders(
            ccEmails.Select(e => new NotificationRecipient { Email = e }).ToList(), templateCode, contextTag)
            .Select(r => r.Email)
            .ToList();
        if (!EmailStopPolicy.IsNeverStopped(templateCode) && PlaceholderEmail.IsPlaceholder(to.Email))
        {
            LogPlaceholderDropped(contextTag, to.Role);
            if (realCc.Count == 0)
            {
                return;
            }

            to = new NotificationRecipient { Email = realCc[0] };
            realCc = realCc.Skip(1).ToList();
        }

        ccEmails = realCc;

        // 2026-11-05: a stopped To hands the email to the first remaining CC; a stopped CC is left off; when
        // nobody is left the email is not written. One lookup covers the To and every CC.
        var stops = await LookupStopsAsync(notificationCase, templateCode, ccEmails.Prepend(to.Email));
        var addressing = EmailStopAddressing.Apply(to.Email, ccEmails, stops, notificationCase.AppointmentId);
        foreach (var dropped in addressing.Dropped)
        {
            LogStopped(stops, dropped, notificationCase, contextTag);
        }
        if (addressing.IsDropped)
        {
            return;
        }

        var rendered = await _renderer.RenderAsync(templateCode, variables, cancellationToken);

        var tenantId = _currentTenant.Id;
        var toEmail = addressing.To!;
        await _outboxManager.EnqueueAsync(
            tenantId,
            toEmail,
            addressing.Cc,
            rendered.Subject,
            rendered.BodyEmail,
            isBodyHtml: true,
            contextTag,
            SendAppointmentEmailArgs.BuildIdempotencyKey(tenantId, toEmail, contextTag, packetRef?.Kind),
            packetRef?.AppointmentId,
            packetRef?.PacketId,
            packetRef?.Kind,
            emailStopCheck: !EmailStopPolicy.IsNeverStopped(templateCode),
            emailStopCaseId: notificationCase.AppointmentId);
        await ScheduleDrainAsync(tenantId);
    }

    // 2026-11-13: placeholder addresses already stored on a case (a live case has unknown@...) must never be
    // mailed, whatever wrote them. One warning per dropped recipient, with the context tag and the role only:
    // never the address, and never a template code.
    private IReadOnlyCollection<NotificationRecipient> WithoutPlaceholders(
        IReadOnlyCollection<NotificationRecipient> recipients, string templateCode, string contextTag)
    {
        // Self-service account mail (activation, password reset and change) goes to the person's own login,
        // which may legitimately look like a placeholder (na@firm.com); it is exempt, for the same lock-out
        // reason email stops exempt it.
        if (EmailStopPolicy.IsNeverStopped(templateCode)
            || !recipients.Any(r => PlaceholderEmail.IsPlaceholder(r.Email)))
        {
            return recipients;
        }

        var kept = new List<NotificationRecipient>();
        foreach (var recipient in recipients)
        {
            if (PlaceholderEmail.IsPlaceholder(recipient.Email))
            {
                LogPlaceholderDropped(contextTag, recipient.Role);
                continue;
            }

            kept.Add(recipient);
        }

        return kept;
    }

    private void LogPlaceholderDropped(string contextTag, HealthcareSupport.CaseEvaluation.Appointments.Notifications.RecipientRole? role)
    {
        _logger.LogWarning(
            "NotificationDispatcher: a placeholder recipient was left off ({Context}, role {Role}).",
            contextTag,
            role?.ToString() ?? "unknown");
    }

    // T10: writes one Pending outbox row (idempotent by key) in the current UoW
    // instead of enqueueing SendAppointmentEmailJob directly. Returns false when
    // the recipient has no email (nothing written). The originating tenant is
    // stamped on the row so the drain re-enters it before a packet-attachment
    // fetch (the 2026-05-11 Bug A fix, now carried by the row TenantId).
    private async Task<bool> WriteRecipientRowAsync(
        NotificationRecipient recipient,
        RenderedNotification rendered,
        string contextTag,
        string templateCode,
        PacketAttachmentRef? packetRef,
        NotificationCase notificationCase)
    {
        if (string.IsNullOrWhiteSpace(recipient.Email))
        {
            _logger.LogWarning(
                "NotificationDispatcher: skipping a recipient with an empty email ({Context}).",
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
            packetRef?.Kind,
            emailStopCheck: !EmailStopPolicy.IsNeverStopped(templateCode),
            emailStopCaseId: notificationCase.AppointmentId);
        return true;
    }

    // 2026-11-05: the recipients left after the staff email stops, from ONE lookup for the whole call.
    private async Task<List<NotificationRecipient>> WithoutStoppedAsync(
        NotificationCase notificationCase,
        string templateCode,
        IReadOnlyCollection<NotificationRecipient> recipients,
        string contextTag)
    {
        var stops = await LookupStopsAsync(notificationCase, templateCode, recipients.Select(r => r.Email));
        if (stops.IsEmpty)
        {
            return recipients.ToList();
        }
        var remaining = new List<NotificationRecipient>();
        foreach (var recipient in recipients)
        {
            if (stops.IsStopped(recipient.Email, notificationCase.AppointmentId))
            {
                LogStopped(stops, recipient.Email, notificationCase, contextTag);
                continue;
            }
            remaining.Add(recipient);
        }
        return remaining;
    }

    // 2026-11-05: ONE stop lookup per dispatch call, over every address it would write (never one per
    // recipient). The self-service account templates are never stopped, so they skip the lookup.
    private Task<EmailStopLookup> LookupStopsAsync(
        NotificationCase notificationCase, string templateCode, IEnumerable<string?> addresses)
    {
        if (EmailStopPolicy.IsNeverStopped(templateCode))
        {
            return Task.FromResult(EmailStopLookup.Empty);
        }
        return LookupOrEmptyAsync(addresses, notificationCase.AppointmentId);
    }

    private async Task<EmailStopLookup> LookupOrEmptyAsync(IEnumerable<string?> addresses, Guid? appointmentId) =>
        await _emailStopPolicy.LookupAsync(addresses, appointmentId) ?? EmailStopLookup.Empty;

    // No address and no template code in the line: only the scope and the context tag.
    private void LogStopped(EmailStopLookup stops, string? email, NotificationCase notificationCase, string contextTag)
    {
        _logger.LogInformation(
            "NotificationDispatcher: email stopped by staff ({Scope}); 1 recipient left off ({Context}).",
            stops.ScopeOf(email, notificationCase.AppointmentId),
            contextTag);
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
