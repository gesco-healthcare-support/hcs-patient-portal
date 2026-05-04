// Phase 9 L7 (2026-05-04) -- shared role classifier used by the post-login
// redirect guard and by app.component's external-user CSS toggle.
//
// Mirrors the canonical external-role names from
// src/.../Domain/Identity/ExternalUserRoleDataSeedContributor.cs and the
// memory note project_role-model.md. Plus "Adjuster" because OLD's domain
// code treats adjusters as external (they self-register via the same
// signup flow).
//
// If a role is renamed in the seed contributor, this list and the mirror
// in app.component.ts both have to be updated. Long term these should
// share one source of truth; the duplication is acknowledged tech debt.

const EXTERNAL_USER_ROLES = [
  'patient',
  'applicant attorney',
  'defense attorney',
  'claim examiner',
  'adjuster',
] as const;

/**
 * Returns true when every role on the user is an external role. Returns
 * false when the user has zero roles (treated as "not authenticated yet,
 * cannot decide -- let auth guard handle it") OR when at least one role
 * is non-external (treated as internal, route to /dashboard).
 *
 * Comparison is case-insensitive trimmed against EXTERNAL_USER_ROLES.
 */
export function hasOnlyExternalRoles(roles: ReadonlyArray<string | null | undefined> | null | undefined): boolean {
  if (!roles || roles.length === 0) {
    return false;
  }
  const normalized = roles
    .map((r) => (r ?? '').toLowerCase().trim())
    .filter((r) => r.length > 0);
  if (normalized.length === 0) {
    return false;
  }
  return normalized.every((r) => (EXTERNAL_USER_ROLES as readonly string[]).includes(r));
}

/**
 * Returns true when the user has at least one external role. Used by
 * app.component to decide whether to hide the LeptonX sidebar (the OLD
 * behaviour: external users see a chrome-less layout). Distinct from
 * {@link hasOnlyExternalRoles} which is the post-login-redirect contract:
 * a user with both external + internal roles (e.g. an admin who is also
 * a registered patient) DOES see the dashboard but DOES NOT get the
 * external CSS class because their internal role takes precedence.
 */
export function hasAnyExternalRole(roles: ReadonlyArray<string | null | undefined> | null | undefined): boolean {
  if (!roles || roles.length === 0) {
    return false;
  }
  const target = new Set(EXTERNAL_USER_ROLES as readonly string[]);
  return roles.some((r) => target.has((r ?? '').toLowerCase().trim()));
}
