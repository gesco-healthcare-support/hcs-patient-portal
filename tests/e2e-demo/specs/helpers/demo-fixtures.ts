/**
 * Demo fixtures: per-tenant credentials + URL constants used across every
 * spec. The values mirror the seed contributors in
 * `src/.../Domain/Identity/InternalUsersDataSeedContributor.cs` and
 * `DemoExternalUsersDataSeedContributor.cs`. If those seeds change, this
 * file is the only place to update.
 */
export const TENANT = {
  slug: 'falkinstein',
  name: 'Falkinstein',
};

export const URLS = {
  spa: `http://${TENANT.slug}.localhost:4200`,
  authServer: `http://${TENANT.slug}.localhost:44368`,
  api: `http://${TENANT.slug}.localhost:44327`,
};

/**
 * Default password for every Development-seeded account. Matches
 * `InternalUsersDataSeedContributor.DefaultPassword`. Leaks in fixtures
 * are not a HIPAA risk -- these accounts only exist in dev.
 */
export const DEFAULT_PASSWORD = '1q2w3E*r';

export const INTERNAL_USERS = {
  // Internal user, NOT IT Admin -- the demo's "internal user that creates
  // slots" is the Staff Supervisor (matches OLD's slot-management role).
  supervisor: {
    email: `supervisor@${TENANT.slug}.test`,
    password: DEFAULT_PASSWORD,
    role: 'Staff Supervisor',
  },
  // Tenant admin (NOT IT Admin -- IT Admin is host-side at it.admin@hcs.test).
  admin: {
    email: `admin@${TENANT.slug}.test`,
    password: DEFAULT_PASSWORD,
    role: 'admin',
  },
  // Clinic Staff -- third internal role for completeness.
  staff: {
    email: `staff@${TENANT.slug}.test`,
    password: DEFAULT_PASSWORD,
    role: 'Clinic Staff',
  },
};

export const EXTERNAL_USERS = {
  // Pre-seeded patient. The demo flow ALSO registers a new patient via the
  // public registration form to exercise that path; the pre-seeded one is
  // a fallback for cases where registration is blocked.
  patient: {
    email: `patient@${TENANT.slug}.test`,
    password: DEFAULT_PASSWORD,
    role: 'Patient',
  },
  applicantAttorney: {
    email: `applicant.attorney@${TENANT.slug}.test`,
    password: DEFAULT_PASSWORD,
    role: 'Applicant Attorney',
  },
  defenseAttorney: {
    email: `defense.attorney@${TENANT.slug}.test`,
    password: DEFAULT_PASSWORD,
    role: 'Defense Attorney',
  },
  // "Adjuster" prefix for the email but the actual role is "Claim Examiner"
  // per the role-naming reconciliation in
  // ExternalUserRoleDataSeedContributor.cs:36-39.
  claimExaminer: {
    email: `adjuster@${TENANT.slug}.test`,
    password: DEFAULT_PASSWORD,
    role: 'Claim Examiner',
  },
};

/**
 * A unique-per-run external user used by the registration-flow spec. The
 * timestamp keeps re-runs from colliding with prior runs' inserts in the
 * shared SQL Server volume. Tests that need a stable identity should pull
 * from `EXTERNAL_USERS` instead.
 */
export function newRunPatient() {
  const stamp = Date.now();
  const localPart = `e2e-patient-${stamp}`;
  return {
    email: `${localPart}@e2e.test`,
    firstName: 'E2E',
    lastName: `Run${stamp}`,
    password: DEFAULT_PASSWORD,
    role: 'Patient',
    dateOfBirth: '1990-01-01',
    phoneNumber: '5551234567',
  };
}
