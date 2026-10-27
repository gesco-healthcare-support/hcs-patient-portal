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
///
/// <para>2026-10-27: a shadow belongs to its operator's host user id, not to an address, and it
/// follows the host account: switching the operator off, deleting them or changing their role
/// disables every shadow they have, and changing their address moves every shadow to the new one
/// (<c>HostOperatorOfficeLogins</c> applies those).</para>
/// </summary>
public interface IIntakeShadowUserProvisioner
{
    /// <summary>
    /// Idempotently ensure the operator's shadow user exists and is active in
    /// <paramref name="officeId"/>'s database, carrying the operator's current address and holding
    /// exactly the per-office role <paramref name="roleName"/> (any other shadow role is removed).
    /// Returns the shadow user's id.
    /// </summary>
    Task<Guid> EnsureShadowUserAsync(Guid officeId, Guid operatorUserId, string roleName);

    /// <summary>
    /// Disable (lock out) the operator's shadow user in <paramref name="officeId"/> if it
    /// exists. Logs a warning when there is none to disable.
    /// </summary>
    Task DisableShadowUserAsync(Guid officeId, Guid operatorUserId);

    /// <summary>Disables the operator's shadow user in every office that has one.</summary>
    Task DisableAllShadowUsersAsync(Guid operatorUserId);

    /// <summary>
    /// Moves the operator's shadow user in every office to their current host address and name.
    /// <paramref name="previousEmail"/> finds a shadow made before shadows were stamped with the
    /// operator's id, which still carries the address the operator had then.
    /// </summary>
    Task SyncShadowUsersAsync(Guid operatorUserId, string? previousEmail);
}
