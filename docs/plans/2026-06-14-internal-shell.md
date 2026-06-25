---
status: draft
date: 2026-06-14
slug: internal-shell
branch: feat/redesign-internal-shell
parent-branch: feat/internal-user-pages
surface: internal (staff) routes
backend: none for MVP (reuses existing count endpoints)
related:
  - "design_handoff_appointment_portal/Internal Shell - Redesign.html"
  - "design_handoff_appointment_portal/components/in-shell.jsx"
  - "design_handoff_appointment_portal/styles/in-shell.css"
depends-on: "feat/redesign-app-shell (LeptonX layout removed; bare router-outlet at root)"
---

# Plan: Internal Shell (navy sidebar + topbar)

## Goal

Build the redesigned internal/staff shell - a collapsible navy sidebar + topbar - as an
Angular layout component that wraps all internal routes, now that the LeptonX layout is
gone (the app renders a bare router-outlet). It is the dependency root for every internal
lifecycle page (dashboard, appointments, detail, workflow, scheduling, config, people,
users, admin).

## Context / foundation

- The LeptonX layout was removed app-wide (feat/redesign-app-shell): AppComponent renders
  `<router-outlet>`, each page owns its chrome. So the internal shell is a LAYOUT COMPONENT
  that wraps internal routes via a PARENT route with a child `<router-outlet>` - not a
  per-page body class.
- Brand: primary #055495, accent #9dc13b, navy sidebar gradient #07304f -> #053a66, Roboto.
  Tokens already global (`_tokens.scss`).
- Roles: IT Admin + Staff Supervisor are host-scoped (tenant switcher); Intake/Clinic Staff
  is tenant-locked (static tenant chip).
- No `BrandingAppService` yet -> tenant name from `ConfigStateService` currentTenant; logo
  placeholder (`assets/images/header-logo.png`), same gap as external.

## Architecture

- `InternalShellLayoutComponent` (`shared/components/internal-shell/`): renders the navy
  sidebar (brand, grouped nav filtered by role + host scope, collapse toggle, badges) + the
  topbar (collapse button, breadcrumb, tenant chip, notifications bell, New-appointment
  button, account menu) wrapping `<router-outlet>` for the page content.
- Route restructure (`app.routes.ts`): group the internal routes under a parent route that
  uses `InternalShellLayoutComponent` as its component with the internal routes as children.
  External (`/`, public/*), the state screens, and the wildcard 404 stay OUTSIDE the shell.
- Nav config: port `IN_NAV` (tenant: supervisor/intake) + `IN_NAV_HOST` (itadmin) from
  `in-shell.jsx` into a typed config; filter items by the resolved role key + host scope.

## Tasks (one-by-one; per-page sub-branch model; commit each)

### Task 1 - internal-user-roles.ts  [test-after]
Add `shared/auth/internal-user-roles.ts`: the three internal role strings (IT Admin /
Staff Supervisor / Intake (Clinic) Staff), `resolveInternalRoleKey(roles): 'itadmin' |
'supervisor' | 'intake' | null`, and `isHostScope(configState)` (currentTenant null = host).
Mirror external-user-roles.ts. Unit-test the role-key mapping + host-scope.

### Task 2 - nav config + InternalShellLayoutComponent (sidebar)  [test-after]
Define the typed nav model (groups -> items {label, icon, route, roles[], badge?}) for
IN_NAV + IN_NAV_HOST. Build the sidebar: brand header, grouped nav filtered by role key +
host scope, collapse toggle (256 <-> 72px), active-item highlight from the router. Use
`<app-icon>`. Unit-test the role/host filtering.

### Task 3 - InternalNavBadgeService  [test-after]
`shared/services/internal-nav-badge.service.ts`: expose `pendingAppointments` (reuse
`AppointmentPendingCountService`'s count) + `pendingChangeRequests` (from
`DashboardService.get()`) as signals, polled on one interval. The sidebar binds them.

### Task 4 - topbar  [test-after]
Collapse button, breadcrumb (route `data.crumb` + active nav label), tenant chip
(switcher for itadmin/supervisor, static for intake - read currentTenant), notifications
bell (count from the badge service), New-appointment button (hidden for host scope),
account menu (My account -> AuthServer /Account/Manage; Sign out -> performFullLogout).

### Task 5 - route restructure  [code]
In `app.routes.ts`, wrap the internal routes (dashboard, appointment-management/*,
appointments [internal], doctor-management/*, user-management/*, reports, internal-users,
users/invite, change-logs, etc.) under a parent route rendering
`InternalShellLayoutComponent` + child `<router-outlet>`. Keep all existing paths. Guard
the parent with authGuard. External/public/state/404 routes stay outside.

### Task 6 - _in-shell.scss  [code]
Port `in-shell.css` onto the global tokens (navy gradient, sidebar, topbar, collapse,
badges, tooltips). `@use` in styles.scss.

### Task 7 - retire the AppointmentPendingCountService LeptonX patch  [test-after]
The service currently patches the LeptonX route-name string ("Appointments (N)"). With no
LeptonX nav, expose a public `pendingCount` signal instead (consumed by the badge service);
drop the RoutesService patch.

## Nav item -> Angular route (verified against app.routes.ts)

IN_NAV (tenant: supervisor/intake):
- dashboard -> `/dashboard` | appointments -> `/appointments` | change-requests ->
  `/appointments/change-requests` | change-logs -> `/appointment-change-logs` | reports ->
  `/reports`
- availabilities -> `/doctor-management/doctor-availabilities` | locations ->
  `/doctor-management/locations` | wcab -> `/doctor-management/wcab-offices`
- appt-types -> `/appointment-management/appointment-types` | appt-statuses ->
  `/appointment-management/appointment-statuses` | doc-types ->
  `/appointment-management/document-types` | languages ->
  `/appointment-management/appointment-languages` | states -> `/configurations/states`
- patients -> `/user-management/patients` | applicant-attorneys -> `/applicant-attorneys` |
  defense-attorneys -> `/defense-attorneys` | claim-examiners -> `/claim-examiners`
- invite-external (Users & Access) -> `/users/invite` | identity (Users & Roles) ->
  `/identity` | notif-templates -> `/text-template-management` | settings (System
  Parameters) -> `/setting-management` | audit -> `/audit-logs`
- New-appointment button -> `/appointments/add` (legacy add until Prompt 12)

IN_NAV_HOST (itadmin): Overview -> `/dashboard` | tenants -> `/saas` (ABP module:
`/saas/tenants`, `/saas/editions`) | internal-users -> `/internal-users` | identity ->
`/identity` | notif-templates -> `/text-template-management` | settings ->
`/setting-management` | audit -> `/audit-logs`. (Verify the ABP `/saas` + `/identity`
sub-routes render inside the shell's child outlet.)

## Open questions (for Adrian)

1. Tenant-switch flow: host vs in-tenant detection - is `currentTenant == null` sufficient
   for host scope, or is a separate signal needed? (Tenant SWITCHING itself is a later task;
   for now the chip is display + a no-op click for intake.)
2. Single parent route path (e.g. wrap in place) vs a `/internal` prefix - in-place wrap
   avoids URL changes; confirm.
3. Does Staff Supervisor render IN_NAV (tenant nav) or IN_NAV_HOST? Prototype treats
   IN_NAV_HOST as IT Admin only; Supervisor uses IN_NAV but can switch tenants.

## Risks

- ABP lazy admin module routes (identity/saas/audit-logs/etc.) under the shell parent: verify
  they render usably inside the shell's child router-outlet.
- Role-name string mapping (ABP role names -> prototype keys) must stay in sync with the
  backend role seed.
- Mobile sidebar (max-width 860px off-canvas) is incomplete in the prototype - defer.

## Verification

- Per task: Angular unit tests (role/host filtering, badge signals) + build.
- Live (stack on :4250): log in as Staff Supervisor (stafsuper1) + Clinic Staff (clistaff1)
  + IT Admin; verify the sidebar groups/items per role, host vs tenant nav, collapse,
  badges, topbar tenant chip, breadcrumb, account menu, and that internal pages render
  inside the shell.

## Out of scope

- Tenant SWITCHING flow (chip is display + intake-static for now).
- Mobile off-canvas sidebar.
- The internal PAGES themselves (dashboard/list/detail/etc.) - separate per-page branches
  that mount inside this shell.
