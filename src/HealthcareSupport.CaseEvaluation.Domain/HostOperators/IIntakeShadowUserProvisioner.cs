using System;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// Phase D (2026-06-25, F-7b) -- on-demand provisioning of the per-office "shadow"
/// user that a host operator impersonates into. This is NOT the office-creation
/// seed (<c>IOfficeDatabaseProvisioner</c>); the office already exists. It ensures
/// exactly ONE IdentityUser -- username == the operator's email, holding a caller-
/// specified per-office role -- inside the target office's database, switching
/// context with <c>CurrentTenant.Change</c> so the write lands in that office's
/// physical DB.
///
/// <para>task_2e8e4dc2 (2026-07-21): generalized from Intake-only to any per-office
/// role, so Staff Supervisor (own Supervisor-role shadow) and IT-Admin (own
/// admin-role shadow) land as themselves too. Intake still provisions eagerly on
/// assignment; Supervisor / IT-Admin provision lazily in the impersonation grant.</para>
///
/// <para>The human never logs into the shadow user; it is purely an impersonation
/// target. Provisioning is idempotent. Unassignment disables it (defense in depth --
/// the assignment gate is the primary block for Intake).</para>
/// </summary>
public interface IIntakeShadowUserProvisioner
{
    /// <summary>
    /// Idempotently ensure the operator's shadow user exists and is active in
    /// <paramref name="officeId"/>'s database, holding exactly the per-office role
    /// <paramref name="roleName"/>. Returns the shadow user's id.
    /// </summary>
    Task<Guid> EnsureShadowUserAsync(Guid officeId, Guid operatorUserId, string roleName);

    /// <summary>
    /// Disable (lock out) the operator's shadow Intake user in
    /// <paramref name="officeId"/> if it exists. No-op when absent.
    /// </summary>
    Task DisableShadowUserAsync(Guid officeId, Guid operatorUserId);
}
