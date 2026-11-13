using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.HostOperators;
using HealthcareSupport.CaseEvaluation.Identity;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.ExternalLogins;

/// <summary>
/// Who counts as an "outside user" whose login a staff supervisor may switch off (2026-11-13). The rule is
/// deny by default: every role the account holds must be one of <see cref="ExternalRoleConsts.All"/> and there
/// must be at least one, so Intake Staff, Staff Supervisor, admin, IT Admin and anything else is out, as is an
/// account with a staff role added to an outside role. A host operator's shadow login in an office is out even
/// when its roles look outside (<c>HostOperatorUserId</c>), because it is the operator's own access.
/// </summary>
internal static class ExternalLoginTargets
{
    public static bool IsOutsideRole(string? role) =>
        role != null && ExternalRoleConsts.All.Contains(role, StringComparer.OrdinalIgnoreCase);

    public static bool IsOutsideUser(IReadOnlyCollection<string> roles) =>
        roles.Count > 0 && roles.All(IsOutsideRole);

    public static bool IsHostOperatorShadow(IdentityUser user) =>
        IntakeShadowUserProvisioner.IsStampedShadow(user);
}
