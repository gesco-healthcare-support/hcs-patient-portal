using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Who a role-based notice goes to (2026-10-26): every holder of the role in the current scope who is
/// switched ON. A switched-off account keeps its role -- an Intake operator's office shadow login is locked,
/// not stripped, when they are unassigned or moved to Staff Supervisor -- so a lookup by role alone still
/// finds it, and would keep sending that office's mail to someone who no longer works it. Every email,
/// digest, alert and in-app notice addressed to an office's staff (or to the IT Admins), and the
/// Responsible User list on the Approve dialog (whoever is picked gets that appointment's mail), reads
/// its users through here, so the rule is in one place.
/// </summary>
public static class StaffRecipients
{
    /// <summary>The switched-on holders of <paramref name="roleName"/> in the current scope.</summary>
    public static async Task<List<IdentityUser>> GetActiveUsersInRoleAsync(
        this IdentityUserManager userManager,
        string roleName)
    {
        return ActiveOnly(await userManager.GetUsersInRoleAsync(roleName));
    }

    /// <summary>The switched-on holders of the role with <paramref name="normalizedRoleName"/>, read through the repository.</summary>
    public static async Task<List<IdentityUser>> GetActiveListByNormalizedRoleNameAsync(
        this IIdentityUserRepository repository,
        string normalizedRoleName,
        CancellationToken cancellationToken = default)
    {
        return ActiveOnly(await repository.GetListByNormalizedRoleNameAsync(
            normalizedRoleName, cancellationToken: cancellationToken));
    }

    /// <summary>The one rule both lookups apply: only switched-on accounts receive anything.</summary>
    public static List<IdentityUser> ActiveOnly(IEnumerable<IdentityUser> users)
    {
        return users.Where(u => u.IsActive).ToList();
    }
}
