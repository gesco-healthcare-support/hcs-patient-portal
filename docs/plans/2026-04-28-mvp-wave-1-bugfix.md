---
feature: w1-bugfix-perf-and-permissions
date: 2026-04-28
status: in-progress
base-branch: feat/mvp-wave-0
related-issues: []
---

# Wave 1 bugfix -- perf, email-async, lookup-scope, healthchecks

## Goal

Make the docker-running app feel as fast as it did pre-W0 / pre-W1, fix the patient/identity-user lookup data-leak across tenants, stop the appointment submit blocking on SMTP, silence the HealthChecks UI poller error spam, and re-scope F3-full + F4-full into Wave 3.

## Context

Adrian smoke-tested the post-W1 docker stack on 2026-04-28 and reported five issues. After server-side log analysis (API responds in 5-200ms per call) and DevTools data Adrian provided (LCP 30.34s, 2.27 MB largest chunk, 13.5 MB total bundle), the diagnoses are:

1. **Slow everything** -- the docker Angular build runs `optimization: false`, so the bundle ships unminified, un-tree-shaken at 13.5 MB total. Parse + execute time on each route nav dominates LCP. Production-class build settings are expected to drop this to ~1-2s LCP.
2. **Submit takes 1.77s** -- `SubmissionEmailHandler` runs synchronously inline via `ILocalEventBus`. ACS SMTP placeholder creds fail auth (`535 Authentication unsuccessful`) after a ~1.7s round-trip. Same will hit Approve / Reject / SendBack via `StatusChangeEmailHandler`.
3. **Patient dropdown shows all-tenants / all-attorneys** -- `GetPatientLookupAsync` queries the patient repository with no scoping. `Patient` does NOT implement `IMultiTenant` per [CLAUDE.md](../../CLAUDE.md), so ABP's automatic tenant filter does not apply.
4. **HealthChecks UI logger spam** -- the in-process poller is configured with a relative path; the library prepends the wildcard listener address (`http://[::]:8080`) which is not a valid target, so every poll fails and writes a 30-line stack trace to logs.
5. **Tenant admin can't open appointments to approve/reject/send-back** + **external users get 403 on the request-confirmation-number link** -- both are auth-model issues; per Adrian's split, the queue-grid Review link + read-only edit-mode gate land in Wave 2, the full role-scope audit + permission redesign land in Wave 3 alongside `appointment-change-requests`.

This plan addresses (1)-(4) directly. (5) is split into the W2 / W3 deferred items.

## Constraints

- Docker-only dev workflow per [feedback memory](C:/Users/RajeevG/.claude/projects/w--patient-portal-main/memory/feedback_docker_only_dev.md). No `dotnet run`, no `ng serve`.
- HIPAA: lookup scoping must not leak cross-tenant patient data. Default to deny, log a deferred-ledger entry for any case unclear at execution time.
- ABP module conventions per [w:\patient-portal\main\CLAUDE.md](../../CLAUDE.md). New jobs go in Domain layer; AppService stays orchestration-only.
- Branch model: cut `fix/w1-bugfix` off `feat/mvp-wave-0`, single squash-merge PR back into `feat/mvp-wave-0`. No `main` push until end of W3.

## Approach

### Build-config flip (T1) -- Option B (production-derived)

Change the docker build config in `angular/angular.json` to inherit production's optimization stack rather than maintaining a parallel near-clone. Override only `fileReplacements` so `environment.docker.ts` keeps routing API/auth URLs to the docker-published ports. Single source of truth for production-class build; `npx ng build --configuration docker` after the flip exercises AOT to reveal any template strictness failures before declaring green.

Rejected: keeping two near-identical configs ("docker" with optimization toggled on alongside "production") creates drift risk; future Angular updates to "production" defaults would not propagate.

### Email send-out-of-band (T2) -- Hangfire fire-and-forget, one-shot

Move the `_emailSender.SendAsync` calls in `SubmissionEmailHandler` and `StatusChangeEmailHandler` behind a Hangfire `IBackgroundJob<SendAppointmentEmailArgs>`. Decorate the job with `[AutomaticRetry(Attempts = 0)]` so failures (placeholder creds) log a warning and stop, no retry queue thrash. Once Adrian provisions real ACS connection-string credentials and decides he wants email-completion to gate the request, two follow-up edits are sufficient: flip `Attempts = 0` -> `Attempts = 10` and replace the handler's `EnqueueAsync` with a direct `await SendAsync` for synchronous behavior.

Rejected: `Task.Run` fire-and-forget is one line shorter but loses Hangfire's logging / dashboard / retry surface that is already in the stack from W0-5. The slight extra ceremony is justified.

### Lookup scoping minimum bar (T3) -- tenant + (booker OR linked-attorney)

Add explicit `Where(x => x.TenantId == CurrentTenant.Id)` to `GetPatientLookupAsync` (Patient is not `IMultiTenant`). Add a private helper `IsApplicantAttorneyAsync()` that checks `ICurrentUser.IsInRole("Applicant Attorney")` (canonical role name verified in [ExternalUserRoleDataSeedContributor.cs:27](../../src/HealthcareSupport.CaseEvaluation.Domain/Identity/ExternalUserRoleDataSeedContributor.cs#L27); no `const` exists -- inline the literal for T3 and log a follow-up to add role-name consts). When true, restrict the patient query to those on appointments where EITHER:

- (a) `Appointment.CreatorId == CurrentUser.Id` (he booked it directly), OR
- (b) an `AppointmentApplicantAttorney` row exists with `UserId == CurrentUser.Id` (the patient/booker selected him as their attorney during their own booking).

Both paths are viable per Adrian's 2026-04-28 clarification. Apply the same shape to `GetIdentityUserLookupAsync`. NOT the comprehensive role-scope helper -- that is the Wave 3 F3-full work. The minimum bar exists to stop the demo data leak without committing to a permission model design that needs cross-role discussion.

Rejected: scoping `IdentityUser` queries by tenant only (without attorney filter) leaves the same data leak Adrian flagged on 2026-04-28. Both endpoints get the same shape.

### HealthChecks UI poller config (T4) -- env var to a real localhost target

One-line YAML: add `App__HealthUiCheckUrl: "http://localhost:8080/health-status"` to the api service environment in `docker-compose.yml`. The poller now calls a real address inside the api container instead of the wildcard listener, so `IPv4 address 0.0.0.0 ...` errors stop and the dashboard at `/health-ui` goes green. See verification below for why Option A (env var) beats Option B (separate UI container) and Option C (drop the library) for this app.

Rejected: dropping the UI library entirely (Option C) is a defensible 5-minute cleanup if Adrian finds he never opens `/health-ui` post-W3; logged as a candidate post-MVP cleanup in the ledger.

### Ledger / docs updates (T5)

Three deferral entries logged in `docs/plans/deferred-from-mvp.md`:

1. **F3-full** (comprehensive role-scope helper across all lookup / list endpoints) -> Wave 3, alongside `appointment-change-requests`.
2. **F4-full** (move class-level `[Authorize(*.Default)]` -> method-level + row-level access predicate) -> Wave 3.
3. **F4-mini** (queue-grid Review link + read-only edit-mode gate on appointment-view) -> Wave 2, alongside `external-user-home`.
4. **B2 retry switch**: when ACS creds land, flip `Attempts = 0` -> `Attempts = 10` and replace `EnqueueAsync` with synchronous `await SendAsync`.
5. **B3 same-firm-attorney design question**: should attorneys at the same firm see each other's patients? Adrian's design call.
6. **B4 dashboard removal candidate** (Option C) -> post-MVP if `/health-ui` proves unused.

Plus an audit pass: read every existing `## From Wave 0` and `## From Wave 1` entry, confirm no orphan deferrals from W0/W1 commits are missing.

`docs/implementation-research/dependencies.md` row counts and effort roll-ups updated to reflect Wave 2 +1 cap, Wave 3 +2 caps. `docs/plans/2026-04-27-mvp-wave-1.md` marked `status: merged` with link to this bugfix plan.

## Tasks

### T1 -- Build-config flip + AOT verification (CRITICAL, demo blocker)

- **id:** T1
- **description:** In `angular/angular.json`, change the `"docker"` build config to derive from `"production"` (set `optimization: true`, `outputHashing: "all"`, `extractLicenses: true`, retain `environment.docker.ts` `fileReplacements`). Bump initial-bundle budget if needed for the docker case (likely identical). Verify with `docker compose build --no-cache angular && docker compose up -d angular` that the AOT build succeeds; fix any template-strictness failures (most common: untyped `let-row` in `ngx-datatable` cells, implicit-any on `*ngFor` over union types, missing imports on standalone components). Smoke-test in Chrome that LCP drops below 3 s.
- **approach:** code (build verification IS the test; no separate unit tests needed for a config flip)
- **files-touched:** `angular/angular.json`, possibly a few `.html` template fixes if AOT flags them
- **acceptance:**
  - `docker compose build angular` succeeds with `optimization: true`
  - Total `dist/CaseEvaluation/browser` size drops from 13.5 MB to <5 MB
  - Largest chunk drops from 2.27 MB to <800 KB
  - Chrome DevTools LCP on `http://localhost:4200/` drops below 3 s on Adrian's machine
  - All previously-working W1 flows still load (login, /appointments queue, book-appointment form, view page)

### T2 -- Hangfire fire-and-forget email job

- **id:** T2
- **description:** Create `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Jobs/SendAppointmentEmailJob.cs` implementing `IBackgroundJob<SendAppointmentEmailArgs>`, decorated with `[AutomaticRetry(Attempts = 0)]`. Move email-send call sites from `SubmissionEmailHandler.SendOfficeEmailAsync` / `SendBookerConfirmationAsync` and `StatusChangeEmailHandler` to enqueue via `IBackgroundJobManager.EnqueueAsync`. Wrap the `_emailSender.SendAsync` call inside the job in try/catch that logs a warning when SMTP creds are missing or auth fails -- DO NOT throw, so Hangfire does not mark the job as failed (which would suppress with `Attempts = 0` anyway, but cleaner to swallow at source). Add `Abp.Mailing.Smtp.UserName` / `Password` placeholder rows to `docker/appsettings.secrets.json` with explicit `REPLACE_WITH_ACS_*` markers and a comment line pointing to the [ACS SMTP docs](https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/send-email-smtp/send-email-smtp).
- **approach:** code (no test; the `Attempts = 0` retry policy + try/catch are simple enough that a unit test would mock everything; covered by manual verification in T7)
- **files-touched:**
  - `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Jobs/SendAppointmentEmailJob.cs` (new)
  - `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Appointments/SendAppointmentEmailArgs.cs` (new -- tiny data carrier)
  - `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Handlers/SubmissionEmailHandler.cs` (replace direct send with enqueue)
  - `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Handlers/StatusChangeEmailHandler.cs` (same)
  - `docker/appsettings.secrets.json` (add SMTP placeholders + comment)
- **acceptance:**
  - `POST /api/app/appointments` returns in <200ms (down from 1.77s)
  - `POST /api/app/appointments/{id}/approve` (and reject / send-back) likewise fast
  - With placeholder ACS creds, the appointment submit succeeds end-to-end and a warning is logged from the Hangfire job (not an exception trace at request level)
  - When real ACS creds are pasted into `appsettings.secrets.json`, emails actually deliver (deferred verification -- happens when Adrian provisions ACS)

### T3 -- Lookup scoping minimum bar

- **id:** T3
- **description:** In `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs`, edit `GetPatientLookupAsync` and `GetIdentityUserLookupAsync` to add `Where(x => x.TenantId == CurrentTenant.Id)` (explicit because `Patient` is not `IMultiTenant`). Add a private helper `Task<bool> IsApplicantAttorneyAsync()` that returns true when `CurrentUser.IsInRole("Applicant Attorney")` -- canonical role name verified in `ExternalUserRoleDataSeedContributor.cs:27` (no const exists; inline the literal). When true, intersect the patient query with the union of: (a) appointments where `Appointment.CreatorId == CurrentUser.Id` (he booked), OR (b) appointments with an `AppointmentApplicantAttorney` row matching `UserId == CurrentUser.Id` (the patient selected him as attorney). Apply the same shape to `GetIdentityUserLookupAsync`. NOT the W3 full role-scope helper.
- **approach:** code (the scoping logic is small; tests for this go into the W3 F3-full wave because the real test is "the helper is reused everywhere")
- **files-touched:**
  - `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs`
- **acceptance:**
  - `pedro.tran@hcs.test` (Applicant Attorney for tenant `Dr Rivera 2`) calls `GET /api/app/appointments/patient-lookup` and sees patients on appointments where EITHER (a) he is the booker (`CreatorId == his user id`) OR (b) he is listed as the applicant attorney via `AppointmentApplicantAttorney`. He does NOT see patients from other tenants or unrelated appointments.
  - Tenant admin `maria.rivera@hcs.test` sees all patients in `Dr Rivera 2` tenant (no attorney filter -- they're admin)
  - No cross-tenant leakage in either case
  - Both endpoints still return in <100ms

### T4 -- HealthChecks UI poller env var

- **id:** T4
- **description:** Add `App__HealthUiCheckUrl: "http://localhost:8080/health-status"` to the `api` service `environment` block in `docker-compose.yml`. Verify after rebuild that the API logs no longer emit `IPv4 address 0.0.0.0 ...` stack traces, and that `http://localhost:44327/health-ui` renders the dashboard in green.
- **approach:** code (config-only)
- **files-touched:** `docker-compose.yml`
- **acceptance:**
  - 5-minute window of API logs after rebuild shows zero `unspecified addresses` errors
  - `http://localhost:44327/health-ui` returns 200 and shows the `CaseEvaluation Health Status` row green

### T5 -- Ledger + plan updates

- **id:** T5
- **description:** Update `docs/plans/deferred-from-mvp.md` with the F3-full / F4-full / F4-mini / B2-retry-switch / B3-same-firm / B4-dropui entries listed in Approach above. Audit pass: read every entry under `## From Wave 0` and `## From Wave 1`; verify no W0/W1 commit (per `git log --oneline feat/mvp-wave-0`) shipped a deferral that's missing from the ledger. Update `docs/implementation-research/dependencies.md` Wave 2 + Wave 3 row counts and effort roll-ups. Mark `docs/plans/2026-04-27-mvp-wave-1.md` `status: merged` with a link to this bugfix plan.
- **approach:** code (docs only)
- **files-touched:**
  - `docs/plans/deferred-from-mvp.md`
  - `docs/implementation-research/dependencies.md`
  - `docs/plans/2026-04-27-mvp-wave-1.md`
- **acceptance:**
  - Six new ledger entries present
  - Dependencies graph row counts updated for W2 (+1) and W3 (+2)
  - W0/W1 audit produces a pass (or new entries added if gaps found)

### T6 -- Verification + smoke

- **id:** T6
- **description:** Full docker rebuild + smoke walk per Adrian's testing pattern.
- **approach:** test-after (manual)
- **files-touched:** none
- **acceptance:**
  1. `docker compose down && docker compose up -d --build` -- all services healthy
  2. `http://localhost:4200/` -- LCP under 3 s on Adrian's machine
  3. Login as `maria.rivera@hcs.test` (tenant admin, Dr Rivera 2) -- nav between dashboards / appointments / locations does NOT require a hard refresh
  4. Login as `pedro.tran@hcs.test` (applicant attorney, Dr Rivera 2) -- patient lookup on book-appointment form shows ONLY his caseload patients
  5. Submit a new appointment -- POST returns in <300 ms; warning logged from Hangfire job about missing ACS creds; appointment row appears in queue
  6. API logs (`docker logs main-api-1 --tail 200`) show zero `unspecified addresses` errors
  7. `http://localhost:44327/health-ui` -> green

## Risk / Rollback

**Blast radius:** small. The build-config flip is the only change with non-trivial risk -- AOT compilation can surface latent template strictness issues. The other four tasks are localized: one new job class + 2 handler edits, two AppService method edits, one YAML line, doc updates.

**Rollback per task:**

- **T1**: revert `angular/angular.json` to the prior config; remove any AOT template fixes if they were intrusive.
- **T2**: revert `SubmissionEmailHandler` / `StatusChangeEmailHandler` to inline `_emailSender.SendAsync`. Delete the new job + args files. Behavior reverts to 1.7s synchronous failure (existing pre-bugfix state).
- **T3**: revert the two AppService method edits. Behavior reverts to "shows all tenants" (existing pre-bugfix state -- HIPAA risk reintroduced, so do not roll back T3 without a forward fix).
- **T4**: remove the env var line from compose; restart api. Behavior reverts to noisy logs.
- **T5**: revert doc edits.

**Pre-existing data**: no migrations, no DB writes. Safe to rollback at any point.

## Verification

End-to-end as per T6. Browser-side LCP measurement is the primary signal that T1 worked. API response time on submit / approve / reject (visible via `docker logs main-api-1`) is the signal for T2. Fresh-login-as-attorney-and-look-at-patient-lookup is the signal for T3. Five minutes of clean logs is the signal for T4.

## Wave 2 / Wave 3 follow-up tasks logged in ledger (NOT executed here)

| Task | Wave | Effort | Why deferred |
|---|---|---|---|
| F4-mini -- queue-grid **Review** link from list to /appointments/view/:id | W2 | XS (~0.5d) | Tenant admin currently can't drill into appointments to use W1-1 Approve/Reject/SendBack. One routerLink + permission gate. |
| F4-mini -- read-only edit-mode gate on appointment-view for external users | W2 | S (~1d) | External users currently 403 on detail link or see fully-editable form. Block edits unless status = AwaitingMoreInfo AND field in `latestSendBackInfo.flaggedFields`. |
| F3-full -- comprehensive role-scope helper applied to ALL lookup / list endpoints | W3 | M (~3-5d) | Pairs with F4-full for coherent auth model. Demo can run on minimum bar from T3. |
| F4-full -- move class-level `[Authorize(*.Default)]` to method-level + row-level access predicate | W3 | M (~2-3d) | External users currently get 403 because `ApplicantAttorneysAppService` and `AppointmentApplicantAttorneysAppService` have class-level Default-permission gates. Fix is part of W3 permission redesign. |

## Estimate

T1: 1-3 hours (most variance from AOT failures requiring template fixes; if no failures, 30 min)
T2: 1-2 hours
T3: 1 hour
T4: 5 minutes
T5: 30-60 minutes (ledger audit pass)
T6: 30 minutes

**Total: 4-7 hours, single-session.**
