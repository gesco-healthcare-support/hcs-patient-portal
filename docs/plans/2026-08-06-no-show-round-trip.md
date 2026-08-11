---
feature: No-show / not-seen round trip from Case Tracker (epic phase 5)
date: 2026-08-06
status: approved
base-branch: main
related-issues: []
---

# Phase 5 -- no-show / not-seen round trip

Epic tracker: `docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md`.
Follows 4e (`docs/plans/2026-08-06-case-tracker-two-case-reschedule.md`).

**BASE BRANCH RESOLVED to `main` (2026-08-07).** Phase 5 is FUNCTIONALLY independent of 4d/4e --
the dependency the tracker recorded ("reuses 4d's create-new-appointment machinery") died with the
replacement (see below). The draft stacked it on 4e for ONE reason: both amend the same passages of
`case-tracker-api-contract.md` (section A's NEVER-sent list). 4e merged as #431, so that rewrite is
on main and the conflict risk is gone. Branch off main.

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
- ~~Decision 2 (Adrian, 2026-08-06, via modal): ONE new UI pill covering both statuses.~~
  **SUPERSEDED 2026-08-07 (Adrian, via modal).** TWO pills, labelled `No Show` and `Not Seen`.
  Verbatim: "I do not want to invent new names, these are long used names throughout the business
  not something new I am making." So no merged "Not Evaluated" label.
  Consequence: the backend needs TWO pill constants and TWO donut slices, not one. That is what
  keeps `StatusPillPolicy` mirroring the Angular util -- a single backend bucket behind two Angular
  labels would recreate exactly the divergence found below.
- Decision 2b (Adrian, 2026-08-07, via modal): the new pills go in the backend `DonutOrder` (so the
  internal dashboard shows the volume) but map to the EXISTING `cancelled` external filter segment.
  No seventh external chip, no external chip count moves. This follows the 4c precedent verbatim:
  pill TEXT becomes honest, the filter bar does not churn. The internal donut and the external chip
  bar are separate surfaces, so they may legitimately differ.

### The draft's premise for decision 2 was HALF WRONG (found 2026-08-07)

The draft argued these appointments are "invisible in the donut and its filters" because
`StatusPillPolicy.ToPill` returns null. True for the BACKEND donut. False for the UI: the Angular
util maps `NoShow` to the **`Cancelled`** pill (`appointment-status.util.ts:46`), so a no-showed
appointment today renders as a grey "Cancelled" chip.

It is therefore MISLABELLED, not invisible -- the same class of defect 4c fixed for the two
REQUESTED states, and worse than absence. Both files' doc comments claim they mirror each other and
they do not; T4 must bring them back into agreement rather than only adding to one side.

- Decision 3 (Adrian, 2026-08-06, via modal): the portal does NOT push these statuses back to Case
  Tracker. With no replacement to announce, the push carries nothing they do not already know --
  they authored it. (This REVERSES the recommendation given earlier in the same session, which
  assumed a replacement would follow and carry a forward link.)
- Decision 4 (this plan): ONE endpoint carrying the outcome in the body, not two routes. The two
  statuses are one event -- "the appointment produced no evaluation, here is why" -- and an explicit
  wire value matches how the integration already speaks (`BillingStatusWire`, `EvaluationKindWire`).
  A third cause later is then a new enum value, not a new route.
- Decision 5 (Adrian, 2026-08-07): **a `RescheduleRequested` appointment can never no-show, and
  refusing it is CORRECT.** Verbatim: "If the reschedule is pending, there is no appointment date
  and the patient is not expected to show up. So a reschedule requested appointment first needs to
  be rescheduled and only then is it possible for it to NoShow." So T6's 409 for a non-`Approved`
  appointment is the right answer, not a gap to widen.
- Decision 6 (Adrian, 2026-08-07): **a Pending appointment can never no-show.** Verbatim: "Only
  approved requests go to case tracker and that means a pending request doesn't exist on the case
  tracker and no one can mark NoShow for that." This closes the B1 path below.

### No open change request can be stranded (verified 2026-08-07)

Marking the outcome cannot leave a change request that can never be decided, because the two states
are mutually exclusive by construction. Do NOT "fix" this later:

- Filing a reschedule against an **Approved** appointment ALWAYS moves it to `RescheduleRequested`
  (`AppointmentChangeRequestManager.cs:319-322` fires `RequestRescheduleAsync` on exactly that
  condition). So `Approved` -- the only status `MarkNoShow` / `MarkNotSeen` are permitted from --
  implies no open reschedule request.
- The one case where an open request coexists with its appointment's original status is B1
  (2026-07-01): internal staff file a reschedule against a **Pending** appointment, which stays
  Pending because no `Pending --RequestReschedule-->` transition exists. Decision 6 covers it -- a
  Pending appointment is not on Case Tracker, so no outcome can arrive for it.
- Consequence for T1's acceptance: "reject from any status other than `Approved`" is not a
  conservative default, it is the domain rule. Keep it.

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

Two differences a POST brings (both RE-EXAMINED 2026-08-07, both were overstated in the draft):

- **`[IgnoreAntiforgeryToken]` is CONTESTED, not settled. Do NOT add it reflexively.** The draft
  asserted the POST "needs" it, citing the two public controllers -- both of which do carry it at
  class level (`PublicChangeRequestConsentController.cs:18`,
  `PublicDocumentUploadController.cs:27`). But TWO records say the opposite for new code:
  `CaseTrackerReconcileController.cs:22-23` ("Sonar flags the attribute as a CRITICAL finding
  (S4502) on new code") and `docs/plans/2026-07-28-case-tracker-reconcile-get.md:68` ("Do NOT add
  `[IgnoreAntiforgeryToken]`; Sonar flags it CRITICAL (S4502) and 43 of 50 controllers omit it").
  That prior justification ends "A GET does not need it regardless", so it does NOT settle a POST.
  **Resolve it by testing, not by copying:** build T5 WITHOUT the attribute and POST with only the
  token in the live gate. Add it ONLY if the request is actually rejected -- then the S4502 hotspot
  has a demonstrated justification rather than an inherited one. Note `CaseTrackerPushController`
  has a POST with no such attribute, though it is authenticated rather than anonymous.
- **the 300/hour rate limit is inherited, but it is SHARED, not free.** The limiter is PREFIX-scoped
  on `/api/integration` precisely "so any future /api/integration endpoint inherits the cap instead
  of having to remember to ask for one" (`CaseEvaluationHttpApiHostModule.cs:703-731`) -- so no code
  is needed. What the draft missed: the partition key is the CLIENT IP
  (`ResolveIntegrationPartitionKey`, `:858-862`, returns `ip:{ip}`), and the window spans the whole
  prefix. So attendance POSTs and reconcile GETs from one Case Tracker host consume ONE 300/hour
  budget, and that 300 was sized for their reconcile repair sweep alone ("a post-outage repair sweep
  needs real headroom"). Adding a second consumer is a capacity decision. The lever is the named
  constant `IntegrationRequestsPerHour` -- "Single constant to raise if their sweep outgrows it."
  ACCEPTED for now: attendance calls are one-per-appointment-per-day, negligible beside a sweep.
  Revisit if their sweep ever reports 429s after this ships.

### Gotchas

- Suppression (decision 3) must key on the appointment's STATUS, not on the code path, exactly as
  4d's gate did -- otherwise a later demographic edit re-pushes the appointment carrying `NoShow`.
  ACCEPTED CONSEQUENCE, stated rather than discovered: once an appointment is NoShow / NotSeen, no
  further edit to it reaches Case Tracker. They already hold the fact that closed it.
- A re-eval booked from a no-showed re-eval will set `OriginalAppointmentId` to the NO-SHOWED
  appointment, chaining evaluation -> reval#1 (no-show) -> reval#2, rather than pointing back past
  it. That is honest history and needs no code; noted so it is a decision rather than a surprise.
- The contract has NO inbound endpoint documented at all today -- the integration is push-only plus
  the reconcile GET. Section F is the section to mirror for documenting one.
- `AppointmentStatusType` is persisted as its int value, so `NotSeen` must take the next free
  number (15) and must never be renumbered.

## Tasks

### T1 -- the NotSeen status and its transition

**BUILT TOGETHER WITH T2 (2026-08-07).** T1's acceptance ("WHEN an Approved appointment is marked
not-seen, THE SYSTEM SHALL move it to `NotSeen`") is not reachable on its own: `BuildMachine` is
`private static` and `ApplyTransitionAsync` is private, so the only seam that can exercise a
transition is a public manager method -- which is T2. Splitting them would have meant either a task
with no runnable acceptance or a throwaway test. Both landed in one commit; the acceptances of both
are covered.

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
  **`ResolveRevalRejectionCode` MUST take the kind too** (verified 2026-08-07): today it is
  `ResolveRevalRejectionCode(bool callerIsItAdmin)` and returns one of exactly two codes keyed on
  admin alone (`AppointmentLifecycleValidators.cs`, called at `AppointmentManager.cs:196`), so it
  cannot express the third rejection reason without a new parameter. Changing `CanCreateReval` alone
  leaves the new code unreachable.
- pattern: the existing `CanCreateReval` + `ResolveRevalRejectionCode` pair, which already models
  "reject, but with the right message".
- approach: tdd
- acceptance (EARS): WHEN the source is `Approved`, THE SYSTEM SHALL allow a re-eval. WHEN the
  source is `NoShow` or `NotSeen` AND its `EvaluationKind` is `ReEvaluation`, THE SYSTEM SHALL allow
  a re-eval. WHEN the source is `NoShow` or `NotSeen` AND its kind is `Evaluation`, THE SYSTEM SHALL
  reject it with the new code and SHALL NOT reuse the not-approved message. THE SYSTEM SHALL
  continue to reject every other source status.

### T4 -- a pill each, and a mislabelling fix

**REWRITTEN 2026-08-07** for decisions 2 (two pills, business names) and 2b, and for the
backend/Angular divergence recorded above. The draft's title ("one pill for both") is superseded.

- what: MODIFY `Application/Appointments/StatusPillPolicy.cs` -- add TWO constants, `NoShow` and
  `NotSeen`, map each status to its own in `ToPill`, and place both in `DonutOrder` beside
  `Cancelled` (same family of terminal non-event). MODIFY
  `angular/src/app/shared/ui/status-pill/status-pill.component.ts` -- add both keys to
  `AppointmentPillStatus` and `PILL_META`, labelled "No Show" and "Not Seen", tone `neutral` to
  match `Cancelled`. These ARE done, so 4c's reason for giving the REQUESTED states an in-progress
  amber does not apply. MODIFY
  `angular/src/app/shared/ui/status-pill/appointment-status.util.ts` -- **REMOVE `NoShow` from the
  `Cancelled` arm** (this is the mislabelling FIX, not an addition), map each status to its own
  pill, and add both to `PILL_TO_SEGMENT` pointing at `'cancelled'` so no external chip moves.
  ADD `NotSeen = 15` to `angular/src/app/proxy/enums/appointment-status-type.enum.ts` -- what
  `abp generate-proxy` would emit; by hand because the generator needs a running API.
- pattern: the `CancelledNoBill or CancelledLate => Cancelled` arm for the backend switch; 4c's
  split of the REQUESTED states out of their terminal pills for the Angular side -- the same move
  for the same reason.
- approach: tdd
- acceptance (EARS): WHEN an appointment is `NoShow`, THE SYSTEM SHALL report the `NoShow` pill;
  WHEN it is `NotSeen`, the `NotSeen` pill; and THE SYSTEM SHALL include both in the donut order.
  WHEN an appointment is `NoShow`, THE SYSTEM SHALL NOT report it as `Cancelled` in either the
  backend policy or the Angular util. WHEN it is any other status, THE SYSTEM SHALL report exactly
  the pill it reports today, and every external filter chip count SHALL be unchanged.

### T5 -- the inbound endpoint

**THE RESULT SHAPE IS NOT THE RECONCILE SERVICE'S (found 2026-08-07).** `CaseTrackerReconcileService`
returns `IntakeEnvelope?` and wraps its whole body in `catch (Exception) -> return null` (`:75-86`)
so that everything collapses to one indistinguishable 404. Copying that shape makes T6's 409
UNREACHABLE: an invalid transition throws a `BusinessException` out of the state machine, the
catch-all swallows it, and the caller is told 404 instead of conflict. The service MUST return a
THREE-way result -- applied / conflict / not-found -- with the catch-all narrowed so transition
failures reach the conflict arm rather than the not-found arm. The not-found arm keeps the reconcile
service's ambiguity exactly (unknown office, unknown appointment, integration disabled); only the
conflict arm is new, and it is safe to distinguish because the caller is token-authenticated by then.

- what: CREATE `Domain/Integration/CaseTracker/CaseTrackerAttendanceService.cs` -- enters the office
  with `using (_currentTenant.Change(tenantId))` (the scope is `IDisposable` and MUST be disposed or
  the ambient tenant leaks into the rest of the request), reads the per-office enable setting inside
  the scope, loads the appointment, and applies the outcome via T2, returning the three-way result
  above. It is a DOMAIN service, not an application service: `ConventionalControllers.Create` in the
  host module auto-exposes every app service over HTTP, so an app service here would gain a SECOND
  route with no token check (`CaseTrackerReconcileService.cs:16-20` states this). CREATE
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
  This is why T5's service returns a three-way result rather than a nullable; see the note on T5.
  Build the conflict arm and its test BEFORE the happy path, so a catch-all that swallows the
  transition exception shows up as a red test rather than a passing 404.
- approach: tdd
- acceptance (EARS): WHEN Case Tracker retries an already-applied outcome, THE SYSTEM SHALL return
  200 and SHALL NOT publish a second status change. WHEN it sends a conflicting outcome, THE SYSTEM
  SHALL return 409 and leave the appointment unchanged.

### T7 -- keep both statuses off the wire

**RE-ANCHORED 2026-08-07. The draft's site list was wrong** -- it named "the same three enqueue
sites 4d used (the re-push in `AppointmentChangedHandler`, `CaseTrackerPacketPublishService`, and
the completeness sweep)". On current main the status gate is ONE shared predicate,
`CaseTrackerPublishPolicy.IsPublished`, consulted at SEVEN sites across SIX files.
`CaseTrackerPacketPublishService` is NOT one of them -- it gates on `HasIntakeAsync`, not status.
Building the draft as written would have wired the gate into a site with no status check and missed
five that have one.

- what: ADD `IsAttendanceClosed(AppointmentStatusType)` naming `NoShow` and `NotSeen`, plus a
  combined `ShouldPublish(status) => IsPublished(status) && !IsAttendanceClosed(status)`, to
  `Domain/Integration/CaseTracker/CaseTrackerPublishPolicy.cs`. MODIFY all seven call sites to
  consult `ShouldPublish` instead of `IsPublished`, each with a `LogDebug` on the skip:
  `Handlers/AppointmentChangedHandler.cs:74` and `:101`; `Handlers/DocumentAcceptedHandler.cs:63`;
  `Handlers/DocumentRemovalHandlerBase.cs:60`; `Handlers/PacketsCompleteHandler.cs:71`;
  `Jobs/CaseTrackerCompletenessSweepJob.cs:191`; `Jobs/CaseTrackerReconciliationJob.cs:156`. Also
  update the sweep's doc comment at `:54`, which names `IsPublished` by `<see cref>`.
  **Do NOT simply add the two statuses to `IsPublished`'s deny list.** `IsPublished` means "the
  intake was pushed, so a follow-up can land", and a no-showed appointment WAS published. Its own
  doc comment states the invariant that would break: the excluded states "are all reachable only
  from `Pending`, so they are a closed set" -- `NoShow` is reachable from `Approved`. Keep the two
  meanings separate.
  Unlike 4d's, this gate is PERMANENT -- comment it as such so a later reader does not delete it as
  leftover scaffolding.
- pattern: `CaseTrackerPublishPolicy` itself -- a static pure predicate over status, with the reason
  for its shape in the doc comment. (4d's `CaseTrackerRescheduleSuppressionPolicy`, which the draft
  cited, was DELETED by 4e and is no longer on main; read that commit only for the call-site shape.)
- note on the sweep: suppressing there is defence in depth, not a trade-off. The sweep only selects
  appointments with NO intake row (`:186-192`), and an appointment Case Tracker can no-show
  necessarily had its intake land -- otherwise they hold no case to mark. The two sets do not
  intersect.
- approach: tdd
- acceptance (EARS): WHEN an appointment is `NoShow` or `NotSeen`, THE SYSTEM SHALL NOT enqueue an
  integration row from ANY of the seven sites and SHALL log the skip. WHEN it is any other published
  status, THE SYSTEM SHALL enqueue exactly as before. THE SYSTEM SHALL leave `IsPublished` answering
  exactly what it answers today for every status.

### T8 -- document the inbound endpoint

- what: MODIFY `docs/integration/case-tracker-api-contract.md` -- add a section for the INBOUND
  endpoint (the contract documents no inbound surface today), mirroring section F's shape: route,
  auth header, body, status codes, idempotency and conflict semantics, rate limit. MOVE `NoShow` out
  of section A's NEVER-sent list -- but state that it is never SENT because the portal suppresses it,
  which is
  a different reason from today's "no API surface". Add `NotSeen` to the status vocabulary with the
  same note.
- pattern: section F (the reconcile GET) for an endpoint section; 4e's strike-through-in-place for
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
- Revert ONE of the seven `ShouldPublish` call sites to `IsPublished`; confirm only that site's test
  fails. Repeat per site -- seven sites need seven tests, and a single shared-policy unit test will
  NOT catch a missed call site.
- Make `ShouldPublish` ignore `IsAttendanceClosed`; confirm the suppression tests fail while every
  existing publish test stays green.
- Widen T5's service catch to `catch (Exception) -> not-found`, the reconcile service's shape;
  confirm the T6 conflict test goes red. If it stays green, the conflict arm is not actually being
  exercised and the test is worthless.

## Live gate

1. `docker ps`; restart `main-api-1` after building.
2. Take an Approved appointment and POST the endpoint with a valid token, correct `{tenantId}`, and
   `{"outcome":"NO_SHOW"}`. Assert 200 and the status in SQL. **Send NO antiforgery cookie or
   header** -- a raw `curl` with only `X-Integration-Token` is exactly what Case Tracker will send.
   THIS STEP SETTLES THE `[IgnoreAntiforgeryToken]` QUESTION: a 200 proves the attribute is
   unnecessary and it stays off (no S4502 hotspot); a 400 proves it is required and the hotspot then
   has a demonstrated justification to record. Do not decide this from the source alone.
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

## Live gate RESULT (2026-08-08, Falkinstein office, local docker stack)

Ran against `CaseEvaluation_falkinstein`, tenant `5B2581DA-...D75D72`, appointments A00031
(first evaluation) and A00036 (re-evaluation).

**Two blockers had to be cleared before step 1 could run, both PRE-EXISTING:**

- `docker-compose.yml` had NO `CaseTracker__*` passthrough at all, so the token could never reach
  the api container and `IntegrationTokenValidator` failed closed on every request. Prod compose
  has it (`docker-compose.prod.yml:289-292`); dev never did. THIS IS WHY THE RECONCILE GET WAS
  NEVER LIVE-GATED. Fixed by mirroring the prod block into the dev api service.
- `CaseEvaluation.Integration.CaseTrackerPushEnabled` had no row in `AbpSettings` anywhere, so it
  fell back to its FALSE default and every call would have answered 404. Enabled per-office.

| Step | Result |
| ---- | ------ |
| 2 -- valid token, NO antiforgery cookie | **200.** SETTLED: `[IgnoreAntiforgeryToken]` is NOT required. It stays off; no S4502 hotspot. Status confirmed `4` in SQL. |
| 3 -- retry same outcome | **200**, and the notification outbox still held exactly ONE fan-out (3 rows = 3 staff recipients, 0.27s apart, distinct idempotency keys). No second status change. |
| 4 -- conflicting outcome | **409**, status still `4`. |
| 5 -- wrong token / no token / unknown appointment | **401 / 401 / 404.** Also verified: unknown office **404**, malformed outcome **400**. |
| 6 -- integration outbox | **PASS.** The only row predates the POST by twelve days (created 2026-07-29, never modified) -- it is the intake row from approval. The POST enqueued nothing. |
| 7 -- staff email | **PASS.** Three rows, subject "Appointment A00031 marked No-Show", to stafsuper1 / clistaff1 / genevieveg. The already-built alert fired with no notification code in this phase. |
| 8 -- UI | **PASS after a fix (below).** A00031 renders "No Show", A00036 "Not Seen", neither as "Cancelled". The Cancelled CHIP reads 4 = 2 real cancellations + these 2, confirming decision 2b: they filter under the existing chip and no seventh chip appeared. |
| 9 -- re-eval gate | **NOT RUN.** Covered by mutation-verified unit tests, but the end-to-end booking path was not exercised. Still owed. |

### Step 6's acceptance was WORDED WRONG in this plan

It said `AppIntegrationOutboxItems` must have gained NO rows "for the appointment" -- read literally
that is a count of ZERO, which can never hold: a published appointment legitimately carries the
intake row from its approval. The correct assertion, and what was checked, is that no NEW row is
enqueued by the attendance POST.

### DEFECT FOUND AND FIXED BY THE GATE: the donut legend printed raw keys

The dashboard status breakdown rendered **"NoShow"** and **"NotSeen"** run together, not the
business names. `internal-dashboard.component.ts` keeps its own `PILL_LABEL` and `PILL_COLOR` maps
keyed on plain strings, with `PILL_LABEL[pill] ?? pill` as the fallback -- so a missing entry is not
a compile error and the raw key leaks to screen. `PILL_COLOR` fell back to `--n-300`, the same grey
as Cancelled, giving three indistinguishable slices.

No test could have caught this: the maps are `Record<string, string>`, so TypeScript does not
require exhaustiveness over `AppointmentPillStatus`. THIS IS A THIRD LABEL SURFACE beyond the two
T4 already fixed (`PILL_META` and `STATUS_LABELS`) -- a fourth would fail the same silent way.

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
