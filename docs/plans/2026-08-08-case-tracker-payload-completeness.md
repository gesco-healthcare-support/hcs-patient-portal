---
feature: Case Tracker payload completeness (epic phase 6)
date: 2026-08-08
status: approved
base-branch: main
related-issues: []
---

# Phase 6 -- Case Tracker payload completeness

Supersedes `docs/plans/2026-08-05-case-tracker-payload-completeness.md`, which lives only on branch
`feat/case-tracker-payload-completeness` and was written five merges ago. Re-audit that produced this
plan: `docs/research/2026-08-08-case-tracker-payload-completeness-reaudit.md`.

Epic tracker: `docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md`.

## Goal

Give Case Tracker the fields it needs to show a complete case: the patient's address, who asked for a
change and who decided it, when each happened, and which side of the matter requested it.

## Decisions (all settled with Adrian, 2026-08-08, via modal)

- **Decision 1 -- adopt `MarkDecided` and LOCK the setters.** `RequestStatus`, `ApprovedById`,
  `RejectedById` and `DecidedAt` together ARE the legal record of a decision, and today every one of
  them has a public setter -- nothing in the type stops a future code path recording "rejected"
  without who or when. Adrian: this is "logs for proper legal processes". The type must make a
  half-recorded decision impossible; convention is not enough.
  This REVERSES the recommendation first given (which argued from "4d already shipped it", i.e. cost
  rather than correctness -- Adrian rejected that framing outright).
- **Decision 2 -- the backfill must be EXACT, never approximated.** Adrian: "we do not want to
  approximate anything, This is logs for proper legal processes, we want them to be exact." The
  original plan's `LastModificationTime` source is therefore REJECTED: it bumps on any write, so a
  request edited after its decision would get a wrong-but-plausible timestamp.
  **An exact source exists.** ABP's audit trail records the real status transitions:
  `AbpEntityPropertyChanges.PropertyName = 'RequestStatus'` with `NewValue` 26 (Accepted) or 27
  (Rejected), joined to `AbpEntityChanges.ChangeTime`. Verified live 2026-08-08 -- e.g.
  `25 -> 26 at 2026-08-06 18:28:32.210`. Rows with no audit record stay NULL.
- **Decision 3 -- document D2** ("every push is a full snapshot"). Phase 6 assigned it to 4e; 4e
  dropped it. Verified 2026-08-08 that the contract contains no such statement. It determines whether
  their receiver may overwrite wholesale or must merge, so it cannot stay implicit.
- **Decision 4 -- the patient address columns are NOT ambiguous.** The old plan claimed no convention
  for which is line 1 and no unit field. Both are wrong. From the booking form
  (`appointment-add-patient-demographics.component.html:211-223`):
  - `Patient.Street` -- labelled "Street", backed by the address autocomplete. STREET LINE 1.
  - `Patient.Address` -- labelled **"Unit #"**. The APARTMENT / SUITE number.
    Shipping the old assumption would have handed Case Tracker a bare unit number in a field called
    `address`; their receiver would very likely render "4B" as a street address.
- **Decision 5 -- phase 6 ships AFTER the current release.** 4b/4c/4d/4e/phase-5 deploy first and get
  verified in a real office; phase 6 changes the payload SHAPE, and bundling both would mean one
  deploy carrying new lifecycle behaviour AND a new contract with no way to bisect a problem.

## Context

### What is already done (do NOT rebuild)

- `AppointmentChangeRequest.DecidedAt` EXISTS (`:118`), shipped by 4d, with both migrations applied
  (`20260806172525` host, `20260806172601` tenant). Only the BACKFILL is missing.
- `ChangeRequestSide { SideA = 1, SideB = 2 }` EXISTS, as does `RequestingSide` (`:127`). Only the
  WIRE mapping is missing. Side A = Patient + Applicant Attorney; Side B = Defense Attorney + Claim
  Examiner.
- Every attribution input EXISTS: `SubmittedByUserId:133`, `ApprovedById:73`, `RejectedById:71`,
  `DecidedAt:118`, `RequestingSide:127`.
- The epic tracker's phase 6 row was already added during phase 5.

### Patterns to mirror

| Need                                        | Anchor                                                                       |
| ------------------------------------------- | ---------------------------------------------------------------------------- |
| Wire enum with a throwing switch (OUTBOUND) | `Payload/EvaluationKindWire.cs`                                              |
| State id -> state NAME                      | `Payload/PartyDetailResolver.StateNameOrNull:94-95` -- reuse, do not rewrite |
| A resolver that batches its lookups         | `Payload/PartyDetailResolver.cs`                                             |
| Payload section shape + comment density     | `Payload/IntakePayload.cs:38-43`                                             |
| Dual-context migration pairing              | 4d's `Added_ChangeRequestDecidedAt` in both sets                             |

### Gotchas

- `IntakeLocationSection` ALSO has `Address` / `City` / `ZipCode`. Those are the CLINIC's. The patient
  section is a different class in the same file -- easy to edit the wrong one.
- `AppointmentStatusType` and `RequestStatusType` are persisted as ints and are NOT contiguous.
  `RequestStatusType`: Pending = 25, Accepted = 26, Rejected = 27.
- The audit tables live in the SAME database as the change requests (verified: `AbpEntityChanges`
  present in `CaseEvaluation_falkinstein`), so the backfill runs per-context like any other migration.
- `AbpEntityChanges.EntityId` is a STRING column. Comparing it to a `uniqueidentifier` needs an
  explicit convert, and the format (braces / casing) MUST be verified against real rows before the
  migration is trusted -- a silently non-matching join would leave every row NULL and look like
  "no audit data".

## Tasks

### T1 -- `MarkDecided` and locked setters

- what: MODIFY `Domain/AppointmentChangeRequests/AppointmentChangeRequest.cs` -- add
  `MarkDecided(RequestStatusType outcome, Guid? decidedById, DateTime nowUtc)` that sets
  `RequestStatus`, the matching `ApprovedById`/`RejectedById`, and `DecidedAt` together. Change those
  four setters to `protected set`. MODIFY every caller, starting with
  `AppointmentChangeRequestsAppService.Approval.cs` `PersistChangeRequestAsync`, which is the single
  seam all four decision paths already pass through.
- pattern: the entity's existing `protected set` properties (`AppointmentId:44`,
  `ChangeRequestType:46`, `RequestingSide:127`) -- the pattern is already in this file.
- approach: tdd
- acceptance (EARS): WHEN a change request is decided, THE SYSTEM SHALL set status, deciding user and
  decided-at in one call. WHERE a caller attempts to set any of those four properties directly, THE
  SYSTEM SHALL fail to compile. WHEN the outcome is neither Accepted nor Rejected, THE SYSTEM SHALL
  throw rather than record a decision.

### T2 -- exact `DecidedAt` backfill (both contexts)

- what: CREATE a NEW migration in BOTH sets (the 4d one is applied and cannot be edited) that fills
  `DecidedAt` from the audit trail. FIRST verify the `EntityId` format against real rows, then join
  `AbpEntityPropertyChanges` (`PropertyName = 'RequestStatus'`, `NewValue` in 26/27) to
  `AbpEntityChanges` and take the LATEST matching `ChangeTime` per request.
  Rows with no audit record stay NULL -- that is the point, not a shortfall.
- pattern: 4d's paired migrations for the file layout; raw SQL via `migrationBuilder.Sql`.
- approach: code
- acceptance (EARS): WHEN a decided change request has an audit record of its status transition, THE
  SYSTEM SHALL set `DecidedAt` to that transition's exact time. WHERE no audit record exists, THE
  SYSTEM SHALL leave `DecidedAt` NULL. THE SYSTEM SHALL NOT derive `DecidedAt` from
  `LastModificationTime` under any circumstance. THE SYSTEM SHALL NOT modify a non-NULL `DecidedAt`.

### T3 -- patient address on the payload DTO

- what: MODIFY `Payload/IntakePayload.cs` -- add to `IntakePatientSection` (NOT
  `IntakeLocationSection`): `Street`, `Unit`, `City`, `State`, `ZipCode`, all `string?`.
  **Name the wire field `unit`, not `address`** -- it carries `Patient.Address`, which the UI labels
  "Unit #". Doc-comment that mapping explicitly, because the column name actively misleads.
- pattern: `IntakePayload.cs:38-43` for comment density.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL expose the patient's street, unit, city, state and zip as
  separate fields, and SHALL NOT emit the unit number in a field named `address`.

### T4 -- resolve the patient address

- what: MODIFY the patient section builder -- map `Street -> street`, `Address -> unit`,
  `City -> city`, `ZipCode -> zipCode`, and `StateId -> state` as a NAME via the existing
  `StateNameOrNull`. Batch the state lookup as `PartyDetailResolver` already does.
- approach: tdd
- acceptance (EARS): WHEN a patient has a state, THE SYSTEM SHALL send its NAME not its id. WHERE any
  address part is absent, THE SYSTEM SHALL send null for that part and still send the others.

### T5 -- `ChangeRequestSideWire`

- what: CREATE `Payload/ChangeRequestSideWire.cs` mapping `ChangeRequestSide` to `SIDE_A` / `SIDE_B`
  with a THROWING switch. Outbound, so throwing is correct here (contrast
  `AttendanceOutcomeWire.TryParse`, which is inbound and must 400 instead).
- pattern: `EvaluationKindWire.cs`.
- approach: tdd
- acceptance (EARS): WHEN the requesting side is known, THE SYSTEM SHALL send its wire value. WHERE
  the value is not a known side, THE SYSTEM SHALL throw.

### T6 -- attribution and timestamps on the payload DTO

- what: MODIFY `Payload/IntakePayload.cs` -- add the change-attribution fields:
  `changeRequestedBySide`, `changeRequestedAtUtc`, `changeFinalizedAtUtc`, and the deciding user.
  Timestamps ISO-8601 UTC via the existing `IntegrationTimestamp` helper.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL expose who requested a change, which side they were on, when it
  was requested and when it was finalized.

### T7 -- resolve attribution and timestamps

- what: MODIFY the payload builder to fill T6 from the selected change request. Selection rule: the
  request the payload is ABOUT. `changeFinalizedAtUtc` comes from `DecidedAt` -- NEVER
  `LastModificationTime`.
- approach: tdd
- acceptance (EARS): WHEN the selected request is still Pending, THE SYSTEM SHALL send
  `changeFinalizedAtUtc` as null. WHEN it is decided, THE SYSTEM SHALL send its `DecidedAt`.

### T8 -- contract updates (RE-ANCHORED)

- what: MODIFY `docs/integration/case-tracker-api-contract.md`. The old plan quotes text that no
  longer exists -- 4e rewrote sections A and E2 and phase 5 added section K. Add the new payload
  fields to section A; document the patient address semantics INCLUDING that `unit` is a unit number
  and that street is line 1; document D2 (every push is a full snapshot) after verifying it against
  `IntakePayloadBuilder`; state that `changeFinalizedAtUtc` is null for requests decided before this
  release where no audit record existed.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL document every new field, and SHALL NOT describe the patient
  address as ambiguous.

### T9 -- correct two stale documents

- what: `case-tracker-correspondence/reply-4-sent.md` -- change `DRAFT - NOT SENT` to a sent marker
  with the date, body verbatim. `case-tracker-open-items.md` -- fix the patient-address description
  per decision 4 and mark N1-N4 / D1 as this phase's.
- approach: code

## Validation loop

```
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
```

No Angular in this phase. Migrations: confirm with `has-pending-model-changes` on BOTH contexts that
nothing drifted, and verify the backfill against real audit rows in a seeded office database rather
than trusting the generator.

Mutation checks (required):

- Point the backfill at `LastModificationTime` instead of the audit trail; confirm the exactness test
  fails. That pair is the whole of decision 2.
- Make `MarkDecided` skip the actor; confirm a test fails rather than a decision being half-recorded.
- Map `Patient.Address` to `street`; confirm the unit/street test fails. That is decision 4.

## Live gate

Deferred until AFTER the current release is deployed and verified (decision 5).

## Risk / rollback

Blast radius: the outbound payload shape, one entity's encapsulation, and a data backfill.

1. **T1 touches merged 4d code.** Locking setters is a compile-time change, so every caller surfaces
   at build time rather than at runtime -- the safest kind of refactor, but the diff will be wider
   than it looks.
2. **The backfill writes historical rows.** It only ever fills NULLs and never overwrites, so it is
   re-runnable; but verify the `EntityId` join format FIRST, because a non-matching join looks
   identical to "no audit data".
3. **New payload fields are a contract change.** Levon must be told before this deploys, not after.
