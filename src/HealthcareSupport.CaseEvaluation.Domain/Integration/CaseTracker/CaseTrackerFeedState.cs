using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// One office's position in the Case Tracker changes feed (#927), and the record that decides whether that
/// office is delivered by PUSH or by FEED. An active record means feed: the drain sends nothing for the office
/// and the feed serves its Pending rows. No record, or an inactive one, means push, as before #927.
///
/// <para>Why delivery state lives here and not on the outbox rows: the feed's cursor is the rows' rowversion,
/// which changes on EVERY update. Marking a row delivered would give it a new rowversion above the cursor, so
/// it would be served again, acknowledged again and marked again -- a loop (decided 2026-09-17, issue #927).
/// So "delivered" is a comparison against <see cref="AcknowledgedPosition"/>, and no row is written when a
/// change is delivered.</para>
///
/// <para>Positions are rowversion values read as <c>bigint</c>. This table has no rowversion column of its own,
/// so writing it on every poll never holds back <c>MIN_ACTIVE_ROWVERSION()</c>.</para>
///
/// <para>Polls and the health job write through set-based updates on the repository, never a tracked save:
/// two overlapping polls, or a poll and the job, would otherwise collide on the concurrency stamp. Only the
/// rare operator actions (<see cref="Start"/>, <see cref="ReturnToPush"/>) go through a tracked save.</para>
/// </summary>
public class CaseTrackerFeedState : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }

    /// <summary>True while the office is delivered by the feed; false once it has returned to push.</summary>
    public virtual bool IsActive { get; protected set; }

    /// <summary>Rows at or below this position are never served: history before cutover stays history.</summary>
    public virtual long FloorPosition { get; protected set; }

    /// <summary>
    /// The highest position the Case Tracker has sent back. Its cursor means "everything up to here is
    /// committed on our side", so every Pending row at or below it counts as delivered. Only ever moves forward.
    /// </summary>
    public virtual long AcknowledgedPosition { get; protected set; }

    /// <summary>
    /// The highest cursor the feed has handed out. A cursor above it was never issued, so accepting it would
    /// count rows the Case Tracker never received as delivered; the feed refuses it instead.
    /// </summary>
    public virtual long HighestIssuedPosition { get; protected set; }

    public virtual DateTime? StartedAt { get; protected set; }

    public virtual DateTime? StoppedAt { get; protected set; }

    /// <summary>When an authenticated request last reached this office's feed. Drives the silence alert.</summary>
    public virtual DateTime? LastRequestAt { get; protected set; }

    /// <summary>When <see cref="AcknowledgedPosition"/> last moved forward. Drives the stall alert.</summary>
    public virtual DateTime? LastAdvancedAt { get; protected set; }

    /// <summary>When the silence alert was sent for the current incident; null when there is none.</summary>
    public virtual DateTime? SilenceAlertedAt { get; protected set; }

    /// <summary>When the stall alert was sent for the current incident; null when there is none.</summary>
    public virtual DateTime? StallAlertedAt { get; protected set; }

    /// <summary>
    /// When the cursor-ahead email was sent; null once a request succeeds again. A consumer stuck on a bad
    /// cursor retries every minute, and without this each refusal would send another email.
    /// </summary>
    public virtual DateTime? CursorAheadAlertedAt { get; protected set; }

    protected CaseTrackerFeedState()
    {
    }

    public CaseTrackerFeedState(Guid id, Guid? tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    /// <summary>
    /// Switches the office to feed delivery from <paramref name="floor"/>. Every position starts at the floor,
    /// so a consumer that sends no cursor reads from there, and anything it sends below it is refused. Also
    /// resets the request history and any open alert, so a restarted feed does not inherit an old incident.
    /// </summary>
    public virtual void Start(long floor, DateTime nowUtc)
    {
        if (IsActive)
        {
            // The manager checks first and answers the operator; reaching here is a programming error.
            throw new InvalidOperationException("The Case Tracker feed is already active for this office.");
        }

        IsActive = true;
        FloorPosition = floor;
        AcknowledgedPosition = floor;
        HighestIssuedPosition = floor;
        StartedAt = nowUtc;
        LastAdvancedAt = nowUtc;
        StoppedAt = null;
        LastRequestAt = null;
        SilenceAlertedAt = null;
        StallAlertedAt = null;
        CursorAheadAlertedAt = null;
    }

    /// <summary>
    /// Returns the office to push delivery. The positions are kept, as a record of how far the feed got; the
    /// drain resumes on its next pass and pushes every Pending row, including ones the feed already delivered
    /// (accepted 2026-09-24: the receiver's upsert absorbs the duplicates).
    /// </summary>
    public virtual void ReturnToPush(DateTime nowUtc)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("The Case Tracker feed is not active for this office.");
        }

        IsActive = false;
        StoppedAt = nowUtc;
        SilenceAlertedAt = null;
        StallAlertedAt = null;
        CursorAheadAlertedAt = null;
    }
}
