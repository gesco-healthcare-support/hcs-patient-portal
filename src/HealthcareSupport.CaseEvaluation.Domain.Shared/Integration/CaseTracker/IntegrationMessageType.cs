namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Discriminates what an outbox row is delivering to the Case Tracker.
///
/// <para>Part 1 ships only <see cref="Intake"/>. The discriminator exists from the
/// start so the later document-update feed adds its own value WITHOUT a migration,
/// and so the ordering rule that feed needs (an appointment's intake must be
/// accepted before any document update for it, or the receiver answers 404) can be
/// implemented then. That gate is deliberately absent here: with only one message
/// type there is nothing to order, so the code would be unreachable.</para>
/// </summary>
public enum IntegrationMessageType
{
    /// <summary>Creates or updates the appointment on the Case Tracker side.</summary>
    Intake = 1,
}
