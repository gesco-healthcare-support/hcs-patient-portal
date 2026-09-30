using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>One outbox row as the feed serves it (#927). <see cref="Payload"/> is PHI: never log it.</summary>
public sealed record CaseTrackerFeedRow(
    long Position,
    IntegrationMessageType MessageType,
    Guid AppointmentId,
    string Payload);

/// <summary>
/// The feed's reads over the outbox by rowversion position (#927). SQL SERVER ONLY: rowversion and
/// <c>MIN_ACTIVE_ROWVERSION()</c> exist nowhere else, so the implementation refuses to run on any other
/// provider rather than answer with an empty page that would read as "nothing to send".
///
/// <para>Every read is scoped to ONE office and to live <c>Pending</c> rows. Pending is not belt and braces:
/// a row that was already Failed at cutover still gets updated later (alerted, resolved), which gives it a
/// fresh rowversion above the floor, and without the clause it would resurface looking new (issue #927,
/// 2026-09-17).</para>
/// </summary>
public interface ICaseTrackerFeedStore
{
    /// <summary>
    /// Up to <paramref name="take"/> Pending rows positioned after <paramref name="after"/>, ascending, and
    /// ONLY rows below <c>MIN_ACTIVE_ROWVERSION()</c>: a row written by a transaction still open when this reads
    /// is withheld, together with everything after it, so it can never be skipped by a cursor that moved past
    /// it before it committed. A short or empty page while a write is in flight is correct.
    /// </summary>
    Task<List<CaseTrackerFeedRow>> ReadPageAsync(
        Guid officeId,
        long after,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Where a newly started feed begins: just below the lower of the oldest committed Pending row and
    /// <c>MIN_ACTIVE_ROWVERSION()</c>. Taking the oldest committed row alone would strand a row an
    /// uncommitted transaction wrote earlier; taking the horizon alone would strand rows still retrying.
    /// </summary>
    Task<long> GetStartFloorAsync(Guid officeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Committed Pending rows after <paramref name="acknowledged"/>, with NO horizon limit, so rows held back
    /// by a long-running transaction count as outstanding: that shows up as a stall, not as silence.
    /// </summary>
    Task<int> CountOutstandingAsync(Guid officeId, long acknowledged, CancellationToken cancellationToken = default);

    /// <summary>Whether any outstanding row (as above) was created before <paramref name="createdBefore"/>.</summary>
    Task<bool> HasOutstandingCreatedBeforeAsync(
        Guid officeId,
        long acknowledged,
        DateTime createdBefore,
        CancellationToken cancellationToken = default);

    /// <summary>The Pending row at exactly <paramref name="position"/>, or null. Used to name a reported skip.</summary>
    Task<CaseTrackerFeedRow?> FindRowAsync(Guid officeId, long position, CancellationToken cancellationToken = default);
}
