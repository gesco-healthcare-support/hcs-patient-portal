using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Identity;
using Volo.Abp.Users;

namespace HealthcareSupport.CaseEvaluation.Users;

/// <summary>
/// Which accounts are privileged, and whether the caller is. "IT Admin" is the host technical role
/// and "admin" the framework's office/host administrator role. A caller who holds neither may not
/// act on an account that holds either: <see cref="UserExtendedAppService"/> applies this to every
/// change on the identity screen, and <c>AccountActivationAppService</c> to resending an activation
/// email, so the two cannot disagree about who may touch an administrator.
/// </summary>
internal static class PrivilegedAccounts
{
    public static readonly IReadOnlyList<string> RoleNames =
        new[] { InternalUserRoleDataSeedContributor.ItAdminRoleName, "admin" };

    public static bool IsPrivilegedRole(string role) =>
        RoleNames.Contains(role, StringComparer.OrdinalIgnoreCase);

    public static bool CallerIsPrivileged(ICurrentUser currentUser) =>
        RoleNames.Any(currentUser.IsInRole);

    public static bool HoldsPrivilegedRole(IEnumerable<string> roles) =>
        roles.Any(IsPrivilegedRole);
}
