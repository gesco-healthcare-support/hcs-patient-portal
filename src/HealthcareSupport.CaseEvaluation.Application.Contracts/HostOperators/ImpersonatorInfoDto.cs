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

    /// <summary>
    /// Display name of the host operator behind the impersonation ("Name Surname",
    /// or user name), so the internal shell chip shows the operator's own identity
    /// instead of the impersonated office account. Empty when not impersonating.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = new();
}
