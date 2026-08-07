---
feature: No-show / not-seen round trip from Case Tracker (epic phase 5)
date: 2026-08-06
status: draft
base-branch: feat/case-tracker-two-case-reschedule
related-issues: []
---

# Phase 5 -- no-show / not-seen round trip

Epic tracker: `docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md`.
Follows 4e (`docs/plans/2026-08-06-case-tracker-two-case-reschedule.md`).

**BASE BRANCH IS A JUDGEMENT CALL, flagged for override.** Phase 5 is now FUNCTIONALLY independent
of 4d/4e -- the dependency the tracker recorded ("reuses 4d's create-new-appointment machinery")
died with the replacement (see below). It is stacked on 4e anyway for ONE reason: both amend the
same passages of `case-tracker-api-contract.md` (§A's NEVER-sent list, which 4e already rewrote),
and branching off `main` would guarantee a conflict in exactly the paragraph both edit. Rebase onto
main once 4d and 4e merge. Say so and this becomes `main` instead.

## Goal

When Case Tracker records that a patient did not show, or showed but was not seen, the portal
learns it, stores it, shows it, and -- where the appointment was itself a re-evaluation -- still
allows a re-eval to be booked against it.

## Context & decisions

### The tracker's description of this phase was WRONG (corrected by Adrian, 2026-08-06)

The tracker says: "the portal sets the appointment to `NoShow` and ALERTS staff -> staff use the
appointment number to create a replacement appointment that is PRE-APPROVED -> the replacement is
pushed to CT". **There is no replacement.** Adrian, verbatim: "Once an appointment is NoShow, we
just mark it as that and move on. There is no replacement. If the clients want an appointment for
the NoShow or NotSeen they have to request a new appointment (It applies to all appointments
including re-evals)."

What that deletes from this phase, and from the epic:

- the pre-approved replacement, and every piece of machinery it implied;
- **the generalized `ReplacesAppointmentId` + `ReplacementReason` column.** It existed only to give
  a no-show replacement somewhere to link from. 4d's reschedule-specific
  `RescheduledFromAppointmentId` is therefore correct as built and **4d does NOT need reopening** --
  reversing what this session recommended an hour earlier;
- extracting 4d's inline replacement-creation into a shared service, for this phase's sake;
- `supersededReason = NO_SHOW`. 4e's open value set stands, but nothing in phase 5 adds to it.

### What the two statuses mean (Adrian, 2026-08-06)

- **NoShow** -- the patient did not show up to the appointment.
- **NotSeen** -- the patient showed up but was not seen by the doctor. Causes include, but are not
  limited to, a missing or incorrect interpreter, and the patient walking out because they were
  impatient waiting.

Both end with no evaluation performed. Neither is recorded by the portal today: intake staff mark
them in CASE TRACKER, which is why this phase exists at all -- the portal has to be told.

### Resolved decisions

- Decision 1 (Adrian, 2026-08-06, via modal): a re-eval MAY be booked against a NoShow / NotSeen
  appointment ONLY IF that appointment was itself a re-evaluation. A no-showed FIRST evaluation
  must come back as a new appointment request. So the gate reads status AND
  `Appointment.EvaluationKind`, not status alone. This deliberately breaks the strict-OLD-parity
  rule recorded at `AppointmentLifecycleValidators.cs:50-55`; recorded here rather than slipped in.
- Decision 2 (Adrian, 2026-08-06, via modal): ONE new UI pill covering both statuses. They are the
  same thing operationally -- the appointment produced no evaluation -- and today `NoShow` maps to
  NO pill at all (`StatusPillPolicy.ToPill` returns null), so these appointments are invisible in
  the donut and its filters. That invisibility is the "make sure it is searchable" half of the ask.
- Decision 3 (Adrian, 2026-08-06, via modal): the portal does NOT push these statuses back to Case
  Tracker. With no replacement to announce, the push carries nothing they do not already know --
  they authored it. (This REVERSES the recommendation given earlier in the same session, which
  assumed a replacement would follow and carry a forward link.)
- Decision 4 (this plan): ONE endpoint carrying the outcome in the body, not two routes. The two
  statuses are one event -- "the appointment produced no evaluation, here is why" -- and an explicit
  wire value matches how the integration already speaks (`BillingStatusWire`, `EvaluationKindWire`).
  A third cause later is then a new enum value, not a new route.

## All needed context

### Verified facts this plan rests on

| Fact                                                                         | Anchor                                                         |
| ---------------------------------------------------------------------------- | -------------------------------------------------------------- |
| `Approved --MarkNoShow--> NoShow` is already a legal transition              | `AppointmentManager.cs:426`                                    |
| `MarkNoShow` has NO caller anywhere -- the trigger is dead code today        | only `AppointmentTransitionTrigger.cs:24`                      |
| `NotSeen` does NOT exist; the enum stops at `InfoRequested = 14`             | `Enums/AppointmentStatusType.cs`                               |
| A re-eval source must be `Approved` -- literally `== Approved`               | `AppointmentLifecycleValidators.cs:50-55`                      |
| `EvaluationKind` is a persisted column (`Evaluation = 1`/`ReEvaluation = 2`) | `Enums/EvaluationKind.cs`; set `AppointmentsAppService.cs:893` |
| `NoShow` has no UI pill                                                      | `StatusPillPolicy.ToPill`, `_ => null`                         |
| The staff email alert for NoShow is ALREADY BUILT and needs nothing          | `StatusChangeEmailHandler.cs:499-528`                          |

### The endpoint pattern to mirror EXACTLY

`CaseTrackerReconcileController` (`HttpApi.Host/Controllers/Integration/`) answers every question
this endpoint has:

- `[AllowAnonymous]` stated explicitly, with the token as the only barrier;
- `IntegrationTokenValidator.IsValid` checked BEFORE any database work, so an unauthenticated
  caller cannot make the portal open an office connection or probe which ids exist;
- `{tenantId}` in the PATH because the portal is database-per-office and tenant headers are blocked
  globally;
- lives in HttpApi.**Host**, not HttpApi, because it depends on Domain types;
- the service enters the office with `_currentTenant.Change(tenantId)` and reads the per-office
  enable setting INSIDE that scope (`CaseTrackerReconcileService.cs:57-71`);
- unknown office and unknown appointment collapse to the SAME not-found answer, so a 500 never
  tells the caller it guessed a real office (`:75-86`).

Two differences a POST brings:

- it needs class-level `[IgnoreAntiforgeryToken]`. The reconcile controller avoided it only because
  a GET does not need one; `PublicChangeRequestConsentController` and `PublicDocumentUploadController`
  set the precedent for an anonymous machine POST.
- **the 300/hour rate limit is inherited for free.** The limiter is PREFIX-scoped on
  `/api/integration` precisely "so any future /api/integration endpoint inherits the cap instead of
  having to remember to ask for one" (`CaseEvaluationHttpApiHostModule.cs:703-731`). Nothing to add.

### Gotchas

- Suppression (decision 3) must key on the appointment's STATUS, not on the code path, exactly as
  4d's gate did -- otherwise a later demographic edit re-pushes the appointment carrying `NoShow`.
  ACCEPTED CONSEQUENCE, stated rather than discovered: once an appointment is NoShow / NotSeen, no
  further edit to it reaches Case Tracker. They already hold the fact that closed it.
- A re-eval booked from a no-showed re-eval will set `OriginalAppointmentId` to the NO-SHOWED
  appointment, chaining evaluation -> reval#1 (no-show) -> reval#2, rather than pointing back past
  it. That is honest history and needs no code; noted so it is a decision rather than a surprise.
- The contract has NO inbound endpoint documented at all today -- the integration is push-only plus
  the reconcile GET. §F is the section to mirror for documenting one.
- `AppointmentStatusType` is persisted as its int value, so `NotSeen` must take the next free
  number (15) and must never be renumbered.

## Tasks

### T1 -- the NotSeen status and its transition

- what: MODIFY `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/AppointmentStatusType.cs`
  -- add `NotSeen = 15` with a doc line stating it means the patient ARRIVED but was not evaluated,
  as distinct from `NoShow`. MODIFY `Domain/Appointments/AppointmentTransitionTrigger.cs` -- add
  `MarkNotSeen`. MODIFY `AppointmentManager.BuildMachine` -- permit
  `Approved --MarkNotSeen--> NotSeen` beside the existing `MarkNoShow` line at `:426`.
- pattern: the `MarkNoShow` trigger and its `.Permit(...)` on the `Approved` configuration.
- approach: tdd
- acceptance (EARS): WHEN an Approved appointment is marked not-seen, THE SYSTEM SHALL move it to
  `NotSeen`. WHERE an appointment is in any status other than `Approved`, THE SYSTEM SHALL reject
  both `MarkNoShow` and `MarkNotSeen` as invalid transitions.

### T2 -- an appointment manager method to apply the outcome

- what: MODIFY `Domain/Appointments/AppointmentManager.cs` -- add
  `MarkAttendanceOutcomeAsync(Guid appointmentId, AppointmentStatusType outcome, ...)` that fires
  the matching trigger through the state machine and publishes the status-changed event, so the
  existing staff email fires with no notification code in this phase.
- pattern: `CloseForRescheduleAsync` (added in 4d), which does exactly this shape for the two
  reschedule triggers.
- approach: tdd
- acceptance (EARS): WHEN the outcome is `NoShow` or `NotSeen`, THE SYSTEM SHALL apply the matching
  trigger and publish the status change. WHERE the outcome is any other status, THE SYSTEM SHALL
  throw rather than coerce it.

### T3 -- the re-eval gate

- what: MODIFY `Domain/Appointments/AppointmentLifecycleValidators.cs` -- `CanCreateReval` takes the
  source's `EvaluationKind` in addition to its status: `Approved` -> allowed;
  `NoShow` / `NotSeen` -> allowed ONLY when the source's kind is `ReEvaluation`; anything else ->
  rejected. ADD a distinct error code + localization for the new rejection ("that appointment was a
  first evaluation that was not completed -- please submit a new appointment request"), beside
  `AppointmentRevalSourceNotApproved` (`CaseEvaluationDomainErrorCodes.cs:305`). MODIFY
  `AppointmentManager.LoadRevalSourceAsync` (`:184-203`) to pass the kind through.
- pattern: the existing `CanCreateReval` + `ResolveRevalRejectionCode` pair, which already models
  "reject, but with the right message".
- approach: tdd
- acceptance (EARS): WHEN the source is `Approved`, THE SYSTEM SHALL allow a re-eval. WHEN the
  source is `NoShow` or `NotSeen` AND its `EvaluationKind` is `ReEvaluation`, THE SYSTEM SHALL allow
  a re-eval. WHEN the source is `NoShow` or `NotSeen` AND its kind is `Evaluation`, THE SYSTEM SHALL
  reject it with the new code and SHALL NOT reuse the not-approved message. THE SYSTEM SHALL
  continue to reject every other source status.

### T4 -- one pill for both

- what: MODIFY `Application/Appointments/StatusPillPolicy.cs` -- add a pill constant covering
  `NoShow` and `NotSeen`, map both to it in `ToPill`, and place it in `DonutOrder`. MODIFY the
  Angular status filter/label surfaces that consume the pill set so the new slice renders.
- pattern: the `CancelledNoBill or CancelledLate => Cancelled` arm, which already folds two statuses
  into one pill.
- approach: tdd
- acceptance (EARS): WHEN an appointment is `NoShow` or `NotSeen`, THE SYSTEM SHALL report the new
  pill and SHALL include it in the donut order. WHEN it is any other status, THE SYSTEM SHALL report
  exactly the pill it reports today.

### T5 -- the inbound endpoint

- what: CREATE `Domain/Integration/CaseTracker/CaseTrackerAttendanceService.cs` -- enters the office
  with `_currentTenant.Change(tenantId)`, reads the per-office enable setting inside the scope,
  loads the appointment, and applies the outcome via T2. CREATE
  `HttpApi.Host/Controllers/Integration/CaseTrackerAttendanceController.cs` -- `[AllowAnonymous]`,
  class-level `[IgnoreAntiforgeryToken]`, route
  `api/integration/offices/{tenantId}/appointments/{appointmentId}/attendance`, token checked before
  any database work. Body: `{ "outcome": "NO_SHOW" | "NOT_SEEN" }`, mapped by a new
  `AttendanceOutcomeWire` with a THROWING switch.
- pattern: `CaseTrackerReconcileController` + `CaseTrackerReconcileService` end to end;
  `EvaluationKindWire` for the wire enum.
- approach: tdd
- acceptance (EARS): WHEN a valid token, known office and Approved appointment are presented, THE
  SYSTEM SHALL apply the outcome and return 200. WHERE the token is missing or wrong, THE SYSTEM
  SHALL return 401 without touching the database. WHERE the office or appointment is unknown, THE
  SYSTEM SHALL return 404 indistinguishably. WHERE the office has the integration disabled, THE
  SYSTEM SHALL return 404.

### T6 -- idempotency and conflict

- what: MODIFY the T5 service -- a repeat call carrying the SAME outcome for an appointment already
  in that status is a no-op returning 200; a call carrying a DIFFERENT outcome for an appointment
  already in one of these two statuses returns 409; an appointment whose status permits neither
  trigger returns 409. Log each non-200 with identifiers only.
- pattern: the "collapse to a non-distinguishing answer" logging style in
  `CaseTrackerReconcileService.cs:75-86`, but note conflicts are DISTINGUISHABLE here on purpose --
  the caller is authenticated by then, and silence would break the log this phase exists to create.
- approach: tdd
- acceptance (EARS): WHEN Case Tracker retries an already-applied outcome, THE SYSTEM SHALL return
  200 and SHALL NOT publish a second status change. WHEN it sends a conflicting outcome, THE SYSTEM
  SHALL return 409 and leave the appointment unchanged.

### T7 -- keep both statuses off the wire

- what: CREATE a small pure policy in `Domain/Integration/CaseTracker/` naming the two statuses as
  not-published, and call it from the same three enqueue sites 4d used (the re-push in
  `AppointmentChangedHandler`, `CaseTrackerPacketPublishService`, and the completeness sweep), each
  with a `LogDebug`. Unlike 4d's, this gate is PERMANENT -- comment it as such so a later reader
  does not delete it as leftover scaffolding.
- pattern: 4d's `CaseTrackerRescheduleSuppressionPolicy` and its three call sites (deleted by 4e --
  read that commit for the shape).
- approach: tdd
- acceptance (EARS): WHEN an appointment is `NoShow` or `NotSeen`, THE SYSTEM SHALL NOT enqueue an
  integration row from any of the three paths and SHALL log the skip. WHEN it is any other published
  status, THE SYSTEM SHALL enqueue exactly as before.

### T8 -- document the inbound endpoint

- what: MODIFY `docs/integration/case-tracker-api-contract.md` -- add a section for the INBOUND
  endpoint (the contract documents no inbound surface today), mirroring §F's shape: route, auth
  header, body, status codes, idempotency and conflict semantics, rate limit. MOVE `NoShow` out of
  §A's NEVER-sent list -- but state that it is never SENT because the portal suppresses it, which is
  a different reason from today's "no API surface". Add `NotSeen` to the status vocabulary with the
  same note.
- pattern: §F (the reconcile GET) for an endpoint section; 4e's strike-through-in-place style for
  correcting a statement that has changed reason.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL document the inbound endpoint completely enough to call
  without reading portal source, and SHALL NOT leave `NoShow` described as unreachable.

### T9 -- tracker

- what: MODIFY the epic tracker -- correct the phase-5 description (it still describes the
  pre-approved replacement), set the row, record that phase 5 turned out NOT to depend on 4d, and
  append learnings.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL NOT leave the replacement flow described as phase 5's design.

## Validation loop

```
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
```

Frontend (T4 touches Angular):

```
export CHROME_BIN="/c/Program Files/Google/Chrome/Application/chrome.exe"
npx prettier --check <changed files>
npx eslint <changed files>
npx ng build
npx ng test --watch=false --browsers=ChromeHeadless
```

No migration: `NotSeen` is a new enum VALUE, not a schema change, and
`AppointmentStatusType` is stored as an int. Confirm with `has-pending-model-changes` on BOTH
contexts that nothing drifted.

Mutation checks (required):

- Remove `NotSeen` from the reval gate's allowed set; confirm only the not-seen reval test fails.
- Make the gate ignore `EvaluationKind`; confirm the "no-showed FIRST evaluation" test fails while
  the "no-showed re-eval" test stays green -- that pair is the whole of decision 1.
- Remove one of the three suppression call sites; confirm only that site's test fails.

## Live gate

1. `docker ps`; restart `main-api-1` after building.
2. Take an Approved appointment and POST the endpoint with a valid token, correct `{tenantId}`, and
   `{"outcome":"NO_SHOW"}`. Assert 200 and the status in SQL.
3. Repeat the same call; assert 200 and that no second status-changed event or duplicate email row
   appeared.
4. POST `{"outcome":"NOT_SEEN"}` to the same appointment; assert 409 and the status unchanged.
5. POST with a wrong token; assert 401. POST to an unknown appointment id; assert 404.
6. Assert `AppIntegrationOutboxItems` gained NO rows for the appointment (decision 3).
7. Confirm the staff email row appeared in `AppNotificationOutboxItems` -- proving the already-built
   alert fires with no notification code in this phase.
8. In the UI, confirm the appointment shows the new pill and is reachable by confirmation-number
   search.
9. Book a re-eval against it: allowed when that appointment was a re-eval, rejected with the NEW
   message when it was a first evaluation.

## Risk / rollback

Blast radius: a new inbound endpoint, one new status value, the re-eval gate, the status pill, and
the Case Tracker publish path. The booking, approval, cancel and reschedule flows are untouched.

1. **A new anonymous endpoint is new attack surface.** Mitigated by reusing the reconcile GET's
   posture exactly -- fail-closed token, constant-time compare, checked before any database work,
   inherited rate limit -- and by it accepting only a fixed enum for one appointment at a time.
2. **The re-eval gate is a deliberate parity break.** If decision 1 is wrong, staff either cannot
   rebook a legitimately no-showed re-eval, or can rebook something they should not. It is a pure
   validator with tests per branch, so it is cheap to change.
3. **`NotSeen = 15` is permanent.** Renumbering it later would silently relabel stored rows.
4. Suppression here is PERMANENT, unlike 4d's. If a future phase gives the portal its own way to
   mark these statuses, that phase must revisit T7 -- the gate would then withhold something Case
   Tracker had not authored.

Rollback: revert the merge. No migration, no data change beyond appointments already marked, which
keep their status -- `NotSeen` rows would be left holding a value the reverted enum no longer names,
so a revert after real use needs those rows reviewed rather than assumed harmless.
