---
feature: Case Tracker payload completeness (epic phase 6; N1-N4 + D1)
date: 2026-08-05
status: draft
base-branch: main
related-issues: []
---

# Phase 6 -- Case Tracker payload completeness

PHASE 6 of the 2026-07-31 reschedule/cancel/calendar epic. INDEPENDENT: it runs parallel to 4d and is
NOT part of the sequential 4a-4e chain.

Numbered 6 because 5 is already taken by the no-show round trip. The number carries no ordering --
phases 1, 2 and 3 were likewise mutually independent, and only 4a-4e is a chain.

Why it belongs to the epic rather than standing alone: it arises from the same Levon correspondence,
amends the same contract document, and the epic roadmap is the living tracker for all Case Tracker
work. A second tracker outside it would drift.

Why it is independent rather than sequential: every item is an ADDITIVE wire field. The epic's own
Case Tracker note records that additive fields need no coordinated release, because a receiver that
ignores unknown fields stays correct, and that only 4e's two-case reschedule is a genuine contract
break. Nothing here touches reschedule machinery, so making it wait behind 4d would couple small
independent work to the epic's longest pole while N3 blocks Levon's proof-of-service document today.

## Goal

Close the four outstanding additive payload gaps Levon asked for, plus one contract clarification he
has accepted, so his proof-of-service document and billing attribution stop depending on data we hold
but do not send.

Independent of 4d/4e because every item is an ADDITIVE wire field. The epic's own Case Tracker note
records that additive fields need no coordinated release -- a receiver that ignores unknown fields
stays correct -- and that only 4e's two-case reschedule is a genuine contract break. Nothing here
touches reschedule machinery.

## Context & decisions

### Resolved decisions (Adrian, 2026-08-05)

1. **`reply-4-sent.md` WAS sent.** Settled by evidence, not memory: that file is the only place we
   asked the MinIO hostname-vs-bare-IP question, proposed "prune only on a 200", and offered the
   unconditional document re-send. Levon's next email answered all three. Its `DRAFT - NOT SENT`
   first line is stale and is corrected as T7 of this plan. Consequence: Levon DOES already know
   about `billingStatus`, `data.doctor.id` and the patient-address commitment.

2. **Staff DO perform cancel-and-rebook by hand for a type change.** The system does not model it --
   `AppointmentManager.UpdateAsync` edits `AppointmentTypeId` in place on the same row
   (`AppointmentManager.cs:226`), with no cancel, no rebook and no second appointment. So R2 (a
   type-change link) is REAL but is not a wire field: it needs a staff-facing "this replaces
   appointment X" action, which is 4d/4e-shaped machinery. **R2 is OUT OF SCOPE here** and is flagged
   for the epic. Separately, `reply-4-sent.md` Q19 told Levon our workflow performs the
   cancel-and-rebook and therefore knows the two are the same exam -- that is wrong and needs
   correcting to him when R2 is scoped.

3. **N4 sends BOTH timestamps** -- the original request and the current consent round -- rather than
   choosing for him. He asked for everything we hold, and his 6-business-day rule is his to compute.

4. **The epic roadmap gets a row for this phase now.** Adrian's call. T8 does it, committed by
   explicit pathspec because that file is shared with the session owning 4d.

### Design decisions taken while writing this plan

5. **`autoCancelled` is DERIVED, not persisted. No migration.** `Appointment.CancelledById`
   (`Appointment.cs:167`) is set to `CurrentUser.Id` on a human cancellation
   (`AppointmentChangeRequestsAppService.Approval.cs:119`) and is NEVER set by the AME auto-cancel
   job, which writes only status (`JointDeclarationAutoCancelJob.cs:175`) and reason (`:180`). So
   "cancelled AND `CancelledById is null`" identifies an auto-cancel from existing persisted state.
   This avoids a new column, and a new column on `Appointment` would have needed migrations in BOTH
   the host and tenant migration sets.

   Why a derived value is still an improvement over what Levon has today: he currently must
   string-match the English sentence in `CancellationReason`, which any copy-edit silently breaks.
   The wire field is explicit; only our internal derivation is implicit, and it rests on a structural
   fact rather than on prose.

6. **Attribution is split into TWO fields, because two different actors exist.** Levon asked for "who
   cancelled -- party type at minimum". The portal records:
   - WHO ASKED: `AppointmentChangeRequest.RequestingSide` (`:113`), a `ChangeRequestSide`.
   - WHO DECIDED: `Appointment.CancelledById`, which is the STAFF member who approved it.

   Sending only one would misrepresent the other. So the payload carries `requestedBySide` and
   `decidedByName`.

   **Honest limitation to state in the contract:** `ChangeRequestSide` is COARSER than the party list
   Levon asked for. SideA covers patient + applicant attorney; SideB covers defense attorney + claim
   examiner (`ChangeRequestSide.cs`). We cannot say which of the two a side's request came from,
   because the portal does not record it. Do not invent precision we do not have.

7. **The local timezone we send is portal-wide, not per clinic.** The only timezone the portal holds
   is the setting `Notifications.Reminders.TimezoneId`
   (`CaseEvaluationSettings.cs:143`), which is reminders-scoped. `AppointmentDateLocal` /
   `AppointmentTimeLocal` are naive local values stored as slot date + `FromTime` with no zone
   attached, so the zone we send is an ASSERTION about how to read them, not the product of a
   conversion. Send the setting value as `localTimezoneId`, null when unset, and say so in the
   contract. A per-clinic timezone would be a new field on `Location` and is not in scope.

8. **Patient address sends BOTH `Address` and `Street` verbatim.** `Patient` has both as separate
   nullable strings (`Patient.cs:51` and `:65`) with NO `Unit` field. Which is line 1 versus line 2 is
   not recorded anywhere. Levon explicitly asked for everything we hold and said extra fields cost him
   nothing, so both go over with our own names and the ambiguity documented, rather than us guessing a
   mapping into a single formatted address.

   NOTE: `docs/integration/case-tracker-open-items.md` describes this as "street, unit, city, state,
   zip". That is WRONG -- there is no `Unit`, and `Address` is a separate field. T7 corrects it.

## All needed context

### Verified anchors (re-checked against `main` at `2ce2ef3f`, 2026-08-05)

| Fact                                                                                                                                                                 | Anchor                                          |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------- |
| `IntakePatientSection` has no address fields                                                                                                                         | `IntakePayload.cs:161-192`                      |
| `Address` / `City` / `ZipCode` already on the payload belong to `IntakeLocationSection` -- the CLINIC. Easy to misread as the patient's                              | `IntakePayload.cs:141-145`                      |
| `CancellationReason` on the payload, nullable                                                                                                                        | `IntakePayload.cs:50`                           |
| `BillingStatus` on the payload, non-nullable, defaults `NONE`                                                                                                        | `IntakePayload.cs:44`                           |
| Only timestamps sent today                                                                                                                                           | `IntakePayload.cs:53,56,59`                     |
| Patient address columns: `Address`, `City`, `ZipCode`, `Street`, `StateId`                                                                                           | `Patient.cs:51,54,57,65,76`                     |
| `Appointment.CancelledById`                                                                                                                                          | `Appointment.cs:167`                            |
| Human cancel writes `CancelledById`                                                                                                                                  | `Approval.cs:119`                               |
| Auto-cancel writes status and reason only, never `CancelledById`                                                                                                     | `JointDeclarationAutoCancelJob.cs:175,180`      |
| `AutoCancelReasonText` is the prose sentence that reaches the wire; `AutoCancelReason` is an internal routing discriminator that does not                            | `JointDeclarationAutoCancelJob.cs:62` and `:54` |
| `AppointmentChangeRequest`: `AppointmentId`, `ChangeRequestType`, `RequestStatus`, `ApprovedById`, `RequestingSide`                                                  | `AppointmentChangeRequest.cs:44,46,65,73,113`   |
| `AppointmentChangeRequest` is `FullAuditedAggregateRoot<Guid>, IMultiTenant` so `CreationTime` / `LastModificationTime` exist and it is tenant-filtered              | `AppointmentChangeRequest.cs:40`                |
| `ChangeRequestConsentRound` is `FullAuditedAggregateRoot<Guid>, IMultiTenant`; current round is `SupersededAt == null`                                               | `ChangeRequestConsentRound.cs:26,60`            |
| `IAppointmentChangeRequestRepository` is a plain `IRepository<AppointmentChangeRequest, Guid>` -- no custom methods, so `GetListAsync(predicate)` is the access path | `IAppointmentChangeRequestRepository.cs:6`      |
| `AppointmentCoreResolver` has THREE constructor deps, so it has room                                                                                                 | ctor of `AppointmentCoreResolver.cs`            |
| `IntakePayloadBuilder` already has SEVEN deps -- at the repo's DI ceiling. Do NOT add an eighth                                                                      | ctor of `IntakePayloadBuilder.cs`               |
| Reminder timezone setting                                                                                                                                            | `CaseEvaluationSettings.cs:143`                 |

### Patterns to mirror

- **State-name resolution:** collect nullable `Guid?` ids, filter to those with values, `Distinct()`,
  one batched query, then a `StateNameOrNull(id, names)` helper at the point of use.
  `PartyDetailResolver.cs:72-82` for the collection, `:112` for the call site. N3 mirrors this exactly.
- **Wire enum mapping:** an explicit `const string` per value plus a `switch` expression, NEVER
  `ToString()`, so an internal rename cannot change the wire. `BillingStatusWire.cs` is the closest
  precedent because, like the new side mapper, it must not throw on an unmapped value.
  `EvaluationKindWire.cs` is the variant that DOES throw. Follow `BillingStatusWire`.
- **Timestamp formatting:** `IntegrationTimestamp.ToIsoUtcOrNull` for nullable instants. It treats
  `DateTimeKind.Unspecified` as already-UTC, which is correct only because the API container runs UTC.
- **Payload section doc comments:** every existing field carries a `<summary>` saying what it is AND
  why, e.g. `IntakePayload.cs:38-43` for `BillingStatus`. Match that density.

### Gotchas

- `IntakePayload` lives in **Domain**, not Application.Contracts, so it is NOT an ABP app-service DTO.
  No `abp generate-proxy` run is needed and no Angular file changes. This phase is backend-only.
- There is exactly ONE payload builder with two callers -- `CaseTrackerIntakeQueue` (push) and
  `CaseTrackerReconcileService` (reconcile GET). Both get every new field automatically. Do not add a
  second construction path.
- A cancellation has NO consent rounds; rounds exist only for reschedules. `currentRoundProposedAtUtc`
  must be null for a cancellation rather than throwing or defaulting.
- The AME auto-cancel has NO change request at all, so `requestedBySide` and the request timestamps are
  null for it. That is correct, and combined with `autoCancelled: true` it is unambiguous.
- Tests that assert an exact payload field list exist and WILL break on an additive change. Expect to
  update `IntakePayloadBuilderTests`.
- Commit by explicit pathspec. The worktree is shared and its branch has moved underneath a session
  twice. Run `git branch --show-current` and `git status --short` immediately before every commit.

## Tasks

### T1 -- patient address fields on the payload DTO

approach: code

Add to `IntakePatientSection` (`IntakePayload.cs:161-192`), after `CellPhoneNumber` and before
`SamePersonGroupKey`: `Address`, `Street`, `City`, `State`, `ZipCode`, all `string?`.

Doc comment must state that `Address` and `Street` are separate columns in the portal with no recorded
convention for which is line 1, that there is no unit/apartment field, and that `State` is a resolved
name rather than a code. Mirror the comment density of `IntakePayload.cs:38-43`.

### T2 -- resolve the patient address, including the state name

approach: tdd

`PartyResolver.ResolvePatientAsync` builds `IntakePatientSection`. Add a
`IRepository<State, Guid>` dependency (it currently has two deps, so this is within the ceiling) and
populate the five new fields from `Patient.Address`, `.Street`, `.City`, `.StateId`, `.ZipCode`
(`Patient.cs:51,65,54,76,57`).

Resolve the state name by mirroring `PartyDetailResolver.cs:72-82` and `:112`: one batched query for
the single `StateId`, then `StateNameOrNull`. A single id does not need the full batching machinery,
but use the same helper shape so the two resolvers read alike.

Tests: address fields populated; null `StateId` yields null `State`; a `StateId` with no matching row
yields null rather than throwing.

### T3 -- `ChangeRequestSideWire`

approach: tdd

New file beside `BillingStatusWire.cs`. Map `ChangeRequestSide` to `SIDE_A` / `SIDE_B`. Follow
`BillingStatusWire`'s non-throwing shape -- return null for an unmapped or null input rather than
throwing, because a cancellation without a change request legitimately has no side.

The doc comment must record WHY the wire value is a side rather than a party: SideA covers patient +
applicant attorney and SideB covers defense attorney + claim examiner (`ChangeRequestSide.cs`), and the
portal does not record which of the two within a side actually asked. This is the honest limitation
from decision 6 and the contract repeats it.

Tests: both sides map; null maps to null; an out-of-range value maps to null and does not throw.

### T4 -- attribution and change timestamps on the payload DTO

approach: code

Add to `IntakePayload`, immediately after `CancellationReason` (`IntakePayload.cs:50`):

| Field                       | Type      | Meaning                                                                                                                                  |
| --------------------------- | --------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `autoCancelled`             | `bool`    | True only for the AME joint-declaration auto-cancel. Non-nullable, defaults false, so the receiver never distinguishes absent from false |
| `requestedBySide`           | `string?` | `SIDE_A` / `SIDE_B`. Who ASKED. Null when there is no change request                                                                     |
| `decidedByName`             | `string?` | The staff member who applied the outcome. Null for an auto-cancel                                                                        |
| `changeRequestedAtUtc`      | `string?` | ISO-8601 UTC. The ORIGINAL change request's creation time                                                                                |
| `currentRoundProposedAtUtc` | `string?` | ISO-8601 UTC. The current consent round's creation time. Always null for a cancellation                                                  |
| `changeFinalizedAtUtc`      | `string?` | ISO-8601 UTC. When the request stopped being Pending                                                                                     |
| `localTimezoneId`           | `string?` | The zone in which `appointmentDateLocal` / `appointmentTimeLocal` should be read. Portal-wide, not per clinic                            |

Each comment states the limitation, not just the meaning -- decisions 6 and 7 exist to be written down
here.

### T5 -- resolve attribution and timestamps

approach: tdd

`AppointmentCoreResolver` already owns `Status`, `BillingStatus`, `CancellationReason` and the existing
timestamps, so this belongs there rather than in a new resolver -- and `IntakePayloadBuilder` cannot
take an eighth dependency.

Add exactly four dependencies to `AppointmentCoreResolver`: `IAppointmentChangeRequestRepository`,
`IChangeRequestConsentRoundRepository`, `IIdentityUserRepository` for the staff name, and
`ISettingProvider` for the timezone. It has three today, so the total becomes SEVEN, which is the
repo's ceiling for a DI constructor -- at the limit, not over it. Do not add a fifth; if a later
field needs one, extract a helper resolver instead.

`IIdentityUserRepository` is usable from Domain and there is a precedent in this same feature area:
`CaseTrackerFailureAlertJob.cs:59,70` already takes it. Mirror that injection.

Resolution rules:

- `autoCancelled` = the status is a cancelled status AND `Appointment.CancelledById is null`
  (decision 5). Do NOT read `CancellationReason`.
- `decidedByName` = display name for `Appointment.CancelledById`, null when that is null.
- The change request = the most recent `AppointmentChangeRequest` for this appointment whose
  `RequestStatus` is not Pending, else the Pending one if that is all there is. Order by
  `CreationTime` descending and take the first, so a second request after a rejected one wins.
- `changeRequestedAtUtc` = that request's `CreationTime`. `changeFinalizedAtUtc` = its
  `LastModificationTime` when `RequestStatus != Pending`, else null.
- `currentRoundProposedAtUtc` = `CreationTime` of the round for that request with
  `SupersededAt == null`, ordered by `RoundNumber` descending. Null when the request is a Cancel.
- `localTimezoneId` = the `Notifications.Reminders.TimezoneId` setting, null when unset.
- Format every instant with `IntegrationTimestamp.ToIsoUtcOrNull`.

Tests: auto-cancel yields `autoCancelled` true with null side and null decider; a human cancel yields
false with a side and a decider; a cancellation yields null `currentRoundProposedAtUtc`; a reschedule
with two rounds yields the non-superseded one; a Pending request yields null
`changeFinalizedAtUtc`; an appointment with no change request yields nulls throughout and does not
throw.

### T6 -- contract updates

approach: code

`docs/integration/case-tracker-api-contract.md`:

- `data.patient` table: the five new address fields, with decision 8's `Address`-versus-`Street`
  ambiguity and the absence of a unit field stated explicitly.
- Section A field table: the seven new top-level fields from T4, each with its limitation.
- **D1**, the item Levon has accepted: a non-200 reconcile response carries NO document information and
  must NEVER drive document pruning. Prune only on a 200. State that the 404's ambiguity between
  unknown appointment, unknown office and switched-off office is DELIBERATE anti-enumeration
  (`CaseTrackerReconcileService.cs:48-51`) and is not to be weakened.
- Do NOT add D2 (every push is a full snapshot). It belongs in section E2, which 4e rewrites; writing
  it here means editing a section that is about to be replaced.

### T7 -- correct two stale documents

approach: code

- `docs/integration/case-tracker-correspondence/reply-4-sent.md`: change the first line from
  `DRAFT - NOT SENT` to a sent marker with the date, per resolved decision 1. Leave the body verbatim;
  it is a record of what was sent.
- `docs/integration/case-tracker-open-items.md`: fix the patient address description per decision 8
  (no `Unit`; `Address` and `Street` are separate), and mark N1-N4 and D1 as belonging to this phase.

### T8 -- epic roadmap row

approach: code

Add this row to the Phase table in
`docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md`, after the phase 5 row:

| 6 | Case Tracker payload completeness (N1-N4 + D1) | `feat/case-tracker-payload-completeness` | `2026-08-05-case-tracker-payload-completeness.md` | TODO -- INDEPENDENT, runs parallel to 4d |

Also amend the line below the table that reads "Phases 1, 2 and 3 are mutually independent. 4a-4e are
strictly sequential" so it names phase 6 as independent too. Otherwise a later reader will assume 6
follows 5, which is exactly the coupling this phase exists to avoid.

Also record in the roadmap's learnings that R2 (type-change marker) is BLOCKED on staff-facing
machinery rather than a wire field, and that reply-4's Q19 answer to Levon needs correcting -- decision 2.

**Run `git status --short` first.** That file is shared with the session owning 4d. Commit it by
explicit pathspec, alone, and leave any file you did not create.

## Acceptance (EARS)

- WHEN an intake payload is built for any appointment, THE SYSTEM SHALL include the patient's
  `address`, `street`, `city`, `state` and `zipCode`, using null for each value the portal does not
  hold.
- WHEN a patient has a `StateId` that resolves to a state row, THE SYSTEM SHALL send that state's NAME;
  and WHEN the id is null or unresolvable, THE SYSTEM SHALL send null rather than throwing.
- WHEN an appointment was cancelled by the AME joint-declaration auto-cancel job, THE SYSTEM SHALL send
  `autoCancelled` true, `requestedBySide` null and `decidedByName` null.
- WHEN an appointment was cancelled by a human, THE SYSTEM SHALL send `autoCancelled` false, the
  requesting side, and the name of the staff member who applied the outcome.
- WHEN an appointment has never been cancelled, THE SYSTEM SHALL send `autoCancelled` false.
- WHEN a change request exists for the appointment, THE SYSTEM SHALL send `changeRequestedAtUtc` as
  that request's creation time in ISO-8601 UTC.
- WHEN that request is no longer Pending, THE SYSTEM SHALL send `changeFinalizedAtUtc`; and WHILE it is
  Pending, THE SYSTEM SHALL send null.
- WHEN the change request is a reschedule with at least one consent round, THE SYSTEM SHALL send
  `currentRoundProposedAtUtc` from the round whose `SupersededAt` is null.
- WHEN the change request is a cancellation, THE SYSTEM SHALL send `currentRoundProposedAtUtc` as null.
- WHEN the reminder timezone setting has a value, THE SYSTEM SHALL send it as `localTimezoneId`; and
  WHEN it is unset, THE SYSTEM SHALL send null.
- WHEN an appointment has no change request at all, THE SYSTEM SHALL send null for every attribution
  and change-timestamp field and SHALL NOT throw.
- WHEN the reconcile endpoint returns any non-200, THE CONTRACT SHALL state that the response carries
  no document information and must not drive pruning.

## Validation loop

Backend only -- `IntakePayload` is a Domain type, not an app-service DTO, so no proxy regeneration and
no Angular build or specs are required. Run every command; a build proves it compiles, only the tests
prove nothing broke.

```bash
cd /c/src/patient-portal/main && dotnet format --verify-no-changes
```

```bash
cd /c/src/patient-portal/main && dotnet build HealthcareSupport.CaseEvaluation.slnx -c Release -warnaserror
```

```bash
cd /c/src/patient-portal/main && dotnet test HealthcareSupport.CaseEvaluation.slnx -c Release --no-build
```

```bash
cd /c/src/patient-portal/main && python .claude/scripts/verify_structure.py
```

Re-establish the test baseline on the first run rather than trusting a recorded number: the handoff
cites 1800 backend and 432 Angular, while the 4c learnings cite 475 frontend specs. Record what this
branch actually starts from before judging whether anything broke.

Expect `IntakePayloadBuilderTests` to need updating -- it asserts an exact field set and this change is
additive.

## Out of scope, deliberately

- **R2 type-change marker.** Real, but needs a staff-facing "replaces appointment X" action, not a wire
  field. Decision 2. Belongs with 4d/4e.
- **R1 reschedule sequence / count.** Meaningless until 4d creates a chain.
- **D2 every push is a full snapshot.** Assigned to 4e, which rewrites the section it belongs in.
- **D3 status list five to seven** and **D4 reschedule link semantics.** Both 4e by definition.
- **Per-clinic timezone.** Decision 7 sends the portal-wide setting; a `Location`-level zone is a new
  field and a separate decision.
- **Deploying anything.** Adrian has explicitly declined a deploy. The cascade PR #410 is not to be
  merged.
