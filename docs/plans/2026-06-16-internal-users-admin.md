---
feature: internal-users-admin
date: 2026-06-16
status: in-progress
base-branch: feat/internal-user-pages
related-issues: []
source: design_handoff_appointment_portal/PROMPTS.md row 16; BACKEND-CHANGES.md F20-F24
---

## Goal

Recreate the internal-staff Users & Access hub and Admin hub (PROMPTS.md row 16) as
pixel-faithful Angular 20 standalone pages inside the internal shell, building the five
net-new backend endpoints they require, replacing the two legacy form components and the
ABP module admin UIs with custom standalone surfaces.

## Context

- Prompts 9-15 (internal shell, dashboard, appointments, detail, add, workflow,
  scheduling, configuration, people) are merged into `feat/internal-user-pages` @ 6128778.
  Row 16 is the last wholly-unstarted internal page; row 17 (Send back) follows.
- Decisions (AskUserQuestion, 2026-06-16): (1) build missing backend in-branch as atomic
  commits ahead of the frontend (the Prompt 15 B3/B4/B5 pattern); (2) rebuild all admin /
  SaaS surfaces fully custom.
- Delivery: two sequential sub-branches off `feat/internal-user-pages`, each backend-then-
  frontend, each squash-merged on live sign-off:
  - Part A `feat/redesign-internal-users` -> Internal Users hub (Invite, Pending Invites,
    Internal Users, Tenants).
  - Part B `feat/redesign-internal-admin` -> Admin hub (Notification Templates, System
    Parameters, Users & Roles matrix, Audit Logs) + Editions.
- Backend reality (verified live, not assumed): SystemParameter CRUD, NotificationTemplate
  CRUD, stock Saas tenant/edition CRUD, stock permission-management, stock AuditLogging,
  and ABP impersonation ("switch into tenant") all EXIST. Only five things are net-new
  (see Tasks A-B1..A-B4, B-B1..B-B2).
- Continuity that must not break: People hub deep-links `/users/invite` with queryParams
  `{email, userType}` (internal-people.component.ts:241); the redesigned Invite section
  keeps that route and consumes the params. Reuse the proven gating model
  (`PermissionService.getGrantedPolicy` per-action signals + `IN_NAV`/`IN_NAV_HOST`
  role+policy filtering). Custom policies are single-key `CaseEvaluation.*`.
- Fidelity notes: template tokens are `##Var##` (design chips show `{{Var}}` - adapt the
  chip insert + preview to `##Var##`); several System Parameters are stored in DAYS though
  the design labels a few as hours (label honestly to the stored unit).

## Approach

- Chosen: full-stack, two sub-branches, backend-first within each. Matches the
  established per-page squash-merge rhythm and keeps each PR digestible. Custom standalone
  rebuilds of every redesign-nav surface, reusing the shared SCSS systems (`_in-*.scss`,
  `ra-*`, `af-btn`), `IconComponent`, `ToasterService`, and the signals/finalize patterns
  from Prompts 9-15.
- Backend reuses existing aggregates wherever possible: extend `Invitation` /
  `InvitationManager` (already soft-delete-ready) for resend/revoke; extend the invite DTO
  for firmName; wrap Identity for the admin reset email; add one host counts endpoint; add
  send-test + variable-catalog to the existing `NotificationTemplate` app service.
- Permission matrix and audit logs ride entirely on stock ABP Angular proxies
  (`@abp/ng.permission-management`, AuditLogging) - no backend. Tenants ride on stock
  `@volo/abp.ng.saas` TenantService + the new counts endpoint + existing impersonation.
- Rejected: "Backend-first separate PR" (more squash-merges to coordinate; the in-branch
  atomic-commit pattern already gives reviewable history). Rejected: "Keep ABP module
  pages" (fails the pixel-close sign-off). Rejected: rebuilding openiddict /
  file-management / language-management (no prototype, no requirement - YAGNI).

## Resolved decisions (2026-06-16)

1. Editions: NO prototype exists. Adrian is generating one via claude design; build is
   DEFERRED until the prototype lands (task B-F6 parked - NOT in this build).
2. openiddict / file-management / language-management: NO prototype. Adrian may generate
   prototypes via claude design; pending that, they stay as stock ABP routes removed from
   the internal nav (out of this build's scope). My recommendation: only Editions is worth
   redesigning; these three are deep platform tooling the design deliberately omitted.
3. Override badge: ADD a derived `isCustomized` flag (part of B-B2). [CONFIRMED]
4. Manage-invites / reset-password permissions: reuse
   `CaseEvaluation.UserManagement.InviteExternalUser` for invite list/resend/revoke and a
   new `CaseEvaluation.InternalUsers.Edit` child for deactivate + reset. [default - flag if
   Adrian wants granular `.ManageInvites` / `.ResetPassword`]
5. Roles lifecycle: the matrix grants/revokes permissions on existing roles only; role
   create/edit/delete stays in ABP identity (out of scope). [default accepted]

## Tasks

### Part A - Users & Access hub (sub-branch feat/redesign-internal-users)

Backend first (atomic commits), then frontend.

- A-B1: Pending invitations API (#20). Add `ResendAsync`/`RevokeAsync` to
  `InvitationManager` (resend re-issues token + resets 7-day expiry + re-dispatches the
  InviteExternalUser email; revoke soft-deletes + invalidates the token). Add list +
  resend + revoke to `ExternalSignupAppService` (or a new `InvitationsAppService`):
  `GetInvitesAsync(GetInvitesInput)` paged (email, role, invitedBy, sentAt, expiresAt,
  status pending/accepted/expired/revoked), `ResendInviteAsync(id)`, `RevokeInviteAsync(id)`.
  Controller routes `GET /api/app/external-users/invites`, `POST .../{id}/resend`,
  `POST .../{id}/revoke`. Regenerate Angular proxy.
  - approach: test-after
  - files-touched: [src/...Domain/Invitations/InvitationManager.cs, src/...Application/ExternalSignups/ExternalSignupAppService.cs, src/...Application.Contracts/ExternalSignups/IExternalSignupAppService.cs, src/...Application.Contracts/ExternalSignups/dto (GetInvitesInput, InviteListItemDto), src/...HttpApi/Controllers/ExternalSignups/ExternalSignupController.cs, angular/src/app/proxy/external-signups/*]
  - acceptance: as supervisor, GET returns pending invites with correct status; resend on a
    pending row resets expiry and re-sends email; revoke marks it revoked and the old token
    fails validation.

- A-B2: Invite firmName (#21). Add optional `FirmName` to the invite input DTO (attorney
  roles only); persist on the Invitation so registration pre-fills the attorney firm.
  Migration if the entity gains a column. Validate firmName only meaningful for
  ApplicantAttorney/DefenseAttorney. Regenerate proxy.
  - approach: test-after
  - files-touched: [src/...Domain/Invitations/Invitation.cs, src/...EntityFrameworkCore/Migrations/*, src/...Application/ExternalSignups/ExternalSignupAppService.cs, src/...Application.Contracts/ExternalSignups/dto, angular/src/app/proxy/external-signups/*]
  - acceptance: inviting an attorney with a firm name stores it; registering via that token
    pre-fills the firm on the attorney record.

- A-B3: Admin-triggered password-reset email (#22). New app-service method (on the
  internal-users app service or a UserManagement service): `SendPasswordResetEmailAsync(userId)`
  - resolve the IdentityUser, generate a reset token via Identity, dispatch the reset email
  via `NotificationDispatcher`. Permission `CaseEvaluation.InternalUsers.Edit`. Controller
  route `POST /api/app/internal-users/{id}/send-password-reset`. Regenerate proxy.
  - approach: test-after (security path; assert authorization + that it errors on
    nonexistent/inactive users)
  - files-touched: [src/...Application/InternalUsers/InternalUsersAppService.cs, src/...Application.Contracts/InternalUsers/IInternalUsersAppService.cs, src/...Application.Contracts/Permissions/CaseEvaluationPermissions.cs, src/...Application.Contracts/Permissions/CaseEvaluationPermissionDefinitionProvider.cs, src/...HttpApi/Controllers/InternalUsers/*, angular/src/app/proxy/internal-users/*]
  - acceptance: as supervisor, calling reset on a user dispatches one reset email; calling
    on an unknown id returns a clean business error.

- A-B4: Per-tenant counts endpoint (#23). Host endpoint returning per-tenant aggregates
  (userCount, appointmentCount) keyed by tenantId, for the Tenants table. Likely
  `GetTenantStatsAsync()` on a TenantsAppService (host-only) or extend dashboard.
  Permission: `Saas.Tenants` (host). Regenerate proxy.
  - approach: test-after
  - files-touched: [src/...Application/Tenants/* (new TenantStatsAppService or method), src/...Application.Contracts/Tenants/*, src/...HttpApi/Controllers/Tenants/*, angular/src/app/proxy/tenants/*]
  - acceptance: as IT Admin (host), the endpoint returns one row per tenant with non-null
    user + appointment counts.

- A-F1: Users hub shell. New standalone OnPush `InternalUsersHubComponent` + route; cf-rail
  section switcher; role-aware section visibility (itadmin: Invite/Pending/Internal
  Users/Tenants; supervisor: Invite/Pending/Internal Users; intake: Invite/Pending). Pure
  section-visibility resolver util.
  - approach: test-after (component); tdd for the section-visibility resolver util
  - files-touched: [angular/src/app/internal-users-hub/internal-users-hub.component.{ts,html}, angular/src/app/internal-users-hub/users-hub.util.ts (+spec), angular/src/app/app.routes.ts]
  - acceptance: each role sees exactly its allowed sections; nothing visible 403s.

- A-F2: Invite External section. Reactive form (firstName/lastName/email/role +
  conditional firmName for attorney roles), POST invite, result card (invite URL + copy
  link), consume `/users/invite` queryParams `{email, userType}` to prefill. Keep
  `/users/invite` resolving to the hub on the Invite section.
  - approach: test-after; tdd for the userType<->role mapping util
  - files-touched: [angular/src/app/internal-users-hub/sections/invite-section.component.{ts,html}, angular/src/app/internal-users-hub/users-hub.util.ts, angular/src/app/app.routes.ts]
  - acceptance: deep-link from People hub prefills email+role; sending shows the result card
    with a copyable URL.

- A-F3: Pending Invites section. Table (email/role/invitedBy/sent/expires/status/actions),
  expiry chip (ok/soon/gone) + status chip mappers, resend/copy/revoke wired to A-B1, toasts.
  - approach: test-after; tdd for expiry-chip + status-chip pure mappers
  - files-touched: [angular/src/app/internal-users-hub/sections/pending-invites-section.component.{ts,html}, angular/src/app/internal-users-hub/users-hub.util.ts (+spec)]
  - acceptance: chips render per expiry/status; resend/revoke update the row + toast.

- A-F4: Internal Users section. Table (name/email/role/tenant/status/actions), create modal
  (tenant/role/names/email/phone -> POST internal-users), deactivate/reactivate (PUT
  user-extended isActive), send-password-reset (A-B3), per-action gating, result/warn card,
  toasts.
  - approach: test-after; tdd for create-payload + status mapper utils
  - files-touched: [angular/src/app/internal-users-hub/sections/internal-users-section.component.{ts,html}, angular/src/app/internal-users-hub/users-hub.util.ts]
  - acceptance: supervisor creates a user (tenant pre-filled if tenant-scoped), toggles
    active, sends reset; intake never sees this section.

- A-F5: Tenants section (host/itadmin only). Table (tenant/subdomain/edition/users/appts/
  status/actions) via stock `TenantService` + A-B4 counts, new/edit modal, switch-into via
  impersonation (`POST /api/account/impersonate-tenant`), per-action gating.
  - approach: test-after; tdd for subdomain/edition display utils
  - files-touched: [angular/src/app/internal-users-hub/sections/tenants-section.component.{ts,html}, angular/src/app/internal-users-hub/users-hub.util.ts]
  - acceptance: IT Admin lists tenants with counts, creates/edits, switches into a tenant.

- A-F6: SCSS `_in-users.scss` (ux-result, ux-exp, ux-role, ux-ed families from
  in-users.css) registered in styles.scss.
  - approach: code
  - files-touched: [angular/src/styles/_in-users.scss, angular/src/styles.scss]
  - acceptance: sections render pixel-close at 1366/1920/2560.

- A-F7: Retire legacy routing + nav. Point `IN_NAV`/`IN_NAV_HOST` at the hub; keep
  `/users/invite` as a hub entry; remove `invite-external-user` + `internal-users-form`
  from routes (files kept on disk until sign-off); clean dead routes.
  - approach: code
  - files-touched: [angular/src/app/app.routes.ts, angular/src/app/shared/components/internal-shell/internal-nav.config.ts]
  - acceptance: nav opens the hub; old routes gone; no console errors.

### Part B - Admin hub (sub-branch feat/redesign-internal-admin)

- B-B1: Notification-template send-test (#24). `POST /api/app/notification-templates/{id}/send-test`
  - render with sample variables, dispatch to the current user's email via
  `NotificationDispatcher`. Permission `CaseEvaluation.NotificationTemplates.Edit`. Proxy.
  - approach: test-after
  - files-touched: [src/...Application/NotificationTemplates/NotificationTemplatesAppService.cs, src/...Application.Contracts/NotificationTemplates/INotificationTemplatesAppService.cs, src/...HttpApi/Controllers/NotificationTemplates/*, angular/src/app/proxy/notification-templates/*]
  - acceptance: send-test on a template emails the current user one rendered message.

- B-B2: Variable catalog + override flag (#24, open-q 3). `GET /api/app/notification-templates/{code}/variables`
  returning the valid `##Var##` tokens for that code from a new server-side registry
  (`NotificationTemplateVariableCatalog`); add derived `isCustomized` to the template DTO
  (content differs from seeded host default).
  - approach: tdd (the catalog registry + isCustomized derivation are pure logic) + code for
    controller wiring
  - files-touched: [src/...Domain/NotificationTemplates/NotificationTemplateVariableCatalog.cs (+ test), src/...Application/NotificationTemplates/NotificationTemplatesAppService.cs, src/...Application.Contracts/NotificationTemplates/dto, angular/src/app/proxy/notification-templates/*]
  - acceptance: catalog returns the correct token set per code; isCustomized true only when
    edited.

- B-F1: Admin hub shell. Standalone OnPush `InternalAdminHubComponent` + route; cf-rail (4
  sections); role gating supervisor/itadmin.
  - approach: test-after; tdd for section resolver
  - files-touched: [angular/src/app/internal-admin-hub/internal-admin-hub.component.{ts,html}, angular/src/app/internal-admin-hub/admin-hub.util.ts (+spec), angular/src/app/app.routes.ts]
  - acceptance: supervisor + itadmin reach all four sections; intake cannot.

- B-F2: Notification Templates section. Split list/editor, search + type filter, variable
  chips (insert `##Var##`), live preview, send-test (B-B1), save (update), override badge
  (B-B2). Uses `NotificationTemplatesService`.
  - approach: test-after; tdd for token-substitution preview + variable-insert utils
  - files-touched: [angular/src/app/internal-admin-hub/sections/notification-templates-section.component.{ts,html}, angular/src/app/internal-admin-hub/admin-hub.util.ts]
  - acceptance: edit subject/body, insert a variable, preview substitutes it, send-test
    emails, save persists, customized badge shows.

- B-F3: System Parameters section. Grouped editor (booking windows / cancellation /
  deadlines / notifications) over `SystemParametersService.get/update`, units + hints,
  revert/save, Intake read-only (gate save by `.Edit`). Label to the stored unit (days).
  - approach: test-after; tdd for param-group mapping util
  - files-touched: [angular/src/app/internal-admin-hub/sections/system-parameters-section.component.{ts,html}, angular/src/app/internal-admin-hub/admin-hub.util.ts]
  - acceptance: supervisor edits + saves params; intake sees read-only; values round-trip.

- B-F4: Permission Matrix (Users & Roles). Role nav (`IdentityRoleService.getAllList`,
  grouped internal/external) with count badges, matrix (`PermissionsService.get` groups ->
  permissions) of checkboxes, IT-Admin locked (disabled, no save), per-role save
  (`PermissionsService.update`).
  - approach: test-after; tdd for matrix grouping/transform + locked-role logic
  - files-touched: [angular/src/app/internal-admin-hub/sections/permission-matrix-section.component.{ts,html}, angular/src/app/internal-admin-hub/admin-hub.util.ts (+spec)]
  - acceptance: selecting a role shows its grants; toggling + save persists; IT Admin shows
    all-granted disabled with no save.

- B-F5: Audit Logs section. Table (time/user/action/method/status/duration), method + status
  chips, search + method filter, expandable detail row (ip/client/tenant/result), CSV export.
  Uses the stock AuditLogging proxy.
  - approach: test-after; tdd for chip mappers + CSV builder util
  - files-touched: [angular/src/app/internal-admin-hub/sections/audit-logs-section.component.{ts,html}, angular/src/app/internal-admin-hub/admin-hub.util.ts (+spec)]
  - acceptance: logs load + filter; row expands to detail; export produces CSV.

- B-F6: Editions (SaaS nav). DEFERRED - parked pending the claude-design prototype Adrian is
  generating. When the prototype lands, build a custom list/CRUD over `EditionService` to
  match it. Not part of this build; fold in as a follow-up commit/sub-branch.
  - approach: test-after (when un-parked)
  - acceptance: IT Admin lists/creates/edits editions; matches the new prototype.

- B-F7: SCSS `_in-admin.scss` (nt, sp, pm, au families from in-admin.css) registered.
  - approach: code
  - files-touched: [angular/src/styles/_in-admin.scss, angular/src/styles.scss]
  - acceptance: sections render pixel-close at 1366/1920/2560.

- B-F8: Retire ABP module routes from nav (identity, saas, audit-logs,
  text-template-management, setting-management); keep openiddict/file-management/
  language-management as ABP routes (out of scope, open-q 2); clean nav.
  - approach: code
  - files-touched: [angular/src/app/app.routes.ts, angular/src/app/shared/components/internal-shell/internal-nav.config.ts]
  - acceptance: nav opens the custom hubs; no console errors; no LeptonX remnants on the
    redesigned surfaces.

## Risk / Rollback

- Blast radius: Part A retires the invite + internal-users routes and adds 4 backend
  endpoints (one migration for firmName); Part B adds 2 backend endpoints and removes ABP
  admin module pages from nav. Backend changes are additive (no destructive schema). The
  permission matrix writes real ABP grants - guard behind IT-Admin/supervisor and verify
  against a test role before touching seeded roles.
- Rollback: each sub-branch is one squash-merge into feat/internal-user-pages; revert that
  commit to drop a hub. Legacy components stay on disk until live sign-off, so re-pointing a
  route restores the old screen. The firmName migration has a Down.

## Verification

After each sub-branch, run the stack (restart angular + api; re-run db-migrator for the
firmName migration) and drive the per-page sign-off as each role on
http://falkinstein.localhost:4250:
- IT Admin (host, it.admin@hcs.test): Tenants (counts, create, switch-into, edit),
  Editions, Permission Matrix (locked), all admin sections, Audit Logs export.
- Staff Supervisor (stafsuper1@gesco.com): Invite (+firm), Pending (resend/revoke), Internal
  Users (create/deactivate/reset), Notification Templates (edit/send-test/save), System
  Parameters (save), Permission Matrix (edit a non-locked role).
- Intake (clistaff1@gesco.com): only Invite + Pending visible; System Parameters read-only;
  no Internal Users / Tenants / matrix.
Confirm: pixel-close to prototypes, real data, no dead buttons, no visible action 403s,
responsive at 1366/1920/2560, ng build clean, karma green for new utils, old components
de-routed, no console errors. Then squash-merge, push, restart angular.
