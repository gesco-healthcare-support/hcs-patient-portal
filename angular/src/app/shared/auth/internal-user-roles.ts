import { ConfigStateService } from '@abp/ng.core';

/**
 * Internal staff role keys the shell nav config filters by (mirrors the
 * roleKey values in design_handoff_appointment_portal/components/in-shell.jsx).
 * `admin` is the ABP host superuser, which sees every nav item.
 */
export type InternalRoleKey = 'itadmin' | 'supervisor' | 'intake' | 'admin';

/**
 * Canonical internal role NAMES -> nav keys. Verified against AbpRoles
 * (seeded): "IT Admin" (host), "Staff Supervisor", "Intake Staff" (tenant),
 * plus the ABP superuser "admin". Comparison is lower-cased + trimmed. The
 * external roles (patient / applicant attorney / defense attorney / claim
 * examiner) are intentionally absent -- external users never reach the shell.
 */
const ROLE_NAME_TO_KEY: Readonly<Record<string, InternalRoleKey>> = {
  'it admin': 'itadmin',
  'staff supervisor': 'supervisor',
  'intake staff': 'intake',
  admin: 'admin',
};

/**
 * Resolve a user's ABP roles to the internal shell nav key. The superuser
 * ('admin') wins when present (it sees all nav). Returns null when the user has
 * no internal role -- i.e. an external user, who never reaches the shell.
 */
export function resolveInternalRoleKey(
  roles: ReadonlyArray<string | null | undefined> | null | undefined,
): InternalRoleKey | null {
  if (!roles || roles.length === 0) {
    return null;
  }
  const normalized = roles.map((r) => (r ?? '').toLowerCase().trim());
  if (normalized.includes('admin')) {
    return 'admin';
  }
  for (const role of normalized) {
    const key = ROLE_NAME_TO_KEY[role];
    if (key) {
      return key;
    }
  }
  return null;
}

/**
 * True when the SPA is in host context (no current tenant) -- the IT Admin or
 * the host superuser at host scope. Drives IN_NAV_HOST vs IN_NAV selection in
 * the shell. A tenant-scoped user (Supervisor / Intake Staff) has a current
 * tenant id and returns false.
 */
export function isHostScope(config: ConfigStateService): boolean {
  const tenant = config.getOne('currentTenant') as { id?: string | null } | null;
  return !tenant || !tenant.id;
}
