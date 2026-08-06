---
feature: Case Tracker two-case reschedule semantics + contract amendment (epic phase 4e)
date: 2026-08-06
status: in-progress
base-branch: feat/reschedule-creates-new-appointment
related-issues: []
---

# Phase 4e -- Case Tracker two-case reschedule semantics

Epic tracker: `docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md`.
Follows phase 4d (`docs/plans/2026-08-05-reschedule-creates-new-appointment.md`, PR #430).
Research: section "Context packet -- phase 4e" in the 4d/4e session, anchors restated below.

**Deploys with 4b + 4c + 4d as ONE release (Adrian, 2026-08-06).** See decision 6.

## Goal

Let the reschedule split reach Case Tracker as what it actually is -- the old case closed and
billed, a new case opened, and each pointing at the other with the reason -- and correct the
contract, which currently promises the opposite.

## Context & decisions

4d creates a second appointment but keeps it off the wire behind three suppression gates, because
`docs/integration/case-tracker-api-contract.md` still tells the receiver the portal "never creates
a second one". 4e removes the gates and makes the contract true.

### The finding that shapes this phase

**The two-case model needs no receiver code change to WORK.** Contract section G is explicit:
"Case dedup key: `appointmentId`. Upsert on it." A new appointment id therefore opens a second case
on their side automatically. What is missing is not mechanism but MEANING -- nothing on the wire
says the new case continues the old one, or why. That is why this phase is mostly four additive
fields plus a document rewrite, and not a negotiation about their upsert logic.

Consequence for coordination: the deploy needs Levon INFORMED, not Levon DEPLOYED.

### Resolved decisions

- Decision 1 (Adrian, 2026-08-06, via modal): the replacement links back with a NEW dedicated pair,
  `rescheduledFromAppointmentId` + `rescheduledFromConfirmationNumber`, NOT by reusing
  `previousAppointmentId`. Because `previousAppointmentId` means RE-EVALUATION, and
  `evaluationKind` exists precisely because that field once carried reschedule links and mislabelled
  case folders (`AppointmentsAppService.cs:899-906`). Reusing it would rebuild that ambiguity one
  layer up, at the wire. Purely additive, so no coordinated release.
- Decision 2 (Adrian, 2026-08-06): the OLD case also points FORWARD, and the link carries its CAUSE
  -- `supersededByAppointmentId` + `supersededReason`. Adrian's criterion was "if the cause is
  determinable from the id alone, a bare id is fine". VERIFIED IT IS NOT: inferring the cause means
  cross-referencing the successor case, which requires that successor to have already arrived, and
  a reschedule and a re-evaluation are not the same relationship at all --

  | Relation   | The old appointment          | Meaning                          |
  | ---------- | ---------------------------- | -------------------------------- |
  | Reschedule | closes terminal, IS replaced | it did not happen                |
  | Re-eval    | untouched, keeps its status  | it happened; a follow-up follows |

  Verified at `AppointmentsAppService.cs:895-898`: booking a re-eval sets `OriginalAppointmentId` on
  the NEW appointment and never touches the source. So one field cannot carry both, and an explicit
  reason is the honest design.

  It also earns its keep against a KNOWN requirement rather than speculation: epic phase 5 creates a
  pre-approved replacement for a no-show -- a third superseding cause -- which will take
  `supersededReason = NO_SHOW` with no further contract amendment.

- Decision 3 (Adrian, 2026-08-06, via modal): document the shared-blob consequence explicitly. 4d
  copies document ROWS, so one `objectKey` now appears under two different document `id`s across two
  cases. Section G's "upsert by id, never treat objectKey as identity" already makes their handling
  correct, but a reader auditing storage deserves to know this is deliberate.
- Decision 4 (Adrian, 2026-08-06, via modal): verification stops at outbox rows + a payload
  snapshot. Their endpoints are not deployed to `.35` (contract item 11), so end-to-end receipt
  cannot be proven from here, and a local stub would test our HTTP client rather than their
  contract -- which earlier phases already cover.
- Decision 5 (Adrian, 2026-08-06, via modal): 4e stacks on the 4d branch, because its first task
  deletes a policy that does not exist on `main`. REBASE ONTO MAIN once #430 merges, so 4e's PR
  shows only 4e's commits.
- Decision 6 (Adrian, 2026-08-06, via modal): 4d and 4e DEPLOY TOGETHER, so the suppression window
  never exists in production. Consequence worth stating plainly: suppression code ships and is
  removed in the same release, so in production it is never active for a single reschedule. It
  earned its place anyway -- it let 4d merge, and be live-gated, without lying to Case Tracker.

  This also removes any backfill question. No appointment is ever created while suppressed, so there
  is nothing for the completeness sweep to recover and nothing to explain as a late burst.

## All needed context

### What 4d left behind, to delete

| What                                                     | Anchor                                      |
| -------------------------------------------------------- | ------------------------------------------- |
| The policy itself                                        | `CaseTrackerRescheduleSuppressionPolicy.cs` |
| Gate 1 -- re-push (covers edits AND the patient fan-out) | `AppointmentChangedHandler.cs:129`          |
| Gate 2 -- packet settle (intake AND document branches)   | `CaseTrackerPacketPublishService.cs:64`     |
| Gate 3 -- hourly recovery                                | `CaseTrackerCompletenessSweepJob.cs:138`    |

Tests that pin suppression and must go WITH it (they assert the opposite of 4e):
`AppointmentChangedHandlerTests.WhenTheOldHalfOfARescheduleSplitIsEdited_NothingIsQueued`,
`.WhenTheReplacementHalfOfARescheduleSplitIsEdited_NothingIsQueued`,
`.WhenAPatientIsEdited_BothHalvesOfARescheduleSplitAreSkipped`,
`PacketsCompleteHandlerTests.WhenTheReplacementHalfsPacketsSettle_NoIntakeIsQueued`,
`.WhenTheOldHalfsPacketsSettleAgain_NothingIsQueued`,
`CaseTrackerCompletenessSweepJobTests.TheNewHalfOfARescheduleSplit_IsNotRecovered`,
`.TheOldHalfOfARescheduleSplit_IsNotRecovered`.

`AppointmentChangedHandlerTests.LifecycleChangesAfterApprovalArePushed` gets `RescheduledNoBill`
back as a theory case -- 4d moved it out, 4e moves it home.

### Where the payload is built

| What                                             | Anchor                                                            |
| ------------------------------------------------ | ----------------------------------------------------------------- |
| The DTO, camelCase-serialized, nulls NOT omitted | `Payload/IntakePayload.cs:65-68` (the `Previous*` pair)           |
| Scalar assignment                                | `Payload/IntakePayloadBuilder.cs:100-101`                         |
| The lookup pattern to mirror EXACTLY             | `Payload/AppointmentCoreResolver.cs:71-77`                        |
| Wire-enum pattern to mirror                      | `Payload/EvaluationKindWire.cs` (const strings + throwing switch) |
| Serializer (camelCase, nulls kept deliberately)  | `IntakePayloadSerializer.cs:17-26`                                |

**Both channels get new fields free**: `CaseTrackerIntakeQueue` and `CaseTrackerReconcileService`
share `IIntakePayloadBuilder`, so a field added to `IntakePayload` reaches the push AND the
reconcile GET with no extra work.

### Billing already works -- do not add code for it

`BillingStatusWire.ToWire` already maps `RescheduledNoBill -> NO_BILL` and
`RescheduledLate -> LATE` (`Payload/BillingStatusWire.cs:39`). The moment suppression is removed,
the old appointment's close carries the correct billing signal with no new code. This is the signal
R5 needs so Case Tracker can close or bill the old date.

### Contract passages that are now FALSE

`docs/integration/case-tracker-api-contract.md`, 749 lines:

| Anchor     | What it says today                                                                             |
| ---------- | ---------------------------------------------------------------------------------------------- |
| `:416-419` | "moves the SAME appointment in place rather than cloning a row ... never creates a second one" |
| `:424-429` | the "RESCHEDULE TRAP": a reschedule is signalled by a CHANGED DATE, not a status change        |
| `:121`     | status table: `Approved` "after a reschedule is finalized"                                     |
| `:136-138` | `RescheduledNoBill` / `RescheduledLate` listed under NEVER sent, "tolerate but do not rely"    |
| `:733`     | Coordination decision 4 (the re-push trigger set)                                              |
| `:740-742` | Coordination decision 7 (linking facts: currently re-eval only)                                |

### Test harness

`IntakePayloadBuilderTests` (`test/.../Integration/CaseTracker/`, 466 lines) already plumbs a
`sourceAppointment` keyed on `SourceAppointmentId` (`:74-86`) -- reusable as-is for the backward
link. The FORWARD link needs a successor lookup, which is a predicate query rather than
`FindAsync(id)`, so the harness needs one new substitute arrangement.

### Gotchas

- `AppointmentCoreResolver.cs:55-56` carries a comment claiming "the in-place reschedule keeps
  [`Appointment.AppointmentDate`] in step". 4d retired the in-place move; fix the comment while the
  file is open (it is three lines from the code being changed).
- Volume: a reschedule becomes TWO messages instead of one. The cap is 100/office/hour and organic
  traffic is single digits (§H `:573-583`), so this is a documentation note, not a risk.
- Nulls are NOT omitted from the JSON by deliberate design (`IntakePayloadSerializer.cs:10-14`), so
  all four new fields appear on every payload as `null` when absent. That is the existing contract
  style -- do not add `JsonIgnore`.

## Tasks

### T1 -- delete the suppression policy and its three gates

- what: DELETE `src/HealthcareSupport.CaseEvaluation.Domain/Integration/CaseTracker/CaseTrackerRescheduleSuppressionPolicy.cs`
  and remove the `if (CaseTrackerRescheduleSuppressionPolicy.IsSuppressed(appointment))` block plus
  its `LogDebug` from `AppointmentChangedHandler.cs:129`, `CaseTrackerPacketPublishService.cs:64`
  and `CaseTrackerCompletenessSweepJob.cs:138`. DELETE the seven suppression tests listed above, and
  restore `AppointmentStatusType.RescheduledNoBill` as an `[InlineData]` case on
  `AppointmentChangedHandlerTests.LifecycleChangesAfterApprovalArePushed`.
- pattern: the 4d commit `be39f982` is the exact inverse -- read it to be sure every arm goes.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL contain no reference to `CaseTrackerRescheduleSuppressionPolicy`,
  and `dotnet build -warnaserror` SHALL succeed. WHEN an appointment moves to `RescheduledNoBill` or
  `RescheduledLate`, THE SYSTEM SHALL enqueue an intake row.

### T2 -- the superseding-reason wire enum

- what: CREATE `src/HealthcareSupport.CaseEvaluation.Domain/Integration/CaseTracker/Payload/SupersededReasonWire.cs`
  -- `public const string Rescheduled = "RESCHEDULED";` and a `ToWire` that throws on anything it
  does not know. Values are strings on the wire, not a C# enum, matching `EvaluationKindWire`.
- pattern: `Payload/EvaluationKindWire.cs` -- const strings + a switch whose default THROWS, so a
  future cause cannot silently serialize as a renamed enum member.
- approach: tdd
- acceptance (EARS): WHEN the cause is a reschedule, THE SYSTEM SHALL return `"RESCHEDULED"`. WHERE
  a cause has no mapping, THE SYSTEM SHALL throw rather than emit a guessed value.

  CLARIFIED AT BUILD TIME (2026-08-06): the task did not say what `ToWire` takes, and there is no
  `SupersededReason` C# enum to map from. The input is `AppointmentStatusType` -- the OLD
  appointment's terminal status IS the cause -- which makes this an exact mirror of
  `BillingStatusWire.ToWire(AppointmentStatusType)` (`BillingStatusWire.cs:36`). Phase 5 adds
  `NoShow => NO_SHOW` with no other change. No new enum is introduced: one would have exactly one
  member today and duplicate a distinction the status already carries.

  It THROWS on an unmapped status (following `EvaluationKindWire`, not `BillingStatusWire`) because
  it is only ever called when a successor exists, so an unmapped status means the caller's guard is
  wrong -- and a wrong CAUSE is worse than a missing one.

### T3 -- four fields on the payload DTO

- what: MODIFY `Payload/IntakePayload.cs` -- add beside the `Previous*` pair (`:65-68`):
  `Guid? RescheduledFromAppointmentId`, `string? RescheduledFromConfirmationNumber`,
  `Guid? SupersededByAppointmentId`, `string? SupersededReason`. XML docs must state that these are
  the RESCHEDULE chain and that `Previous*` remains the RE-EVALUATION chain -- the same
  disambiguation 4d wrote onto the entity.
- pattern: the `PreviousAppointmentId` / `PreviousConfirmationNumber` doc block, `IntakePayload.cs:64-68`.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL expose the four properties and SHALL serialize them camelCase
  with nulls present, per `IntakePayloadSerializer`.

### T4 -- resolve both directions of the chain

- what: MODIFY `Payload/AppointmentCoreResolver.cs` -- after the existing re-eval block (`:71-77`)
  add: (a) when `appointment.RescheduledFromAppointmentId` is set, look up that appointment and
  carry its `RequestConfirmationNumber` onto a new
  `AppointmentCoreSection.RescheduledFromConfirmationNumber`; (b) find the SUCCESSOR --
  `FirstOrDefaultAsync(a => a.RescheduledFromAppointmentId == appointment.Id)` -- and carry its id
  onto a new `AppointmentCoreSection.SupersededByAppointmentId`. MODIFY
  `Payload/IntakePayloadBuilder.cs:100-101` to assign all four, setting `SupersededReason` to
  `SupersededReasonWire.Rescheduled` ONLY when `SupersededByAppointmentId` is non-null. Fix the stale
  in-place comment at `AppointmentCoreResolver.cs:55-56` while here.
- pattern: the re-eval lookup immediately above, `AppointmentCoreResolver.cs:71-77`.
- approach: tdd
- acceptance (EARS): WHEN an appointment was created by a reschedule, THE SYSTEM SHALL populate
  `rescheduledFromAppointmentId` and `rescheduledFromConfirmationNumber`. WHEN an appointment was
  replaced by a reschedule, THE SYSTEM SHALL populate `supersededByAppointmentId` and
  `supersededReason = "RESCHEDULED"`. WHEN an appointment is neither, THE SYSTEM SHALL leave all
  four null AND SHALL leave `previousAppointmentId` untouched by this feature.

  **CORRECTED AT BUILD TIME (2026-08-06).** The task said "predicate query" and the obvious call is
  `_appointmentRepository.FirstOrDefaultAsync(a => ...)`, which the codebase uses in a dozen app
  services. It CANNOT be used here: it is an EXTENSION method
  (`RepositoryAsyncExtensions:80`, verified by decompiling `Volo.Abp.Ddd.Domain 10.0.2`), not an
  interface member, so NSubstitute cannot intercept it -- the arrangement compiles, silently does
  nothing, and the real extension runs against the substitute and returns null.

  Uses `GetListAsync(predicate, ...)` instead, which IS an interface member and therefore a real
  test seam. At most one row can match, since a closed appointment is terminal and cannot be
  rescheduled again, so taking the first narrows nothing.

  This surfaced as a FAILING test rather than a passing-but-wrong one only because the assertion was
  written before the implementation. Had the test been written after, the null would have looked
  like correct "no successor" behaviour. Generalisation worth carrying: **an extension method on a
  substituted interface is not a seam** -- same family as 4c's NSubstitute auto-mock learning.

### T5 -- payload snapshot + outbox-row proof

- what: MODIFY `test/.../Integration/CaseTracker/IntakePayloadBuilderTests.cs` -- add cases for
  both halves of a split, using the existing `sourceAppointment` plumbing (`:74-86`) plus one new
  substitute arrangement for the successor predicate query. CREATE a serialized-JSON snapshot
  assertion for the replacement's payload (the contract change IS the wire shape, so assert the
  wire, not just the object).

  **CORRECTED AT BUILD TIME (2026-08-06): the MultiOffice half of this task is DROPPED.** It was
  written as "invert 4d's zero-rows assertion", but no such test exists -- 4d asserted zero outbox
  rows in SQL during its LIVE GATE, not in the suite. Writing one now would prove nothing either
  way: the MultiOffice harness renders no packets, and `IntakeSettlePolicy.IsSettled` (`:44-60`)
  reports settled only when the packet set is complete OR the appointment is older than the
  30-minute cutoff. A freshly seeded appointment is neither, so nothing is enqueued regardless of
  suppression -- a "two rows" assertion would fail for an unrelated reason, and a "zero rows" one
  would pass vacuously while looking like proof.

  The enqueue decision is covered deterministically by the three Domain suites T1 touched (handler,
  packet-publish, sweep), and end-to-end by the live gate, where packets really do render -- as
  verified in 4d's gate, where the replacement's three packets rendered at 18:29:27.

- pattern: existing `IntakePayloadBuilderTests` facts for `previousAppointmentId`.
- approach: tdd
- acceptance (EARS): WHEN a reschedule is finalized, THE SYSTEM SHALL enqueue an intake row for the
  old appointment carrying `billingStatus` `NO_BILL` or `LATE` and `supersededByAppointmentId`, AND
  an intake row for the new appointment carrying `rescheduledFromAppointmentId`. THE SYSTEM SHALL
  serialize both link pairs as camelCase JSON with nulls present.

### T6 -- rewrite the contract

- what: MODIFY `docs/integration/case-tracker-api-contract.md`:
  1. §E2 `:416-419` -- replace the in-place paragraph with the two-case description: the old
     appointment closes to `RescheduledNoBill` / `RescheduledLate` with its billing status, and a
     NEW `appointmentId` opens a second case. Keep the superseded text struck through with its date,
     matching how §H and Coordination 3 already record reversals.
  2. §E2 `:424-429` -- the RESCHEDULE TRAP is now the opposite advice. Rewrite: a reschedule IS a
     status change, and a receiver keying off a changed date will now see a case that never moves.
  3. §A `:121` -- `Approved` after a reschedule now describes a DIFFERENT appointment.
  4. §A `:136-138` -- move `RescheduledNoBill` / `RescheduledLate` OUT of "NEVER sent" and INTO the
     received-status table as terminal-rescheduled.
  5. §A `data` table -- document the four new fields with source anchors, in the style of
     `previousAppointmentId` (`:112-113`).
  6. §B / §C -- state the shared-blob consequence (decision 3): one `objectKey` under two document
     `id`s across two cases, upsert by `id` as §G already requires.
  7. §H -- note two messages per reschedule and that the cap is unaffected.
  8. Coordination decision 4 (`:733`) and 7 (`:740`) -- restate the trigger set and add the
     reschedule linking facts alongside the re-eval ones.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL NOT state anywhere that the portal never creates a second
  appointment, and every wire field the code emits SHALL appear in §A with a source anchor.

### T7 -- tracker + deploy train

- what: MODIFY `docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md` -- set the 4e
  row DONE with its PR/sha, append 4e learnings, and record that the release is now
  **4b + 4c + 4d + 4e together** (decision 6) with the note that suppression is never active in
  production.
- pattern: the 4d entries added in `85593a2f`.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL record the four-phase release train and SHALL NOT leave 4e
  described as pending.

### T8 -- written change summary for Levon

- what: CREATE `docs/integration/case-tracker-two-case-reschedule-change-summary.md` -- what
  changes on the wire, what they must do (nothing, to keep working; read two fields, to gain the
  link), and what their staff will see. State plainly that their upsert already handles it because
  the case key is `appointmentId`.
- pattern: the contract's own voice -- decisions with their reasons, not a changelog.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL state the receiver-side impact as "no code change required"
  and SHALL list the four new fields with their meanings. Sending it is Adrian's call, not a build
  step; his sign-off is NOT a gate on this phase.

## Validation loop

Backend:

```
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
```

No migrations in this phase -- no entity changes. (`has-pending-model-changes` should stay clean;
if it does not, something was added that this plan did not intend.)

No Angular changes -- the chain is already surfaced by 4d's read side. `ng build` / `ng test` are
NOT required unless a task unexpectedly touches `angular/`.

Mutation checks (required):

- Delete the `SupersededByAppointmentId` assignment in `IntakePayloadBuilder`; confirm the old-half
  payload test fails and the new-half test does NOT.
- Point `RescheduledFromConfirmationNumber` at `OriginalAppointmentId` instead of the reschedule
  chain; confirm the snapshot test fails -- this is the mislabel decision 1 exists to prevent.

## Live gate

Reuse the 4d fixture path: falkinstein, a fresh Approved appointment with child rows, taken through
submit -> confirm -> consent -> finalize as internal staff at `admin.localhost:4200` -> Enter
practice.

1. `docker ps`; `docker compose up -d` if the stack is down. Restart `main-api-1` after building.
2. Finalize a reschedule.
3. Assert `AppIntegrationOutboxItems` now has intake rows for BOTH appointment ids -- the exact
   inverse of 4d's step 5.
4. Read the old row's payload JSON: `status` is `RescheduledNoBill`, `billingStatus` is `NO_BILL`,
   `supersededByAppointmentId` is the new appointment, `supersededReason` is `RESCHEDULED`.
5. Read the new row's payload JSON: `rescheduledFromAppointmentId` + `rescheduledFromConfirmationNumber`
   point at the old one, and `previousAppointmentId` is null (it is not a re-eval).
6. Confirm the reconcile GET returns the same four fields, since it shares the builder.

Note: nothing is DELIVERED -- their endpoints are not deployed to `.35` (contract item 11), so rows
will sit queued or dead-letter. That is expected and is not a failure of this phase.

## Risk / rollback

Blast radius: the Case Tracker publish path only. No entity, no migration, no Angular, no
appointment behaviour. The reschedule split itself is untouched -- 4e changes only what is said
about it.

1. **The contract becomes true only when this DEPLOYS.** Between merging 4e and deploying the
   release, the repo describes behaviour production does not have. Mitigated by decision 6: 4d and
   4e deploy together, so production never runs one without the other.
2. **Two cases per reschedule is a real change to their queue.** Their staff will see a closed case
   and a new one where they previously saw one case move. This is the intended outcome (staff need
   to bill or close the old date), but it is the change most likely to surprise, which is what T8
   exists for.
3. **Deleting suppression is irreversible in effect.** Once a replacement has been pushed, a revert
   does not unsend it. Rollback is therefore "revert the code, then tell them to close the extra
   case", not a clean undo -- another reason the two phases deploy together and are gated on a
   fresh go.
4. Removing `RescheduledNoBill` from "NEVER sent" means a receiver that hard-coded that assumption
   could reject the message. Their contract copy is the mitigation; T8 is the notice.

Rollback: `git revert` the squash-merge. No data migration to undo.
