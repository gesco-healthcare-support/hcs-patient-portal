import type { IconName } from '../../ui/icon/icon.registry';
import type { InternalRoleKey } from '../../auth/internal-user-roles';

/**
 * Internal staff shell navigation, ported from the design handoff
 * (design_handoff_appointment_portal/components/in-shell.jsx -- IN_NAV /
 * IN_NAV_HOST). The prototype's numeric `badge` literals are replaced by a
 * BADGE KEY that the live count signals (InternalNavBadgeService) bind to;
 * the prototype `id` values are dropped in favour of the real Angular route,
 * verified against app.routes.ts (see docs/plans/2026-06-14-internal-shell.md).
 */

/** Which live count drives a nav item's badge. */
export type NavBadgeKey = 'appointments' | 'changeRequests';

export interface InternalNavItem {
  /** Stable id (used for active-item tracking + trackBy). */
  id: string;
  label: string;
  icon: IconName;
  /** Absolute Angular route the item links to. */
  route: string;
  /** Role keys that see this item ('admin' superuser sees everything). */
  roles: InternalRoleKey[];
  /** Live count badge, when this item carries one. */
  badge?: NavBadgeKey;
}

export interface InternalNavGroup {
  sect: string;
  items: InternalNavItem[];
}

/**
 * Tenant operational nav (Staff Supervisor + Intake Staff; also IT Admin once
 * a tenant-switch flow exists -- out of scope for the MVP). Routes verified
 * against app.routes.ts.
 */
export const IN_NAV: readonly InternalNavGroup[] = [
  {
    sect: 'Workspace',
    items: [
      {
        id: 'dashboard',
        label: 'Dashboard',
        icon: 'grid',
        route: '/dashboard',
        roles: ['supervisor', 'intake'],
      },
      {
        id: 'appointments',
        label: 'Appointments',
        icon: 'calendar',
        route: '/appointments',
        roles: ['supervisor', 'intake'],
        badge: 'appointments',
      },
      {
        id: 'change-requests',
        label: 'Change Requests',
        icon: 'refresh',
        // The /appointments/change-requests parent has no index route (only
        // reschedules / cancellations children), so the nav targets the first
        // queue. See change-request-routes.ts.
        route: '/appointments/change-requests/reschedules',
        roles: ['supervisor'],
        badge: 'changeRequests',
      },
      {
        id: 'change-logs',
        label: 'Change Logs',
        icon: 'clock',
        route: '/appointment-change-logs',
        roles: ['supervisor'],
      },
      { id: 'reports', label: 'Reports', icon: 'list', route: '/reports', roles: ['supervisor'] },
    ],
  },
  {
    sect: 'Scheduling',
    items: [
      {
        id: 'availabilities',
        label: 'Doctor Availabilities',
        icon: 'calendar',
        route: '/doctor-management/doctor-availabilities',
        roles: ['supervisor'],
      },
      {
        id: 'locations',
        label: 'Locations',
        icon: 'map',
        route: '/doctor-management/locations',
        roles: ['supervisor'],
      },
      {
        id: 'wcab',
        label: 'WCAB Offices',
        icon: 'map',
        route: '/doctor-management/wcab-offices',
        roles: ['supervisor'],
      },
    ],
  },
  {
    sect: 'Configuration',
    items: [
      {
        id: 'appt-types',
        label: 'Appointment Types',
        icon: 'list',
        route: '/appointment-management/appointment-types',
        roles: ['supervisor'],
      },
      {
        id: 'appt-statuses',
        label: 'Appointment Statuses',
        icon: 'list',
        route: '/appointment-management/appointment-statuses',
        roles: ['supervisor'],
      },
      {
        id: 'doc-types',
        label: 'Document Types',
        icon: 'doc',
        route: '/appointment-management/document-types',
        roles: ['supervisor'],
      },
      {
        id: 'languages',
        label: 'Appointment Languages',
        icon: 'list',
        route: '/appointment-management/appointment-languages',
        roles: ['supervisor'],
      },
      {
        id: 'states',
        label: 'States',
        icon: 'map',
        route: '/configurations/states',
        roles: ['supervisor'],
      },
    ],
  },
  {
    sect: 'People',
    items: [
      {
        id: 'patients',
        label: 'Patients',
        icon: 'users',
        route: '/user-management/patients',
        roles: ['supervisor', 'intake'],
      },
      {
        id: 'applicant-attorneys',
        label: 'Applicant Attorneys',
        icon: 'user',
        route: '/applicant-attorneys',
        roles: ['supervisor'],
      },
      {
        id: 'defense-attorneys',
        label: 'Defense Attorneys',
        icon: 'user',
        route: '/defense-attorneys',
        roles: ['supervisor'],
      },
      {
        id: 'claim-examiners',
        label: 'Claim Examiners',
        icon: 'user',
        route: '/claim-examiners',
        roles: ['supervisor'],
      },
    ],
  },
  {
    sect: 'Administration',
    items: [
      {
        id: 'invite-external',
        label: 'Users & Access',
        icon: 'user',
        route: '/users/invite',
        roles: ['supervisor', 'intake'],
      },
      {
        id: 'identity',
        label: 'Users & Roles',
        icon: 'users',
        route: '/identity',
        roles: ['supervisor'],
      },
      {
        id: 'notif-templates',
        label: 'Notification Templates',
        icon: 'doc',
        route: '/text-template-management',
        roles: ['supervisor'],
      },
      {
        id: 'settings',
        label: 'System Parameters',
        icon: 'settings',
        route: '/setting-management',
        roles: ['supervisor'],
      },
      {
        id: 'audit',
        label: 'Audit Logs',
        icon: 'clock',
        route: '/audit-logs',
        roles: ['supervisor'],
      },
    ],
  },
];

/**
 * IT Admin platform (host / cross-tenant) nav. Routes target the ABP SaaS +
 * admin lazy modules; verify they render inside the shell's child outlet.
 */
export const IN_NAV_HOST: readonly InternalNavGroup[] = [
  {
    sect: 'Platform',
    items: [
      { id: 'dashboard', label: 'Overview', icon: 'grid', route: '/dashboard', roles: ['itadmin'] },
    ],
  },
  {
    sect: 'SaaS',
    items: [
      {
        id: 'tenants',
        label: 'Tenants',
        icon: 'users',
        route: '/saas/tenants',
        roles: ['itadmin'],
      },
      {
        id: 'editions',
        label: 'Editions',
        icon: 'list',
        route: '/saas/editions',
        roles: ['itadmin'],
      },
      {
        id: 'internal-users',
        label: 'Internal Users',
        icon: 'user',
        route: '/internal-users',
        roles: ['itadmin'],
      },
    ],
  },
  {
    sect: 'Administration',
    items: [
      {
        id: 'identity',
        label: 'Users & Roles',
        icon: 'users',
        route: '/identity',
        roles: ['itadmin'],
      },
      {
        id: 'notif-templates',
        label: 'Notification Templates',
        icon: 'doc',
        route: '/text-template-management',
        roles: ['itadmin'],
      },
      {
        id: 'settings',
        label: 'System Parameters',
        icon: 'settings',
        route: '/setting-management',
        roles: ['itadmin'],
      },
      { id: 'audit', label: 'Audit Logs', icon: 'clock', route: '/audit-logs', roles: ['itadmin'] },
    ],
  },
];

/**
 * Keep only the items the role key can see; drop groups left empty. The
 * 'admin' superuser sees every item. (A null role key -- an external user --
 * is filtered out upstream; the shell never renders for them.)
 */
export function filterNavGroups(
  groups: readonly InternalNavGroup[],
  roleKey: InternalRoleKey,
): InternalNavGroup[] {
  return groups
    .map((g) => ({
      ...g,
      items: g.items.filter((it) => roleKey === 'admin' || it.roles.includes(roleKey)),
    }))
    .filter((g) => g.items.length > 0);
}

/**
 * Resolve the nav groups for a user. Per the 2026-06-14 decision (host scope +
 * role): the platform nav (IN_NAV_HOST) shows only when the user is at host
 * scope AND is an IT Admin or the superuser; everyone else -- including a Staff
 * Supervisor or Intake Staff inside a tenant -- gets the tenant operational nav
 * (IN_NAV). This guarantees a Supervisor lands on IN_NAV even if the ABP seed
 * scopes the role at host, and lets a future IT-Admin tenant-switch flip to
 * IN_NAV naturally.
 */
export function resolveNavGroups(roleKey: InternalRoleKey, hostScope: boolean): InternalNavGroup[] {
  const usesHostNav = hostScope && (roleKey === 'itadmin' || roleKey === 'admin');
  return filterNavGroups(usesHostNav ? IN_NAV_HOST : IN_NAV, roleKey);
}
