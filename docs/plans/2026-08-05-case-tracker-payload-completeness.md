---
feature: Case Tracker payload completeness (epic phase 6; N1-N4 + D1)
date: 2026-08-05
revised: 2026-08-07
status: ready
base-branch: main
branch: feat/case-tracker-payload-completeness
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

Why it is independent rather than sequential: every WIRE item is an ADDITIVE field. The epic's own
Case Tracker note records that additive fields need no coordinated release, because a receiver that
ignores unknown fields stays correct, and that only 4e's two-case reschedule is a genuine contract
break. Nothing here touches reschedule machinery, so making it wait behind 4d would couple small
independent work to the epic's longest pole while N3 blocks Levon's proof-of-service document today.

## FOR THE IMPLEMENTING SESSION -- read this first

- **The branch already exists.** `feat/case-tracker-payload-completeness`, cut from `main`, currently
  holds one commit: this plan file (`7025f502`). Do NOT create a new branch and do NOT re-base it
  somewhere else. `git switch feat/case-tracker-payload-completeness`.
- **The worktree at `C:/src/patient-portal/main` is SHARED** with a session working epic phase 4d on
  `feat/reschedule-creates-new-appointment`. Run `git branch --show-current` and `git status --short`
  immediately before every commit. Stage by explicit pathspec only. NEVER `git add -A` and never a
  bare `git commit`. Leave any file you did not create. Do not create a git worktree.
- **Do not deploy.** Adrian has explicitly declined a deploy, and the `main -> development` cascade
  PR #410 is not to be merged. Do not raise deploying unless he raises it first.
- **ASCII only** in code, comments, docs and commit messages.
- This plan was audited against `main` at `2ce2ef3f` on 2026-08-07 and eight defects in the first
  draft were corrected. The `## Audit trail` section at the bottom records what changed and why, so
  you do not re-derive it or reintroduce it.
- **Phase 4e is IN FLIGHT and rewriting the same contract file T8 edits.** Read the coordination
  section below before you touch `docs/integration/case-tracker-api-contract.md`.

### Coordination with phase 4e (IN FLIGHT as of 2026-08-07)

The other session moved from 4d to 4e during this plan's revision. Observed live in the shared
worktree: branch `feat/case-tracker-two-case-reschedule`, with
`docs/plans/2026-08-06-case-tracker-two-case-reschedule.md`,
`docs/integration/case-tracker-two-case-reschedule-change-summary.md`, and a heavily modified
`docs/integration/case-tracker-api-contract.md`. The epic roadmap was briefly in an unresolved
merge conflict (`UU`), so that file is being actively merged too.

`git diff main...feat/case-tracker-two-case-reschedule -- docs/integration/case-tracker-api-contract.md`
shows roughly twenty hunks spread across the whole contract, including the section-A field-table
region and the claim/party tables. The first draft of this plan called the two "mostly disjoint".
That assessment predates 4e having a plan and is now WRONG for the contract file specifically.

Consequences for this phase, none of which change its code scope:

- **Do T8 LAST**, after every code task is green, and immediately after
  `git fetch origin && git rebase origin/main`. Rebasing first means you edit whatever version of the
  contract actually survived, instead of resurrecting a stale one.
- **Phase 6 only ADDS to the contract.** If you hit a conflict there, 4e's text is authoritative for
  sections E2, section A's STATUS table, section H timing, and Coordination decisions 4 and 6. Never
  resolve it by taking "ours" wholesale -- that silently reverts 4e's rewrite.
- **T10 has the same hazard** on the epic roadmap. Re-read the Phase table immediately before editing;
  4e may already have added or moved rows.
- The code scope is untouched by all of this. `IntakePayload`, the resolvers and the migrations do not
  overlap 4e at all -- the collision is confined to two markdown files.

## Goal

Close the four outstanding payload gaps Levon asked for, plus one contract clarification he has
accepted, so his proof-of-service document and billing attribution stop depending on data we hold but
do not send.

## Context & decisions

### Resolved decisions (Adrian, 2026-08-05)

1. **`reply-4-sent.md` WAS sent.** Settled by evidence, not memory: that file is the only place we
   asked the MinIO hostname-vs-bare-IP question, proposed "prune only on a 200", and offered the
   unconditional document re-send. Levon's next email answered all three. Its `DRAFT - NOT SENT`
   first line is stale and is corrected as T9 of this plan. Consequence: Levon DOES already know
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

4. **The epic roadmap gets a row for this phase now.** Adrian's call. T10 does it, committed by
   explicit pathspec because that file is shared with the session owning 4d.

### Resolved decisions (Adrian, 2026-08-07, after the audit)

5. **`decidedByName` stays, with an explicit tenancy fallback and a LIVE check before the resolver is
   written.** See decision 11 and T7. The risk is that internal staff are host users invisible to a
   tenant-scoped identity lookup, which would make the field silently null forever.

6. **`changeFinalizedAtUtc` gets a REAL `DecidedAt` column** rather than reusing
   `LastModificationTime`. This is the one place Phase 6 accepts a schema change, deliberately,
   because the field decides who gets billed and an approximate value under a precise name is the
   wrong failure. It requires migrations in BOTH the host and tenant migration sets. T1 and T2.

### Design decisions taken while writing this plan

7. **`autoCancelled` is DERIVED, not persisted. No migration for THIS field.**
   `Appointment.CancelledById` (`Appointment.cs:167`) is set to `CurrentUser.Id` on a human
   cancellation (`AppointmentChangeRequestsAppService.Approval.cs:119`) and is NEVER set by the AME
   auto-cancel job, which writes only status (`JointDeclarationAutoCancelJob.cs:175`) and reason
   (`:180`). Verified by grepping `CancelledById` across all of `src/`: exactly one writer, plus the
   entity and the two DbContext mappings. So "cancelled AND `CancelledById is null`" identifies an
   auto-cancel from existing persisted state.

   Why a derived value is still an improvement over what Levon has today: he currently must
   string-match the English sentence in `CancellationReason`, which any copy-edit silently breaks.
   The wire field is explicit; only our internal derivation is implicit, and it rests on a structural
   fact rather than on prose.

8. **Attribution is split into TWO fields, because two different actors exist.** Levon asked for "who
   cancelled -- party type at minimum". The portal records:
   - WHO ASKED: `AppointmentChangeRequest.RequestingSide` (`:113`), a `ChangeRequestSide`.
   - WHO DECIDED: the staff member who applied the outcome (see decision 11 for the source).

   Sending only one would misrepresent the other. So the payload carries `requestedBySide` and
   `decidedByName`.

   **Honest limitation to state in the contract:** `ChangeRequestSide` is COARSER than the party list
   Levon asked for. SideA covers patient + applicant attorney; SideB covers defense attorney + claim
   examiner (`ChangeRequestSide.cs:6-8`). We cannot say which of the two a side's request came from,
   because the portal does not record it. Do not invent precision we do not have.

9. **NO new timezone field.** The payload ALREADY sends the zone: `IntakePayload.TimeZone`
   (`IntakePayload.cs:85`, "IANA zone the local date/time are expressed in"), populated from the
   const `AppointmentCoreResolver.ClinicTimeZone = "America/Los_Angeles"` (`:25`, written at `:52`),
   and pinned by an existing test (`IntakePayloadBuilderTests`, `data.TimeZone.ShouldBe(...)`).

   The first draft of this plan added a second field, `localTimezoneId`, sourced from the
   reminders-scoped setting `CaseEvaluationSettings.RemindersPolicy.ReminderTimezoneId`. That was a
   defect: two timezone fields that could disagree, the new one from a worse source.

   The genuine open question underneath is whether `TimeZone` should become per-office. The existing
   code comment already flags it: "every office is a California workers'-comp practice. An office
   outside Pacific time would need this to become per-office" (`AppointmentCoreResolver.cs:21-23`).
   That is a change to an EXISTING field and is **out of scope**; T10 flags it to the epic.

10. **Patient address sends BOTH `Address` and `Street` verbatim.** `Patient` has both as separate
    nullable strings (`Patient.cs:51` and `:65`) with NO `Unit` field. Which is line 1 versus line 2
    is not recorded anywhere. Levon explicitly asked for everything we hold and said extra fields cost
    him nothing, so both go over with our own names and the ambiguity documented, rather than us
    guessing a mapping into a single formatted address.

    NOTE: `docs/integration/case-tracker-open-items.md` describes this as "street, unit, city, state,
    zip". That is WRONG -- there is no `Unit`, and `Address` is a separate field. T9 corrects it.

11. **`decidedByName` resolves from THREE sources and must survive a host-user lookup.**

    Source, in order of precedence:
    - `Appointment.CancelledById` when the appointment is cancelled (`Appointment.cs:167`).
    - else `AppointmentChangeRequest.ApprovedById` (`:73`) when the selected request was accepted.
    - else `AppointmentChangeRequest.RejectedById` (`:71`) when it was rejected.

    The first draft used only `CancelledById`, which left `decidedByName` null for every reschedule
    even though `Approval.cs:131` and `:189` record the decider.

    **The tenancy hazard.** Nothing in the codebase currently resolves any of these three ids to a
    name -- verified by grepping `src/` and `angular/src`; the DTOs expose raw `Guid?`
    (`AppointmentDto.cs:37`, `AppointmentChangeRequestDto.cs:95,97`). So there is NO precedent to
    mirror, and `CaseTrackerFailureAlertJob` is NOT one: it calls `GetListByNormalizedRoleNameAsync`
    (list by role), not a lookup by id.

    The payload is built under an ambient tenant. If internal staff are HOST users
    (`TenantId is null`), a tenant-scoped `IIdentityUserRepository.FindAsync(id)` returns null and the
    field is silently empty in production while a substituted repository in unit tests happily
    returns a name. That is the exact failure mode `~/.claude/rules/testing.md` warns about.

    Therefore: look up under the ambient tenant, and on a miss retry inside
    `ICurrentTenant.Change(null)`. AND T7 opens with a live verification step -- confirm which scope
    actually holds the user BEFORE writing the resolver. Do not skip it because the mocked test
    passes.

## All needed context

### Verified anchors (re-checked against `main` at `2ce2ef3f`, 2026-08-07)

| Fact                                                                                                                                                                 | Anchor                                                                                 |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| `IntakePatientSection` has no address fields                                                                                                                         | `IntakePayload.cs:161-192`                                                             |
| `Address` / `City` / `ZipCode` already on the payload belong to `IntakeLocationSection` -- the CLINIC. Easy to misread as the patient's                              | `IntakePayload.cs:141-145`                                                             |
| `TimeZone` ALREADY on the payload -- do not add a second one                                                                                                         | `IntakePayload.cs:85`, written `AppointmentCoreResolver.cs:52` from the const at `:25` |
| `CancellationReason` on the payload, nullable                                                                                                                        | `IntakePayload.cs:50`                                                                  |
| `BillingStatus` on the payload, non-nullable, defaults `NONE`                                                                                                        | `IntakePayload.cs:44`                                                                  |
| Only timestamps sent today                                                                                                                                           | `IntakePayload.cs:53,56,59`                                                            |
| Payload props are PascalCase, serialized camelCase by the client's serializer options -- do NOT add `[JsonPropertyName]`                                             | `IntakePayload.cs:8-9`                                                                 |
| Patient address columns: `Address`, `City`, `ZipCode`, `Street`, `StateId`                                                                                           | `Patient.cs:51,54,57,65,76`                                                            |
| `State` is `FullAuditedAggregateRoot<Guid>, IMultiTenant` -- the lookup only resolves under the ambient tenant, which is how `PartyDetailResolver` already does it   | `State.cs:13`                                                                          |
| `Appointment.CancelledById`, exactly one writer in all of `src/`                                                                                                     | `Appointment.cs:167`, written `Approval.cs:119`                                        |
| Auto-cancel writes status and reason only, never `CancelledById`                                                                                                     | `JointDeclarationAutoCancelJob.cs:175,180`                                             |
| `AutoCancelReasonText` is the prose sentence that reaches the wire; `AutoCancelReason` is an internal routing discriminator that does not                            | `JointDeclarationAutoCancelJob.cs:62` and `:54`                                        |
| `AppointmentChangeRequest` has NO finalize timestamp today -- only `ApprovedById` / `RejectedById` (who, not when) and the two `Side*ConsentRespondedAt`             | full property read of `AppointmentChangeRequest.cs`                                    |
| `RequestStatus` is assigned at exactly FIVE sites; four are finalizations, one is construction                                                                       | `Approval.cs:130,188,506,587`; `AppointmentChangeRequest.cs:184`                       |
| Each finalization sets `ApprovedById` / `RejectedById` on the very next line                                                                                         | `Approval.cs:131,189,507,588`                                                          |
| The consent expiry sweep does NOT finalize a request -- it only calls `round.MarkSideExpired`, so there is no fifth write site                                       | `ChangeRequestConsentExpirySweepJob.cs:92-93`                                          |
| `RequestStatusType`: `Pending = 25`, `Accepted = 26`, `Rejected = 27` (non-contiguous, OLD-faithful)                                                                 | `RequestStatusType.cs:11-13`                                                           |
| `AppointmentChangeRequest` is configured in BOTH DbContexts, so it needs BOTH migration sets                                                                         | `CaseEvaluationDbContext.cs:689`, `CaseEvaluationTenantDbContext.cs:625`               |
| Its table is `CaseEvaluationConsts.DbTablePrefix + "AppointmentChangeRequests"` = `AppAppointmentChangeRequests`                                                     | `CaseEvaluationDbContext.cs:691`                                                       |
| `AppointmentChangeRequest` is `FullAuditedAggregateRoot<Guid>, IMultiTenant`                                                                                         | `AppointmentChangeRequest.cs:40`                                                       |
| `ChangeRequestConsentRound` is `FullAuditedAggregateRoot<Guid>, IMultiTenant`; current round is `SupersededAt == null`                                               | `ChangeRequestConsentRound.cs:26,60`                                                   |
| `IChangeRequestConsentRoundRepository.GetCurrentAsync(changeRequestId)` ALREADY returns "highest `RoundNumber` not superseded" -- call it, do not re-implement       | `IChangeRequestConsentRoundRepository.cs`                                              |
| `IAppointmentChangeRequestRepository` is a plain `IRepository<AppointmentChangeRequest, Guid>` -- no custom methods, so `GetListAsync(predicate)` is the access path | `IAppointmentChangeRequestRepository.cs:6`                                             |
| `AppointmentCoreResolver` has THREE constructor deps                                                                                                                 | ctor of `AppointmentCoreResolver.cs:31-34`                                             |
| `PartyResolver` has TWO constructor deps                                                                                                                             | ctor of `PartyResolver.cs:26-28`                                                       |
| `IntakePayloadBuilder` already has EIGHT deps -- already OVER the repo's 7-param DI ceiling. Do not add to it                                                        | ctor of `IntakePayloadBuilder.cs:27-35`                                                |
| `TenantWorkRunner.ForEachOfficeAsync` wraps work in `_currentTenant.Change(officeId)`                                                                                | `TenantWorkRunner.cs:36`                                                               |
| `ChangeRequestSide`: `SideA = 1`, `SideB = 2`; SideA = patient + applicant attorney, SideB = defense attorney + claim examiner                                       | `ChangeRequestSide.cs:6-8,12-13`                                                       |

### Patterns to mirror

- **Domain method that sets several fields together:** `ChangeRequestConsentRound.MarkSideExpired`
  (`:169-177`) -- an idempotent guard, then set the status and its timestamp in one place so they
  cannot drift apart. T1's `MarkDecided` follows this exactly.
- **Dual-context migration:** `20260805201352_Added_ChangeRequestConsentRounds.cs` (host) and
  `20260805201408_Added_ChangeRequestConsentRounds.cs` (tenant) -- same logical change, one migration
  per set, different timestamps. Generated per context, not hand-copied.
- **State-name resolution:** collect nullable `Guid?` ids, filter to those with values, `Distinct()`,
  one batched query, then a `StateNameOrNull(id, names)` helper at the point of use.
  `PartyDetailResolver.cs:72-82` for the collection, `:94-95` for the helper, `:112` for a call site.
  T4 mirrors this exactly.
- **Wire enum mapping:** an explicit `const string` per value plus a `switch` expression, NEVER
  `ToString()`, so an internal rename cannot change the wire. `BillingStatusWire.cs` is the closest
  precedent because, like the new side mapper, it must not throw on an unmapped value (`_ => None`).
  `EvaluationKindWire.cs` is the variant that DOES throw. Follow `BillingStatusWire`.
- **Timestamp formatting:** `IntegrationTimestamp.ToIsoUtcOrNull` for nullable instants (`:43`). It
  treats `DateTimeKind.Unspecified` as already-UTC, which is correct only because the API container
  runs UTC.
- **Payload section doc comments:** every existing field carries a `<summary>` saying what it is AND
  why, e.g. `IntakePayload.cs:38-43` for `BillingStatus`. Match that density.
- **Test file placement:** all Case Tracker unit tests live in
  `test/HealthcareSupport.CaseEvaluation.Domain.Tests/Integration/CaseTracker/`, one file per type
  (`BillingStatusWireTests.cs`, `PartyDetailResolverTests.cs`, `InjuryResolverTests.cs`).

### Gotchas

- `IntakePayload` lives in **Domain**, not Application.Contracts, so it is NOT an ABP app-service DTO.
  No `abp generate-proxy` run is needed and no Angular file changes. **This phase is backend-only.**
- There is exactly ONE payload builder with two callers -- `CaseTrackerIntakeQueue` (push) and
  `CaseTrackerReconcileService` (reconcile GET). Both get every new field automatically. Do not add a
  second construction path.
- A cancellation has NO consent rounds; rounds exist only for reschedules. `currentRoundProposedAtUtc`
  must be null for a cancellation rather than throwing or defaulting.
- The AME auto-cancel has NO change request at all, so `requestedBySide` and the request timestamps
  are null for it. That is correct, and combined with `autoCancelled: true` it is unambiguous.
- `IntakePayloadBuilderTests` asserts INDIVIDUAL field values, not an exact field set. An additive
  change will therefore NOT break it -- which means nothing forces you to add coverage. Add the
  assertions deliberately (T3, T6); do not wait for a red test to tell you.
- A consent round that expires leaves the parent request `Pending` forever with no decision. That is
  existing behaviour, not a bug introduced here: such a request correctly reports a null
  `changeFinalizedAtUtc`.

## Tasks

Ordered: schema first, then DTO, then resolvers, then docs. T7 depends on T1.

### T1 -- `DecidedAt` column and a `MarkDecided` domain method

approach: tdd

Add to `AppointmentChangeRequest`:

```
/// <summary>
/// When the request stopped being Pending, UTC. Null while Pending, and null for historical
/// rows decided before this column existed unless the backfill in T2 reached them.
/// Distinct from LastModificationTime, which bumps on ANY write -- including the phase 4c
/// consent responses this entity records -- and therefore cannot answer "when was this decided".
/// </summary>
public virtual DateTime? DecidedAt { get; protected set; }
```

Add a domain method mirroring `ChangeRequestConsentRound.MarkSideExpired` (`:169-177`):

```
/// <summary>
/// Applies a terminal outcome: the status, the acting staff member, and the decision instant,
/// set together so they cannot drift apart. Idempotent -- a request that is already decided is
/// left alone, so a retried approval does not move the timestamp.
/// </summary>
public void MarkDecided(RequestStatusType outcome, Guid? decidedById, DateTime nowUtc)
```

It must reject `RequestStatusType.Pending` as an outcome (fail fast -- `Pending` is not a decision),
set `ApprovedById` for `Accepted` and `RejectedById` for `Rejected`, and set `DecidedAt = nowUtc`.

Then replace the four finalization sites with a `MarkDecided` call. Use `Clock.Now.ToUniversalTime()`
for the instant -- `Clock` is already inherited from the ABP `ApplicationService` base, so NO new
dependency is needed, and `.ToUniversalTime()` is the house idiom in this very file
(`Approval.cs:267`, `AppointmentChangeRequestsAppService.cs:258`). Do not inject `IClock`.

- `AppointmentChangeRequestsAppService.Approval.cs:130-131` (cancel approve, `Accepted`)
- `:188-189` (cancel reject, `Rejected`)
- `:506-507` (reschedule finalize, `Accepted`)
- `:587-588` (reschedule reject, `Rejected`)

Do NOT touch `AppointmentChangeRequest.cs:184`, which sets `Pending` at construction.

SCOPE NOTE: leave the `RequestStatus` setter public. Tightening it to `protected set` would force the
single-sink invariant, and that is the better end state, but it is a refactor of existing code beyond
this feature -- flag it to the epic in T10 instead of doing it here.

Tests (`test/.../Integration/CaseTracker/` is for payload types; this one belongs in
`test/HealthcareSupport.CaseEvaluation.Domain.Tests/AppointmentChangeRequests/AppointmentChangeRequestTests.cs`,
creating that file if absent):
`Accepted` sets `ApprovedById` and `DecidedAt` and leaves `RejectedById` null; `Rejected` mirrors it;
`Pending` throws; a second `MarkDecided` on an already-decided request does not move `DecidedAt`.

### T2 -- EF config and dual-context migrations for `DecidedAt`

approach: code

Add the property mapping alongside the existing `AppointmentChangeRequest` config in BOTH contexts:
`CaseEvaluationDbContext.cs:689` and `CaseEvaluationTenantDbContext.cs:625`. Mirror how the
neighbouring nullable `DateTime?` columns are configured in the same block.

Generate ONE migration per set -- host into `Migrations/`, tenant into `TenantMigrations/` -- named
`Added_ChangeRequestDecidedAt`. Mirror `20260805201352` / `20260805201408` from 4c: same logical
change, generated per context, two different timestamps. Do not hand-copy one into the other.

**Backfill, in both migrations.** Existing decided rows would otherwise report a null
`changeFinalizedAtUtc` forever:

```
migrationBuilder.Sql(
    "UPDATE AppAppointmentChangeRequests " +
    "SET DecidedAt = LastModificationTime " +
    "WHERE RequestStatus <> 25 AND DecidedAt IS NULL AND LastModificationTime IS NOT NULL;");
```

`25` is `RequestStatusType.Pending` (`RequestStatusType.cs:11`); the enum is non-contiguous and
OLD-faithful, so do not assume `0`. This backfill uses precisely the approximation decision 6
rejected as a permanent design -- that is intentional and acceptable ONCE, for history, and it is
strictly better than null. T8 records in the contract that rows decided before this deploy carry an
approximate value.

Verify the column in SQL in BOTH databases rather than trusting the generator -- a dual-context entity
has silently landed in only one set before.

### T3 -- patient address fields on the payload DTO

approach: code

Add to `IntakePatientSection` (`IntakePayload.cs:161-192`), after `CellPhoneNumber` and before
`SamePersonGroupKey`: `Address`, `Street`, `City`, `State`, `ZipCode`, all `string?`.

Doc comment must state that `Address` and `Street` are separate columns in the portal with no recorded
convention for which is line 1, that there is no unit/apartment field, and that `State` is a resolved
name rather than a code. Mirror the comment density of `IntakePayload.cs:38-43`.

Add the corresponding assertions to `IntakePayloadBuilderTests` -- it will not fail without them.

### T4 -- resolve the patient address, including the state name

approach: tdd

`PartyResolver.ResolvePatientAsync` builds `IntakePatientSection`. Add an `IRepository<State, Guid>`
dependency (it has two deps today, so three is well inside the ceiling) and populate the five new
fields from `Patient.Address`, `.Street`, `.City`, `.StateId`, `.ZipCode`
(`Patient.cs:51,65,54,76,57`).

Resolve the state name by mirroring `PartyDetailResolver.cs:72-82` and its `StateNameOrNull` helper
(`:94-95`). A single id does not need the full batching machinery, but use the same helper shape so
the two resolvers read alike.

`State` is `IMultiTenant` (`State.cs:13`), so this only resolves under the ambient tenant -- which is
where the payload is built. Do not attempt a host-side lookup.

Tests in a new `test/.../Integration/CaseTracker/PartyResolverTests.cs`: address fields populated;
null `StateId` yields null `State`; a `StateId` with no matching row yields null rather than throwing.

### T5 -- `ChangeRequestSideWire`

approach: tdd

New file beside `BillingStatusWire.cs`. Map `ChangeRequestSide` to `SIDE_A` / `SIDE_B`. Follow
`BillingStatusWire`'s non-throwing shape -- return null for an unmapped or null input rather than
throwing, because a cancellation without a change request legitimately has no side.

The doc comment must record WHY the wire value is a side rather than a party: SideA covers patient +
applicant attorney and SideB covers defense attorney + claim examiner (`ChangeRequestSide.cs:6-8`),
and the portal does not record which of the two within a side actually asked. This is the honest
limitation from decision 8 and the contract repeats it.

Tests in `test/.../Integration/CaseTracker/ChangeRequestSideWireTests.cs`: both sides map; null maps
to null; an out-of-range cast value maps to null and does not throw.

### T6 -- attribution and change timestamps on the payload DTO

approach: code

Add to `IntakePayload`, immediately after `CancellationReason` (`IntakePayload.cs:50`):

| Field                       | Type      | Meaning                                                                                                                                  |
| --------------------------- | --------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `autoCancelled`             | `bool`    | True only for the AME joint-declaration auto-cancel. Non-nullable, defaults false, so the receiver never distinguishes absent from false |
| `requestedBySide`           | `string?` | `SIDE_A` / `SIDE_B`. Who ASKED. Null when there is no change request                                                                     |
| `decidedByName`             | `string?` | The staff member who applied the outcome. Null for an auto-cancel and when the request is still Pending                                  |
| `changeRequestedAtUtc`      | `string?` | ISO-8601 UTC. The selected change request's creation time                                                                                |
| `currentRoundProposedAtUtc` | `string?` | ISO-8601 UTC. The current consent round's creation time. Always null for a cancellation                                                  |
| `changeFinalizedAtUtc`      | `string?` | ISO-8601 UTC, from `DecidedAt`. Null while Pending, and null for rows decided before the T2 backfill could reach them                    |

NO timezone field -- `TimeZone` already exists at `IntakePayload.cs:85`. See decision 9.

Each comment states the limitation, not just the meaning -- decisions 8 and 11 exist to be written
down here. Add the corresponding assertions to `IntakePayloadBuilderTests`.

### T7 -- resolve attribution and timestamps

approach: tdd

**STEP 0, before writing any code -- resolve the tenancy question live.** Pick a real cancelled
appointment, read its `CancelledById`, and determine whether that user row lives in the office
database or in the host database. A `sqlcmd` query against both is enough. Record the answer in this
plan file as a decision. Do NOT infer it from a passing unit test: `IIdentityUserRepository` is
substituted in unit tests and will return whatever the test sets up, hiding the real behaviour
(`~/.claude/rules/testing.md`, "Substituted dependencies can hide real defects").

`AppointmentCoreResolver` already owns `Status`, `BillingStatus`, `CancellationReason`, `TimeZone` and
the existing timestamps, so this belongs there rather than in a new resolver -- and
`IntakePayloadBuilder` is already at eight deps and cannot take another.

Add exactly THREE dependencies to `AppointmentCoreResolver`: `IAppointmentChangeRequestRepository`,
`IChangeRequestConsentRoundRepository`, and `IIdentityUserRepository`. It has three today, so the
total becomes SIX -- one under the repo's 7-param DI ceiling. If a later field needs a seventh,
extract a helper resolver instead of growing this constructor.

`IIdentityUserRepository` is usable from Domain; `CaseTrackerFailureAlertJob.cs:59,70` already injects
it. Mirror the INJECTION only -- that job's usage is a list-by-role query, not a lookup by id, so it
is not a precedent for the resolution itself.

Resolution rules:

- **Select the change request** = the most recent `AppointmentChangeRequest` for this appointment by
  `CreationTime` descending, take the first. Full stop.
  The first draft said "most recent non-Pending, else the Pending one if that is all there is", which
  picks a stale rejected request over a live Pending one -- wrong for an appointment currently sitting
  in `CancellationRequested`, where the Pending request IS the relevant one.
- `autoCancelled` = the status is a cancelled status AND `Appointment.CancelledById is null`
  (decision 7). Do NOT read `CancellationReason`.
- `requestedBySide` = `ChangeRequestSideWire.ToWire(request?.RequestingSide)`.
- `decidedByName` = display name for, in order: `Appointment.CancelledById`, else
  `request.ApprovedById`, else `request.RejectedById`; null when all three are null (decision 11).
  Look the id up under the ambient tenant, and on a miss retry inside `ICurrentTenant.Change(null)`.
- `changeRequestedAtUtc` = the selected request's `CreationTime`.
- `changeFinalizedAtUtc` = the selected request's `DecidedAt` (T1). Not `LastModificationTime`.
- `currentRoundProposedAtUtc` = `CreationTime` of
  `IChangeRequestConsentRoundRepository.GetCurrentAsync(request.Id)`. Call that method; do not
  re-implement "highest `RoundNumber` not superseded". Null when the request is a `Cancel` and null
  when `GetCurrentAsync` returns null (a freshly submitted reschedule with no confirmed date).
- Format every instant with `IntegrationTimestamp.ToIsoUtcOrNull`.

Tests in a new `test/.../Integration/CaseTracker/AppointmentCoreResolverTests.cs`: auto-cancel yields
`autoCancelled` true with null side and null decider; a human cancel yields false with a side and a
decider; a reschedule approval yields a decider from `ApprovedById`; a reschedule rejection yields one
from `RejectedById`; a cancellation yields null `currentRoundProposedAtUtc`; a reschedule whose
`GetCurrentAsync` returns a round yields that round's creation time; a Pending request yields null
`changeFinalizedAtUtc`; a Pending request that is NEWER than a decided one is the one selected; an
appointment with no change request yields nulls throughout and does not throw.

### T8 -- contract updates

approach: code

**Do this task LAST, and run `git fetch origin && git rebase origin/main` immediately before it.**
Phase 4e is rewriting this same file; see the coordination section at the top. Phase 6 only ADDS.

`docs/integration/case-tracker-api-contract.md`:

- `data.patient` table: the five new address fields, with decision 10's `Address`-versus-`Street`
  ambiguity and the absence of a unit field stated explicitly.
- Section A field table: the six new top-level fields from T6, each with its limitation. State that
  `changeFinalizedAtUtc` is null for requests decided before this release except where the backfill
  reached them, and that backfilled values are approximate (they come from the row's last
  modification, not the decision instant).
- Note that `timeZone` is unchanged and remains a portal-wide constant, so Levon does not read the new
  attribution fields as implying a new zone source.
- **D1**, the item Levon has accepted: a non-200 reconcile response carries NO document information
  and must NEVER drive document pruning. Prune only on a 200. State that the 404's ambiguity between
  unknown appointment, unknown office and switched-off office is DELIBERATE anti-enumeration
  (`CaseTrackerReconcileService.cs:48-51`) and is not to be weakened.
- Do NOT add D2 (every push is a full snapshot). It belongs in section E2, which 4e rewrites; writing
  it here means editing a section that is about to be replaced.

### T9 -- correct two stale documents

approach: code

- `docs/integration/case-tracker-correspondence/reply-4-sent.md`: change the first line from
  `DRAFT - NOT SENT` to a sent marker with the date, per resolved decision 1. Leave the body verbatim;
  it is a record of what was sent.
- `docs/integration/case-tracker-open-items.md`: fix the patient address description per decision 10
  (no `Unit`; `Address` and `Street` are separate), and mark N1-N4 and D1 as belonging to this phase.

### T10 -- epic roadmap row and flags

approach: code

Add this row to the Phase table in
`docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md`, after the phase 5 row:

| 6 | Case Tracker payload completeness (N1-N4 + D1) | `feat/case-tracker-payload-completeness` | `2026-08-05-case-tracker-payload-completeness.md` | TODO -- INDEPENDENT, runs parallel to 4d |

Also amend the line below the table that reads "Phases 1, 2 and 3 are mutually independent. 4a-4e are
strictly sequential" so it names phase 6 as independent too. Otherwise a later reader will assume 6
follows 5, which is exactly the coupling this phase exists to avoid.

Record in the roadmap's learnings, each in one line:

- R2 (type-change marker) is BLOCKED on staff-facing machinery rather than a wire field, and
  `reply-4-sent.md` Q19's answer to Levon needs correcting -- decision 2.
- `IntakePayload.TimeZone` is a hardcoded Pacific constant; making it per-office is an open change to
  an existing field, deliberately left out of phase 6 -- decision 9.
- `AppointmentChangeRequest.RequestStatus` still has a public setter; `MarkDecided` (T1) is the
  intended single sink, and tightening the setter to enforce it is a candidate refactor.

**Run `git status --short` first, and re-read the Phase table before editing it.** That file is shared
with the session owning 4e, which has already had it in an unresolved merge conflict once. Commit it
by explicit pathspec, alone, and leave any file you did not create.

## Acceptance (EARS)

- WHEN an intake payload is built for any appointment, THE SYSTEM SHALL include the patient's
  `address`, `street`, `city`, `state` and `zipCode`, using null for each value the portal does not
  hold.
- WHEN a patient has a `StateId` that resolves to a state row, THE SYSTEM SHALL send that state's
  NAME; and WHEN the id is null or unresolvable, THE SYSTEM SHALL send null rather than throwing.
- WHEN an appointment was cancelled by the AME joint-declaration auto-cancel job, THE SYSTEM SHALL
  send `autoCancelled` true, `requestedBySide` null and `decidedByName` null.
- WHEN an appointment was cancelled by a human, THE SYSTEM SHALL send `autoCancelled` false, the
  requesting side, and the name of the staff member who applied the outcome.
- WHEN a change request was accepted or rejected and the appointment was not cancelled, THE SYSTEM
  SHALL send `decidedByName` from that request's `ApprovedById` or `RejectedById`.
- WHERE the acting staff member is a host user rather than an office user, THE SYSTEM SHALL still
  resolve `decidedByName` rather than sending null.
- WHEN an appointment has never been cancelled, THE SYSTEM SHALL send `autoCancelled` false.
- WHEN a change request exists for the appointment, THE SYSTEM SHALL select the most recent by
  creation time and send `changeRequestedAtUtc` as that request's creation time in ISO-8601 UTC.
- WHEN a change request is marked decided, THE SYSTEM SHALL record `DecidedAt` in the same operation
  that sets its terminal status.
- WHEN `MarkDecided` is called on an already-decided request, THE SYSTEM SHALL leave `DecidedAt`
  unchanged.
- WHEN `MarkDecided` is called with `Pending` as the outcome, THE SYSTEM SHALL throw.
- WHEN the selected request is no longer Pending, THE SYSTEM SHALL send `changeFinalizedAtUtc` from
  `DecidedAt`; and WHILE it is Pending, THE SYSTEM SHALL send null.
- WHEN the change request is a reschedule with a current consent round, THE SYSTEM SHALL send
  `currentRoundProposedAtUtc` from the round returned by `GetCurrentAsync`.
- WHEN the change request is a cancellation, THE SYSTEM SHALL send `currentRoundProposedAtUtc` as
  null.
- WHEN an appointment has no change request at all, THE SYSTEM SHALL send null for every attribution
  and change-timestamp field and SHALL NOT throw.
- WHEN the migration runs against a database with already-decided requests, THE SYSTEM SHALL backfill
  `DecidedAt` from `LastModificationTime` for every row whose status is not `Pending`.
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

Because T2 adds a column to a dual-context entity, ALSO confirm the column exists in BOTH databases in
SQL rather than trusting the generator. A dual-context entity has silently landed in only one set
before, and `dotnet build` cannot detect it.

First resolve the SQL container name -- it differs per worktree/compose project, so do NOT hardcode
it (`docs/onboarding/GETTING-STARTED.md:270` says `patient-portal-db`;
`docs/runbooks/database-per-office-go-live-isolation-gate.md:46` shows a different one):

```bash
docker ps --format '{{.Names}}' | grep -i sql
```

Then, substituting that name. `MSYS_NO_PATHCONV=1` and the `bash -c` wrapper are BOTH required: Git
Bash rewrites the leading `/opt/...` into a Windows path before Docker sees it
(`docs/runbooks/DOCKER-DEV.md:232`, and `docs/plans/2026-07-31-...-epic.md:426-427`). The password
variable is `MSSQL_SA_PASSWORD`, expanded INSIDE the container:

```bash
MSYS_NO_PATHCONV=1 docker exec -i <sql-container> bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C' <<'SQL'
SELECT name FROM sys.databases WHERE name LIKE '%CaseEvaluation%';
GO
SQL
```

Run the column check against the host database AND at least one office database, using the names that
query returns:

```bash
MSYS_NO_PATHCONV=1 docker exec -i <sql-container> bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d <database>' <<'SQL'
SELECT COUNT(*) AS ColumnExists FROM sys.columns
WHERE object_id = OBJECT_ID('AppAppointmentChangeRequests') AND name = 'DecidedAt';
SELECT COUNT(*) AS StillNull FROM AppAppointmentChangeRequests
WHERE RequestStatus <> 25 AND DecidedAt IS NULL AND LastModificationTime IS NOT NULL;
GO
SQL
```

`ColumnExists` must be 1 in every database checked, and `StillNull` must be 0 -- a non-zero
`StillNull` means the T2 backfill did not run in that database.

Re-establish the test baseline on the first run rather than trusting a recorded number: the handoff
cites 1800 backend and 432 Angular, while the 4c learnings cite 475 frontend specs. Record what this
branch actually starts from before judging whether anything broke.

## Out of scope, deliberately

- **R2 type-change marker.** Real, but needs a staff-facing "replaces appointment X" action, not a
  wire field. Decision 2. Belongs with 4d/4e.
- **R1 reschedule sequence / count.** Meaningless until 4d creates a chain.
- **D2 every push is a full snapshot.** Assigned to 4e, which rewrites the section it belongs in.
- **D3 status list five to seven** and **D4 reschedule link semantics.** Both 4e by definition.
- **Making `timeZone` per-office.** Decision 9. A change to an existing field, flagged to the epic.
- **Tightening the `RequestStatus` setter.** T1 adds `MarkDecided` as the intended sink but leaves the
  public setter; enforcing it is a refactor beyond this feature.
- **Deploying anything.** Adrian has explicitly declined a deploy. The cascade PR #410 is not to be
  merged.

## Audit trail (2026-08-07)

The first draft was audited against `main` at `2ce2ef3f`. Eight defects were found and corrected.
Recorded so they are not reintroduced.

| #   | Defect in the first draft                                                                                                                                                                                                             | Correction                                                                                                                                             |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1   | Added a `localTimezoneId` field. The payload ALREADY sends `TimeZone` (`IntakePayload.cs:85`), so this would have put two timezone fields on the wire, able to disagree, with the new one from a worse (reminders-scoped) source      | Field removed. Decision 9. The per-office question is flagged to the epic instead                                                                      |
| 2   | `changeFinalizedAtUtc` came from `LastModificationTime`, which bumps on any write including 4c consent responses, so it meant "last touched" not "finalized"                                                                          | Real `DecidedAt` column, `MarkDecided` domain method, dual-context migrations, backfill. Decisions 6, T1, T2                                           |
| 3   | `decidedByName` read only `Appointment.CancelledById`, leaving it null for every reschedule despite `ApprovedById` / `RejectedById` holding the answer                                                                                | Three-source precedence. Decision 11                                                                                                                   |
| 4   | `decidedByName` cited `CaseTrackerFailureAlertJob` as precedent. That job lists users by role; it never looks one up by id. No precedent exists anywhere in the repo, and a tenant-scoped lookup of a host user returns null silently | Explicit `ICurrentTenant.Change(null)` fallback plus a mandatory live verification step before the resolver is written. T7 step 0                      |
| 5   | T5 re-implemented "current consent round" by hand                                                                                                                                                                                     | `IChangeRequestConsentRoundRepository.GetCurrentAsync` already does exactly this. T7 calls it                                                          |
| 6   | Request selection was "most recent non-Pending, else Pending", which picks a stale rejected request over a live Pending one                                                                                                           | Most recent by `CreationTime`, full stop. T7                                                                                                           |
| 7   | Claimed `IntakePayloadBuilder` has seven deps and is at the ceiling                                                                                                                                                                   | It has eight and is already over it. The conclusion (keep the work out of it) is unchanged and better supported                                        |
| 8   | Claimed `IntakePayloadBuilderTests` asserts an exact field set and would break on an additive change                                                                                                                                  | It asserts individual values. An additive change does NOT break it, so nothing forces new coverage -- T3 and T6 now say to add assertions deliberately |

Also added, previously missing: named test files for every `tdd` task; the note that `State` is
`IMultiTenant`; the note that payload properties are PascalCase and serialized camelCase so nobody
adds `[JsonPropertyName]`; the fact that the branch already exists off `main` with the plan commit on
it; and the SQL column check in the validation loop.
