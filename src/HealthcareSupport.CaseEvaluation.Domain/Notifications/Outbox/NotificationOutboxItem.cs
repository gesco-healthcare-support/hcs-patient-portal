using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Notifications.Outbox;

/// <summary>
/// Phase 2 (T8): a durable per-recipient email delivery ledger row. Written
/// Pending inside the approval unit of work (atomic with the state change that
/// caused it), then delivered out-of-band by a drain job that claims the row via
/// a visibility-timeout lease, sends via SMTP, and marks it Sent ONLY on success.
///
/// <para>This is the "no-loss" half of the resilience epic that a packet sweep
/// cannot provide: a packet-completeness check sees "3 Generated" and does
/// nothing while emails silently vanished (BUG-033). The ledger makes each
/// intended send individually recoverable.</para>
///
/// <para>Lives in the per-office (tenant) DB so the Pending write commits in the
/// same transaction as the approval; <see cref="IMultiTenant"/> for that reason.
/// Cross-worker claim races are arbitrated in the database by an atomic status-gated
/// lease (INotificationOutboxRepository.TryLeaseAsync): one UPDATE flips
/// <see cref="LockedUntil"/> only while the row is still due, so exactly one drain
/// wins it and the losers update zero rows and skip -- no AbpDbConcurrencyException.
/// <see cref="TryClaim"/> encodes the same gate in the domain (and drives the
/// unit-test lease emulation).</para>
///
/// <para>HIPAA: <see cref="Body"/> is the already-rendered email (may contain the
/// same PHI-adjacent content the recipient is authorized to see); it is never
/// logged. <see cref="IdempotencyKey"/> is a SHA-256 digest, not raw address.</para>
/// </summary>
public class NotificationOutboxItem : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    private const string CcSeparator = "\n";

    public virtual Guid? TenantId { get; set; }

    /// <summary>Primary recipient address.</summary>
    public virtual string To { get; protected set; } = null!;

    /// <summary>
    /// CC recipients for a single addressed notice, newline-joined (email
    /// addresses never contain a newline). Null/empty for a solo send. Use
    /// <see cref="GetCcList"/> to read back the list.
    /// </summary>
    public virtual string? Cc { get; protected set; }

    public virtual string Subject { get; protected set; } = null!;

    /// <summary>Fully rendered email body (nvarchar(max)); HTML when <see cref="IsBodyHtml"/>.</summary>
    public virtual string Body { get; protected set; } = null!;

    public virtual bool IsBodyHtml { get; protected set; }

    /// <summary>Free-text correlation label (mirrors SendAppointmentEmailArgs.Context).</summary>
    public virtual string Context { get; protected set; } = null!;

    /// <summary>
    /// Deterministic dedup key (SHA-256 hex) shared with SendAppointmentEmailArgs;
    /// unique per tenant so an idempotent enqueue collapses a retry to one row.
    /// </summary>
    public virtual string IdempotencyKey { get; protected set; } = null!;

    // Packet attachment reference (all null for a plain email). Stored as loose
    // scalars rather than an owned type to keep the migration + query trivial.
    public virtual Guid? PacketAppointmentId { get; protected set; }
    public virtual Guid? PacketId { get; protected set; }
    public virtual PacketKind? PacketKind { get; protected set; }

    public virtual NotificationOutboxStatus Status { get; protected set; }

    public virtual int AttemptCount { get; protected set; }

    public virtual int MaxAttempts { get; protected set; }

    /// <summary>Earliest UTC time this row may be retried after a failure; null = due now.</summary>
    public virtual DateTime? NextAttemptAt { get; protected set; }

    /// <summary>Lease expiry (visibility timeout); a claim past this is reclaimable.</summary>
    public virtual DateTime? LockedUntil { get; protected set; }

    /// <summary>UTC timestamp of confirmed SMTP delivery; null until Sent.</summary>
    public virtual DateTime? SentAt { get; protected set; }

    /// <summary>Last delivery error (bounded); cleared on Sent.</summary>
    public virtual string? LastError { get; protected set; }

    protected NotificationOutboxItem()
    {
    }

    public NotificationOutboxItem(
        Guid id,
        Guid? tenantId,
        string to,
        IEnumerable<string>? cc,
        string subject,
        string body,
        bool isBodyHtml,
        string context,
        string idempotencyKey,
        Guid? packetAppointmentId = null,
        Guid? packetId = null,
        PacketKind? packetKind = null,
        int maxAttempts = NotificationOutboxConsts.DefaultMaxAttempts)
        : base(id)
    {
        TenantId = tenantId;
        To = Check.NotNullOrWhiteSpace(to, nameof(to), NotificationOutboxConsts.ToMaxLength);
        Cc = JoinCc(cc);
        Subject = Check.NotNullOrWhiteSpace(subject, nameof(subject), NotificationOutboxConsts.SubjectMaxLength);
        Body = Check.NotNullOrWhiteSpace(body, nameof(body));
        IsBodyHtml = isBodyHtml;
        Context = Check.NotNullOrWhiteSpace(context, nameof(context), NotificationOutboxConsts.ContextMaxLength);
        IdempotencyKey = Check.NotNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey), NotificationOutboxConsts.IdempotencyKeyMaxLength);
        PacketAppointmentId = packetAppointmentId;
        PacketId = packetId;
        PacketKind = packetKind;
        MaxAttempts = maxAttempts < 1 ? 1 : maxAttempts;
        Status = NotificationOutboxStatus.Pending;
        AttemptCount = 0;
    }

    /// <summary>Reads <see cref="Cc"/> back as a list (empty when none).</summary>
    public virtual IReadOnlyList<string> GetCcList() =>
        string.IsNullOrEmpty(Cc)
            ? Array.Empty<string>()
            : Cc.Split(CcSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Attempts to lease this row for a delivery attempt. Succeeds only when the
    /// row is Pending, no active lease is held, and any post-failure backoff has
    /// elapsed -- otherwise returns false (the caller skips it). On success the
    /// lease is extended to <paramref name="nowUtc"/> + <paramref name="leaseDuration"/>
    /// so a crashed worker's row becomes reclaimable after the timeout.
    /// </summary>
    public virtual bool TryClaim(DateTime nowUtc, TimeSpan leaseDuration)
    {
        if (Status != NotificationOutboxStatus.Pending)
        {
            return false;
        }
        if (LockedUntil.HasValue && LockedUntil.Value > nowUtc)
        {
            return false; // another worker holds an unexpired lease
        }
        if (NextAttemptAt.HasValue && NextAttemptAt.Value > nowUtc)
        {
            return false; // post-failure backoff has not elapsed
        }
        LockedUntil = nowUtc.Add(leaseDuration);
        return true;
    }

    /// <summary>
    /// Marks delivery confirmed. Idempotent: a second call (e.g. a duplicate drain
    /// of an already-Sent row) is a no-op so the send timestamp never moves.
    /// </summary>
    public virtual void MarkSent(DateTime nowUtc)
    {
        if (Status == NotificationOutboxStatus.Sent)
        {
            return; // idempotent -- a duplicate drain must not move the send time
        }
        Status = NotificationOutboxStatus.Sent;
        SentAt = nowUtc;
        LockedUntil = null;
        LastError = null;
    }

    /// <summary>
    /// Records a failed attempt. Below <see cref="MaxAttempts"/> the row returns to
    /// Pending with a backoff (<paramref name="retryBackoff"/>); at the cap it
    /// becomes a terminal Failed dead-letter. Never resurrects a Sent row.
    /// </summary>
    public virtual void MarkFailed(DateTime nowUtc, string? error, TimeSpan retryBackoff)
    {
        if (Status == NotificationOutboxStatus.Sent)
        {
            return; // never resurrect a delivered email
        }
        AttemptCount++;
        LastError = Truncate(error, NotificationOutboxConsts.LastErrorMaxLength);
        LockedUntil = null;
        if (AttemptCount >= MaxAttempts)
        {
            Status = NotificationOutboxStatus.Failed; // terminal dead-letter
            NextAttemptAt = null;
        }
        else
        {
            Status = NotificationOutboxStatus.Pending;
            NextAttemptAt = nowUtc.Add(retryBackoff);
        }
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength
            ? value
            : value.Substring(0, maxLength);

    private static string? JoinCc(IEnumerable<string>? cc)
    {
        if (cc == null)
        {
            return null;
        }
        var cleaned = cc
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .ToList();
        return cleaned.Count == 0 ? null : string.Join(CcSeparator, cleaned);
    }
}
