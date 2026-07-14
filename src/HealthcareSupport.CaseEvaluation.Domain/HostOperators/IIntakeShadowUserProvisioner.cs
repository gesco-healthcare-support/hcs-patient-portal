using System;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// Phase D (2026-06-25, F-7b) -- on-demand provisioning of the LIMITED per-office
/// "shadow" Intake user that a host Intake operator impersonates into. This is
/// NOT the office-creation seed (<c>IOfficeDatabaseProvisioner</c>); the office
/// already exists. It ensures exactly ONE IdentityUser -- username == the
/// operator's email, holding the per-tenant Intake Staff role -- inside the
/// target office's database, switching context with <c>CurrentTenant.Change</c>
/// so the write lands in that office's physical DB.
///
/// <para>The human never logs into the shadow user; it is purely an
/// impersonation target. Provisioning is eager (on assignment, O-D3) and
/// idempotent. Unassignment disables it (defense in depth -- the assignment gate
/// is the primary block).</para>
/// </summary>
public interface IIntakeShadowUserProvisioner
{
    /// <summary>
    /// Idempotently ensure the operator's limited shadow Intake user exists and
    /// is active in <paramref name="officeId"/>'s database, holding the
    /// per-tenant Intake Staff role. Returns the shadow user's id.
    /// </summary>
    Task<Guid> EnsureShadowUserAsync(Guid officeId, Guid operatorUserId);

    /// <summary>
    /// Disable (lock out) the operator's shadow Intake user in
    /// <paramref name="officeId"/> if it exists. No-op when absent.
    /// </summary>
    Task DisableShadowUserAsync(Guid officeId, Guid operatorUserId);
}
