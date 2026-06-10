---
feature: reminder-jobs-config-driven-cadence
date: 2026-06-05
status: in-progress
base-branch: feat/replicate-old-app
related-issues: []
---

## Goal

Make the six date-driven appointment-reminder jobs source their cadence from
config (one place, per-tenant tunable) instead of hardcoded arrays/consts, and
settle the two G-05 sub-decisions -- closing Group L (the final Phase 3 parity
slice).

## Context

Group L / records `docs/parity-research/G-05-01/02/03.md`. Research (2026-06-05)
established:

- A config-driven cadence schema already exists but is **completely unwired**:
  `CaseEvaluationSettings.RemindersPolicy` (10 settings) + their definitions in
  `CaseEvaluationSettingDefinitionProvider` (lines 75-84). No job reads any of
  them; every job hardcodes the identical values.
- OLD's reminder cadence is **unrecoverable from source** (external SQL Agent /
  AWS task POSTed to `/api/Scheduler/postscheduler`; `spm.*` procs are stubs).
  So "source the cadence from OLD" is impossible; the de-facto cadence is what
  the NEW jobs currently hardcode.
- Nine Hangfire recurring jobs are registered in
  `CaseEvaluationHttpApiHostModule.ConfigureHangfireRecurringJobs` (lines
  1080-1147), each with a class-const cron. Six are date-driven reminders (in
  scope); the two digests + JDF auto-cancel are not cadence-driven (out of
  scope).

Locked decisions (Adrian, 2026-06-05):
1. **Scope:** all 6 date-driven jobs -> config + shared helper.
2. **Defaults = current values** -> zero cadence behavior change.
3. **`RemindersEnabled` default = false** -> reminders are *muted* after this
   slice until Adrian lifts the gate (intentional behavior change; aligns with
   the Phase-1 email-minimization stance).
4. **G-05-01** (responsible-user owner nudge): **skip/defer** (OLD send was dead
   code; de-scoped from the contract).
5. **G-05-02** (JDF reminder): **Option B** -- distinct recognizable JDF template,
   shared cadence (no second cron, no `!IsJointDeclaration` exclusion).
6. **Cron config-driven (old T5): DROPPED** -- crons stay as host-level class
   consts. A Hangfire recurring job has one schedule (cron cannot vary per
   tenant), and reading it from a setting at startup adds risk for little value.

Constraints: strict-parity mission (but cadence is a choice, see above); PHI --
reminder emails carry confirmation #, due date, doc names only (no SSN/DOB/name
in body); Mapperly/ABP conventions; ASCII; backend-only (no Angular/proxy
churn); SMS stays dropped (G-05-INTENT-01).

## Approach

**Chosen:** lean on the existing `RemindersPolicy` settings group as the single
config home; add a small pure `ReminderCadence` value object (mirroring the
existing `JointDeclarationCutoff` helper convention) that the five exact-day
jobs consume; gate all six on `RemindersEnabled`. Keep
`PackageDocumentReminderJob`'s at-or-past cutoff model (it is semantically
different from exact-day anchors -- forcing it into anchors would change
behavior, violating decision 2).

Two cadence models exist and must not be conflated:
- **Exact-day-match** (5 jobs): fire when a computed integer day-count is in an
  anchor set. `RequestScheduling`/`CancellationReschedule` compute *days
  elapsed* (since creation / last-modification); `AppointmentDay`/
  `DueDateApproaching`/`DueDateDocumentIncomplete` compute *days until*
  (appointment / due date). All reduce to `anchors.Contains(dayCount)` ->
  `ReminderCadence.ShouldFire(int)`.
- **At-or-past cutoff** (`PackageDocumentReminderJob`): fires while within a
  single rolling cutoff window via `JointDeclarationCutoff.IsAtOrPastCutoff`.
  Already reads its window from the `PackageDocumentReminderDays` setting, so it
  is already config-driven for cadence; it only needs the `RemindersEnabled`
  gate.

The substantive per-tenant knobs -- day-anchors + the enabled flag -- are
evaluated at run time inside the per-tenant loop (resolved via
`ISettingProvider`). Cron wake-times stay as the existing host-level class
consts (decision 6); the pre-existing `*Cron` settings in `RemindersPolicy`
remain defined but reserved/unused (a future host-level wiring can consume
them).

**Alternatives rejected:**
- *Migrate `PackageDocumentReminderJob` to the anchor model* -- rejected:
  changes its behavior (at-or-past window -> exact days), violating decision 2.
- *Leave the CCR jobs hardcoded, wire document jobs only* -- rejected by
  decision 1; would leave the existing CCR `RemindersPolicy` settings dead and
  cadence split across two places.
- *Config-driven cron / per-tenant cron* -- rejected (decision 6): a Hangfire
  recurring job has one schedule; cron is inherently host-global, and
  startup settings-resolution is risk for little value.
- *Build G-05-01 owner nudge / split a JDF cron (Option A)* -- rejected by
  decisions 4 and 5.

## Tasks

- **T1: `ReminderCadence` pure value object + tests.**
  - approach: tdd
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Domain/Notifications/ReminderCadence.cs, test/HealthcareSupport.CaseEvaluation.Domain.Tests/Notifications/ReminderCadenceTests.cs]
  - detail: parse a CSV anchor string -> ordered-irrelevant int set (skip blanks/whitespace/dupes; ignore non-int and negative tokens defensively); `ShouldFire(int dayCount)` = membership; null/empty -> never fires (mirror `JointDeclarationCutoff` null-handling). No I/O.
  - acceptance: `"30, 60 ,75"` -> {30,60,75}; `ShouldFire(60)` true, `ShouldFire(61)` false; `""`/null -> `ShouldFire(any)` false; dupes collapse. Tests green.

- **T2: Extend `RemindersPolicy` anchor settings; flip `RemindersEnabled` default to false.**
  - approach: code
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Domain/Settings/CaseEvaluationSettings.cs, src/HealthcareSupport.CaseEvaluation.Domain/Settings/CaseEvaluationSettingDefinitionProvider.cs, src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json]
  - detail: add to `RemindersPolicy`: `DueDateApproachingAnchors` (default `"14,7,3"`) and `DueDateDocumentIncompleteAnchors` (default `"7"`). (CCR anchors already exist; `PackageDocumentReminderJob` keeps its `PackageDocumentReminderDays` scalar.) No new cron settings (T5 dropped). Flip `RemindersEnabled` default `"true"` -> `"false"`. Add `Setting:<name>` + `:Description` en.json keys for the 2 new settings.
  - acceptance: the 2 new anchor settings defined with the listed defaults; `RemindersEnabled` default false; en.json keys present; solution builds.

- **T3: Wire the 5 exact-day jobs to anchors + `RemindersEnabled`.**
  - approach: test-after
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Notifications/Jobs/RequestSchedulingReminderJob.cs, src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Notifications/Jobs/CancellationRescheduleReminderJob.cs, src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Notifications/Jobs/AppointmentDayReminderJob.cs, src/HealthcareSupport.CaseEvaluation.Domain/Notifications/Jobs/DueDateApproachingJob.cs, src/HealthcareSupport.CaseEvaluation.Domain/Notifications/Jobs/DueDateDocumentIncompleteJob.cs, test/HealthcareSupport.CaseEvaluation.Domain.Tests/...]
  - detail: inject `ISettingProvider`; inside the per-tenant `ProcessTenantAsync`, (a) short-circuit (enqueue nothing) when `RemindersEnabled` resolves false; (b) read this job's anchor setting, build a `ReminderCadence`, replace the hardcoded `static readonly int[]`/`const` field with `cadence.ShouldFire(dayCount)`. Delete the hardcoded cadence fields. Each job keeps computing its own day-count (elapsed vs until).
  - acceptance: with `RemindersEnabled=true` + default anchors, each job selects exactly the same appointments it does today (regression-equivalent); with `RemindersEnabled=false`, each enqueues zero. Per-job unit tests (mocked `ISettingProvider` + repo) green.

- **T4: Gate `PackageDocumentReminderJob` on `RemindersEnabled` (keep cutoff model).**
  - approach: test-after
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Domain/Notifications/Jobs/PackageDocumentReminderJob.cs, test/HealthcareSupport.CaseEvaluation.Domain.Tests/...]
  - detail: add the `RemindersEnabled` short-circuit. Leave the `PackageDocumentReminderDays` at-or-past cutoff untouched (semantics differ from anchors; preserves behavior).
  - acceptance: muted when `RemindersEnabled=false`; otherwise byte-identical selection to today. Test green.

- **T5: G-05-02 Option B -- distinct JDF reminder template, shared cadence.**
  - approach: test-after
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Domain.Shared/.../NotificationTemplateConsts.cs, src/.../EmailSubjects (ByCode), src/.../EmailBodies/JointDeclarationUploadReminder.html, src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json, src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/PackageDocumentReminderEmailHandler.cs, test/HealthcareSupport.CaseEvaluation.Application.Tests/...]
  - detail: add `NotificationTemplateConsts.Codes.JointDeclarationUploadReminder` + a subject (ByCode) + a body HTML file + en.json subject key (mirror Group K's authored-body pattern -- bodies are code-seeded). Branch `PackageDocumentReminderEmailHandler` to dispatch the JDF code when `eventData.IsJointDeclaration`, else `UploadPendingDocuments`. No new job, no cron, no `!IsJointDeclaration` exclusion (cadence stays shared, so no duplicate-send risk).
  - acceptance: a JDF-flag row renders the distinct JDF subject/body; a package row renders unchanged; handler unit test asserts the template-code branch on `IsJointDeclaration`. Test green.

- **T6: ADR + parity bookkeeping.**
  - approach: code
  - files-touched: [docs/decisions/013-config-driven-reminder-cadence.md, docs/parity-research/G-05-01.md (status note), docs/parity-research/G-05-02.md (status note), docs/parity-research/G-05-03.md (status note)]
  - detail: ADR 013 (context: OLD cadence unrecoverable; decision: `RemindersPolicy` config home + `ReminderCadence` helper; `RemindersEnabled=false` mutes reminders pending gate-lift; G-05-01 deferred; G-05-02 Option B; crons remain host-level consts and the `*Cron` settings are reserved/unused). Add a short resolution note to each G-05 record (G-05-01 -> deferred; G-05-02 -> Option B shipped; G-05-03 -> config-driven cadence shipped, defaults = current). ADR 012 is taken by Group K (PR #294) on its own branch; this is 013.
  - acceptance: ADR present (Diataxis: explanation/decision); no internal-only paths in the eventual commit body (rationale restated inline per commit rules); Docs Structure Check passes.

## Risk / Rollback

- **Blast radius:** Domain (1 new helper, 6 job edits, settings), Domain.Shared
  (settings consts, template const, en.json), Application (1 handler branch),
  docs. Backend-only; no migration, no DbContext, no Angular/proxy, no host
  module change (T5 dropped). The two digests + JDF auto-cancel are untouched.
- **Intended behavior change:** `RemindersEnabled=false` mutes all six reminder
  jobs after merge (they send unconditionally today). This is the locked
  decision; flip the host setting to re-enable. Surface clearly in the PR.
- **Risks:**
  - *Existing job tests* that assert enqueues will now see zero under the
    default-false gate -- they must set `RemindersEnabled=true` (or assert the
    mute). Handle in T3/T4 (re-read `test/` first).
  - *Over-notification* -- avoided: defaults equal today's values; anchors are
    exact-day; `PackageDocumentReminderJob` cutoff unchanged.
- **Rollback:** revert the branch; no schema/data changes to undo. Re-enabling
  reminders is a single host-setting flip, not a code change.

## Verification

After all tasks (build-time + dev-stack; live email send deferred per Adrian):
1. `dotnet build` green; full unit suite green (helper + per-job + handler).
2. With `RemindersEnabled=true` (test config) each of the 6 jobs selects the
   same appointments as the pre-change hardcoded logic (regression-equivalent
   unit assertions).
3. With `RemindersEnabled=false` (the shipped default) every reminder job
   enqueues zero -- assert in unit tests and confirm at the `/hangfire`
   dashboard (jobs registered, runs no-op).
4. Handler unit test: `IsJointDeclaration=true` -> JDF template code;
   otherwise package code (T5).
5. Override a day-anchor setting in `/setting-management` for the demo tenant
   and confirm (unit/integration) the job's selected day-set changes -- proves
   per-tenant config-driven cadence.
