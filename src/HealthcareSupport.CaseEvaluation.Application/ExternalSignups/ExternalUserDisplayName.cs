namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// Phase 1 / C2 / D4 (firm-based AA/DA registration, 2026-06-11) -- shared
/// display-name resolution for external users.
///
/// <para>Firm AA/DA accounts register with a blank <c>IdentityUser.Name</c>/
/// <c>Surname</c> and only a <c>FirmName</c> extension property (D1). Without
/// a fallback, every place that renders an external user's name shows a blank
/// or the raw email. This helper resolves a single display string.</para>
///
/// <para>Precedence (locked by Adrian, Q1 2026-06-11 -- fall back to firm name
/// when first/last are absent): "First Last" if present, else FirmName, else
/// email, else empty. Whitespace-only inputs are treated as absent.</para>
///
/// <para>Pure (no DI / no DB): callers pass the already-loaded values. The TS
/// mirror is <c>resolveExternalUserDisplayName</c> in
/// <c>angular/src/app/shared/</c>; keep the two precedence rules in sync.</para>
/// </summary>
internal static class ExternalUserDisplayName
{
    internal static string Resolve(string? firstName, string? lastName, string? firmName, string? email)
    {
        var fullName = $"{firstName?.Trim()} {lastName?.Trim()}".Trim();
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        if (!string.IsNullOrWhiteSpace(firmName))
        {
            return firmName.Trim();
        }

        return email?.Trim() ?? string.Empty;
    }
}
