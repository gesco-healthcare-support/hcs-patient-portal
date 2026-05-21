---
feature: task-a-config-driven-email-urls
date: 2026-05-20
status: in-progress
base-branch: main
working-branch: feat/parallel-worktree-stacks
related-issues: []
closes: [BUG-014]
spec: docs/superpowers/specs/2026-05-20-task-a-config-driven-email-urls.md
task-of: parallel-worktree-stacks (A of 6)
---

# Plan: Task A -- config-driven email URLs (BUG-014)

## Goal

Replace the hardcoded `http://falkinstein.localhost:4200` and
`http://falkinstein.localhost:44368` literals in backend email rendering
with a config-overridable env var + a tenant subdomain composer, so emails
sent from any Docker stack (canonical or offset ports) point at that
stack's own URLs.

## Context

Task A is the first of six commits on `feat/parallel-worktree-stacks` (off
`main`). All six tasks ship as ONE pull request at the end -- no per-task
PR. Per-task smoke gates are verification checkpoints inside the branch,
not merge gates.

BUG-014 is one of two technical blockers (the other is BUG-015, fixed in
Task B) for reversing OBS-9 (2026-05-14 decision to abandon parallel
Docker stacks). Without Task A, emails from one worktree's stack hijack
URLs from the other worktree's stack, regardless of port shifting.

Authoritative design context: `docs/superpowers/specs/2026-05-20-task-a-config-driven-email-urls.md`.

## Approach

**Chosen: ABP `Settings:` config-prefix override + tenant subdomain composer
at resolve-site boundary.**

Two layers:

1. **Config override (Layer 1).** `Settings__CaseEvaluation__Notifications__PortalBaseUrl`
   and `Settings__CaseEvaluation__Notifications__AuthServerBaseUrl` env vars
   in the AuthServer + api blocks of `docker-compose.yml`. Values are
   tenant-less (`http://localhost:${NG_PORT:-4200}`). ABP's built-in
   `ConfigurationSettingValueProvider` reads these and overrides the
   `SettingDefinition` literal default at runtime. No C# change to the
   provider class.

2. **Tenant composer (Layer 2).** New static helper
   `TenantUrlComposer.ComposeForTenant(baseUrl, tenantName)` (regex lifted
   byte-for-byte from `angular/src/tenant-bootstrap.ts:99`). Wraps the 7
   resolve sites that read `PortalBaseUrl` / `AuthServerBaseUrl`.
   `ICurrentTenant` is already injected at every site.

**Alternatives rejected:**

- **IConfiguration injection into `SettingDefinitionProvider`** (BUG-014.md's
  recommendation). Same outcome, larger code change (constructor injection
  + reading config at definition time). My approach is smaller and leverages
  ABP's existing fallback chain.
- **DbMigrator seeds per-tenant `Notifications.PortalBaseUrl` rows.** Phase
  1B-friendly but requires re-seed on tenant creation; seeding-coverage
  hazard. Larger architectural footprint.
- **Literal `falkinstein.` prefix in env var (no composer).** Smaller (4
  lines) but doesn't make Task A Phase 1B-ready; would need a follow-up PR
  touching the same files. Adrian chose Phase 1B-ready upfront.

## Tasks

- T1: Add `App__AngularUrl` env var to `docker-compose.yml` (revised 2026-05-20)
  - approach: code
  - files-touched: [docker-compose.yml]
  - acceptance: AuthServer + api blocks each have a new `App__AngularUrl: "http://localhost:${NG_PORT:-4200}"` env var. `AuthServer__Authority` already exists in both blocks (lines 157, 216) and supplies AuthServerBaseUrl's default. `docker compose config` parses without error.
  - **Deviation history:** Initial plan was four `Settings__CaseEvaluation__Notifications__*` env vars. T4 smoke test caught that Docker silently drops env-var names with literal dots, so ABP's flat-key Configuration lookup (which requires those dots) never picks up the override. Reverted, switched to IConfiguration-injection mechanism (new T1b below).

- T1b: Inject IConfiguration into CaseEvaluationSettingDefinitionProvider
  - approach: code
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Domain/Settings/CaseEvaluationSettingDefinitionProvider.cs]
  - acceptance: Constructor accepts `IConfiguration`. `PortalBaseUrl` default reads from `_configuration["App:AngularUrl"]?.TrimEnd('/')` with literal-fallback `"http://falkinstein.localhost:4200"`. `AuthServerBaseUrl` default reads from `_configuration["AuthServer:Authority"]?.TrimEnd('/')` with literal-fallback `"http://falkinstein.localhost:44368"`. `dotnet build` succeeds. **This task was added as a deviation from the initial spec after T4 smoke FAIL.**

- T2: Write `TenantUrlComposer` and its unit tests
  - approach: tdd
  - files-touched:
    - test/HealthcareSupport.CaseEvaluation.Application.Tests/Notifications/TenantUrlComposerTests.cs (new)
    - src/HealthcareSupport.CaseEvaluation.Application/Notifications/TenantUrlComposer.cs (new)
  - acceptance: Test file with 8 xUnit + Shouldly cases (mapping 1:1 to spec section 6 table) is written FIRST and fails to compile (composer doesn't exist yet). Implementing the composer (regex `(^|//)localhost(?=([:/]|$))` per spec section 2) makes all 8 cases green. `dotnet test --filter FullyQualifiedName~TenantUrlComposerTests` reports 8/8 pass.

- T3: Wrap the 7 resolve sites
  - approach: test-after
  - files-touched:
    - src/HealthcareSupport.CaseEvaluation.Application/Emailing/CaseEvaluationAccountEmailer.cs (1 site -- only AuthServerBaseUrl; spec's claim of a PortalBaseUrl resolver here was wrong, no such method exists)
    - src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/DocumentEmailContextResolver.cs (1 site, **inject ICurrentTenant** -- deviation from initial plan, per Option 4 hybrid)
    - src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/BookingSubmissionEmailHandler.cs (2 sites, _currentTenant already injected)
    - src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/AccessorInvitedEmailHandler.cs (1 site, _currentTenant already injected)
    - src/HealthcareSupport.CaseEvaluation.Application/ExternalAccount/ExternalAccountAppService.cs (1 site, **Phase 1A literal "Falkinstein"** -- deviation, anonymous-endpoint tenant resolution deferred to Phase 1B follow-up)
  - acceptance: All 7 wrap sites pass through `TenantUrlComposer.ComposeForTenant`. DocumentEmailContextResolver has `ICurrentTenant` injected (caller-scoped tenant context, verified safe by AccessorInvitedEmailHandler.cs:88 `_currentTenant.Change(eventData.TenantId)` precedent). ExternalAccountAppService hardcodes `"Falkinstein"` as the tenant name parameter with a `// TODO Phase 1B` marker citing this plan and the deferred anonymous-endpoint tenant-resolution work. `dotnet build` succeeds. `dotnet test` for the whole solution: no new failures vs pre-task baseline.

- T4: Smoke test Phase 1 -- env-var override pathway
  - approach: code
  - files-touched: [] (verification, no code change)
  - acceptance: Steps per spec section 6 "Phase 1". Sentinel value `http://test.example.com/sentinel-A` set in `docker-compose.yml` AuthServer block (temporary edit, reverted at end). `docker compose up -d` in main worktree. Trigger Falkinstein-scoped resend-verification flow. `docker compose logs authserver` contains the sentinel string in a rendered email body. **STOP if FAIL.** Restore env var to `http://localhost:${NG_PORT:-4200}` after pass.

- T5: Smoke test Phase 2 -- composer end-to-end
  - approach: code
  - files-touched: [] (verification, no code change)
  - acceptance: With env vars set to real values (`http://localhost:${NG_PORT:-4200}`), restart stack, trigger Falkinstein-scoped resend-verification. Logs show rendered email body containing `http://falkinstein.localhost:4200`. **STOP if FAIL.** If FAIL: composer isn't being called OR `_currentTenant.Name` is null at the resolve site -- investigate before continuing.

- T6: Commit Task A as a single commit (no push, no PR)
  - approach: code
  - files-touched: [] (git only)
  - acceptance: After T1-T5 all pass, ALL 8 Task A files (spec doc + plan doc + 1 new helper + 1 new test + 5 edited resolve sites + 1 edited compose) staged and committed together via `git commit -m "..."` with the message from "Task A commit message" section below. **DO commit.** **DO NOT run `git push`.** **DO NOT run `gh pr create`.** `git log --oneline -1` shows the new commit at HEAD of `feat/parallel-worktree-stacks`. PR opens only after all six tasks + cross-worktree verification.

## Risk / Rollback

**Blast radius:** Email-rendering hot path. Every email-sending code path
invokes one of the 7 resolve sites. A composer bug surfaces immediately
on the next email trigger. No data path (no DB migration, no schema
change, no setting-value seed). Frontend unchanged.

**Rollback:** Trivial. `git restore` the working-tree changes (since
nothing is committed). If somehow committed: revert the single commit.
No data cleanup required. No coordination needed (single PR not opened
yet).

**Smoke-test gate behavior on FAIL:**

- T4 FAIL: ABP's `ConfigurationSettingValueProvider` is not in the chain
  for this stack. STOP. Investigate the registration. Do NOT proceed to T5.
- T5 FAIL: Composer wrapping isn't taking effect, OR `_currentTenant.Name`
  is null at the resolve site. STOP. Re-read the wrap sites. Do NOT
  proceed to other tasks.

## Verification

End-to-end Task A verification (after T1-T5 all pass):

1. `dotnet test` whole solution: green (no regressions, +8 new green tests).
2. `docker compose ps` from main worktree: all 7 services healthy on
   canonical ports.
3. Trigger one email from each of these three flows (synthetic
   Falkinstein-scoped users per the 2026-05-14 memory):
   - Resend verification (`SoftwareThree@gesco.com`): URL in body should
     be `http://falkinstein.localhost:44368/...` (AuthServerBaseUrl path).
   - Patient packet email (any approved appointment): URL in body should
     be `http://falkinstein.localhost:4200/...` (PortalBaseUrl path).
   - Document accepted email (any appointment with an accepted document):
     URL in body should be `http://falkinstein.localhost:4200/...`.
4. All three URLs reflect the composer output (subdomain prefix
   `falkinstein.` automatically added by Layer 2; port from `${NG_PORT}` /
   `${AUTH_PORT}` env vars by Layer 1).

Cross-task verification (after ALL of B-F also complete, before the
single PR):
- Bring up main's stack on canonical ports.
- In `replicate-old-app` worktree: temporary local merge
  `git merge feat/parallel-worktree-stacks` (NOT pushed).
- Apply Task D's per-worktree override block to `replicate-old-app/.env`.
- `docker compose up -d` in `replicate-old-app` worktree.
- Both stacks healthy concurrently. No port collisions. Trigger one email
  flow from each stack. Each email points at its own stack's URLs (no
  cross-stack hijack).
- ONLY THEN: open single PR from `feat/parallel-worktree-stacks` to `main`.

## Task A commit message

After T1-T5 acceptance criteria all pass, commit Task A's deliverables as
ONE commit on `feat/parallel-worktree-stacks`. Do NOT push. Do NOT open PR.
B-F follow as their own one-commit-each entries on the same branch. PR
opens only after all six tasks pass cross-worktree verification.

```
feat(notifications): config-driven email URLs + tenant subdomain composer

- Settings__CaseEvaluation__Notifications__* env vars in docker-compose.yml
  override the SettingDefinition default at runtime via ABP's
  ConfigurationSettingValueProvider (no C# change needed)
- TenantUrlComposer wraps 7 resolve sites; rewrites bare-localhost host
  token to <tenant>.localhost using ICurrentTenant.Name
- 8 xUnit cases in TenantUrlComposerTests cover the regex behavior

Closes BUG-014.
```

The Task A commit is followed by B, C, E, F commits on the same branch
(D produces no commit on this branch -- it's a runbook step against the
gitignored `replicate-old-app/.env` in the sibling worktree). PR opens
only after all six tasks pass + the cross-task verification above.

## Out of scope

- Dead-const cleanup in 4 caller files (separate follow-up after Task F merge).
- Phase 1B multi-tenant cross-tenant validation (Pelton, etc.).
- **ExternalAccountAppService anonymous-endpoint tenant resolution** -- this
  file uses a `"Falkinstein"` literal in the composer call for now (Option 4
  hybrid). Proper fix: resolve tenant from `user.TenantId` after the
  user-by-email lookup, then look up tenant name via `ITenantStore`.
  Deferred because anonymous endpoints have no `ICurrentTenant.Name` and
  the design needs Phase 1B's per-tenant URL story locked first.
- OBS-9 documentation reversal (handled in Task F).
- BUG-015 fix (Task B).
- Compose hygiene for MinIO/Gotenberg ports (Task C).
- `replicate-old-app` retrofit (Task D).
- Worktree role markers (Task E).
