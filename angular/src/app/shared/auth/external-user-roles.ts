// Phase 9 L7 (2026-05-04) -- shared role classifier used by the post-login
// redirect guard and the external-user-match canMatch guard. (Formerly also
// drove app.component's external-user CSS toggle; removed 2026-06-14 when the
// LeptonX layout was dropped in favour of a bare router-outlet.)
//
// Mirrors the canonical external-role names from
// src/.../Domain/Identity/ExternalUserRoleDataSeedContributor.cs and the
// memory note project_role-model.md.
//
// OLD has 4 external roles total (verified at
// P:\PatientPortalOld\PatientAppointment.Models\Enums\Roles.cs):
//   Patient = 4, Adjuster = 5, PatientAttorney = 6, DefenseAttorney = 7.
// NEW renamed two for clarity:
//   OLD Adjuster        -> NEW "Claim Examiner"
//   OLD PatientAttorney -> NEW "Applicant Attorney"
// "Adjuster" and "Claim Examiner" are the SAME role under different
// labels (NEW chose "Claim Examiner" to match the AppointmentClaimExaminer
// entity name). An earlier draft mistakenly listed both as separate
// roles -- reconciled to the four canonical names the seed contributor
// actually creates.
//
// 2026-11-04: six external roles now -- the four above plus 'Applicant Paralegal' and
// 'Defense Paralegal' (law-office staff, one side each). Missing them here would route a
// paralegal to the INTERNAL dashboard.

const EXTERNAL_USER_ROLES = [
  'patient',
  'applicant attorney',
  'defense attorney',
  'applicant paralegal',
  'defense paralegal',
  'claim examiner',
] as const;

/**
 * Returns true when every role on the user is an external role. Returns
 * false when the user has zero roles (treated as "not authenticated yet,
 * cannot decide -- let auth guard handle it") OR when at least one role
 * is non-external (treated as internal, route to /dashboard).
 *
 * Comparison is case-insensitive trimmed against EXTERNAL_USER_ROLES.
 */
export function hasOnlyExternalRoles(
  roles: ReadonlyArray<string | null | undefined> | null | undefined,
): boolean {
  if (!roles || roles.length === 0) {
    return false;
  }
  const normalized = roles.map((r) => (r ?? '').toLowerCase().trim()).filter((r) => r.length > 0);
  if (normalized.length === 0) {
    return false;
  }
  return normalized.every((r) => (EXTERNAL_USER_ROLES as readonly string[]).includes(r));
}

// ---- law-office side helpers (paralegal feature) ---------------------------------------------
// A paralegal belongs to ONE side, the same side as the matching attorney role. Screens that
// mean "the caller may use the attorney UI of side X" take the side helpers below; screens that
// mean "the caller IS the attorney of record" keep the attorney helpers. Role strings are the ABP
// role names the server seeds; comparison is case-insensitive and trimmed.

export const APPLICANT_ATTORNEY_ROLE = 'Applicant Attorney';
export const DEFENSE_ATTORNEY_ROLE = 'Defense Attorney';
export const APPLICANT_PARALEGAL_ROLE = 'Applicant Paralegal';
export const DEFENSE_PARALEGAL_ROLE = 'Defense Paralegal';

export type LawOfficeSide = 'applicant' | 'defense';
type RoleList = ReadonlyArray<string | null | undefined> | null | undefined;

function holds(roles: RoleList, name: string): boolean {
  const wanted = name.toLowerCase();
  return (roles ?? []).some((r) => (r ?? '').toLowerCase().trim() === wanted);
}

export const isApplicantAttorneyRole = (roles: RoleList): boolean =>
  holds(roles, APPLICANT_ATTORNEY_ROLE);
export const isDefenseAttorneyRole = (roles: RoleList): boolean =>
  holds(roles, DEFENSE_ATTORNEY_ROLE);
export const isApplicantParalegalRole = (roles: RoleList): boolean =>
  holds(roles, APPLICANT_PARALEGAL_ROLE);
export const isDefenseParalegalRole = (roles: RoleList): boolean =>
  holds(roles, DEFENSE_PARALEGAL_ROLE);

/** Either paralegal role. */
export const isParalegalRole = (roles: RoleList): boolean =>
  isApplicantParalegalRole(roles) || isDefenseParalegalRole(roles);

/** Applicant-side external: the Applicant Attorney OR the Applicant Paralegal. */
export const isApplicantSideExternal = (roles: RoleList): boolean =>
  isApplicantAttorneyRole(roles) || isApplicantParalegalRole(roles);

/** Defense-side external: the Defense Attorney OR the Defense Paralegal. */
export const isDefenseSideExternal = (roles: RoleList): boolean =>
  isDefenseAttorneyRole(roles) || isDefenseParalegalRole(roles);

/** Any attorney or paralegal (the "law office" external roles). */
export const isLawOfficeExternal = (roles: RoleList): boolean =>
  isApplicantSideExternal(roles) || isDefenseSideExternal(roles);

/** The paralegal role of a side, as the server names it. */
export function paralegalRoleForSide(side: LawOfficeSide): string {
  return side === 'applicant' ? APPLICANT_PARALEGAL_ROLE : DEFENSE_PARALEGAL_ROLE;
}

/**
 * Role names offered in the "Paralegal / Authorized User" picker. Internal staff get both
 * sides' paralegals; a law-office booker gets only their own side's paralegal (the server
 * re-checks); everyone else gets the four party roles only.
 */
export function authorizedUserRoleOptions(roles: RoleList, isInternal: boolean): string[] {
  const base = ['Patient', APPLICANT_ATTORNEY_ROLE, DEFENSE_ATTORNEY_ROLE, 'Claim Examiner'];
  if (isInternal) {
    return [...base, APPLICANT_PARALEGAL_ROLE, DEFENSE_PARALEGAL_ROLE];
  }
  if (isApplicantSideExternal(roles)) {
    return [...base, APPLICANT_PARALEGAL_ROLE];
  }
  if (isDefenseSideExternal(roles)) {
    return [...base, DEFENSE_PARALEGAL_ROLE];
  }
  return base;
}

/** Display text for a role in the Paralegal / Authorized User picker; the posted value stays the role string. */
export function authorizedUserRoleLabel(role: string): string {
  if (role === APPLICANT_PARALEGAL_ROLE) return 'Paralegal (Applicant side)';
  if (role === DEFENSE_PARALEGAL_ROLE) return 'Paralegal (Defense side)';
  return role;
}
