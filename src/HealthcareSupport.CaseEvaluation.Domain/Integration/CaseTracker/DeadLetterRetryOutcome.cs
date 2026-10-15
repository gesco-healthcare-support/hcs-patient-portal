using System;
using System.Collections.Generic;
using System.Linq;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Whether a dead-letter retry may resolve the dead letter, judged from what the requeue actually
/// produced rather than trusted (#961).
///
/// <para>Why it exists: before #915 a retry of an unchanged dead letter rebuilt byte-identical content,
/// the enqueue handed back the dead letter ITSELF, and the retry then marked it Resolved -- the operator
/// was told "queued" and nothing was ever sent. #915 removed that path, but the only recovery a human
/// has for a stuck message must not silently depend on the enqueue rule staying right. So the rule is
/// checked here, and anything that is not provably on its way (or provably already there) refuses.</para>
///
/// <para>Pure, so every branch is unit-testable without a database.</para>
/// </summary>
public sealed record DeadLetterRetryOutcome(
    bool CanResolve,
    bool AlreadyDelivered,
    Guid? QueuedOutboxItemId,
    string? RefusalReason)
{
    /// <summary>
    /// Resolvable when a queued row is Pending (on its way), or when every row is Sent: the current
    /// content was already delivered by a NEWER row, so the Case Tracker holds the current state and
    /// the dead letter has nothing left to deliver (decided 2026-09-23). Everything else refuses and
    /// leaves the dead letter in the list: nothing queued, or any row whose status means it cannot be
    /// delivered -- which includes the dead letter handed back as its own retry.
    /// </summary>
    public static DeadLetterRetryOutcome Evaluate(
        IntegrationOutboxItem deadLetter,
        IReadOnlyCollection<IntegrationOutboxItem> queued)
    {
        ArgumentNullException.ThrowIfNull(deadLetter);
        ArgumentNullException.ThrowIfNull(queued);

        if (queued.Count == 0)
        {
            return Refuse("Nothing could be queued for this failure. It has been left in the list.");
        }

        // The STATUS is the invariant, not the identity (#961's own wording). This also refuses the
        // original defect -- the enqueue handing back the dead letter itself -- because a dead letter
        // is Failed by definition when it is retried. A separate identity check was tried and removed:
        // a mutation probe showed no test could tell it apart from this one.
        if (queued.Any(r => r.Status is not (IntegrationOutboxStatus.Pending or IntegrationOutboxStatus.Sent)))
        {
            return Refuse("The retry could not queue a message that can be delivered. It has been left in the list.");
        }

        var pending = queued.FirstOrDefault(r => r.Status == IntegrationOutboxStatus.Pending);
        return pending != null
            ? new DeadLetterRetryOutcome(true, false, pending.Id, null)
            : new DeadLetterRetryOutcome(true, true, queued.First().Id, null);
    }

    private static DeadLetterRetryOutcome Refuse(string reason) => new(false, false, null, reason);
}
