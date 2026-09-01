---
feature: Grant Staff Supervisor the Case Tracker integration permissions
date: 2026-07-31
status: in-progress
base-branch: main
related-issues: []
---

# Staff Supervisor: Case Tracker integration permissions

Phase 1 of the 2026-07-31 reschedule/cancel/calendar/integration epic. Smallest,
independently shippable slice.

## Goal

The host `Staff Supervisor` role holds `Appointments.PushToCaseTracker` and
`Appointments.ViewIntegrationDeadLetters`, so a Supervisor sees the
`/admin/integration-failures` screen in their own navigation and can retry a
dead-lettered Case Tracker push.

## Context & decisions

Why now: the permission constants' own doc comments state both capabilities
"belong with IT Admin / Staff Supervisor"
(`src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs:127,137`),
but the seeder only yields them inside `ItAdminGrants()`. PR #406
(`058fd57f`, in `origin/main`) fixed the IT Admin half TODAY; Staff Supervisor is
the remaining half of the same defect. Verified against `origin/main`: the CT
permissions appear 0 times inside `StaffSupervisorHostGrants()`.

Resolved decisions:

- Decision: grant HOST-side only (`StaffSupervisorHostGrants()`), not
  `StaffSupervisorTenantGrants()`, because the dead-letter screen is host-scoped and
  aggregates every office through `ITenantWorkRunner`
  (`CaseTrackerDeadLetterAppService.GetListAsync`); a tenant-side grant would gate
  nothing that screen reads. Adrian confirmed both IT Admin and Staff Supervisor
  legitimately have all-office access, so cross-office visibility is intended, not a
  leak.
- Decision: fix in the SEEDER rather than by ticking boxes in the role permission
  editor, because a UI grant is per-environment and would be absent from every fresh
  seed and every new deployment. The seeder is the durable, reviewable source of truth.
- Decision: no new permission is defined and no migration is needed, because both
  permission constants and their `AddChild` definitions already exist
  (`CaseEvaluationPermissionDefinitionProvider.cs:35-36`).

DEVIATION found during build (2026-07-31, after tasks 1-3 passed): the original request's
CT1 has TWO halves -- "accessible using UI" AND "IT admin and Staff Supervisor ... access
that page". Tasks 1-3 deliver only the second. Verified live: after the grant, a Staff
Supervisor CAN load `/admin/integration-failures`, but nothing in the UI links there.
`integration-failures` is in the in-page hub rail (`admin-hub.util.ts:79`) yet ABSENT from
the sidebar (`internal-nav.config.ts`) for EVERY role including IT Admin, and bare `/admin`
redirects to `/admin/templates` whose policy a Supervisor lacks -- so they land on "You
don't have access" and can never reach the rail. Tasks 4 and 5 close this.

- Decision: name the screen "Case Tracker Delivery" everywhere (sidebar, hub rail, hub
  subtitle), replacing "Case Tracker Failures", because the screen also turns push on per
  clinic and shows queued counts -- naming it after failures misdescribes it, and a noun
  phrase matches its sidebar siblings (Users & Roles, Audit Logs, File Management).
- Decision: bare `/admin` resolves to the first section the caller may actually see,
  because a fixed redirect to a tenant-scoped section guarantees a 403 landing for any role
  lacking it (today: Staff Supervisor).
- Decision: the "may see" rule is extracted as ONE pure helper reused by both the hub's
  `canSee` and the new redirect, rather than duplicated, so the two can never disagree.

## All needed context

- Insertion point: `StaffSupervisorHostGrants()` at
  `src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs:397`,
  body ends `:429`. Append after the `AbpIdentity.Users.Update` yield at `:428`.
- Pattern to mirror EXACTLY: the same two yields already added to `ItAdminGrants()` at
  `InternalUserRoleDataSeedContributor.cs:326-327`, written as
  `yield return $"{Group}.Appointments.PushToCaseTracker";` and
  `yield return $"{Group}.Appointments.ViewIntegrationDeadLetters";`
  (`Group` is the `"CaseEvaluation"` const at `:50`).
- Grants are applied by `GrantAllAsync`
  (`InternalUserRoleDataSeedContributor.cs:134-140`), which calls
  `_permissionManager.SetAsync(..., isGranted: true)` UNCONDITIONALLY on every seed
  pass. Only `EnsureRoleAsync` (`:122-132`) is create-once-guarded. GOTCHA that makes
  this fix work retroactively: because `GrantAllAsync` is not create-once, the existing
  deployed `Staff Supervisor` role gains the permission on the next seed run - no data
  migration or manual grant needed.
- Host pass runs under `_currentTenant.Change(null)` (`:78`), seeding
  `StaffSupervisorRoleName` with `tenantId: null` (`:83-84`).
- Host-side grantability is already PROVEN empirically: `IT Admin` is a host role
  (seeded `tenantId: null`, `:80-81`) and holds both permissions today with the screen
  working, so no `MultiTenancySides` change is required.
- Test file to extend: `test/HealthcareSupport.CaseEvaluation.Domain.Tests/Identity/InternalUserRoleGrantsTests.cs`.
  Mirror `SupervisorHost_has_operator_powers` (a `[Theory]` with `[InlineData]` rows
  asserting `SupervisorHost.ShouldContain(permission)`); `SupervisorHost` is the
  `HashSet<string>` built at `:26-27`. Pure static test, no DB.
- UI needs NO change: `/admin/integration-failures` is already routed
  (`angular/src/app/app.routes.ts:297-303`) and its admin-hub rail entry is gated by
  policy `CaseEvaluation.Appointments.ViewIntegrationDeadLetters`
  (`angular/src/app/admin/admin-hub.util.ts:78-87`), hidden by `canSee()`
  (`internal-admin-hub.component.ts:318-323`). Granting the permission is sufficient to
  reveal it.
- Worktree is STALE (on `fix/staff-email-host-routing`, behind `origin/main` which now
  contains PRs through #406). Build MUST start by fast-forwarding to `origin/main` and
  branching from there, or the diff will resurrect superseded code.
- No `subst P:` needed: this worktree path (`C:/src/patient-portal/main`) is short
  enough for `Microsoft.Data.SqlClient.SNI.dll`.

## Tasks

### Task 1 - grant the two permissions to the host Supervisor

- what: MODIFY
  `src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs` -
  inside `StaffSupervisorHostGrants()`, after the `AbpIdentity.Users.Update` yield
  (`:428`), append `yield return $"{Group}.Appointments.PushToCaseTracker";` and
  `yield return $"{Group}.Appointments.ViewIntegrationDeadLetters";`, with a short
  comment giving the WHY (Supervisor operates the failures screen; mirrors the IT Admin
  grant added in #406) and NOT restating the code.
- pattern: `InternalUserRoleDataSeedContributor.cs:326-327` (the identical pair in
  `ItAdminGrants()`); comment style as at `:411-412`.
- approach: tdd (security path - a role permission grant; the assertion is written
  first and must fail before the yields are added)
- acceptance (EARS):
  - WHEN `StaffSupervisorHostGrants()` is enumerated, THE SYSTEM SHALL include
    `CaseEvaluation.Appointments.PushToCaseTracker` and
    `CaseEvaluation.Appointments.ViewIntegrationDeadLetters`.
  - WHEN the host seed pass runs against a database whose `Staff Supervisor` role
    already exists, THE SYSTEM SHALL grant both permissions to that existing role
    without creating a duplicate role.
  - THE SYSTEM SHALL NOT add either permission to `StaffSupervisorTenantGrants()`.

### Task 2 - pin the grant in the role matrix test

- what: MODIFY
  `test/HealthcareSupport.CaseEvaluation.Domain.Tests/Identity/InternalUserRoleGrantsTests.cs` -
  add two `[InlineData]` rows to `SupervisorHost_has_operator_powers`:
  `"CaseEvaluation.Appointments.PushToCaseTracker"` and
  `"CaseEvaluation.Appointments.ViewIntegrationDeadLetters"`.
- pattern: the existing `[InlineData]` rows on `SupervisorHost_has_operator_powers`
  (`InternalUserRoleGrantsTests.cs:51-62`).
- approach: tdd
- acceptance (EARS):
  - WHEN `dotnet test` runs the Domain test project, THE SYSTEM SHALL execute a case per
    added permission asserting `SupervisorHost.ShouldContain(permission)` and both SHALL
    pass.
  - If either permission is later removed from `StaffSupervisorHostGrants()`, then the
    suite SHALL fail.

### Task 3 - verify the grant reaches a seeded database

- what: MODIFY nothing. Run the seeder path and confirm the grant row exists for the
  `Staff Supervisor` role, then confirm the rail entry appears for a Supervisor login on
  the local stack at `admin.localhost`.
- pattern: local stack bring-up + seeded-login procedure already used for prior QA in
  this repo; `db-migrator` is the component that executes `IDataSeeder`.
- approach: test-after
- acceptance (EARS):
  - WHEN the seeder has run, THE SYSTEM SHALL have an `AbpPermissionGrants` row with
    `ProviderName = 'R'`, `ProviderKey = 'Staff Supervisor'`, and `Name` equal to each of
    the two permissions.
  - WHEN a user holding only the host `Staff Supervisor` role opens the internal admin
    hub, THE SYSTEM SHALL display the `integration-failures` rail entry and the screen
    SHALL return data rather than 403.

### Task 4 - make the screen reachable from the sidebar, correctly named

- what: MODIFY `angular/src/app/shared/components/internal-shell/internal-nav.config.ts` -
  add an `InternalNavItem` to the HOST `Administration` group (the one at `:368`, whose
  existing items are `identity` / `audit` / `file-management`): `id: 'integration-failures'`,
  `label: 'Case Tracker Delivery'`, `icon: 'alert'`, `route: '/admin/integration-failures'`,
  `roles: ['itadmin', 'supervisor']`,
  `requiredPolicy: 'CaseEvaluation.Appointments.ViewIntegrationDeadLetters'`.
  ALSO MODIFY `angular/src/app/admin/admin-hub.util.ts:81` label -> `'Case Tracker Delivery'`
  and `angular/src/app/admin/internal-admin-hub.component.html:9` subtitle so it describes
  delivery rather than only failures.
- pattern: the sibling `audit` item at `internal-nav.config.ts:387-393` (same group, same
  `requiredPolicy` + `roles` shape). `IconName` must accept `'alert'` -- it is already used
  by `admin-hub.util.ts:80`.
- approach: test-after (UI wiring; no domain logic)
- acceptance (EARS):
  - WHEN a user holding `CaseEvaluation.Appointments.ViewIntegrationDeadLetters` opens the
    internal shell, THE SYSTEM SHALL show a sidebar item labelled "Case Tracker Delivery"
    under Administration linking to `/admin/integration-failures`.
  - WHERE a user lacks that policy, THE SYSTEM SHALL NOT render that sidebar item.
  - THE SYSTEM SHALL NOT display the string "Case Tracker Failures" anywhere in the app.

### Task 5 - bare /admin lands on a section the caller can see

- what: MODIFY `angular/src/app/admin/admin-hub.util.ts` - add pure
  `isAdminSectionVisible(section: AdminSection, isGranted: (policy: string) => boolean,
  isHost: boolean): boolean` implementing "policy granted AND (not tenantScoped OR not
  host)", plus `firstVisibleAdminSection(isGranted, isHost): AdminSection | undefined`
  using it. MODIFY `angular/src/app/admin/internal-admin-hub.component.ts:319-323` so
  `canSee` delegates to `isAdminSectionVisible`. MODIFY `angular/src/app/app.routes.ts:265`
  replacing `{ path: '', redirectTo: 'templates', pathMatch: 'full' }` with a functional
  `redirectTo` that injects `PermissionService` + `ConfigStateService` and returns the first
  visible section's route, falling back to `/dashboard` when none is visible.
- pattern: `canSee` at `internal-admin-hub.component.ts:319-323` is the rule being
  extracted; `isHostScope(config)` from `angular/src/app/shared/auth/internal-user-roles.ts:54`;
  Angular 20 supports a function-valued `redirectTo` evaluated in an injection context.
- approach: tdd (pure helpers, and a wrong answer here sends users to a 403)
- acceptance (EARS):
  - WHEN a caller granted only `ViewIntegrationDeadLetters` navigates to `/admin`, THE
    SYSTEM SHALL redirect to `/admin/integration-failures`.
  - WHEN a caller granted `CaseEvaluation.NotificationTemplates` inside a clinic navigates
    to `/admin`, THE SYSTEM SHALL redirect to `/admin/templates` (unchanged behaviour).
  - While at host scope, THE SYSTEM SHALL NOT redirect `/admin` to a `tenantScoped` section.
  - If no section is visible, then THE SYSTEM SHALL redirect to `/dashboard` rather than
    render a permission error.

## Validation loop

SUPERSEDED by the tasks 4-5 deviation: the diff now touches Angular, so per
`~/.claude/rules/testing.md` the loop MUST add `ng build` AND `ng test` -- a build alone
misses specs that pin an exact section/nav list, which is exactly the kind of spec
`admin-hub.util.spec.ts` contains.

```bash
cd /c/src/patient-portal/main && dotnet format --verify-no-changes
```

```bash
cd /c/src/patient-portal/main && dotnet build -warnaserror
```

```bash
cd /c/src/patient-portal/main && dotnet test test/HealthcareSupport.CaseEvaluation.Domain.Tests --filter "FullyQualifiedName~InternalUserRoleGrantsTests"
```

```bash
cd /c/src/patient-portal/main && dotnet test
```

```bash
cd /c/src/patient-portal/main/angular && npx ng build
```

```bash
cd /c/src/patient-portal/main/angular && export CHROME_BIN="/c/Program Files/Google/Chrome/Application/chrome.exe" && npx ng test --watch=false --browsers=ChromeHeadless --include='**/admin/**/*.spec.ts' --include='**/internal-shell/**/*.spec.ts'
```

## Risk / rollback

Blast radius: one host role gains two permissions. Both are additive: no permission is
removed from any role, no schema changes, no migration, no API contract change, no
Angular change. The widest real consequence is that a Staff Supervisor can now re-send an
appointment's PHI to the Case Tracker (`PushToCaseTracker`) - intended, and the same
capability IT Admin already has. The dead-letter DTO itself carries no PHI
(`CaseTrackerDeadLetterDto.cs:8-11`).

Rollback: revert the commit. Note that reverting the code does NOT revoke an already
granted permission, because `GrantAllAsync` only ever sets `isGranted: true` and never
removes; to fully revoke, untick both permissions for `Staff Supervisor` in the role
permission editor, or delete the two `AbpPermissionGrants` rows for
`ProviderKey = 'Staff Supervisor'`.
