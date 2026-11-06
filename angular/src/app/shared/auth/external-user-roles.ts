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

const CLAIM_EXAMINER_ROLE = 'Claim Examiner';

/** Localization key of the claim examiner helper's picker label (shown through `abpLocalization`). */
export const CLAIM_EXAMINER_ASSISTANT_LABEL_KEY =
  '::Appointment:AuthorizedUsers:ClaimExaminerAssistant';

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
 * Role names offered in the "Paralegal / Authorized User" picker at booking. Internal staff get every
 * role. An external booker gets only what the server lets them add (office decisions 3-5): a law-office
 * booker their own side's attorney + paralegal, a claim examiner booker the Claim Examiner role (their
 * assistant), a Patient nothing (the wizard then shows no section). A Patient or Claim Examiner role is
 * NOT offered to a law-office booker: the server refuses it, and the refusal would fail the whole booking.
 */
export function authorizedUserRoleOptions(roles: RoleList, isInternal: boolean): string[] {
  if (isInternal) {
    return [
      'Patient',
      APPLICANT_ATTORNEY_ROLE,
      DEFENSE_ATTORNEY_ROLE,
      'Claim Examiner',
      APPLICANT_PARALEGAL_ROLE,
      DEFENSE_PARALEGAL_ROLE,
    ];
  }
  const options: string[] = [];
  if (isApplicantSideExternal(roles)) {
    options.push(APPLICANT_ATTORNEY_ROLE, APPLICANT_PARALEGAL_ROLE);
  }
  if (isDefenseSideExternal(roles)) {
    options.push(DEFENSE_ATTORNEY_ROLE, DEFENSE_PARALEGAL_ROLE);
  }
  if (options.length === 0 && holds(roles, CLAIM_EXAMINER_ROLE)) {
    options.push(CLAIM_EXAMINER_ROLE);
  }
  return options;
}

/** Display text for a role in the Paralegal / Authorized User picker; the posted value stays the role string. */
export function authorizedUserRoleLabel(role: string, external = false): string {
  // An external claim examiner's helper is an assistant; the label is a localization key.
  if (external && role === CLAIM_EXAMINER_ROLE) return CLAIM_EXAMINER_ASSISTANT_LABEL_KEY;
  return roleDisplayLabel(role);
}

/** Display text for the applicant-side paralegal (the role NAME "Applicant Paralegal" never changes). */
export const APPLICANT_PARALEGAL_LABEL = 'Paralegal / Case Manager (Applicant side)';
/** Display text for the defense-side paralegal (the role NAME "Defense Paralegal" never changes). */
export const DEFENSE_PARALEGAL_LABEL = 'Paralegal / Case Manager (Defense side)';

/**
 * The one place a role NAME becomes display text for the paralegal roles. Every screen that renders a
 * role name to a person goes through here; any other role comes back unchanged. Display only: role
 * names, ExternalUserType values, grants and invite keys are identifiers and stay as they are.
 */
export function roleDisplayLabel(role: string | null | undefined): string {
  const name = (role ?? '').trim();
  const key = name.toLowerCase();
  if (key === APPLICANT_PARALEGAL_ROLE.toLowerCase()) return APPLICANT_PARALEGAL_LABEL;
  if (key === DEFENSE_PARALEGAL_ROLE.toLowerCase()) return DEFENSE_PARALEGAL_LABEL;
  return name;
}

/** What the page knows about the caller's standing on ONE case (decisions 3, 4 and 5). */
export interface CaseStanding {
  roles: RoleList;
  /** The caller's verified email; leave empty when the email is not verified. */
  verifiedEmail?: string | null;
  isBooker: boolean;
  isAuthorizedUser: boolean;
  applicantAttorneyEmail?: string | null;
  defenseAttorneyEmail?: string | null;
  claimExaminerEmail?: string | null;
}

function sameEmail(a?: string | null, b?: string | null): boolean {
  const x = (a ?? '').trim().toLowerCase();
  return x.length > 0 && x === (b ?? '').trim().toLowerCase();
}

/**
 * The Authorized User roles an EXTERNAL caller may add or remove on a case after booking, mirroring the
 * server rule (AccessorManagementRules). A law-office side is manageable when the caller is its named
 * attorney (verified email equals that side's attorney column AND the attorney role), or is the booker /
 * an Authorized User holding a role of that side. A claim examiner may manage Claim Examiner helpers only
 * (named by verified email in the claim examiner column, or the booker / an Authorized User holding that
 * role). A Patient manages nothing. Cosmetic: the server stays authoritative.
 */
export function manageableAccessorRoles(s: CaseStanding): string[] {
  const roles: string[] = [];
  const sidePartner = (
    holdsSide: boolean,
    holdsAttorney: boolean,
    column: string | null | undefined,
    attorney: string,
    paralegal: string,
  ): void => {
    const named = holdsAttorney && sameEmail(s.verifiedEmail, column);
    if (holdsSide && (named || s.isBooker || s.isAuthorizedUser)) {
      roles.push(attorney, paralegal);
    }
  };
  sidePartner(
    isApplicantSideExternal(s.roles),
    isApplicantAttorneyRole(s.roles),
    s.applicantAttorneyEmail,
    APPLICANT_ATTORNEY_ROLE,
    APPLICANT_PARALEGAL_ROLE,
  );
  sidePartner(
    isDefenseSideExternal(s.roles),
    isDefenseAttorneyRole(s.roles),
    s.defenseAttorneyEmail,
    DEFENSE_ATTORNEY_ROLE,
    DEFENSE_PARALEGAL_ROLE,
  );
  if (
    holds(s.roles, CLAIM_EXAMINER_ROLE) &&
    (sameEmail(s.verifiedEmail, s.claimExaminerEmail) || s.isBooker || s.isAuthorizedUser)
  ) {
    roles.push(CLAIM_EXAMINER_ROLE);
  }
  return roles;
}
