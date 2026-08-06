---
feature: Reschedule creates a new appointment (epic phase 4d)
date: 2026-08-05
status: in-progress
base-branch: main
related-issues: []
---

# Phase 4d -- reschedule creates a new appointment

Epic tracker: `docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md`.
Research packet: `docs/research/2026-08-05-reschedule-creates-new-appointment.md`.
Follows phase 4c (`docs/plans/2026-08-05-reschedule-consent-rounds.md`, PR #428 -> `2ce2ef3f`).

**4b and 4c are merged but NOT DEPLOYED and must deploy together. 4d joins that release train --
do not deploy any of them without a fresh explicit go.**

## Goal

Finalizing a reschedule stops moving one appointment and instead closes the old one into a
terminal Rescheduled status and creates a NEW Approved appointment carrying every child group
forward, linked back by a dedicated chain column -- with no new Case Tracker traffic until 4e.

## Context & decisions

Source item R3: "old appointment goes to a Rescheduled status and a NEW appointment is created in
the old one's status, slot freed, history linked."

### The locked decision that does not hold (correcting the tracker)

The tracker locks: _"the new appointment is created by REUSING the existing create pipeline
(`CreateRevalAsync`-style `AppointmentCreateDto` path) rather than resurrecting the deleted cascade
cloner."_

**Verified false.** `AppointmentCreateDto`
(`src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/AppointmentCreateDto.cs`,
71 lines) declares **16 scalar properties plus `CustomFieldValues`** and NOTHING else -- no
injuries, body parts, employer details, accessors, attorney links, insurances or claim examiners.
The child cascade is CLIENT-SIDE: the Angular wizard fires six further POSTs after create, plus two
attorney upserts on the app service. Finalize is a server-side staff action with no wizard in the
loop, so reusing the create pipeline would produce an appointment row with custom field values and
nothing else. A server-side copier must be written; only its shape was negotiable.

Amending that locked decision is IN SCOPE for 4d (T16).

### Resolved decisions (Adrian, 2026-08-05)

- Decision 1: server-side cascade copier scoped to 4d, with an explicit per-group audit and ONE
  TEST PER GROUP, because that is what makes it not-a-patchwork and stops bug F18 repeating.
  Rejected: growing `AppointmentCreateDto` into a create-with-children DTO -- the better
  end-state, but it changes the highest-traffic flow in the product and belongs in its own phase.
- Decision 2: a NEW dedicated `RescheduledFromAppointmentId` column, NOT `OriginalAppointmentId`,
  because `EvaluationKind` exists precisely to stop a dual-purpose link mislabelling a Case Tracker
  case folder (`Appointment.cs:151-154`). Costs one column + a migration in BOTH contexts.
- Decision 3: the new appointment INHERITS THE SOURCE APPOINTMENT'S STATUS, with no re-approval --
  both sides already consented to this exact date in 4c. In practice that is `Approved`, because
  an external reschedule requires an Approved source.

  **CORRECTED AT BUILD TIME (2026-08-05, Adrian via modal).** The plan said "starts **Approved**",
  which was wrong for a real path. B1 (2026-07-01) lets INTERNAL STAFF reschedule a still-`Pending`
  appointment, and `SubmitRescheduleAsync` deliberately skips the Approved -> RescheduleRequested
  transition for a Pending source because no such transition exists -- so the appointment reaches
  finalize still `Pending`. Hardcoding `Approved` would have turned an unapproved appointment into
  an approved one purely by rescheduling it, bypassing the approval gate AND the claim-information
  check that guards it. The new row now inherits the source status literally, as the decision's own
  wording ("inherits the old status") always said.

- Decision 8 (Adrian, 2026-08-05, at build): `ConfirmReschedule` / `ConfirmRescheduleLate` are
  additionally permitted FROM `Pending`, so a Pending-source reschedule can close the old row
  through the state machine like any other. Without it finalize throws an invalid-transition for
  that entire path -- caught by a 4c test failing with a `BusinessException` rather than an
  assertion, which is what distinguished a real defect from a merely stale expectation.
- Decision 4: the old appointment goes to `RescheduledNoBill` / `RescheduledLate` from the billing
  outcome staff pick at finalize, using transitions that already exist but are unreachable
  (`AppointmentManager.cs:409-410`). No new enum value, no migration.
- Decision 5: the change request and its consent rounds STAY on the old appointment and are
  SURFACED on the new one through the chain link, so 4c's audit trail stays byte-for-byte while
  the new appointment still explains itself. Rejected: repointing the request, which would falsify
  the record -- it was filed against the old appointment and consent was agreed against it.
- Decision 6: the old appointment's packet is left intact as a historical record. It correctly
  documents what WAS scheduled and what parties were sent; regenerating would rewrite a document
  already in inboxes, deleting would destroy medical-legal evidence.

  **CORRECTED AT BUILD TIME (2026-08-05).** T7 said to call
  `AppointmentDocumentsAppService.RegeneratePacketAsync`. That method's FIRST act is
  `_readAccessGuard.EnsureCanReadAsync`, a guard written for a user-facing HTTP caller -- and staff
  finalizing a reschedule are not a party to the appointment they have just created, so it threw
  "You do not have permission to access this appointment". Finalize now enqueues the SAME
  `GenerateAppointmentPacketArgs` job that method enqueues. Same generator, no borrowed
  authorization. Calling one app service from another silently inherits its authorization.

- Decision 7 (Adrian, 2026-08-05, at design): **4d sends NOTHING new to Case Tracker.** Both the
  old appointment's terminal-status re-push AND the new appointment's intake push are SUPPRESSED
  until 4e amends the contract and Levon's receiver is ready. This is not the free default -- see
  the next section. Accepted cost: suppression code that 4e deletes.

### Why suppression is required, not automatic

`CaseTrackerPublishPolicy.IsPublished`
(`src/HealthcareSupport.CaseEvaluation.Domain/Integration/CaseTracker/CaseTrackerPublishPolicy.cs:19-26`)
returns TRUE for every status except `Pending`, `Rejected` and `InfoRequested`. So without
deliberate suppression:

- the OLD appointment landing on `RescheduledNoBill` / `RescheduledLate` is an
  `EntityUpdatedEventData<Appointment>`, which `AppointmentChangedHandler`
  (`Domain/Integration/CaseTracker/Handlers/AppointmentChangedHandler.cs:40-42`, re-push at
  `:137`) turns into an intake re-push once its packet set has settled;
- the NEW **Approved** appointment gets an intake push of its own once its packets settle
  (`CaseTrackerPacketPublishService.cs:65`), producing a SECOND case.

`docs/integration/case-tracker-api-contract.md` section E2 (`:395`) still states the portal
"moves the SAME appointment in place rather than cloning a row ... it never creates a second one",
and `:403` tells the receiver a reschedule is signalled by a CHANGED DATE, not a status change.
Both statements become false the moment 4d ships, which is why 4e owns the contract rewrite and 4d
must stay silent on the wire.

## All needed context

### The child-copy SHAPES (this is where F18 lives)

Bug F18 dropped 2 of 8 groups silently. The reason a flat "copy every table with an `AppointmentId`"
loop is not sufficient is that the groups have TWO different shapes:

| Shape                                                                | Groups                                                                                                                                                   | Copy rule                                                                       |
| -------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------- |
| **A. Direct child** (FK `AppointmentId`)                             | accessors, applicant attorneys, defense attorneys, claim examiners, employer details, injury details, primary insurances, custom field values, documents | new row, `AppointmentId` = new appointment                                      |
| **B. Grandchild** (FK points at a COPIED CHILD, not the appointment) | body parts -- `AppointmentBodyPart.AppointmentInjuryDetailId` (`AppointmentBodyPart.cs:18`)                                                              | must be copied AFTER injury details and re-pointed at the NEW injury-detail ids |

Shape B is the one a naive copier misses entirely: body parts carry no `AppointmentId`, so a
"copy every table with an AppointmentId" loop drops the group silently -- exactly F18.

**CORRECTED AT BUILD TIME (2026-08-05).** This table originally carried a third shape,
"C. Many-to-many join", claiming accessors link through
`AppointmentAccessorAppointment(AppointmentAccessorId, AppointmentId)` and that copying the
accessor row would duplicate a person. **That is wrong.** `AppointmentAccessorAppointment` is DEAD
CODE: nothing under `src/` writes it and it is neither mapped nor a `DbSet` in either context.
`AppointmentAccessor` carries `AppointmentId` directly, mapped as a plain required FK
(`CaseEvaluationDbContext.cs:924`, tenant `:816`). Accessors are Shape A, and copying one row per
appointment is CORRECT -- access is granted per appointment, so without its own rows the new
appointment would lose every authorized user.

Entities that ALSO carry an `AppointmentId` but must NOT be copied: `AppointmentChangeRequest`
(decision 5 -- stays on the old row), `AppointmentPacket` (decision 6 + regenerated for the new
row), `AppointmentInfoRequest` (belongs to the old row's review history), `ActiveSlotAppointment`
(a capacity projection, not user data).

### The code 4d rewrites

| What                                                              | Anchor                                                    |
| ----------------------------------------------------------------- | --------------------------------------------------------- |
| `ApproveRescheduleAsync` (finalize, as 4c left it)                | `AppointmentChangeRequestsAppService.Approval.cs:433`     |
| The in-place move block that becomes a split                      | `AppointmentChangeRequestsAppService.Approval.cs:474-483` |
| `RescheduleInPlacePolicy.ResolveFinalizedStatus` -- retired by 4d | `RescheduleInPlacePolicy.cs:30`                           |

### Machinery that already exists -- reuse, do not reinvent

- **The two transitions 4d needs are defined and currently unreachable:**
  `.Permit(ConfirmReschedule, RescheduledNoBill)` and `.Permit(ConfirmRescheduleLate, RescheduledLate)`
  at `AppointmentManager.cs:409-410`. Dead today because 4c keeps the status via
  `RescheduleInPlacePolicy` and records the outcome on the change-request row.
- **Confirmation numbers:** `ConfirmationNumberRetryPolicy.RunWithRetryAsync` wrapping
  `GenerateNextRequestConfirmationNumberAsync` (`AppointmentsAppService.cs:838-840`; generator at
  `:1047`). `(TenantId, RequestConfirmationNumber)` is a hard unique index, so the new appointment
  MUST get its own number and the retry policy exists for exactly that race.
- **Packet regeneration:** `AppointmentDocumentsAppService.RegeneratePacketAsync` (`:837`) enqueues
  all three kinds. Reuse it.
- **Documents:** `AppointmentDocument.BlobName` (`AppointmentDocument.cs:43`) is a pointer, so
  copying rows SHARES the blob; `AppointmentDocument.AppointmentId` is indexed but not unique, so
  copying is cheap. Consequence already accepted in the tracker: delete becomes soft-delete-only
  for shared blobs, matching the retention guarantee given to Case Tracker.

### Test harness

`test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/MultiOffice/` --
`CaseEvaluationMultiOfficeTestBase`, `MultiOfficeTestDatabase`, `MultiOfficeSeeder`
(`SeededOffice` gives `AppointmentTypeId`, `DoctorAvailabilityId`, `AppointmentId`, location ids).
It resolves real app services, bypasses authorization, and exercises the local-event -> handler ->
outbox chain. **Seed slots at `DateTime.Today + N`:** `BookingPolicyValidator` anchors on
`DateTime.Today` and the harness's own seeded slot is in the past (4c learning).

### Gotchas

- Dual-context: every mapping and migration comes in a PAIR (`CaseEvaluationDbContext` +
  `CaseEvaluationTenantDbContext`, `Migrations/` + `TenantMigrations/`).
- `Check.Positive` / `Check.NotDefaultOrNull` make an entity ctor self-guarding -- worth more than
  a test for an invariant (4c learning).
- A compiler-caught mutation beats a test-caught one; if inverting a branch produces CS86xx, the
  branch is type-enforced and needs no test (4b + 4c learning).
- Never bind a template expression that ALLOCATES to an ABP signal input -- it loops change
  detection and hangs the tab silently in a production build (4b learning).
- SonarCloud `new_duplicated_lines_density` will likely fail again (dual mapping + dual migration
  are required duplication). Reliability/security/maintainability are the conditions that matter.

## Tasks

### T1 -- chain column on the appointment

- what: MODIFY `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Appointment.cs` -- add
  `public virtual Guid? RescheduledFromAppointmentId { get; set; }` beside `OriginalAppointmentId`
  (`:146`), with an XML doc stating it is the RESCHEDULE chain and that `OriginalAppointmentId`
  remains the RE-EVALUATION chain, so the two meanings never merge again.
- pattern: the `OriginalAppointmentId` + `EvaluationKind` doc block (`:143-157`).
- approach: code
- acceptance (EARS): THE SYSTEM SHALL expose `RescheduledFromAppointmentId` as a nullable Guid on
  `Appointment`, and `OriginalAppointmentId` SHALL retain its existing meaning and callers.

### T2 -- EF mapping in BOTH contexts

- what: MODIFY `CaseEvaluationDbContext.cs` and `CaseEvaluationTenantDbContext.cs` -- map
  `RescheduledFromAppointmentId` in the `Entity<Appointment>` block, plus a non-unique index on it
  (the read-side join in T11 filters by it).
- pattern: the adjacent `OriginalAppointmentId` property mapping in the same block.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL map the column in both contexts and `dotnet build` SHALL
  succeed.

### T3 -- migrations in BOTH sets

- what: RUN, from `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore`:
  `dotnet ef migrations add Added_RescheduledFromAppointmentId -c CaseEvaluationDbContext -o Migrations`
  and `... -c CaseEvaluationTenantDbContext -o TenantMigrations`.
- pattern: `docs/database/MIGRATION-GUIDE.md`.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL contain one new migration in EACH set, and
  `has-pending-model-changes` SHALL report none for BOTH contexts.

### T4 -- the child cascade copier (THE HIGH-RISK TASK)

**CORRECTED AT BUILD TIME (2026-08-05, Adrian via modal).** The plan originally placed this in the
Domain layer and left the copy mechanism unspecified. Two problems surfaced on first contact:

1. A hand-written property copy is ~60 assignments across ten groups, and every column added to
   any child entity later must be remembered here or it is silently lost -- bug F18 one level down,
   at field granularity instead of group granularity.
2. The original acceptance ("the same number of rows in each group") proves only that a GROUP was
   not dropped. A copier producing the right row count with half the fields blank passes it.

Decision: copy via EF's `Entry(clone).CurrentValues.SetValues(source)`, which carries every mapped
scalar automatically -- a future column is copied with no code change. Consequence accepted: the
copier lives in the **EntityFrameworkCore** project, not Domain, because it needs the `DbContext`.

- what: CREATE
  `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Appointments/AppointmentChildCascadeCopier.cs`
  -- `CopyAllAsync(Guid sourceAppointmentId, Guid targetAppointmentId, Guid? tenantId)` copying, IN
  THIS ORDER: (1) employer details, (2) injury details -- capturing an old-id -> new-id map,
  (3) body parts re-pointed via that map, (4) applicant attorneys, (5) defense attorneys,
  (6) claim examiners, (7) primary insurances, (8) custom field values, (9) accessor JOIN rows,
  (10) documents (sharing `BlobName`). Each clone: `CurrentValues.SetValues(source)`, then override
  `Id` (fresh Guid), the appointment FK, and reset the audit columns so ABP stamps them fresh.
  Expose a per-group result (`CopiedGroupCounts`) so a caller and a test can assert each group
  individually. Declare the contract as `IAppointmentChildCascadeCopier` in Domain so the
  application layer depends on the abstraction, not the EF project.
- pattern: shape table above; accessors are an ordinary Shape A copy (`AppointmentId` FK);
  `AppointmentBodyPart(id, appointmentInjuryDetailId, bodyPartDescription)` for the
  grandchild.
- approach: tdd
- acceptance (EARS): WHEN `CopyAllAsync` runs against an appointment holding rows in all nine
  groups plus documents, THE SYSTEM SHALL create the same number of rows in each group against the
  target appointment, AND each copied row SHALL equal its source on every mapped scalar except
  `Id`, the appointment FK and the audit columns. WHEN injury details are copied, THE SYSTEM SHALL
  re-point every body part at the NEW injury-detail id and SHALL NOT leave any body part pointing
  at a source injury detail. WHEN accessors are copied, THE SYSTEM SHALL create join rows only and
  SHALL NOT duplicate any `AppointmentAccessor` row. WHEN documents are copied, THE SYSTEM SHALL
  reuse the source `BlobName` and SHALL NOT copy the blob. THE SYSTEM SHALL NOT copy change
  requests, packets, info requests or slot projections.

### T5 -- one test per group (NON-NEGOTIABLE)

- what: CREATE
  `test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/MultiOffice/MultiOfficeAppointmentChildCascadeTests.cs`
  -- seed one appointment with at least one row in EVERY group (including two injury details each
  with two body parts, and one accessor shared with a second appointment), run the copier, and
  assert EACH GROUP IN ITS OWN TEST. Ten tests minimum: nine groups + documents. Add one test
  asserting the not-copied set stays uncopied.
  Each group's test asserts FULL FIELD EQUALITY, not just a row count: compare the copied row to
  its source across every mapped scalar, excluding `Id`, the appointment FK and audit columns.
  Use the EF model metadata to enumerate those properties so a newly added column is compared
  automatically rather than needing the test updated.
- pattern: `MultiOfficeAppointmentsAppServiceTests` for resolution + seeding; seed slots at
  `DateTime.Today + N`.
- approach: tdd
- acceptance (EARS): THE SYSTEM SHALL have one test per group; deleting any single group from the
  copier SHALL fail that group's test AND the per-group counts test while every other group's test
  stays green; and blanking any single copied FIELD SHALL fail exactly that group's test.

  RESULT (2026-08-05, both mutations run then reverted): deleting the body-parts copy failed
  `Copies_body_parts_and_repoints_them_at_the_new_injury_details` + `Reports_a_count_for_every_group`
  (2 of 12; the other 10 stayed green, proving group isolation). Corrupting one field
  (`EmployerName`) failed `Copies_employer_details` alone (1 of 12). NOTE: the original wording
  said a dropped group fails "exactly one test". It fails TWO, because the counts test covers every
  group by design -- a better signal, not a defect.

  Seeding note: four groups carry REQUIRED FKs the office seeder does not create
  (`AppointmentAccessor.IdentityUserId`, `AppointmentApplicantAttorney.ApplicantAttorneyId`,
  `AppointmentDefenseAttorney.DefenseAttorneyId`, `CustomFieldValue.CustomFieldId`). Their parents
  are seeded in the test; without them SQLite fails with a bare "FOREIGN KEY constraint failed"
  naming no column. Only ONE accessor is seeded because the harness creates exactly one real
  identity user.

### T6 -- old-appointment terminal status policy

- what: CREATE
  `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentChangeRequests/RescheduleSplitPolicy.cs`
  -- pure `internal static`: `ResolveOldAppointmentTrigger(AppointmentStatusType outcome)` mapping
  `RescheduledNoBill -> AppointmentTransitionTrigger.ConfirmReschedule` and
  `RescheduledLate -> ConfirmRescheduleLate`, throwing
  `ChangeRequestInvalidRescheduleOutcome` for anything else. DELETE
  `RescheduleInPlacePolicy.cs` and its unit tests once T7 no longer references it.
- pattern: `ChangeRequestApprovalValidator` (pure `internal static` + `InternalsVisibleTo`).
- approach: tdd
- acceptance (EARS): WHEN the outcome is `RescheduledNoBill`, THE SYSTEM SHALL return
  `ConfirmReschedule`. WHEN it is `RescheduledLate`, THE SYSTEM SHALL return
  `ConfirmRescheduleLate`. WHEN it is any other status, THE SYSTEM SHALL throw
  `ChangeRequestInvalidRescheduleOutcome`.

### T7 -- finalize becomes a split

- what: MODIFY `AppointmentChangeRequestsAppService.Approval.cs` -- replace the in-place move
  (`:474-483`) with: (a) create the new appointment (status `Approved`, the round's slot, its own
  confirmation number via `ConfirmationNumberRetryPolicy`, `RescheduledFromAppointmentId` = old id,
  `OriginalAppointmentId` and `EvaluationKind` COPIED UNCHANGED from the source); (b) run
  `AppointmentChildCascadeCopier.CopyAllAsync`; (c) drive the OLD appointment through
  `AppointmentManager` with the trigger from `RescheduleSplitPolicy`; (d) call
  `RegeneratePacketAsync(newAppointmentId)`; (e) leave the old packet untouched. Inject
  `AppointmentChildCascadeCopier`, `IAppointmentDocumentsAppService` (or the packet regenerate
  seam) and the confirmation-number generator.
- pattern: `AppointmentsAppService.cs:838-840` for the retry-wrapped number; the existing
  `_localEventBus.PublishAsync(new AppointmentStatusChangedEto(...))` calls already in finalize.
- approach: tdd
- acceptance (EARS): WHEN a reschedule is finalized, THE SYSTEM SHALL create exactly one new
  appointment in status `Approved` on the consented round's slot, with its OWN
  `RequestConfirmationNumber` and `RescheduledFromAppointmentId` set to the old appointment.
  THE SYSTEM SHALL move the old appointment to `RescheduledNoBill` or `RescheduledLate` per the
  billing outcome. THE SYSTEM SHALL regenerate packets for the NEW appointment and SHALL leave the
  old appointment's packets unchanged. THE SYSTEM SHALL leave the change request and its consent
  rounds pointing at the OLD appointment.

### T8 -- verify the old slot needs no explicit release

- what: VERIFY (read-only, then codify) whether the old appointment's slot frees itself once the
  row is terminal, since the capacity model counts ACTIVE appointments per slot. If it does, add a
  test pinning it and NO production code. If it does not, release it in T7.
- pattern: `ActiveSlotAppointment` + the capacity count used by `GetDoctorAvailabilityLookupAsync`.
- approach: tdd
- acceptance (EARS): WHEN the old appointment reaches a terminal Rescheduled status, THE SYSTEM
  SHALL NOT count it against its slot's capacity, and that slot SHALL become offerable again.

  RESULT (2026-08-05): **VERIFIED -- no production code.** The capacity gate is
  `activeCount >= DoctorAvailability.Capacity` (`AppointmentsAppService.cs:1009`) fed by
  `IAppointmentRepository.GetActiveCountForSlotAsync`, whose predicate already excludes
  `RescheduledNoBill` and `RescheduledLate` (`EfCoreAppointmentRepository.cs:437-438`, and three
  sibling queries at `:462`, `:490`, `:534`). Closing the old row IS the release.

  `ActiveSlotAppointment` in the plan's pattern line is a query PROJECTION for the staff-schedule
  chips, not a stored slot reservation -- there is no reservation row to delete.

  Pinned by two tests, because the two clauses fail differently:
  - `MultiOfficeAppointmentsAppServiceTests.CreateAsync_WhenSlotHasRescheduledAppointments_DoesNotCountThem`
    -- a `[Theory]` over BOTH statuses, booking through the REAL gate on a **capacity-1** slot, so
    "offerable again" is exercised rather than restated. The pre-existing freed-status test covers
    `Rejected` only; these two arms were untested because until 4d nothing could produce them.
  - `MultiOfficeRescheduleConsentTests.Finalize_frees_the_old_slot_and_takes_the_agreed_one` -- the
    origin slot's active count goes 1 -> 0 and the agreed slot's 0 -> 1 across a real finalize.

  MUTATION (run, then reverted): dropping `RescheduledNoBill` from the `GetActiveCountForSlotAsync`
  predicate failed EXACTLY those two (78/80 passed) -- the consent test on the count assertion, the
  booking test with "This time slot is full (1 of 1 booked)" from `AppointmentsAppService.cs:1014`.
  The `RescheduledLate` theory case stayed green, proving the arms are independently covered.

### T9 -- suppress the old appointment's CT re-push

- what: MODIFY `Domain/Integration/CaseTracker/Handlers/AppointmentChangedHandler.cs` -- skip the
  re-push when the transition is INTO `RescheduledNoBill` / `RescheduledLate`, with a comment
  naming phase 4e as the owner and a `LogDebug` recording the skip so it is observable rather than
  silent.
- pattern: the existing settle-gate skip + `LogDebug` at `:127-134`.
- approach: tdd
- acceptance (EARS): WHEN an appointment moves to `RescheduledNoBill` or `RescheduledLate`, THE
  SYSTEM SHALL NOT enqueue an intake row and SHALL log the skip. WHEN an appointment moves to any
  other published status, THE SYSTEM SHALL enqueue exactly as before.

  **CORRECTED AT BUILD TIME (2026-08-05).** The plan split the condition by task -- T9 gates the OLD
  half, T10 the NEW one -- which reads as one condition per site. Wrong on both counts:

  1. BOTH conditions are needed at ALL THREE sites. The replacement is `Approved` with no intake
     row, so any later edit to it reaches `AppointmentChangedHandler` and pushes a FIRST intake --
     T10's hole, opened through T9's file. Gating T9's site on the closure status alone would have
     left it.
  2. The gate belongs in `RePushAsync` (after the row is re-read), not in the two
     `HandleEventAsync` overloads: one placement then covers the direct edit AND the patient
     demographic fan-out, which is a second, independent way into the same push.

  Both conditions therefore live in ONE new pure policy,
  `Domain/Integration/CaseTracker/CaseTrackerRescheduleSuppressionPolicy.cs`, called from three
  sites -- so 4e deletes one file plus three call sites and cannot leave an arm behind. Leaving one
  behind would show up as a silent divergence between the two systems, not as an error, which is
  risk 2 in this plan.

  ALSO CORRECTED: `AppointmentChangedHandlerTests.LifecycleChangesAfterApprovalArePushed` asserted
  `RescheduledNoBill` IS pushed -- the exact behaviour 4d reverses. It did not fail, because the
  test's `Build` stub made `FindAsync` return an `Approved` appointment whatever status the event
  carried, so the theory never exercised its own parameter past the `IsPublished` check. Fixed on
  both counts: the case moved to the suppression tests, and the stub now answers with the real row.

### T10 -- suppress the new appointment's CT intake push

- what: MODIFY the intake-publish path so an appointment whose `RescheduledFromAppointmentId` is
  set does NOT enqueue an intake row (`CaseTrackerPacketPublishService.cs:65` and the
  `CaseTrackerCompletenessSweepJob` enqueue at `:152`, so the sweep does not re-introduce it).
  Same 4e-owner comment + `LogDebug`.
- pattern: T9.
- approach: tdd
- acceptance (EARS): WHEN a rescheduled-from appointment's packet set completes, THE SYSTEM SHALL
  NOT enqueue an intake row. WHEN the completeness sweep runs, THE SYSTEM SHALL NOT enqueue one
  either. WHEN a normal appointment's packet set completes, THE SYSTEM SHALL enqueue exactly as
  before.

  RESULT (T9 + T10, 2026-08-05). Three gates, all calling
  `CaseTrackerRescheduleSuppressionPolicy.IsSuppressed`, each with a `LogDebug` naming 4e as the
  owner: `AppointmentChangedHandler.RePushAsync`,
  `CaseTrackerPacketPublishService.PublishSettledPacketsAsync` (before the intake-vs-document
  branch, so neither message type escapes), and the `CaseTrackerCompletenessSweepJob` loop.

  Ten tests across the three existing CaseTracker test files. MUTATIONS (each run, then reverted) --
  one per site, to prove the sites are independently covered and not all riding one test:
  - handler gate off -> 4 failed / 246 passed (both closure statuses, the replacement edit, and the
    patient fan-out);
  - packet-publish gate off -> 3 failed / 247 passed (the replacement's first-contact intake, and
    both closure statuses on the document feed);
  - sweep gate off -> 3 failed / 247 passed (both halves).

  Each mutation failed ONLY its own site's tests, so no site is covered by accident.

### T11 -- surface the chain on the new appointment (read side)

- what: MODIFY the appointment read DTO + its app service to expose
  `RescheduledFromAppointmentId`, the source appointment's `RequestConfirmationNumber`, and the
  agreed date -- populated set-based, mirroring 4b's `PopulateAppointmentContextAsync` /
  `ChangeRequestQueueContext` pattern rather than per-row lookups.
- pattern: `ChangeRequestQueueContext` + `PopulateAppointmentContextAsync` (phase 4b).
- approach: tdd
- acceptance (EARS): WHEN an appointment created by a reschedule is fetched, THE SYSTEM SHALL
  return the source appointment's confirmation number and the agreed date. WHEN a normal
  appointment is fetched, THE SYSTEM SHALL return nulls for those fields and SHALL issue no extra
  query per row.

### T12 -- regenerate Angular proxies

- what: RUN `abp generate-proxy -t ng -u http://localhost:44327 --module app` (DOTNET GLOBAL TOOL;
  `npx abp` fails). Keep ONLY the touched feature files + `generate-proxy.json`; revert the rest.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL expose the new chain fields on the appointment DTO and
  `npx ng build` SHALL succeed.

### T13 -- "rescheduled from" block on the appointment detail

- what: MODIFY the internal and external appointment detail components to render a block reading
  e.g. "Rescheduled from A00036, agreed by both sides on Aug 20, 2026" when the chain fields are
  present, linking to the source appointment. Render nothing when absent.
- pattern: the 4c consent block in `internal-change-request-inbox.component.html`; `formatSlotLabel`
  in `cr-approve.util.ts` for the date wording.
- approach: test-after
- acceptance (EARS): WHILE an appointment has a reschedule source, THE SYSTEM SHALL show the
  source's confirmation number and the agreed date and SHALL link to that appointment. WHILE it has
  none, THE SYSTEM SHALL render no such block.

### T14 -- pure util + specs for the chain block

- what: CREATE the derivation (`hasRescheduleSource`, `rescheduleSourceLabel`) as a pure util
  beside the component, with a spec, so the component holds no logic.
- pattern: `cr-approve.util.ts` + its spec.
- approach: tdd
- acceptance (EARS): THE SYSTEM SHALL return no label when the source id is absent, and a label
  containing the confirmation number and formatted date when present.

### T15 -- localization

- what: MODIFY `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`
  -- keys for the chain block copy.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL resolve every `abpLocalization` key used by the modified
  templates.

### T16 -- amend the tracker's locked decision + record 4d

- what: MODIFY `docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md` -- strike
  through the "reuse the create pipeline" locked decision with a dated CORRECTION explaining why it
  cannot hold (the DTO's 16 scalars, the client-side cascade) and what replaced it; set the 4d row
  to DONE with its PR/sha; move the "packets go stale after an in-place reschedule" pre-existing
  bug to FIXED; add 4d learnings; note that 4d joins the 4b+4c deploy train.
- pattern: the existing CORRECTED entries at `:169-175` and `:337-344`.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL NOT leave the create-pipeline decision stated as current
  fact, and the tracker SHALL record what 4d defers to 4e.

### T17 -- commit the research packet

- what: COPY `C:/src/patient-portal/handoff/PHASE-4D-RESEARCH.md` to
  `docs/research/2026-08-05-reschedule-creates-new-appointment.md`, correcting the two anchor
  errors found at design time (the DTO is 16 scalars + `CustomFieldValues`, not 17 + it; the
  in-place move is `:474-483`, not `:466`) and adding the three-shape child table and the
  suppression finding from this plan. Do NOT commit the handoff files themselves; delete them once
  4d lands.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL contain the research packet under `docs/research/`, and its
  cited anchors SHALL match the code at the merge commit.

## Validation loop

Backend:

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

Mutation checks (required, not optional):

- Per child group: delete that group from the copier, confirm EXACTLY ONE test in T5 fails, revert.
- Body parts: leave them pointing at the SOURCE injury-detail ids, confirm the re-point test fails.
- Accessors: copy the accessor ROW instead of the join row, confirm the duplication test fails.
- Suppression: remove the T9/T10 skip, confirm an outbox-row-count test fails.

## Live gate

`docker ps` first (the full backend suite has OOM'd the stack before); `docker compose up -d` to
restore; `docker restart main-api-1 main-angular-1` and wait for health + "Accepting connections".

Internal staff sign in at `admin.localhost:4200` -> `/host/my-offices` -> "Enter practice"
(`clistaff1@gesco.com` / `1q2w3E*r`). `admin@falkinstein.test` is a TENANT admin and exercises
neither real path.

1. Take a reschedule request through 4c's flow to a granted round, then FINALIZE.
2. Assert in SQL: TWO appointment rows -- the old one `RescheduledNoBill`/`RescheduledLate`, the new
   one `Approved` on the round's slot with a DIFFERENT `RequestConfirmationNumber` and
   `RescheduledFromAppointmentId` = the old id.
3. Assert every child group copied: count rows per group for both appointment ids, and confirm the
   new appointment's body parts point at the NEW injury-detail ids.
4. Assert the change request and both consent rounds still point at the OLD appointment.
5. Assert `AppIntegrationOutboxItems` gained NO new rows for either appointment (decision 7).
6. Assert the new appointment's packets regenerated and the old appointment's packets are unchanged.
7. Open the new appointment in the UI and confirm the "rescheduled from A000xx" block renders and
   links; open a normal appointment and confirm it does not.

Note: falkinstein **A00036** is a ready-made multi-round fixture (Approved at Aug 20 13:30, accepted
request, rounds 1 superseded / 2 granted, six consent outbox rows). Reset it if 4d needs it clean.

## Risk / rollback

Blast radius: the reschedule FINALIZE path, the appointment entity (one nullable column), the Case
Tracker publish path (two suppression gates), and the appointment detail read side. Booking,
submit, cancel and 4c's confirm/resend are untouched.

Highest risks:

1. **The copier is bug F18's ground.** Mitigated by the three-shape table, a per-group result, one
   test per group, and per-group mutation checks. If a group is discovered that is not in the
   table, STOP and update this plan rather than adding it silently.
2. **Suppression is code written to be deleted.** If 4e slips, the portal creates second
   appointments that Case Tracker never hears about -- a silent divergence. The `LogDebug` on each
   skip is what makes it observable; 4e must remove both gates together with the contract rewrite.
3. **Two migrations must land together.** A partial apply leaves one context without the column.
4. Deleting `RescheduleInPlacePolicy` removes a currently-tested class; its tests go with it, which
   will show as a coverage delta rather than a regression.
5. SonarCloud `new_duplicated_lines_density` will likely fail again on the required dual mapping +
   dual migration. Check WHICH condition failed before overriding; reliability is the one that
   matters.

Rollback: `git revert` the squash-merge. The column is additive and nullable; appointments already
created by a split keep their chain value harmlessly. Any second appointment already created stays
-- it is real user data and must not be auto-deleted.
