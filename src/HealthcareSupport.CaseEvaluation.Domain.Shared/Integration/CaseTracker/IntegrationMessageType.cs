namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Discriminates what an outbox row is delivering to the Case Tracker.
///
/// <para>The discriminator was introduced in Part 1 carrying only <see cref="Intake"/> so that
/// <see cref="DocumentUpdate"/> could be added here WITHOUT a migration -- the column already
/// stores an int. Part 2 adds it.</para>
///
/// <para>Ordering (an appointment's intake must land before any document update for it, or the
/// receiver answers 404) is NOT enforced by an enqueue-time gate. The contract classifies 404 as
/// retryable, so a document update that overtakes its intake is retried and self-corrects, which
/// is cheaper than a gate that would have to reason about another row's delivery state.</para>
/// </summary>
public enum IntegrationMessageType
{
    /// <summary>Creates or updates the appointment on the Case Tracker side.</summary>
    Intake = 1,

    /// <summary>
    /// Adds, updates or removes entries in an appointment's document list. The body is a BARE JSON
    /// array, not the standard envelope -- see contract section G.
    /// </summary>
    DocumentUpdate = 2,
}
