---
feature: Reschedule / Cancel / Calendar / Case-Tracker epic -- ROADMAP
date: 2026-07-31
status: in-progress
base-branch: main
related-issues: []
---

# Epic roadmap: reschedule redesign, cancel outcomes, staff calendar, CT admin access

LIVING DOCUMENT. This is the phase-by-phase tracker for the 9-item request of
2026-07-31. Each phase gets its OWN plan file and its OWN PR. Update the Phase table
and "Learnings carried forward" after every phase lands, BEFORE designing the next one
-- later phases are expected to change based on what implementation teaches us.

## Working rules for this epic

- Branch off `main` ONLY. Fast-forward local `main` to `origin/main` first, every time.
- NEVER create a new git worktree for this epic, even if another session is writing code
  in the same worktree. Work in `C:/src/patient-portal/main`.
- Because a worktree may be shared with another session, `.git/index` is shared:
  commit BY PATHSPEC (`git commit -F - -- <explicit paths>`), never a bare `git commit`.
- Branch names are DESCRIPTIVE, never phase letters. The phase labels below exist only
  in this doc.
- One plan file per phase in `docs/plans/`, squash-merged to `main` via PR, then cascaded
  to `development` by the CI-gated auto-PR.

## Source request (verbatim intent, condensed)

Reschedule: (R1) calendar picker instead of a slot dropdown, same availabilities and
rules as booking; (R2) staff pick the new date, not the requestor, and BOTH sides then
consent; (R3) old appointment goes to a Rescheduled status and a NEW appointment is
created in the old one's status, slot freed, history linked; (R4) notify Case Tracker
only once the new appointment is approved, carry documents over, regenerate packets with
new dates; (R5) staff select a billing outcome on the rescheduled appointment so Case
Tracker can close or bill it.
Cancel: (C1) on approval, status Cancelled with reason + billing status, pushed to Case
Tracker with identifiers.
Calendar: (CAL1) Google-Calendar-style staff schedule showing booked / requested / empty
slots with patient name and/or appointment number; (CAL2) chips positioned at the real
date+time, clickable through to the appointment view.
Case Tracker: (CT1) the manual-send / dead-letter admin page reachable in the UI for IT
Admin and Staff Supervisor.

## Locked decisions (Adrian, 2026-07-31)

- Decision: reschedule produces TWO Case Tracker cases (old closed/billed, new opened),
  reversing the 2026-07-01 in-place design, because staff need to bill or close the old
  date while tracking the new one separately.
- Decision: the new appointment is created by REUSING the existing create pipeline
  (`CreateRevalAsync`-style `AppointmentCreateDto` path) rather than resurrecting the
  deleted cascade cloner -- with an explicit per-child-group audit + tests, because the
  old cloner caused bug F18 (silently dropped 2 of 8 child groups). Adrian's condition:
  "implement the concrete fixes ... not a patchwork."
- Decision: the requestor gets NO date picker for now (reason only); keep
  `NewDoctorAvailabilityId` and the suggestion wiring intact so a future "suggested date"
  needs no migration.
- Decision: a declined/expired consent creates a NEW consent ROUND per staff-proposed
  date (needs a round/supersedes link + migration), because Adrian wants the full audit
  trail of every date proposed and who declined it.
- Decision: uploaded documents carry over by COPYING rows that SHARE the same MinIO
  object key, because the rescheduled appointment must keep its own history; consequence
  is delete becomes soft-delete-only for shared blobs, which already matches the
  retention guarantee given to Case Tracker.
- Decision: packets are REGENERATED for the new appointment, never carried over,
  because the packet unique index is `(TenantId, AppointmentId, Kind)` and packet content
  embeds the appointment date.
- Decision: build the calendar on FullCalendar MIT plugins (`@fullcalendar/angular`
  7.0.2, `@angular/core` peer `16 - 22`, timeGrid + dayGrid), NOT the paid resource /
  timeline views. Adrian's caveat: if it does not do what he wants, we replace it and
  hand-build the whole thing -- so the backend endpoint stays VIEW-AGNOSTIC and a swap
  must remain frontend-only.
- Decision: Case Tracker coordination is NOT a build gate. We build and test our side;
  Levon already knows the design change; after implementation + testing we send him a
  detailed written change summary so he can adjust his receiver.
- Decision: Staff Supervisor gets the CT permissions HOST-side only, because the
  dead-letter screen is host-scoped and aggregates all offices; both IT Admin and Staff
  Supervisor legitimately have all-office access.
- Decision: separate plan + PR per phase, not one epic PR.

## Phases

| #   | Phase                                       | Branch                                    | Plan                                                               | Status                                |
| --- | ------------------------------------------- | ----------------------------------------- | ------------------------------------------------------------------ | ------------------------------------- |
| 1   | Staff Supervisor CT permissions             | `fix/supervisor-case-tracker-permissions` | `2026-07-31-staff-supervisor-case-tracker-permissions.md`          | **DONE** - PR #409 -> main `7b0d9c30` |
| 2   | Cancellation reason + billing status to CT  | `feat/cancel-reason-to-case-tracker`      | `2026-07-31-cancellation-reason-billing-status-to-case-tracker.md` | **DONE** - PR #414 -> main `baa1fee6` |
| 3   | Staff schedule calendar (FullCalendar)      | `feat/staff-schedule-calendar`            | `2026-07-31-staff-schedule-calendar.md`                            | **DONE** - PR #418 -> main `1784a6bb` |
| 4a  | Extract reusable availability calendar      | `refactor/extract-availability-calendar`  | not written                                                        | TODO (prereq for 4b)                  |
| 4b  | Staff pick the reschedule date              | `feat/staff-picks-reschedule-date`        | not written                                                        | TODO (after 4a)                       |
| 4c  | Consent rounds, both sides, after date pick | `feat/reschedule-consent-rounds`          | not written                                                        | TODO (after 4b)                       |
| 4d  | Reschedule creates a new appointment        | `feat/reschedule-creates-new-appointment` | not written                                                        | TODO (after 4c)                       |
| 4e  | CT two-case semantics + contract amendment  | `feat/case-tracker-two-case-reschedule`   | not written                                                        | TODO (after 4d)                       |
| 5   | No-show round trip (INBOUND from CT)        | `feat/no-show-round-trip`                 | not written                                                        | TODO (after 4d)                       |

Phase 5 ADDED 2026-07-31 (Adrian, while resolving phase 2's NoShow question). It is NOT a phase-2
rider: it needs an INBOUND Case Tracker -> portal endpoint, which does not exist today (the
integration is push-only plus a reconcile GET). Flow: CT staff mark a no-show -> CT calls a NEW
portal endpoint with the appointment info -> the portal sets the appointment to `NoShow` and ALERTS
staff -> staff use the appointment number to create a replacement appointment that is PRE-APPROVED
-> external parties may then cancel/reschedule it by the normal routes -> the replacement is pushed
to CT so it starts tracking. Sequenced after 4d because "create a pre-approved replacement" reuses
4d's create-new-appointment machinery.

Open questions to settle before planning phase 5:

- Auth: reuse the static `X-Integration-Token` that already guards the reconcile GET (contract §F),
  or mint something separate?
- Office resolution: database-per-office means the request must carry `{tenantId}` as a PATH
  segment, exactly as §F documents for the reconcile GET -- a header will not do, since the portal
  blocks tenant headers globally.
- Idempotency: what happens when CT retries the same no-show?
- MUST VERIFY FIRST: whether `Approved -> NoShow` is even a legal transition. `NoShow` is in
  `AppointmentStatusType` but the contract records it as having NO API surface today, so the state
  machine (`AppointmentManager.BuildMachine`) has to be read before any of this can be planned.

Phases 1, 2 and 3 are mutually independent. 4a-4e are strictly sequential.

## Cross-phase research findings (verified 2026-07-31)

Facts later phases depend on. Re-verify before asserting -- code moves.

- The Case Tracker contract at `docs/integration/case-tracker-api-contract.md` is marked
  FINAL and BUILT and documents the OPPOSITE of decision 1: section E2 (`:395`) says the
  portal "moves the SAME appointment in place rather than cloning a row ... it never
  creates a second one", and `:403` warns the receiver that a reschedule is signalled by
  a changed date, NOT a status change. Phase 4e must rewrite E2, the section A status
  table, section H timing, and Coordination decisions 4 and 6.
- `Appointment.OriginalAppointmentId` (`Appointment.cs:146`) already exists and is
  already used to link a re-eval to its source (`AppointmentsAppService.cs:879-891`).
  Reschedule chaining can reuse it, but `EvaluationKind` is persisted SEPARATELY precisely
  so a dual-purpose link cannot mislabel a case -- keep the two meanings disambiguated.
- `(TenantId, RequestConfirmationNumber)` is a hard unique index
  (`CaseEvaluationDbContext.cs:574-576`, tenant context `:523-524`): a new appointment
  MUST get its own confirmation number.
- Consent is already two-sided on the change-request row (`SideA*` / `SideB*`, 5 columns
  each, migration `20260701195010_TwoSidedChangeRequestConsent`). The requestor's side is
  currently AUTO-GRANTED (`AutoGrantSide`, `AppointmentChangeRequestsAppService.cs:244`) --
  that is the line Phase 4c changes. The staff-initiated branch (`:257-287`) already asks
  BOTH sides and is the path to reuse.
- Phase 4c blocker: `IssueSideConsent` (`AppointmentChangeRequest.cs:192-199`) is only
  valid from `NotRequired`, so a side already Approved/Rejected cannot be re-tokened
  without a reset or a new round row.
- Phase 4c ordering inversion: consent is checked BEFORE the slot is resolved
  (`AppointmentChangeRequestsAppService.Approval.cs:204-205` vs `:207`). The new flow needs
  slot-first, then consent.
- `RegeneratePacketAsync` (`AppointmentDocumentsAppService.cs:836-851`) already exists and
  enqueues all three kinds -- reuse it in 4d, do not write a new generator.
- Packet-set completion (the gate a CT notify must wait on) is
  `PacketSetPolicy.IsComplete` via `PacketsCompleteHandler` -- reuse, do not reinvent.
- `AppointmentDocument.AppointmentId` is indexed but NOT unique
  (`CaseEvaluationDbContext.cs:609-612`), so copying document rows is cheap; the packet
  unique index `(TenantId, AppointmentId, Kind)` filtered on `IsDeleted=0`
  (`:833`) is why packets must be regenerated instead.
- The booking calendar is NOT reusable as-is: `AppointmentAddScheduleComponent` is a
  template shell, while the real logic (`availableDateKeys`,
  `markAppointmentDateDisabled`, `loadAvailableDatesBySelection`, the 60/90-day horizon)
  lives on the 3764-line `AppointmentAddComponent`. That extraction is Phase 4a.
- The reschedule modal today lacks the 60/90-day horizon rules, so "same rules as
  booking" requires porting them, not just swapping the widget.
- `ChangeRequestApproveModalComponent` has NO slot picker at all -- Phase 4b adds one.
- No calendar library exists in `angular/package.json`; Phase 3 adds the first one.
- No endpoint joins slots to appointments. `getSlotPatientNames` returns names only (no
  id, no status), so it cannot power a clickable chip -- Phase 3 needs a new joined
  endpoint returning per-slot occupancy plus appointment id / confirmation number /
  status.
- Permission grants are applied by `GrantAllAsync`
  (`InternalUserRoleDataSeedContributor.cs:134-140`) UNCONDITIONALLY on every seed pass;
  only `EnsureRoleAsync` is create-once. So a seeder grant reaches ALREADY-DEPLOYED roles
  on the next `db-migrator` run.

## Pre-existing bugs found during research

Not part of the original request. Folded into the phase that already touches the code
rather than expanding scope; Adrian was told and did not exclude them.

- `BookingStatus.Booked` is never set by ANY code path -- only `Available` / `Reserved`
  are ever assigned, so the existing week-grid's "Booked" colour is only reachable by a
  manual admin edit and is not ground truth. -> Phase 3 (which needs real occupancy
  counts anyway).
- Packets go STALE after an in-place reschedule: packet content embeds the appointment
  date (`PacketTokenResolver.cs:222-233`) but nothing calls regeneration from the
  reschedule approval path. -> Fixed by Phase 4d. Affects production until then.
- Consent can hang forever: there is NO expiry job, so a side stays `Pending`
  indefinitely and blocks finalize, and the consent email's "referred to our clinic
  staff" promise has no implementation. -> Phase 4c.

## Learnings carried forward

Append after each phase.

### Process

- (2026-07-31, pre-build) The worktree was stale on a merged branch while `origin/main`
  had advanced through PR #406. Always fast-forward `main` before designing OR building a
  phase; a plan written against a stale tree can cite superseded anchors.
- (2026-07-31, pre-build) Phase 1 shrank mid-design: PR #406 (`058fd57f`) had already
  granted IT Admin the same two permissions hours earlier, leaving only the Staff
  Supervisor half. Re-check `origin/main` at the START of each phase's design -- other
  sessions are shipping into this repo. Main moved FOUR times during 2026-07-31 alone
  (#406, #408, #411-#413, then this epic's own merges).
- (P1) SHARED-WORKTREE BRANCH HAZARD, cost a recovery: another session switched the
  worktree's branch mid-build, so the commit landed on THEIR branch. Committing by pathspec
  protects their FILES but CANNOT protect against the branch ref moving. Re-check
  `git rev-parse --abbrev-ref HEAD` IMMEDIATELY before every commit. Recovery that rewrote
  nothing pushed: switch to the intended branch, `git cherry-pick <sha>`, then
  `git branch -f <their-branch> origin/<their-branch>`.
- (P1) SCOPE LESSON: the CT1 ask contained TWO requirements -- "accessible using UI" AND
  "IT admin and Staff Supervisor can access". The plan covered only the second, and the
  missing sidebar entry surfaced during live QA. Decompose each requested SENTENCE into its
  separate verifiable claims BEFORE writing the plan.
- (P2) Do not mark an app-service task `tdd` without first checking a harness exists. The
  change-request area has ZERO integration coverage and no test-data seeder, and
  `AppointmentChangeRequestsAppService` takes 10 ctor dependencies (five CONCRETE classes)
  plus 13 uses of ABP ambient members -- so it is neither unit- nor integration-testable
  today. Phase 4c NEEDS that harness; it is tracked separately.
- (P2) When a plan says "reuse that exact string", READ THE CONSUMERS FIRST.
  `"JDF-not-uploaded"` turned out to be a routing DISCRIMINATOR that
  `JdfAutoCancelledEmailHandler` filters on, and the column it would have been persisted
  into is rendered to patients. Two constants were needed, not one.

- (P3) **A PURE MAPPER TESTED ONLY AGAINST ITSELF PROVES NOTHING ABOUT THE CONTRACT.** The single
  most useful lesson of the epic so far. 432 frontend + 1831 backend tests were green while EVERY
  calendar band and chip rendered with no occupancy colour, because FullCalendar v7 renamed the event
  class property (v6 `classNames: string[]` -> v7 `class`/`className` as a STRING) and the v6 shape
  still TYPE-CHECKS: `EventInput` tolerates unknown keys on account of `extendedProps`, so the
  library silently ignored it. The unit tests asserted the object shape WE invented, not the shape
  the library consumes. Where a mapper feeds a third-party component, at least one check must
  exercise the real component or the real rendered output.
- (P3) Task 2's `tdd` flag WAS achievable here, unlike phase 2's -- this app service has an
  integration harness (`DoctorAvailabilitiesAppServiceTests<T>` + `EfCore...` subclass) and the
  seed data was ideal: Slot1 is seeded `BookingStatus.Booked` AND holds Appointment1
  (`Pending`, `A90001`), so ONE read proves the endpoint's whole point. Check for a harness and
  usable seed data BEFORE assuming a task cannot be TDD'd.
- (P3) Phase 3's SonarCloud quality gate PASSED, where phase 2's failed at 77.8% new-code coverage.
  The difference was purely that the logic here was extracted into pure, tested units.

- (P4a) **AN EXTRACTED COMPONENT INHERITS AMBIENT DI FROM ITS HOST, SO "MOVE THE MARKUP" CAN CHANGE
  BEHAVIOUR WITH NO CODE CHANGE.** The picked date rendered as an EMPTY input for three fix attempts.
  `ngbDatepicker` converts every control value through the ambient `NgbDateAdapter` inside
  `writeValue`, and that token is resolved from the HOST injector: both booking surfaces provide ABP's
  string-based `DateAdapter` at component level, while a modal or a bare spec falls back to
  ng-bootstrap's struct-based default. The same value therefore renders in one host and blanks in
  another. When extracting markup that binds a third-party directive, enumerate the tokens that
  directive injects and PIN the ones that define your contract on the new component. Generalises to
  4b: the reschedule modal is exactly the host that would have re-broken this.
- (P4a) THE COROLLARY TO P3's MAPPER LESSON, in its sharpest form: assert THE OBSERVABLE THE USER SEES.
  The specs covered component state, the emitted output, disabled days and the availability highlight
  -- all correct throughout -- and none covered the input's displayed TEXT, the one thing that was
  wrong. 452 green specs plus a clean build shipped an unusable picker.
- (P4a) WHEN THE SECOND FIX FOR ONE SYMPTOM FAILS, STOP CHANGING THE MECHANISM. Three attempts at the
  blank input each swapped the binding mechanism (getter -> memoised getter -> field -> `FormControl`)
  while leaving the MODEL SHAPE wrong, and attempts 5 and 6 were each caused by the fix to the
  previous one. Ten minutes reading `NgbDateAdapter` replaced three rounds of guessing. Read what the
  third-party code does with the value before trying another way to hand it over.
- (P4a) A ROUTE THAT NOTHING NAVIGATES TO IS NOT HARMLESS -- it is a second surface you must verify
  forever. `/appointments/add` served the WIZARD to internal staff and the legacy add form to external
  users on the same path via `canMatch`, while every real entry point had already moved to
  `/appointments/request`. The dead branch cost a full round of confusion about what "both booking
  surfaces" even meant. Retired via a redirect (deletion would have 404'd external users, since the
  internal shell is `internalUserOnlyMatchGuard`-gated). Two things to carry: check what NAVIGATES to
  a route before treating it as live, and when a component is wired
  `loadComponent: () => Promise.resolve(X)` it is EAGER -- that one line held a 3763-line component in
  the initial bundle, and removing it cut 176 kB (2.32 MB -> 2.15 MB).
- (P4a) Never bind a two-way binding (`[ngModel]`, `[(x)]`) to an expression that ALLOCATES. A getter
  returning a fresh object made every change-detection pass see a new reference and schedule another,
  wedging the browser hard enough to hang the automation tooling for 1800s -- which read as a tool
  fault, not an app fault. Reference identity IS the change signal.

### Environment / tooling

- (P1) Dev containers build from BIND-MOUNTED source at container START, so after a branch
  switch `api`/`angular` still serve the OLD branch's code. Restart both before any live
  verification. Angular takes ~2 minutes to come back.
- (P1) `ng lint` / `yarn lint` is BROKEN locally: `@angular-eslint/builder` 20 expects
  ESLint 9 flat config but ESLint 8.57.1 is installed, so it reports "Invalid lint
  configuration. Nothing to lint". CI's lint job passes. Use `npx eslint <changed files>`,
  which honours the legacy `.eslintrc.json`.
- (P1) Run `npx prettier --check` on changed frontend files BEFORE committing -- it caught a
  spec that would otherwise have failed CI's Frontend: Format Check.
- (P1) Playwright MCP writes screenshots to the HOME ROOT, violating the no-artifacts rule.
  Move them to the scratchpad, or to `.github/pr-media/` for a PR.
- (P1) Shell gotcha: `grep -c` exits 1 when the count is 0, silently breaking an `&&` chain
  and truncating the rest of a diagnostic command. Use `;` between independent checks.
- (P2) Passing SQL through `tr '"' "'"` mangles any escaped quotes inside the statement. Use
  a quoted heredoc piped into `cat > /tmp/x.sql; sqlcmd ... -i /tmp/x.sql`, and keep the
  `/opt/...` sqlcmd path OFF the front of `bash -c` or MSYS rewrites it to a Windows path.
- (P1) `AbpPermissionGrants` has NO audit columns, so a hand-ticked grant is indistinguishable
  from a seeded one. To prove a seeder change on a dirty local DB, use a role that LACKS the
  grant as the control.
- (P3) FullCalendar v7 specifics, for whoever touches the calendar next: there is NO
  `@fullcalendar/timegrid` v7 (asking for it resolves 6.1.21 against a 7.0.2 core with unmet
  peers) -- plugins come from the `fullcalendar` BUNDLE subpaths (`fullcalendar/timegrid`,
  `fullcalendar/themes/classic`), a theme plugin must be registered, and 3 CSS files are needed.
  `FullCalendarComponent` is NOT standalone: import `FullCalendarModule`. Click type is
  `EventClickInfo` (v6: `EventClickArg`); range type is `DatesSetInfo`. v7 emits HASHED class names
  (`fc-classic-dl1`), so v6's `.fc` / `.fc-bg-event` selectors match NOTHING -- style via the
  `full-calendar` element plus our own event classes, and force chip colour onto descendants
  (`&, *`) because v7 wraps the title in a div setting `color: #fff`.
- (P3) FullCalendar weeks are Sunday-start here, and its `datesSet.end` is EXCLUSIVE while
  `GetScheduleInput` treats both bounds as INCLUSIVE -- the client sends `end - 1 day`.
- (P3) `abp generate-proxy` churns the WHOLE repo (rewrote 40 files, deleted `proxy/books/`, added
  `proxy/integration/`). Repo precedent (`0f904e7a`, same app service) commits ONLY the touched
  feature files + `generate-proxy.json` and reverts the rest. `angular/src/app/proxy/**` is
  `linguist-generated -diff` in `.gitattributes`, which is why those show as "Binary files differ"
  -- intentional noise suppression, not corruption. Also: the CLI cannot resolve
  `admin.api.localhost`; use `http://localhost:44327`.
- (P3) `angular/tsconfig.json` had `lib: es2018` while `target` was already `ES2022`, an inherited
  inconsistency. Widened `lib` to es2022 rather than setting `skipLibCheck: true`, so dependency
  `.d.ts` files stay type-checked.
- (P3) The angular container serves a STATIC build made at container START -- source edits do NOT
  hot-reload. `docker restart main-angular-1` and wait ~75s for "Accepting connections" after every
  frontend change before re-checking live.
- (P3) DO NOT RUN THE PORTAL AND MRR STACKS AT ONCE. WSL2 is capped at 12 GB on a 15.5 GB host;
  starting the 7-container portal stack alongside the 11-container MRR stack took the whole WSL2 VM
  down -- every container SIGKILLed at the same instant with `OOMKilled=false`, which is the
  VM-level signature rather than a cgroup kill.

### Verification

- (P2) The live check is worth its cost: it proved the persist assignment executes AND that
  the outbox payload serializes the new fields, neither of which any existing test could
  cover. Recipe: SQL-seed a change request with both consent sides set to `Approved` (2),
  approve through the office UI, then assert on the appointment row and the queued
  `AppIntegrationOutboxItems.Payload`.
- (P2) Consent gating is ON (`AppointmentChangeRequestConsts.ConsentGatingEnabled = true`),
  so any approval test must set consent state directly -- the tokenised email click cannot be
  simulated.
- (P2) Left behind by the phase-2 live check, on purpose rather than silently reverted:
  falkinstein appointment **A00034** is now `CancelledLate` and carries a seeded
  change-request row. Restore it if a later phase needs A00034 back in `Approved`.
- (P3) Best live-check data for the calendar: office **Demo Clinic South**
  (`A0A00005-0000-4000-9000-000000000002`), week of **2026-07-12** -- 24 slots, 24 appointments,
  exactly 1 Approved (A00005) and 23 Pending/InfoRequested. The CURRENT week is empty for that
  office, so the calendar legitimately looks blank until you page back; do not read that as a bug.
- (P3) Known cosmetic gap shipped deliberately: the calendar sets no `slotMinTime`/`slotMaxTime`, so
  all 24 hours render and night hours sit empty. Clipping the day could HIDE a real slot, which on a
  staff schedule is a correctness bug rather than a cosmetic one. A compact fix that still hides
  nothing is to derive bounds from the loaded slots, but that needs `calendarOptions` to become
  reactive, which the component avoids on purpose (FullCalendar deep-checks that input; a new object
  per cycle churns the calendar). Only `events` is data-bound.

### Case Tracker coordination

- (P2) Additive wire fields need NO coordinated release -- a receiver that ignores unknown
  fields stays correct. Only phase 4e's two-case reschedule is a genuine contract BREAK
  requiring Levon to change his receiver.
