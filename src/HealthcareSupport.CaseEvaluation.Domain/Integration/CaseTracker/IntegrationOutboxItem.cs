using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// A durable outbound-message ledger row for the Case Tracker integration. Written Pending
/// inside the unit of work that caused it (so the enqueue is atomic with the approval), then
/// delivered out-of-band by a drain job that claims the row via a visibility-timeout lease,
/// POSTs it, and marks it Sent ONLY on a confirmed 2xx.
///
/// <para>Deliberately a sibling of <c>NotificationOutboxItem</c> rather than a reuse of it:
/// the mechanics are identical but the payload shape is not (HTTP target + JSON body vs
/// recipient + subject + body), and conflating them would force nullable email columns onto
/// integration rows and vice versa.</para>
///
/// <para>Two policy differences from the email ledger:</para>
/// <list type="bullet">
///   <item>Retries run for 24 hours from the first failure on growing waits, with an early-warning
///   email on the second failure (#917) -- see <see cref="IntegrationOutboxConsts.MaxAttempts"/> and
///   <see cref="MarkFailed"/>.</item>
///   <item><see cref="MarkFatal"/> exists: responses that a retry can never fix (401 bad token,
///   400/415 malformed request) dead-letter immediately instead of burning the cap. The email
///   sender has no equivalent because SMTP failures are effectively all transient.</item>
/// </list>
///
/// <para>HIPAA: <see cref="Payload"/> is a rendered intake body and DOES contain PHI. It must
/// never be logged, echoed into an alert, or included in an exception message.
/// <see cref="IdempotencyKey"/> is a SHA-256 digest, not readable identity data.</para>
/// </summary>
public class IntegrationOutboxItem : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    /// <summary>Which outbound message this row delivers.</summary>
    public virtual IntegrationMessageType MessageType { get; protected set; }

    /// <summary>Relative request path on the Case Tracker, e.g. <c>api/intake/appointments</c>.</summary>
    public virtual string TargetPath { get; protected set; } = null!;

    /// <summary>The appointment this message concerns; the correlation key staff search on.</summary>
    public virtual Guid AppointmentId { get; protected set; }

    /// <summary>Rendered JSON request body (nvarchar(max)). Contains PHI -- never log it.</summary>
    public virtual string Payload { get; protected set; } = null!;

    /// <summary>
    /// Deterministic dedup key, unique per tenant, so a replayed enqueue collapses to one row
    /// instead of pushing the same case twice.
    /// </summary>
    public virtual string IdempotencyKey { get; protected set; } = null!;

    public virtual IntegrationOutboxStatus Status { get; protected set; }

    public virtual int AttemptCount { get; protected set; }

    public virtual int MaxAttempts { get; protected set; }

    /// <summary>Earliest UTC time this row may be retried after a failure; null = due now.</summary>
    public virtual DateTime? NextAttemptAt { get; protected set; }

    /// <summary>Lease expiry (visibility timeout); a claim past this is reclaimable.</summary>
    public virtual DateTime? LockedUntil { get; protected set; }

    /// <summary>UTC timestamp of the confirmed 2xx; null until Sent.</summary>
    public virtual DateTime? SentAt { get; protected set; }

    /// <summary>Last delivery error (bounded); cleared on Sent.</summary>
    public virtual string? LastError { get; protected set; }

    /// <summary>
    /// When internal staff were alerted about this dead letter; null until they were.
    ///
    /// <para>A column rather than a computed time window because "has this row been alerted?" is a fact
    /// about the row: it survives restarts, and it cannot drift the way a last-run timestamp can. This is
    /// what stops a systemic failure -- a bad token failing every row at once -- from mailing staff once
    /// per row.</para>
    /// </summary>
    public virtual DateTime? AlertedAt { get; protected set; }

    /// <summary>When a human dealt with this dead letter via the admin screen; null otherwise.</summary>
    public virtual DateTime? ResolvedAt { get; protected set; }

    /// <summary>
    /// When this row FIRST failed; null while it has never failed (#917). The 24-hour retry window
    /// is measured from here, which is why it is a column: nothing else records when failing started,
    /// and "how long has this been failing" must survive restarts.
    /// </summary>
    public virtual DateTime? FirstFailedAt { get; protected set; }

    /// <summary>
    /// When staff got the early-warning email about this row -- sent once it has failed
    /// <see cref="IntegrationOutboxConsts.EarlyWarningAfterAttempts"/> times and is still retrying
    /// (#917); null until then. The throttle for that email, as <see cref="AlertedAt"/> is for the
    /// dead-letter one. Written only by the repository's set-based update
    /// (<c>StampEarlyWarnedAsync</c>), never by a tracked save: these rows are still being retried when
    /// they are stamped, and a tracked save would race the drain's own save of the same row.
    /// </summary>
    public virtual DateTime? EarlyWarnedAt { get; protected set; }

    protected IntegrationOutboxItem()
    {
    }

    public IntegrationOutboxItem(
        Guid id,
        Guid? tenantId,
        IntegrationMessageType messageType,
        string targetPath,
        Guid appointmentId,
        string payload,
        string idempotencyKey,
        int maxAttempts = IntegrationOutboxConsts.MaxAttempts)
        : base(id)
    {
        TenantId = tenantId;
        MessageType = messageType;
        TargetPath = Check.NotNullOrWhiteSpace(targetPath, nameof(targetPath), IntegrationOutboxConsts.TargetPathMaxLength);
        AppointmentId = appointmentId;
        Payload = Check.NotNullOrWhiteSpace(payload, nameof(payload));
        IdempotencyKey = Check.NotNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey), IntegrationOutboxConsts.IdempotencyKeyMaxLength);
        MaxAttempts = maxAttempts < 1 ? 1 : maxAttempts;
        Status = IntegrationOutboxStatus.Pending;
        AttemptCount = 0;
    }

    /// <summary>
    /// Attempts to lease this row for a delivery attempt. Succeeds only when the row is Pending,
    /// holds no active lease, and any post-failure backoff has elapsed -- otherwise returns false
    /// and the caller skips it. On success the lease runs to
    /// <paramref name="nowUtc"/> + <paramref name="leaseDuration"/> so a crashed worker's row
    /// becomes reclaimable rather than stuck forever.
    /// </summary>
    public virtual bool TryClaim(DateTime nowUtc, TimeSpan leaseDuration)
    {
        if (Status != IntegrationOutboxStatus.Pending)
        {
            return false;
        }
        if (LockedUntil.HasValue && LockedUntil.Value > nowUtc)
        {
            return false; // another drain holds an unexpired lease
        }
        if (NextAttemptAt.HasValue && NextAttemptAt.Value > nowUtc)
        {
            return false; // post-failure backoff has not elapsed
        }
        LockedUntil = nowUtc.Add(leaseDuration);
        return true;
    }

    /// <summary>
    /// Marks delivery confirmed. Idempotent: a duplicate drain of an already-Sent row is a no-op
    /// so the send timestamp never moves.
    /// </summary>
    public virtual void MarkSent(DateTime nowUtc)
    {
        if (Status == IntegrationOutboxStatus.Sent)
        {
            return;
        }
        Status = IntegrationOutboxStatus.Sent;
        SentAt = nowUtc;
        LockedUntil = null;
        LastError = null;
    }

    /// <summary>
    /// Records a RETRYABLE failed attempt (#917). The row keeps retrying, on waits that grow
    /// (<see cref="WaitAfterAttempt"/>), until <see cref="IntegrationOutboxConsts.RetryWindowHours"/>
    /// have passed since its FIRST failure, then dead-letters. <see cref="MaxAttempts"/> still ends it
    /// early if reached, which is how rows created before #917 keep their old limit of 3. Never
    /// resurrects a Sent row.
    /// </summary>
    public virtual void MarkFailed(DateTime nowUtc, string? error)
    {
        if (Status == IntegrationOutboxStatus.Sent)
        {
            return;
        }
        FirstFailedAt ??= nowUtc;
        AttemptCount++;
        LastError = Truncate(error, IntegrationOutboxConsts.LastErrorMaxLength);
        LockedUntil = null;
        var windowExpired = nowUtc - FirstFailedAt.Value >= TimeSpan.FromHours(IntegrationOutboxConsts.RetryWindowHours);
        if (windowExpired || AttemptCount >= MaxAttempts)
        {
            Status = IntegrationOutboxStatus.Failed;
            NextAttemptAt = null;
        }
        else
        {
            Status = IntegrationOutboxStatus.Pending;
            NextAttemptAt = nowUtc.Add(WaitAfterAttempt(AttemptCount));
        }
    }

    /// <summary>
    /// The wait before the next attempt, after <paramref name="attemptCount"/> failures: 5, 10 and 20
    /// minutes, then every 30 (agreed schedule, #917). Growing waits give a short blip a quick retry
    /// without hammering a service that is down for longer.
    /// </summary>
    public static TimeSpan WaitAfterAttempt(int attemptCount)
    {
        var index = attemptCount - 1;
        var minutes = index >= 0 && index < IntegrationOutboxConsts.RetryWaitMinutes.Count
            ? IntegrationOutboxConsts.RetryWaitMinutes[index]
            : IntegrationOutboxConsts.SteadyRetryWaitMinutes;
        return TimeSpan.FromMinutes(minutes);
    }

    /// <summary>
    /// Records a FATAL failure and dead-letters immediately, regardless of the attempt cap. Used
    /// where the response proves a retry cannot help: 401 (bad or missing token), 400 / 415
    /// (malformed request or content type). Retrying those would just delay the alert a human
    /// needs to see. Never resurrects a Sent row.
    /// </summary>
    public virtual void MarkFatal(DateTime nowUtc, string? error)
    {
        if (Status == IntegrationOutboxStatus.Sent)
        {
            return;
        }
        FirstFailedAt ??= nowUtc;
        AttemptCount++;
        LastError = Truncate(error, IntegrationOutboxConsts.LastErrorMaxLength);
        LockedUntil = null;
        NextAttemptAt = null;
        Status = IntegrationOutboxStatus.Failed;
    }

    /// <summary>
    /// Stamps that internal staff have been told about this dead letter. Idempotent, and that matters:
    /// this flag IS the alert throttle, so a second stamp must not let a later run mail staff again
    /// about a failure they have already seen.
    /// </summary>
    public virtual void MarkAlerted(DateTime nowUtc)
    {
        AlertedAt ??= nowUtc;
    }

    /// <summary>
    /// Marks a dead letter as dealt with, removing it from the outstanding-failures list.
    ///
    /// <para>Only acts on a <see cref="IntegrationOutboxStatus.Failed"/> row. Resolving a Pending row
    /// would silently cancel a push that is still due, and resolving a Sent row would rewrite delivery
    /// history -- neither is ever what the caller means.</para>
    /// </summary>
    public virtual void MarkResolved(DateTime nowUtc)
    {
        if (Status != IntegrationOutboxStatus.Failed)
        {
            return;
        }

        Status = IntegrationOutboxStatus.Resolved;
        ResolvedAt = nowUtc;
        LockedUntil = null;
        NextAttemptAt = null;
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength
            ? value
            : value.Substring(0, maxLength);
}
