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
  /**
   * ABP permission policy that actually gates the item's route -- the SAME
   * `requiredPolicy` string the route guard checks (verified against the route
   * providers, Prompt 15 2026-06-15). When set, the nav hides the item unless
   * the policy is granted, so a visible item always resolves (never a
   * click-into-403). Items with no single ABP policy (Dashboard) or whose
   * policy lives in an ABP framework module not declared in our source
   * (Users & Roles, Notification Templates, System Parameters, Audit Logs)
   * omit it and fall back to the coarse role filter.
   */
  requiredPolicy?: string;
  /** Live count badge, when this item carries one. */
  badge?: NavBadgeKey;
}

export interface InternalNavGroup {
  sect: string;
  /**
   * When true the group renders as a collapsible accordion (toggle + chevron);
   * otherwise it is an always-open static header. Only Configuration and People
   * collapse (2026-06-19) -- the operational groups stay open so their items are
   * always one click away.
   */
  collapsible?: boolean;
  items: InternalNavItem[];
}

/**
 * Tenant operational nav (Staff Supervisor + Intake Staff; also IT Admin once
 * a tenant-switch flow exists -- out of scope for the MVP). Routes verified
 * against app.routes.ts; requiredPolicy verified against the route providers.
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
        requiredPolicy: 'CaseEvaluation.Appointments',
        badge: 'appointments',
      },
      {
        id: 'change-requests',
        label: 'Change Requests',
        icon: 'refresh',
        // Prompt 13 (2026-06-15): the reschedule + cancellation queues are unified
        // into one tabbed inbox at the parent path. See change-request-routes.ts.
        route: '/appointments/change-requests',
        roles: ['supervisor'],
        requiredPolicy: 'CaseEvaluation.AppointmentChangeRequests',
        badge: 'changeRequests',
      },
      {
        id: 'change-logs',
        label: 'Change Logs',
        icon: 'clock',
        route: '/appointment-change-logs',
        roles: ['supervisor'],
        requiredPolicy: 'CaseEvaluation.AppointmentChangeLogs',
      },
      {
        id: 'reports',
        label: 'Reports',
        icon: 'list',
        route: '/reports',
        roles: ['supervisor'],
        requiredPolicy: 'CaseEvaluation.Reports',
      },
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
        requiredPolicy: 'CaseEvaluation.DoctorAvailabilities',
      },
      {
        id: 'locations',
        label: 'Locations',
        icon: 'map',
        route: '/doctor-management/locations',
        roles: ['supervisor'],
        requiredPolicy: 'CaseEvaluation.Locations',
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
        requiredPolicy: 'CaseEvaluation.UserManagement.InviteExternalUser',
      },
      {
        id: 'identity',
        label: 'Users & Roles',
        icon: 'users',
        route: '/admin/roles',
        roles: ['supervisor'],
        requiredPolicy: 'AbpIdentity.Roles',
      },
      {
        id: 'notif-templates',
        label: 'Notification Templates',
        icon: 'doc',
        route: '/admin/templates',
        roles: ['supervisor'],
        requiredPolicy: 'CaseEvaluation.NotificationTemplates',
      },
      {
        id: 'settings',
        label: 'System Parameters',
        icon: 'settings',
        route: '/admin/parameters',
        // MO4 (2026-06-30): Intake staff are read-only operators; System
        // Parameters is supervisor-only. Dropped 'intake' from the role filter.
        roles: ['supervisor'],
        requiredPolicy: 'CaseEvaluation.SystemParameters',
      },
      {
        id: 'audit',
        label: 'Audit Logs',
        icon: 'clock',
        route: '/admin/audit',
        roles: ['supervisor'],
        requiredPolicy: 'AuditLogging.AuditLogs',
      },
    ],
  },
  {
    sect: 'Configuration',
    collapsible: true,
    items: [
      {
        id: 'appt-types',
        label: 'Appointment Types',
        icon: 'list',
        route: '/appointment-management/appointment-types',
        roles: ['supervisor'],
        requiredPolicy: 'CaseEvaluation.AppointmentTypes',
      },
      {
        id: 'appt-statuses',
        label: 'Appointment Statuses',
        icon: 'list',
        route: '/appointment-management/appointment-statuses',
        roles: ['supervisor'],
        requiredPolicy: 'CaseEvaluation.AppointmentStatuses',
      },
      {
        id: 'doc-types',
        label: 'Document Types',
        icon: 'doc',
        route: '/appointment-management/document-types',
        roles: ['supervisor'],
        requiredPolicy: 'CaseEvaluation.AppointmentDocumentTypes',
      },
      {
        id: 'languages',
        label: 'Appointment Languages',
        icon: 'list',
        route: '/appointment-management/appointment-languages',
        roles: ['supervisor'],
        requiredPolicy: 'CaseEvaluation.AppointmentLanguages',
      },
      {
        id: 'states',
        label: 'States',
        icon: 'map',
        route: '/configurations/states',
        roles: ['supervisor'],
        requiredPolicy: 'CaseEvaluation.States',
      },
      {
        id: 'wcab',
        label: 'WCAB Offices',
        icon: 'map',
        route: '/doctor-management/wcab-offices',
        roles: ['supervisor'],
        requiredPolicy: 'CaseEvaluation.WcabOffices',
      },
      {
        // Phase E (2026-06-25): in-office branding editor (current office).
        id: 'office-branding',
        label: 'Office Branding',
        icon: 'doc',
        route: '/office-branding',
        roles: ['supervisor', 'itadmin', 'admin'],
        requiredPolicy: 'CaseEvaluation.Branding.Edit',
      },
    ],
  },
  {
    sect: 'People',
    collapsible: true,
    items: [
      {
        id: 'patients',
        label: 'Patients',
        icon: 'users',
        route: '/user-management/patients',
        roles: ['supervisor', 'intake'],
        requiredPolicy: 'CaseEvaluation.Patients',
      },
      {
        id: 'applicant-attorneys',
        label: 'Applicant Attorneys',
        icon: 'user',
        route: '/applicant-attorneys',
        roles: ['supervisor'],
        requiredPolicy: 'CaseEvaluation.ApplicantAttorneys',
      },
      {
        id: 'defense-attorneys',
        label: 'Defense Attorneys',
        icon: 'user',
        route: '/defense-attorneys',
        roles: ['supervisor'],
        requiredPolicy: 'CaseEvaluation.DefenseAttorneys',
      },
      {
        id: 'claim-examiners',
        label: 'Claim Examiners',
        icon: 'user',
        route: '/claim-examiners',
        roles: ['supervisor'],
        requiredPolicy: 'CaseEvaluation.ClaimExaminers',
      },
    ],
  },
];

/**
 * IT Admin platform (host / cross-tenant) nav. Routes target the ABP SaaS +
 * admin lazy modules; verify they render inside the shell's child outlet.
 * These items are IT-Admin role-gated; their ABP framework policies are not
 * declared in our source, so they keep the role filter (no requiredPolicy).
 */
export const IN_NAV_HOST: readonly InternalNavGroup[] = [
  {
    sect: 'Platform',
    items: [
      // Phase D: the host Staff Supervisor shares the host overview (cross-office
      // aggregation). The Intake operator has no dashboard -- it lands on its
      // office switcher (see the Operators section).
      {
        id: 'dashboard',
        label: 'Overview',
        icon: 'grid',
        route: '/dashboard',
        roles: ['itadmin', 'supervisor'],
      },
    ],
  },
  {
    // Item 1 (2026-06-30): the former 'Offices' + 'SaaS' groups merged into one
    // "Practice Management" group. Per the ratified noun model a tenant = a
    // doctor's PRACTICE, so the office surfaces read as practices; the external-
    // user invite hub (Users & Access) folds in beside internal-staff management.
    // Overview stays in Platform. Labels are UI-only -- ids/routes/policies stay.
    sect: 'Practice Management',
    items: [
      {
        id: 'tenants',
        label: 'Practices', // UI label: 'Practices' (code id: tenants, route /users/tenants)
        icon: 'users',
        route: '/users/tenants',
        roles: ['itadmin', 'supervisor'],
        requiredPolicy: 'Saas.Tenants',
      },
      {
        // Phase E (2026-06-25): host-side central per-office branding manager.
        id: 'host-branding',
        label: 'Practice Branding', // UI label: 'Practice Branding' (code id: host-branding, route /host/branding)
        icon: 'doc',
        route: '/host/branding',
        roles: ['itadmin', 'supervisor'],
        requiredPolicy: 'CaseEvaluation.Branding',
      },
      {
        // Phase D: host Staff Supervisor creates internal operators (Intake +
        // Supervisor) -- gated by InternalUsers.Create, which it holds.
        id: 'internal-users',
        label: 'Staff Management', // UI label: 'Staff Management' (code id: internal-users, route /users/internal)
        icon: 'users',
        route: '/users/internal',
        roles: ['itadmin', 'supervisor'],
        requiredPolicy: 'CaseEvaluation.InternalUsers.Create',
      },
      {
        // Prompt 16 (2026-06-16): the redesigned Users & Access hub serves IT Admin's
        // invite / pending / internal-users / tenants surfaces via its rail.
        id: 'users-access',
        label: 'Users & Access',
        icon: 'user',
        route: '/users/invite',
        roles: ['itadmin'],
        requiredPolicy: 'CaseEvaluation.UserManagement.InviteExternalUser',
      },
      {
        id: 'intake-assignments',
        label: 'Staff Assignments', // UI label: 'Staff Assignments' (code id: intake-assignments, route /host/intake-assignments)
        icon: 'user',
        route: '/host/intake-assignments',
        roles: ['itadmin', 'supervisor'],
        requiredPolicy: 'CaseEvaluation.IntakeAssignments',
      },
      {
        id: 'my-offices',
        label: 'Assigned Practices', // UI label: 'Assigned Practices' (code id: my-offices, route /host/my-offices)
        icon: 'map',
        route: '/host/my-offices',
        roles: ['intake'],
        requiredPolicy: 'CaseEvaluation.IntakeImpersonation',
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
        route: '/admin/roles',
        roles: ['itadmin'],
        requiredPolicy: 'AbpIdentity.Roles',
      },
      // Notification Templates + System Parameters are TENANT-scoped (they 403 at
      // host); IT Admin manages them by switching into a clinic, where the tenant
      // nav (IN_NAV) surfaces them. Intentionally absent from the host nav.
      {
        id: 'audit',
        label: 'Audit Logs',
        icon: 'clock',
        route: '/admin/audit',
        roles: ['itadmin'],
        requiredPolicy: 'AuditLogging.AuditLogs',
      },
      {
        id: 'file-management',
        label: 'File Management',
        icon: 'folder',
        route: '/file-management',
        roles: ['itadmin'],
        requiredPolicy: 'FileManagement.FileDescriptor',
      },
      {
        id: 'languages',
        label: 'Languages',
        icon: 'globe',
        route: '/language-management',
        roles: ['itadmin'],
        requiredPolicy: 'LanguageManagement.Languages',
      },
    ],
  },
];

/**
 * Keep only the items the user can both SEE (role key) and OPEN (granted
 * permission). The role filter is the coarse first pass; the `isGranted`
 * predicate (ABP PermissionService) is authoritative for any item carrying a
 * `requiredPolicy`, so the nav can never show a link that the route guard
 * would 403. Items without a `requiredPolicy` fall back to role-only. The
 * 'admin' superuser sees every item (and ABP grants it every policy). A null
 * role key -- an external user -- is filtered out upstream; the shell never
 * renders for them. `isGranted` defaults to allow-all so role-only callers
 * (and unit tests) keep their existing semantics.
 */
export function filterNavGroups(
  groups: readonly InternalNavGroup[],
  roleKey: InternalRoleKey,
  isGranted: (policy: string) => boolean = () => true,
): InternalNavGroup[] {
  return groups
    .map((g) => ({
      ...g,
      items: g.items.filter(
        (it) =>
          (roleKey === 'admin' || it.roles.includes(roleKey)) &&
          (!it.requiredPolicy || isGranted(it.requiredPolicy)),
      ),
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
 * IN_NAV naturally. `isGranted` (ABP PermissionService.getGrantedPolicy) gates
 * each item's `requiredPolicy`; it defaults to allow-all for role-only callers.
 */
export function resolveNavGroups(
  roleKey: InternalRoleKey,
  hostScope: boolean,
  isGranted: (policy: string) => boolean = () => true,
): InternalNavGroup[] {
  // Phase D (2026-06-25): Staff Supervisor + Intake Staff are HOST operators now,
  // so at host scope they get the platform nav (office switch + assignments),
  // NOT the tenant nav (which would 403 with no current tenant). Once they switch
  // into an office, hostScope is false and they fall back to IN_NAV (the
  // impersonated admin / shadow Intake user's tenant nav).
  const usesHostNav =
    hostScope &&
    (roleKey === 'itadmin' ||
      roleKey === 'admin' ||
      roleKey === 'supervisor' ||
      roleKey === 'intake');
  return filterNavGroups(usesHostNav ? IN_NAV_HOST : IN_NAV, roleKey, isGranted);
}
