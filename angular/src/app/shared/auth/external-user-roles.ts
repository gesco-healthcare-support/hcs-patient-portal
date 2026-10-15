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

const EXTERNAL_USER_ROLES = [
  'patient',
  'applicant attorney',
  'defense attorney',
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
