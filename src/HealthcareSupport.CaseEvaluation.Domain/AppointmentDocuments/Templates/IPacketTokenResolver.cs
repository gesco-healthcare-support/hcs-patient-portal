namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;

/// <summary>
/// Walks an appointment's full entity graph and produces the
/// <see cref="PacketTokenContext"/> consumed by the OpenXml renderer.
///
/// <para>One context fits all 3 active OLD templates -- callers resolve
/// once per appointment, then the renderer applies the same context to
/// each template (Patient / Doctor / AttorneyClaimExaminer).</para>
/// </summary>
public interface IPacketTokenResolver
{
    /// <summary>
    /// Resolves all 60 token values for the given appointment. Caller
    /// must be inside the right tenant scope (<c>ICurrentTenant.Change</c>);
    /// the resolver does not switch tenants on its own.
    /// </summary>
    Task<PacketTokenContext> ResolveAsync(Guid appointmentId, CancellationToken cancellationToken = default);
}
