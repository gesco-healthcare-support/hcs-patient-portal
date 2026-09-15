/**
 * Phase 1 / C2 / D4 (firm-based AA/DA registration, 2026-06-11) -- TS mirror of
 * the C# `ExternalUserDisplayName.Resolve` helper
 * (src/.../Application/ExternalSignups/ExternalUserDisplayName.cs).
 *
 * Firm AA/DA accounts register with a blank IdentityUser name/surname and only
 * a FirmName extension property (D1). This resolves a single display string for
 * the home banner, external-user pickers, and party tables so a firm account
 * shows its firm name instead of a blank or the raw email.
 *
 * Precedence (locked by Adrian, Q1 2026-06-11 -- fall back to firm name when
 * first/last are absent): "First Last" if present, else FirmName, else email,
 * else "". Whitespace-only inputs are treated as absent. Keep the two precedence
 * rules in sync with the C# helper.
 */
export function resolveExternalUserDisplayName(
  firstName?: string | null,
  lastName?: string | null,
  firmName?: string | null,
  email?: string | null,
): string {
  const fullName = `${(firstName ?? '').trim()} ${(lastName ?? '').trim()}`.trim();
  if (fullName) {
    return fullName;
  }

  const firm = firmName?.trim();
  if (firm) {
    return firm;
  }

  return email?.trim() ?? '';
}
