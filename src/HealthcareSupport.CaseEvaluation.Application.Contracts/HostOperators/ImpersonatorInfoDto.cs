using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// 2026-07-10 (QA item 5): the internal role(s) of the HOST OPERATOR behind an
/// impersonation session, resolved from the <c>AbpClaimTypes.ImpersonatorUserId</c>
/// claim. Lets the internal shell show the operator's OWN role (e.g. "Staff
/// Supervisor") instead of the impersonated office account's role ("Administrator"),
/// which is what the session token actually carries. <see cref="Roles"/> is empty and
/// <see cref="IsImpersonating"/> is false when the caller is not an impersonation.
/// </summary>
public class ImpersonatorInfoDto
{
    public bool IsImpersonating { get; set; }

    public List<string> Roles { get; set; } = new();
}
