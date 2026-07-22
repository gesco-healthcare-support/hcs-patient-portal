---
feature: Host-scope naming consistency ("Management")
date: 2026-07-22
status: in-progress
base-branch: main
related-issues: []
---

## Goal

Make every host/admin-scope surface (UI chrome + emails + create-result) refer to the host
context consistently as "Management", eliminating "Dr. Appointment Portal", "All offices",
"All tenants", "Platform", and "Platform administration".

## Context & decisions

Research (this session) produced the full surface inventory + live repro; no open decisions
remain.

- Decision: canonical host label is "Management" because it is already the established
  switch-back / crest term, reads naturally as a place, and Adrian selected it over
  "Administration" / "All Practices".
- Decision: gate host labels on `hostScope()`, NOT `platform()`, because `platform()` =
  `hostScope && (itadmin||admin)`, which wrongly excludes a Staff Supervisor (and Intake) at
  host scope -- the exact cause of the "Dr. Appointment Portal" pill.
- Decision: the shared welcome email uses ROLE-ONLY copy (no "all practices" claim) because the
  same template serves Staff Supervisor AND Intake Staff, and Intake is office-scoped -- an
  "access across all practices" line would be false for Intake.
- Decision: `InternalUsersAppService` host-operator label const becomes "Management" (was "All
  offices"); it flows to `InternalUserCreatedDto.TenantName`, which the create-result UI shows.
- Out of scope (do NOT touch): the `officeName` pipe (correct for offices); the invite email's
  "Dr. ##TenantName##" (tenant-scoped, correct); the office-scope dashboard greeting; the office
  switcher mark letter for offices.

## All needed context

Anchors (verified this session):

- Shell template `angular/src/app/shared/components/internal-shell/internal-shell-layout.component.html`
  - :72  breadcrumb root: `{{ platform() ? 'Platform' : 'Home' }}`
  - :89  switcher mark:    `{{ platform() ? 'A' : tenantInitial() }}`
  - :90  switcher pill:    `{{ platform() ? 'All tenants' : (tenantName() | officeName) }}`
  - :5   crest alt "Management" and :125 switch-back exit "Management" -- ALREADY correct, keep.
- Shell component `.../internal-shell-layout.component.ts`
  - :301 `brandSubtitle = computed(() => this.hostScope() ? 'Platform administration' : this.tenantName())`
  - :151 `hostScope` signal, already used in template (`@if (hostScope())` at html:97/134/138).
- Host nav `angular/src/app/shared/components/internal-shell/internal-nav.config.ts:290` -> `sect: 'Platform'`.
  Spec asserting it: `internal-nav.config.spec.ts:54` (`toContain('Platform')`), `:69`, `:81`
  (`not.toContain('Platform')`), and the superuser case ~`:84+` (verify).
- Email subject `src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailSubjects.cs:288-289`
  -> `InternalUserCreated = "Welcome to ##TenantName##"`.
- Email body `.../EmailBodies/InternalUserCreated.html`
  - :6  `<h2 ...>Welcome to ##TenantName##</h2>`
  - :8  `Your account was created for Dr. <strong>##TenantName##</strong>'s Appointment Portal. You have been assigned the <strong>##RoleName##</strong> role.`
  - :23 `>Sign in to ##TenantName##<`
- App service `src/HealthcareSupport.CaseEvaluation.Application/InternalUsers/InternalUsersAppService.cs`
  - :131 `const string tenantName = "All offices";` -> flows to :241 `TenantName = tenantName`
    (`InternalUserCreatedDto`) AND :450 `["TenantName"] = tenantName` (email var; template will
    stop referencing it).
  - :530-539 the list DTO does NOT set `tenantName` (blank for host) -- no change needed there.
- Create-result display: `internal-users-form.component.html:181` (`{{ r.tenantName }}`) and
  `internal-users-hub.component.html:149` (`{{ res.tenantName }}`, both bound to the create DTO).

Gotchas:
- The `InternalUserCreated` email is DB-seeded (NotificationTemplate row). Editing the .html +
  subject const will NOT change an already-seeded row unless reseeded -- to live-verify the
  email, re-run the db-migrator template seeder (or inspect the rendered send), and confirm the
  seeder updates a non-customized row rather than insert-only.
- Windows -> container file-watch does not propagate; restart the `main-angular-1` container
  after Angular edits (and rebuild/restart `main-api-1`/`main-authserver-1` after backend edits).
- Karma on Windows needs `CHROME_BIN` + `--browsers=ChromeHeadless` (see memory).

## Tasks

### Task 1 -- Shell host labels -> "Management" (test-after)
- what: MODIFY `internal-shell-layout.component.html`
  - :72  `{{ hostScope() ? 'Management' : 'Home' }}`
  - :89  `{{ hostScope() ? 'M' : tenantInitial() }}`
  - :90  `{{ hostScope() ? 'Management' : (tenantName() | officeName) }}`
  MODIFY `internal-shell-layout.component.ts:301` subtitle -> `this.hostScope() ? 'Management' : this.tenantName()`
- pattern: mirror the existing `hostScope()` usage already in this template (html:97,134,138).
- acceptance (EARS): WHEN any operator (IT-Admin, Staff Supervisor, or Intake) is at host scope,
  THE SYSTEM SHALL render the switcher pill, breadcrumb root, and brand subtitle as "Management"
  and the switcher mark as "M"; WHILE inside an office, THE SYSTEM SHALL render the office name
  (via the `officeName` pipe) and its initial.

### Task 2 -- Host nav section "Platform" -> "Management" (test-after)
- what: MODIFY `internal-nav.config.ts:290` `sect: 'Platform'` -> `sect: 'Management'`.
  MODIFY `internal-nav.config.spec.ts` every `'Platform'` assertion (:54 toContain, :69/:81
  not.toContain, + superuser case) -> `'Management'`.
- pattern: the sibling groups (`sect: 'Practice Management'`, `'Administration'`) are the style.
- acceptance (EARS): WHEN the host nav is resolved for an IT-Admin or Staff Supervisor at host
  scope, THE SYSTEM SHALL include a section titled "Management" and SHALL NOT include one titled
  "Platform"; the internal-nav spec SHALL pass.

### Task 3 -- Welcome email copy (code)
- what: MODIFY `EmailSubjects.cs:289` -> `"Welcome to the Appointment Portal"`.
  MODIFY `InternalUserCreated.html`:
  - :6  `<h2 ...>Welcome to the Appointment Portal</h2>`
  - :8  `Your account has been created with the <strong>##RoleName##</strong> role.`
  - :23 `>Sign in to the Appointment Portal<`
- pattern: role-only copy; no ##TenantName##, no "Dr.". Mirror the plain-copy style of
  `ResetPassword.html` / `UserRegistered.html` (no tenant token).
- acceptance (EARS): WHEN the `InternalUserCreated` email renders for a host operator, THE
  SYSTEM SHALL NOT contain "Dr.", "All offices", or "##TenantName##"; THE SYSTEM SHALL greet
  "Welcome to the Appointment Portal" and state the ##RoleName## role.

### Task 4 -- Host-operator label const -> "Management" (code)
- what: MODIFY `InternalUsersAppService.cs:131` `const string tenantName = "Management";` and its
  :130 comment; leave :241 (DTO) and :450 (email var) referencing the const unchanged.
- pattern: single source of truth already threads the const to both sinks.
- acceptance (EARS): WHEN an internal operator is created, THE SYSTEM SHALL return
  `InternalUserCreatedDto.TenantName == "Management"`, so the create-result UI
  (`internal-users-form.component.html:181`, `internal-users-hub.component.html:149`) shows
  "Management", never "All offices".

## Validation loop

1. Backend build: `dotnet build HealthcareSupport.CaseEvaluation.slnx` (repo root) -> 0 errors.
2. Backend unit tests touching changed code: `dotnet test test/HealthcareSupport.CaseEvaluation.Application.Tests`
   (InternalUsers) and `.Domain.Tests` (notification templates). Update any assertion pinning
   "All offices" / old email strings. (Integration-host tests may skip on the known ABP license
   blocker -- that is pre-existing, not introduced here.)
3. Frontend lint + build: `cd angular && yarn lint && yarn build` -> clean.
4. Frontend spec: `cd angular && export CHROME_BIN="$(cygpath -w "$(command -v chrome || true)")" ;
   yarn ng test --include='**/internal-nav.config.spec.ts' --watch=false --browsers=ChromeHeadless`
   -> green.
5. Live (Playwright, the done-bar): restart `main-angular-1`; reseed the notification template;
   then, at `http://admin.localhost:4200`:
   - Sign in `stafsuper1@gesco.com` -> pill/breadcrumb/subtitle = "Management"; mark "M".
   - Sign in an IT-Admin -> same "Management" labels; nav section reads "Management".
   - Create an internal user -> result card shows "Management" (not "All offices").
   - Confirm the rendered welcome email (DB template preview or `docker compose logs api |
     grep -i EMAIL-LINKS` send) contains no "Dr."/"All offices".

## Risk / rollback

- Blast radius: LOW. UI copy/labels + one nav section rename (+ its spec) + one email
  template/subject + one backend const. No schema, logic, auth, or PHI change.
- Rollback: `git revert` / drop the `fix/host-scope-naming` branch. The DB template row reverts
  on the next reseed with the old .html (or via the IT-Admin template editor).
