using System;
using System.Threading;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Builds the intake envelope for one appointment.
///
/// <para>An interface because two callers need it: the outbound push (Part 1) and the reconcile
/// read endpoint (Part 4), which must return a byte-identical shape so the Case Tracker can reuse
/// one deserializer for both push and pull.</para>
/// </summary>
public interface IIntakePayloadBuilder
{
    /// <summary>
    /// Assembles the full envelope. Must be called inside the appointment's office (tenant) scope
    /// so the repository queries resolve against the right database.
    /// </summary>
    Task<IntakeEnvelope> BuildAsync(Guid appointmentId, CancellationToken cancellationToken = default);
}
