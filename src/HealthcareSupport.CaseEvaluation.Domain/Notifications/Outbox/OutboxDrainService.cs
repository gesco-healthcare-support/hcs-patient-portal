using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
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
    private readonly ILogger<OutboxDrainService> _logger;

    public OutboxDrainService(
        NotificationOutboxManager outboxManager,
        IOutboxEmailSender sender,
        IClock clock,
        ILogger<OutboxDrainService> logger)
    {
        _outboxManager = outboxManager;
        _sender = sender;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Drains up to <paramref name="batchSize"/> due rows in the current tenant
    /// scope. Returns (sent, failed) counts for logging.
    /// </summary>
    public virtual async Task<OutboxDrainResult> DrainDueAsync(int? batchSize = null)
    {
        var lease = TimeSpan.FromSeconds(NotificationOutboxConsts.LeaseDurationSeconds);
        var backoff = TimeSpan.FromSeconds(NotificationOutboxConsts.RetryBackoffSeconds);
        var size = batchSize ?? NotificationOutboxConsts.DrainBatchSize;

        var claimed = await _outboxManager.ClaimDueBatchAsync(_clock.Now, lease, size);

        var sent = 0;
        var failed = 0;
        foreach (var row in claimed)
        {
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
