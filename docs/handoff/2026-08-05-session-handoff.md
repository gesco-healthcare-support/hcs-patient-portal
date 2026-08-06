# Session handoff -- Patient Portal reschedule epic, phases 4c -> 4d

Written 2026-08-05 to move work to a NEW SESSION ON A DIFFERENT CLAUDE ACCOUNT. The incoming
session has zero memory of the prior conversation; everything it needs is either in this file or
cited from a committed file.

**Disposable.** Delete this file once phase 4d has landed.

---

## 0. READ THIS FIRST -- two traps for a fresh session

1. **The repo's root `CLAUDE.md` describes a DIFFERENT mission.** It is titled
   "CLAUDE.md -- branch `feat/replicate-old-app`" and is about porting the legacy portal at
   `P:\PatientPortalOld` onto the new stack. It is committed on `main` and WILL auto-load. The
   work described in this handoff is the **reschedule/cancel/calendar epic on `main`**, governed by
   `docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md`. Do not let the
   auto-loaded file redirect the work. The `.claude/rules/*.md` files (angular, dotnet, dotnet-env,
   hipaa-data, test-data) DO apply.
2. **The worktree is shared with other sessions.** Another session can switch the branch under
   you and untracked files can vanish. Re-check `git rev-parse --abbrev-ref HEAD` IMMEDIATELY
   before every commit, and commit BY PATHSPEC. Never create a git worktree for this epic.

---

## 1. Where things stand

- **`main` is at `2ce2ef3f`** -- "feat(patient-portal): collect reschedule consent in rounds (#428)".
- **Phase 4c is DONE and merged.** PR #428, squash-merged with `--admin`.
- **Phase 4d is RESEARCHED, decisions resolved, NOT designed and NOT built.** The next command is
  `/feature-design`.

Epic phase table (authoritative copy lives in the tracker):

| #   | Phase                                       | Status                                 |
| --- | ------------------------------------------- | -------------------------------------- |
| 1   | Staff Supervisor CT permissions             | DONE -- PR #409                        |
| 2   | Cancellation reason + billing status to CT  | DONE -- PR #414                        |
| 3   | Staff schedule calendar                     | DONE -- PR #418                        |
| 4a  | Extract reusable availability calendar      | DONE -- PR #420                        |
| 4b  | Staff pick the reschedule date              | DONE -- PR #423                        |
| 4c  | Consent rounds, both sides, after date pick | **DONE -- PR #428 -> `2ce2ef3f`**      |
| 4d  | Reschedule creates a new appointment        | **NEXT -- researched, design pending** |
| 4e  | CT two-case semantics + contract amendment  | TODO (after 4d)                        |
| 5   | No-show round trip (inbound from CT)        | TODO (after 4d)                        |

4a-4e are STRICTLY SEQUENTIAL.

---

## 2. THE MOST URGENT ITEM -- 4b and 4c are built but NOT DEPLOYED

**They must deploy TOGETHER as one server release.** 4b stopped issuing reschedule consent at
submit, and only 4c reissues it after the staff date pick. Neither is deployable alone: shipping
4b without 4c means a supervisor can finalize a reschedule with ZERO consent recorded.

Merging stays per-phase (already done); only the DEPLOY is paired. A deploy needs a fresh
explicit go from Adrian per change -- do not deploy on your own initiative.

---

## 3. What phase 4c shipped (merged, for context only -- do not redo)

Staff flow is now three steps: **PICK** a date (client-side only, no server effect, no emails) ->
**CONFIRM** ("Confirm date & request consent", which opens a consent ROUND and emails both sides)
-> **FINALIZE** (allowed once the round's solicited sides agreed; billing outcome chosen here).

Backend:

- `ChangeRequestConsentRound` entity + `IChangeRequestConsentRoundRepository` + EF mapping in BOTH
  DbContexts + migrations in BOTH sets (`Added_ChangeRequestConsentRounds`).
- `ChangeRequestConsentManager` resolves tokens from TWO stores -- rounds for reschedule, the
  request's flat columns for cancellation. `ChangeRequestConsentMatch.Round` is NULLABLE.
- `RescheduleConsentGate` (reschedule finalize); `OpposingConsentValidator` still serves cancel.
- `ConfirmRescheduleDateAsync`, `ResendConsentRequestAsync`; `ApproveRescheduleAsync` became
  FINALIZE and takes its slot from the consented round.
- `ApproveRescheduleInput` LOST `OverrideSlotId` and `AdminReScheduleReason` (breaking wire change,
  safe only because 4b was unreleased).
- `ChangeRequestConsentExpirySweepJob` -- hourly, per-office, registered in
  `CaseEvaluationHttpApiHostModule`.
- Consent email context tag now carries `/r{round}/a{attempt}`.

Frontend:

- Three-stage approve modal in `internal-change-request-inbox.component.{ts,html}`.
- `cr-approve.util.ts`: `rescheduleStage`, `canConfirmDate`, `canFinalizeReschedule`,
  `formatSlotLabel`, `consentStatusLabel`.
- New `RescheduleRequested` + `CancellationRequested` status pills (amber), honest banners and
  labels, and NO detail-page Reschedule/Cancel while a request is in flight.

Three verified defects fixed, plus a fourth found during review:

1. **The outbox silently swallowed duplicate consent sends** -- `NotificationOutboxManager
.EnqueueAsync` returns the existing row on an idempotency-key match with NO throw and NO log.
   The consent tag carried no round/attempt, so round 2's email and every resend would have
   vanished. Now proven live: six outbox rows across `/r1/a1`, `/r1/a2`, `/r2/a1`.
2. **Two stale readers of the proposed slot** (consent email handler, public consent page) read
   the `NewDoctorAvailabilityId` that 4b leaves null -- a party would have been asked to approve a
   reschedule with no date shown anywhere.
3. **Misleading in-flight status** (pre-existing on `main`) -- `RescheduleRequested` rendered as
   the blue "Rescheduled" pill and the external banner claimed the appointment had been
   rescheduled while nothing had moved.
4. **The change-request SUBMIT email was a THIRD stale reader** -- and the only one already
   reaching real inboxes, sending blank date/time on every external reschedule. Adrian removed
   that email for reschedule entirely; cancellation keeps it (its consent is issued at submit, so
   there is no later message to fold the notice into).

Test counts after 4c: Domain 555, Application 949, EF Core MultiOffice 65, Angular 499.

---

## 4. Phase 4d -- researched, decided, NOT designed

**Read `docs/research/2026-08-05-reschedule-creates-new-appointment.md` in full.** It contains the
context packet, verified anchors and all six resolved decisions.

The headline: **a locked epic decision does not hold.** The tracker says to build the new
appointment by reusing the create pipeline; but `AppointmentCreateDto` carries only 17 scalars +
custom field values, and the child cascade lives in the Angular wizard as six separate POSTs. A
server-side cascade copier has to be written. Resolved: write it, scoped to 4d, with a per-group
audit and ONE TEST PER GROUP.

The six decisions (all resolved by Adrian, 2026-08-05):

1. Child copy -> server-side cascade copier scoped to 4d, one test per group.
2. Chain link -> NEW dedicated `RescheduledFromAppointmentId` column (not `OriginalAppointmentId`).
3. New appointment status -> **Approved**, inherits the old status, no re-approval.
4. Old appointment status -> `RescheduledNoBill` / `RescheduledLate` from the billing outcome.
5. Change request + rounds -> stay on the OLD appointment, but SURFACED on the new one via the
   chain link (read-side join + UI so the new appointment explains itself).
6. Old appointment's packet -> left intact as a historical record.

**The tracker's locked-decision list must be amended** to record that correction as part of 4d.

Next action: `/feature-design` to write `docs/plans/2026-08-XX-reschedule-creates-new-appointment.md`.

---

## 5. Outstanding items and known noise

| Item                                                                         | State                                                                                                                                                                                                                                                                                                                                                                                     |
| ---------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Deploy 4b + 4c together**                                                  | NOT DONE. Needs Adrian's explicit go. See section 2.                                                                                                                                                                                                                                                                                                                                      |
| PR #410 (main -> development cascade)                                        | OPEN. **DO NOT TOUCH.** Also leave #384 and all Dependabot PRs alone.                                                                                                                                                                                                                                                                                                                     |
| Orphaned `ChangeRequestApprovalValidator.ResolveNewSlotAndEnsureAdminReason` | Dead production code after 4c; only its 7 unit tests reference it. Left in place deliberately because 4d reworks the same path. A background task chip was spawned for it.                                                                                                                                                                                                                |
| `EfCoreSampleAppServiceTests.Initial_Data_Should_Contain_Admin_User`         | **Fails LOCALLY, passes in CI.** ABP scaffold test asserting a seeded `admin` identity user. Deterministic locally, touches nothing in the epic diff, and CI on the 4c branch was green. Treat as a local-environment artifact; CI is the arbiter.                                                                                                                                        |
| SonarCloud `new_duplicated_lines_density`                                    | 5.2% vs a 3% threshold -- the ONLY failing gate condition on #428. Reliability / security / maintainability all A, coverage 88.5%. Dominant cause is the dual-DbContext mapping block the repo REQUIRES be duplicated verbatim, plus the two generated migration files. Adrian approved merging past it. Expect 4d to hit the same thing (it adds another dual mapping + dual migration). |
| CodeQL aggregate check                                                       | Reports "1 configuration not found: `security.yml`". That workflow is `schedule` + `workflow_dispatch` only, so it never runs on a PR. `CodeQL: csharp` and `CodeQL: javascript-typescript` both pass. Config noise, not a finding.                                                                                                                                                       |

### Environment state left behind

- **Docker stack was UP** at the end of the session (api, authserver, angular, sql-server, redis,
  minio, packet-renderer). `docker ps` to confirm; `docker compose up -d` to restore.
- **Dev DB (falkinstein):** appointment **A00036** is now `Approved` at Aug 20 13:30 with an
  ACCEPTED reschedule request carrying two consent rounds -- round 1 (Aug 13, superseded, Side B
  `Rejected`) and round 2 (Aug 20, current, both sides `Approved`) -- and six consent outbox rows.
  Deliberately left as a ready-made multi-round audit-trail fixture. Reset it if 4d needs A00036
  clean.

---

## 6. Working rules in force (Adrian's, non-negotiable)

**Process**

- RPE workflow: `/feature-research` -> `/feature-design` -> `/feature-build` -> ship. Scale the
  ceremony to the change; a one-sentence change gets a mini-plan, not a report.
- **Surface EVERY question and decision through the `AskUserQuestion` modal**, never as inline
  prose. This is an explicit instruction from Adrian and applies to all decisions.
- Do not launch subagents or Workflows without stating the scale and getting a yes.
- If the plan turns out to be wrong: STOP, say so, update the plan, resume from the corrected
  task. Do not silently work around it. (This happened four times in 4c -- see the plan file.)

**Git**

- Branch off `main`, fast-forward first. Descriptive branch names, never phase letters.
- **NEVER create a worktree.** Work in `C:/src/patient-portal/main`.
- Commit BY PATHSPEC. Re-check the current branch immediately before every commit.
- Squash-merge. `gh pr merge --squash --admin` on green -- no second reviewer exists.
- **NEVER delete branches.** Never push to `development` (the CI-gated auto-PR owns that cascade).
- Deploys need a fresh explicit go per change.

**Validation loop (must cover every layer the diff touches)**

```
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
```

Migrations, from `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore` -- BOTH contexts:

```
dotnet ef migrations has-pending-model-changes -c CaseEvaluationDbContext
dotnet ef migrations has-pending-model-changes -c CaseEvaluationTenantDbContext
```

Frontend:

```
export CHROME_BIN="/c/Program Files/Google/Chrome/Application/chrome.exe"
npx prettier --check <changed files>
npx eslint <changed files>
npx ng build
npx ng test --watch=false --browsers=ChromeHeadless
```

- **Mutation-check every new test**: deliberately break the code and confirm the intended test
  fails, then revert. A test that has never failed proves nothing.
- HIPAA: synthetic data everywhere. ASCII only. No ADRs -- fold decisions into the plan.

**Tooling gotchas**

- `ng lint` is broken locally -> use `npx eslint`. Run `npx prettier --check` before committing.
- Never `ng serve` / `yarn start` / `ng build --watch` (Vite breaks ABP DI).
- The Angular container serves a STATIC build made at container start. After frontend changes:
  `docker restart main-angular-1`, wait ~70-110s for "Accepting connections".
- `abp generate-proxy` is a DOTNET GLOBAL TOOL (`~/.dotnet/tools/abp`); `npx abp` FAILS. It
  rewrites the whole proxy tree -- keep ONLY the feature's files + `generate-proxy.json` and revert
  the rest. Use `-u http://localhost:44327` (it cannot resolve `admin.api.localhost`).
- Dual-context migrations always come in pairs:
  `dotnet ef migrations add <Name> -c CaseEvaluationDbContext -o Migrations` AND
  `-c CaseEvaluationTenantDbContext -o TenantMigrations`.
- The full backend suite takes ~15 min and can OOM the stack (killed `main-sql-server-1` once).
- SQL against the dev DB -- use a quoted heredoc, and keep the `/opt/...` path OFF the front of
  `bash -c`:

```
cat <<'SQL' | docker compose exec -T sql-server bash -c 'cat > /tmp/q.sql; /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -I -d CaseEvaluation_falkinstein -W -i /tmp/q.sql'
SET NOCOUNT ON;
SELECT ...;
SQL
```

Column names differ from C# properties (`AppAppointments` has `AppointmentDate` +
`AppointmentStatus`); the outbox table is `AppNotificationOutboxItems`.

---

## 7. Live verification -- through the correct door or it proves nothing

**Internal staff are HOST users.** Sign in at `admin.localhost:4200`, land on `/host/my-offices`,
click "Enter practice" to impersonate into the tenant. Only then does the internal shell render.

- internal: `clistaff1@gesco.com` (Intake Staff), `stafsuper1@gesco.com` (Staff Supervisor),
  `it.admin@hcs.test`
- external (tenant DBs, at `falkinstein.localhost:4200`): `patient@falkinstein.test` (Patient),
  `appatty1@gesco.com` (Applicant Attorney)
- All passwords `1q2w3E*r`. Offices: Falkinstein, Hekmat, Longacre, Pelton.

**THE TRAP:** `admin@falkinstein.test` is a TENANT admin, NOT internal staff -- it exercises
neither real path. Using it is how a hard 404 shipped in 4a.

Use Playwright MCP for live UI. A 5s click timeout is usually navigation outrunning the tool, not
a failure -- snapshot and carry on. Screenshots land in the HOME ROOT; move them to
`.github/pr-media/`.

**The live gate is worth its cost.** In 4c it caught two bugs that 499 green specs did not, both
sequencing bugs invisible to unit tests: the modal never advanced after a successful confirm (a
`queueMicrotask` re-pointed it before the reload's HTTP round trip returned), and a round with one
side declined still offered a dead-end "Resend".

---

## 8. Key files for orientation

| Purpose                                              | Path                                                                              |
| ---------------------------------------------------- | --------------------------------------------------------------------------------- |
| Epic tracker (phases, locked decisions, learnings)   | `docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md`            |
| Phase 4c plan (with its four build-time corrections) | `docs/plans/2026-08-05-reschedule-consent-rounds.md`                              |
| Phase 4c research packet                             | `docs/research/2026-08-05-reschedule-consent-rounds.md`                           |
| **Phase 4d research packet (READ THIS)**             | `docs/research/2026-08-05-reschedule-creates-new-appointment.md`                  |
| Change-request feature conventions                   | `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentChangeRequests/CLAUDE.md` |
| Case Tracker contract (4e will rewrite section E2)   | `docs/integration/case-tracker-api-contract.md`                                   |
| Migration guide                                      | `docs/database/MIGRATION-GUIDE.md`                                                |

Stack: .NET 10, ABP Commercial 10.0.2, Angular 20.3.19, SQL Server, EF Core, OpenIddict,
subdomain multi-tenant DATABASE-PER-OFFICE. Ports: AuthServer 44368, API 44327, Angular 4200.
