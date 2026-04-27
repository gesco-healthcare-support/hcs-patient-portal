# External-user /home landing page

## Source gap IDs

- UI-16 -- [../../gap-analysis/09-ui-screens.md](../../gap-analysis/09-ui-screens.md) line 154.

Scope-blocker track-09 Q1 at `docs/gap-analysis/09-ui-screens.md:202`: "External-user UX: OLD gives all 4 external roles (Patient/Adjuster/PatAtty/DefAtty) an identical /home-based minimal UI. Is that still MVP, or does NEW need role-distinguished external dashboards?"

## NEW-version code read

- `angular/src/app/app.routes.ts:17-22` -- root route `path: ''` lazy-loads `HomeComponent` with NO guard. UI-16 inventory claim "NEW has /dashboard only; no external-user-optimized home" is already obsolete.
- `angular/src/app/app.routes.ts:23-28` -- `/dashboard` wraps `DashboardComponent` with `[authGuard, permissionGuard]` requiring `CaseEvaluation.Dashboard.Host | .Tenant`. External roles (empty shells per track 05 lines 167-170) get empty admin dashboard if they visit directly.
- `angular/src/app/home/home.component.ts:38-52` -- on init, fetches appointment list if `isPatientUser`; sets `accessorIdentityUserId` filter for attorneys (lines 44-46), else `identityUserId` filter. Matches OLD per-role list behaviour.
- `angular/src/app/home/home.component.ts:54-59` -- `isAttorneyUser` scopes to `'applicant attorney'` + `'defense attorney'`.
- `angular/src/app/home/home.component.ts:65-74` -- `isPatientUser` whitelists three role names: `patient`, `applicant attorney`, `defense attorney`. **Gap: `claim examiner` missing.** `ExternalUserRoleDataSeedContributor.cs:26` seeds `Claim Examiner` (the OLD "Adjuster" rename). A Claim Examiner logging in today sees the anonymous-visitor branch.
- `angular/src/app/home/home.component.html:19-24` -- "Book Re-evaluation" button has no `(click)` binding. Dead button. OLD links to `/appointments/add?type=2`.
- `angular/src/app/home/home.component.html:117` -- "Book Appointment" wires `bookAppointment()` -> `/appointments/add?type=1`. Matches OLD.
- `angular/src/app/route.provider.ts:12-28` -- Home menu entry at `/` (no requiredPolicy); Dashboard at `/dashboard` requires `Dashboard.Host | .Tenant`. ABP sidebar correctly hides Dashboard from externals.

## Live probes

- `curl -sk -o /dev/null -w "%{http_code}" http://localhost:4200/` -> HTTP 200. Root route serves index.html which bootstraps Angular -> renders `HomeComponent`. Probe log: [../probes/external-user-home-2026-04-24T23-35-00.md](../probes/external-user-home-2026-04-24T23-35-00.md).

## OLD-version reference

- OLD landing `/home` for Patient/Adjuster/PatAtty/DefAtty per `docs/gap-analysis/09-ui-screens.md:70-95`. Layout: 3 tiles (Book Appointment, Book Re-evaluation, My Appointments Requests).
- `AccessPermissionService.ts` in OLD collapses 4 external roles into one `ExternalUserModules` list. OLD external UX was deliberately uniform across roles.
- Screenshots: `docs/gap-analysis/screenshots/old/patient/01-home.png`, `/adjuster/01-home.png`, `/patatty/01-home.png`, `/defatty/01-home.png`.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, Angular 20 standalone components. ADR-005 (no ng serve). ADR-004 (doctor-per-tenant). No ADR directly affected.
- 4 external role names seeded (`Patient`, `Claim Examiner`, `Applicant Attorney`, `Defense Attorney`); component recognises 3.
- External roles are empty shells; admin grants `CaseEvaluation.Appointments.*` at runtime.
- HIPAA: "My Appointments Requests" datatable surfaces patient first/last name + appointment status. Row-level IMultiTenant filter + `identityUserId` filter provide access control; not revisited here.

## Research sources consulted

- ABP Angular Route Configuration -- https://abp.io/docs/latest/framework/ui/angular/modifying-the-menu (accessed 2026-04-24)
- ABP Angular Auth Guards -- https://abp.io/docs/latest/framework/ui/angular/authorization (accessed 2026-04-24)
- Angular Router Guards -- https://angular.dev/guide/routing/route-guards (accessed 2026-04-24)
- ABP ConfigStateService -- https://abp.io/docs/latest/framework/ui/angular/config-state-service (accessed 2026-04-24)

## Alternatives considered

A. **Single `/` route with identical 3-tile UI for all 4 external roles.** Chosen. Matches OLD's uniform-external-UX (track-09 Q1 default). Minimal delta: 2-file edit.
B. **Per-role dashboards with distinct tiles.** Rejected -- scope-blocked on Q1; contradicts OLD's uniform pattern; 3-4 new components to maintain. Revisit only if Adrian answers Q1 "role-distinguished".
C. **Drop /home; reuse /dashboard with role-based component selection.** Rejected -- `/dashboard` requires Dashboard.Host/Tenant permission; granting externals those would expose admin KPI cards.
D. **Post-login redirect guard that sends admin to /dashboard and externals to /.** Conditional (nice UX polish). Not MVP-critical; defer.

## Recommended solution for this MVP

Two surgical edits to existing Angular code, no new routes/guards/migrations:

1. **`angular/src/app/home/home.component.ts:72`** -- add `'claim examiner'` to the `externalUserRoles` set so Claim Examiner users see the 3-tile branch. Matches OLD parity.
2. **`angular/src/app/home/home.component.html:22`** -- add `(click)="bookReEvaluation()"` to the Book Re-evaluation button; implement `bookReEvaluation()` by analogy with `bookAppointment()` at lines 117-119: `this.router.navigateByUrl('/appointments/add?type=2')`.
3. **Optional post-MVP polish (defer):** `postLoginRedirect` guard that sends admin to `/dashboard`.

Files touched: `home.component.ts` (+6 lines), `home.component.html` (+1 attribute). No backend changes.

## Why this solution beats the alternatives

- Chesterton's Fence honoured: existing `HomeComponent` works for 3 of 4 external roles; extending costs less than replacing.
- Respects track-09 Q1 default (uniform UX) until Adrian answers otherwise.
- Preserves ADR-005 (no new route config).
- Smallest diff that closes the gap.

## Effort (sanity-check vs inventory estimate)

Inventory: **S** (README scale: 0.5 to 1 day). Confirmed **S** (~0.5 day). Two-file edit plus smoke test against 4 seeded external users.

## Dependencies

- **Blocks:** per-role manual testing of external-facing features (Claim Examiner rendering).
- **Blocked by:** `internal-role-seeds` (external test users must be provisionable before manual validation); `new-sec-04-external-signup-real-defaults` (ExternalSignup fix must land before self-service Patient creation is clean).
- **Blocked by open question:** track-09 Q1 verbatim from `docs/gap-analysis/09-ui-screens.md:202`.

## Risk and rollback

- **Blast radius:** limited to `HomeComponent`. Admin users do NOT pass `isPatientUser`; admin landing on `/` continues to see the anonymous-visitor branch.
- **Rollback:** `git revert` on the 2-line diff. No migration down.

## Open sub-questions surfaced by research

1. Should Claim Examiner see the SAME 3 tiles as Patient/Attorneys, or is the OLD uniformity a legacy artefact? Default: keep uniform; flip on Q1.
2. `home.component.ts:87-89` `displayRoleName` returns `role || 'Patient'` -- dead-code fallback, defer.
3. Admin post-login redirect to `/dashboard` is a UX paper-cut; follow-on ticket.
