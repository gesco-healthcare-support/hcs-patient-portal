---
doc: appointment-draft-save-resume
date: 2026-06-19
type: plan
status: draft
base-branch: feat/frontend-rework
backlog-item: 15
session: B
effort: XL
approach: mixed (tdd for entity/security/purge logic + test-after for wizard UI + guard)
review: PENDING -- Adrian reads this before any implementation
---

# #15 Real draft save / resume on the booking wizard

## Goal

Replace the cosmetic "Draft saved" pill and the destroy-wiped `localStorage` autosave
with a real server-persisted draft: autosave survives navigate-away, resumes on return, a
CanDeactivate prompt offers save-or-discard, and a Hangfire job purges stale PHI-bearing
drafts.

## Context (verified against source)

- Wizard parent `AppointmentAddComponent` ([appointment-add.component.ts:519](angular/src/app/appointments/appointment-add.component.ts)) owns a
  55-control reactive form. `AppointmentWizardComponent extends AppointmentAddComponent`
  ([appointment-wizard.component.ts](angular/src/app/appointments/wizard/appointment-wizard.component.ts)) is the redesigned 9-step shell
  (Schedule -> Patient -> Applicant -> Defense -> Insurance -> Examiner -> Claim -> Docs ->
  Review). Submit is two-phase: create the appointment, then upload staged documents.
- **Current draft (the thing we are replacing):** key `'ra-wizard-draft'`
  ([appointment-wizard.component.ts:134](angular/src/app/appointments/wizard/appointment-wizard.component.ts)); `restoreDraft()` on init (:222); a
  `valueChanges.pipe(debounceTime(600))` autosave (:224) writing
  `{ v: form.getRawValue(), step }` (:413); and `localStorage.removeItem(DRAFT_KEY)` in
  `ngOnDestroy` (:257) -- so the draft dies on navigate-away. No CanDeactivate guard exists.
- **No server draft exists today** -- greenfield. No `AppointmentDraft` entity / DTO / service / migration.
- **Background worker pattern already exists** (backlog's "first-of-its-kind" note is
  WRONG): Hangfire is production-configured ([CaseEvaluationHttpApiHostModule.cs:1006](src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs),
  SqlServer storage, 5-retry) and `ConfigureHangfireRecurringJobs()` (:1178) already
  registers ~7 recurring jobs via `RecurringJob.AddOrUpdate<TJob>(TJob.RecurringJobId,
  TJob.CronExpression, ...)`. Each job exposes static `RecurringJobId` + `CronExpression`
  and an `ExecuteAsync`. The cleanup job follows this exact pattern.
- **Auth/PHI:** the wizard create is gated `[Authorize(CaseEvaluationPermissions.Appointments.Create)]`
  ([AppointmentsAppService.cs:712](src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs)); both `/appointments/add` (internal staff) and
  `/appointments/request` (external party) hold it. `Appointment` is
  `FullAuditedAggregateRoot<Guid>, IMultiTenant`. The draft holds PHI (SSN, patient
  name/DOB, addresses, attorney + examiner contacts).
- **Guards:** the app uses functional guards (`CanMatchFn`, e.g. external-user-match.guard.ts).
  No `CanDeactivate` guard exists yet -- this is the first.

## Recommended decisions (for your review -- adjust before I build)

| # | Decision | Rationale |
| --- | --- | --- |
| D1 | Store the form as a JSON payload in ONE `nvarchar(max)` column, not a 55-field mirror table. Plus queryable columns: `TenantId`, creator (FullAudited `CreatorId`), `CurrentStep`, `LastSavedTime`, and a short non-PHI `Label` (e.g. appointment-type name). | Mirrors the existing localStorage shape; avoids a giant Appointment mirror + per-keystroke column churn. KISS/YAGNI. |
| D2 | ONE active draft per (tenant, user) -- upsert on save. | Matches today's single-key localStorage. Multiple named drafts can come later (YAGNI). |
| D3 | Persist to the server at CHECKPOINTS (step Continue + the CanDeactivate "Save & leave"), keep `localStorage` as the instant in-step cache. Resume from server on wizard open. Stop wiping on destroy; delete the server draft only on successful submit. | Avoids streaming PHI on every keystroke; still durable across navigate-away. |
| D4 | Self-scoped `IAppointmentDraftAppService` resolving `CurrentUser.Id` (no target id accepts others), gated `Appointments.Create`. | Mirrors the #9 MyAttorneyProfile self-scoped pattern; structurally prevents reading another user's PHI draft. |
| D5 | PHI hygiene: scope strictly to creator+tenant; do NOT log the payload; suppress/limit auditing of the blob; TTL purge ~30 days since `LastSavedTime` via a Hangfire recurring job. | Drafts are transient PHI; minimize retention + exposure. |
| D6 | Scope this iteration to the `AppointmentWizardComponent` (external `/appointments/request`), where the autosave lives today. Confirm whether internal `/appointments/add` (legacy template) also needs server drafts now. | The autosave currently exists only on the wizard subclass. |

## Open questions (please confirm when reviewing)

1. JSON blob vs normalized entity? (recommend blob -- D1)
2. One active draft per user vs multiple named drafts? (recommend one -- D2)
3. Autosave cadence: checkpoint-only + local cache vs debounced server writes? (recommend checkpoint -- D3)
4. Draft TTL before purge? (recommend 30 days)
5. Internal `/appointments/add` in scope this round, or external wizard only? (recommend wizard only first -- D6)
6. Resume UX: auto-restore vs a "Resume your saved draft? / Start fresh" prompt? (recommend prompt)
7. Is a non-PHI `Label` on the draft acceptable, or keep the resume entirely PHI-free?

## Task breakdown (backend-first; XL)

| # | Task | Approach | Notes |
| --- | --- | --- | --- |
| T1 | `AppointmentDraft` entity (`FullAuditedAggregateRoot<Guid>, IMultiTenant`: `PayloadJson`, `CurrentStep`, `Label`, `LastSavedTime`) + Consts + **migration** (my exclusive lane). | tdd (entity guards) | Tenant + creator scoped. |
| T2 | EF config (both DbContexts if tenant-scoped) + repository `GetByCurrentUserAsync` / upsert. | code | Mirror the doc-types dual-context care. |
| T3 | `IAppointmentDraftAppService` + DTOs + self-scoped `AppointmentDraftAppService` (GetMine / Upsert / Discard), gated `Appointments.Create`, resolves `CurrentUser.Id`. | tdd (security path) | No cross-user read/write. |
| T4 | `DraftCleanupJob` (Hangfire recurring; purge `LastSavedTime < now - TTL`) + register in `ConfigureHangfireRecurringJobs()` with static `RecurringJobId` + `CronExpression`. | tdd (purge query) | Follows the 7 existing jobs' pattern. |
| T5 | Backend build + pathspec commit; request proxy regen (Session A's lane) via Adrian. | code | |
| T6 | Angular draft service: upsert at step Continue, resume-on-open prompt, drop the destroy-wipe, delete on submit success; keep local cache for instant autosave. | test-after | Wire into `AppointmentWizardComponent`. |
| T7 | First `CanDeactivate` functional guard + "Save / Discard / Stay" modal; attach to the wizard route. Must NOT block the submit-success navigation. | test-after | New guard infra. |
| T8 | Verify live: save -> navigate away (prompt) -> resume -> submit clears the draft; purge job verified (unit + manual trigger). | verify-live | Screenshots. |

## Risks + mitigations

- **PHI at rest in a JSON blob (high):** strict creator+tenant scope (self-scoped service),
  no payload logging, limited auditing, TTL purge. Confirm SQL TDE expectation with Adrian.
- **CanDeactivate correctness:** the guard must allow the normal submit navigation and only
  prompt on genuine abandon-with-dirty-form; getting this wrong blocks booking.
- **Wizard is a subclass:** autosave + lifecycle live in `AppointmentWizardComponent`;
  changing `ngOnDestroy`/init order can regress the existing restore.
- **Autosave chattiness / race:** checkpoint persistence + debounced local cache avoids
  per-keystroke server writes; upsert must be idempotent under rapid saves.
- **Migration lane:** single-writer (me), one at a time; clean snapshot gate before adding.

## Coordination (parallel-build protocol)

- Migration is my exclusive lane (`dotnet ef migrations add <Name> --context CaseEvaluationDbContext`).
- Proxy regen is Session A's lane -- commit backend first, then coordinate via Adrian.
- One shared stack -- coordinate db-migrator/api rebuild + angular restart via Adrian.
- Pathspec commits only; back up the dev DB before applying the migration (no destructive
  data step here, but it adds a PHI table).

## Verification (T8 evidence)

- Save a partial booking, navigate away -> CanDeactivate prompt -> Save -> return -> draft
  resumes at the saved step with values intact (screenshot).
- Submit a booking -> the server draft is deleted (no orphan).
- Purge: a draft older than TTL is removed by the job (unit test on the purge predicate +
  a manual `RecurringJob` trigger).
