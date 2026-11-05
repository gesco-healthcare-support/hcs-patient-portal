using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.EmailStops;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;
using Volo.Abp.Timing;

namespace HealthcareSupport.CaseEvaluation.Notifications.Outbox;

/// <summary>
/// T10: sends due outbox rows. Claims a batch via the manager (lease), hands each
/// row to <see cref="IOutboxEmailSender"/>, and records the outcome: MarkSent only
/// after a confirmed send, MarkFailed (reschedule / dead-letter) on any exception.
/// A row is thus never lost (a crash before MarkSent leaves it Pending for the next
/// drain) and never double-sent (the T3 idempotency key collapses a logical send to
/// one row, and MarkSent is idempotent).
/// </summary>
public class OutboxDrainService : ITransientDependency
{
    private readonly NotificationOutboxManager _outboxManager;
    private readonly IOutboxEmailSender _sender;
    private readonly IClock _clock;
    private readonly ISettingProvider _settingProvider;
    private readonly ILogger<OutboxDrainService> _logger;
    private readonly IEmailStopPolicy _emailStopPolicy;

    public OutboxDrainService(
        NotificationOutboxManager outboxManager,
        IOutboxEmailSender sender,
        IClock clock,
        ISettingProvider settingProvider,
        ILogger<OutboxDrainService> logger,
        IEmailStopPolicy emailStopPolicy)
    {
        _outboxManager = outboxManager;
        _sender = sender;
        _clock = clock;
        _settingProvider = settingProvider;
        _logger = logger;
        _emailStopPolicy = emailStopPolicy;
    }

    /// <summary>
    /// Drains up to <paramref name="batchSize"/> due rows in the current tenant
    /// scope. Returns (sent, failed) counts for logging.
    /// </summary>
    public virtual async Task<OutboxDrainResult> DrainDueAsync(int? batchSize = null)
    {
        // #4a master email switch: read per drain in the current (office) tenant scope --
        // OutboxDrainJob enters args.TenantId before calling this -- so a toggle takes
        // effect on the next sweep and a per-tenant override beats the host default. When
        // disabled, claim nothing: due rows stay Pending and resume automatically once
        // re-enabled, with no failed-attempt cost (MarkFailed never runs).
        if (!await _settingProvider.IsTrueAsync(CaseEvaluationSettings.NotificationsPolicy.EmailEnabled))
        {
            _logger.LogInformation(
                "OutboxDrainService: email is disabled (EmailEnabled=false); holding due rows Pending.");
            return new OutboxDrainResult(0, 0);
        }

        var lease = TimeSpan.FromSeconds(NotificationOutboxConsts.LeaseDurationSeconds);
        var backoff = TimeSpan.FromSeconds(NotificationOutboxConsts.RetryBackoffSeconds);
        var size = batchSize ?? NotificationOutboxConsts.DrainBatchSize;

        var claimed = await _outboxManager.ClaimDueBatchAsync(_clock.Now, lease, size);
        var stops = await LookupStopsAsync(claimed);

        var sent = 0;
        var failed = 0;
        foreach (var row in claimed)
        {
            if (await ApplyEmailStopsAsync(row, stops))
            {
                continue;
            }
            try
            {
                await _sender.SendAsync(ToEmailArgs(row));
                row.MarkSent(_clock.Now);
                sent++;
            }
            catch (Exception ex)
            {
                // Do NOT rethrow: one bad recipient must not abort the batch or
                // un-claim the rest. The row is rescheduled (or dead-lettered at
                // the cap); it is never lost.
                row.MarkFailed(_clock.Now, ex.Message, backoff);
                failed++;
                _logger.LogWarning(
                    ex,
                    "OutboxDrainService: send failed for outbox row {RowId} ({Context}); attempt {Attempt}/{Max}.",
                    row.Id, row.Context, row.AttemptCount, row.MaxAttempts);
            }

            await _outboxManager.SaveAsync(row);
        }

        return new OutboxDrainResult(sent, failed);
    }

    // 2026-11-05 email stops: a stop saved after a row was written must still hold. ONE lookup covers the
    // whole claimed batch (every To and CC of every row that asks to be re-checked).
    private async Task<EmailStopLookup> LookupStopsAsync(IReadOnlyCollection<NotificationOutboxItem> claimed)
    {
        var pairs = claimed
            .Where(r => r.RequiresEmailStopCheck())
            .SelectMany(r => r.GetCcList().Prepend(r.To).Select(a => (r.GetEmailStopCaseId(), (string?)a)))
            .ToList();
        if (pairs.Count == 0)
        {
            return EmailStopLookup.Empty;
        }
        return await _emailStopPolicy.LookupBatchAsync(pairs) ?? EmailStopLookup.Empty;
    }

    // Returns true when the row was fully stopped (marked Stopped, saved, not sent). A row that only lost
    // some addresses is re-addressed and sent to the rest.
    private async Task<bool> ApplyEmailStopsAsync(NotificationOutboxItem row, EmailStopLookup stops)
    {
        if (stops.IsEmpty || !row.RequiresEmailStopCheck())
        {
            return false;
        }
        var caseId = row.GetEmailStopCaseId();
        var addressing = EmailStopAddressing.Apply(row.To, row.GetCcList(), stops, caseId);
        if (!addressing.Changed)
        {
            return false;
        }
        foreach (var dropped in addressing.Dropped)
        {
            _logger.LogInformation(
                "OutboxDrainService: email stopped by staff ({Scope}); 1 recipient left off outbox row {RowId}.",
                stops.ScopeOf(dropped, caseId),
                row.Id);
        }
        if (addressing.IsDropped)
        {
            row.MarkStopped(_clock.Now);
            await _outboxManager.SaveAsync(row);
            return true;
        }
        row.ReplaceRecipients(addressing.To!, addressing.Cc);
        return false;
    }

    private static SendAppointmentEmailArgs ToEmailArgs(NotificationOutboxItem row)
    {
        return new SendAppointmentEmailArgs
        {
            To = row.To,
            Cc = row.GetCcList().ToList(),
            Subject = row.Subject,
            Body = row.Body,
            IsBodyHtml = row.IsBodyHtml,
            Context = row.Context,
            TenantId = row.TenantId,
            IdempotencyKey = row.IdempotencyKey,
            PacketRef = row.PacketId.HasValue && row.PacketAppointmentId.HasValue && row.PacketKind.HasValue
                ? new PacketAttachmentRef
                {
                    AppointmentId = row.PacketAppointmentId.Value,
                    PacketId = row.PacketId.Value,
                    Kind = row.PacketKind.Value,
                }
                : null,
            ExtraPacketRefs = row.GetExtraPacketRefs().ToList(),
        };
    }
}

/// <summary>Outcome of a single drain pass.</summary>
public class OutboxDrainResult
{
    public OutboxDrainResult(int sent, int failed)
    {
        Sent = sent;
        Failed = failed;
    }

    public int Sent { get; }
    public int Failed { get; }
}
