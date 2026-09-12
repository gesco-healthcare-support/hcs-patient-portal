# Reschedule creates a new appointment -- Research (epic phase 4d)

Research-only output. No code edits in that pass. Produced 2026-08-05 against `main` at
`2ce2ef3f` (immediately after phase 4c merged as PR #428).

Epic tracker: `docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md`.
Companion plan: `docs/plans/2026-08-05-reschedule-creates-new-appointment.md`.

Every claim below was verified by reading the code at the cited `file:line`. Line numbers drift --
re-verify before editing.

**Committed after the build, with corrections folded in.** Entries marked CORRECTED were found to
be wrong once the code was actually written against them; they are kept, not deleted, because what
research got wrong is the most useful thing here. The plan's "Resolved decisions" section carries
the same corrections with their evidence.

---

## 1. The ask (source item R3)

"Old appointment goes to a Rescheduled status and a NEW appointment is created in the old one's
status, slot freed, history linked."

Before 4d, finalize moved the SAME appointment in place. 4d splits that into two rows.

---

## 2. THE HEADLINE FINDING -- a locked epic decision does not hold

The tracker recorded this locked decision:

> the new appointment is created by REUSING the existing create pipeline (`CreateRevalAsync`-style
> `AppointmentCreateDto` path) rather than resurrecting the deleted cascade cloner -- with an
> explicit per-child-group audit + tests, because the old cloner caused bug F18 (silently dropped
> 2 of 8 child groups).

**It cannot work as written.** `AppointmentCreateDto`
(`src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/AppointmentCreateDto.cs`,
71 lines total) carries **16 scalar properties plus `CustomFieldValues` -- and nothing else**. No
injuries, body parts, employer details, accessors, attorney details, insurances or claim examiners.

> CORRECTED (design time): the research pass first said "17 scalar fields plus `CustomFieldValues`".
> The count is 16. It changes nothing about the conclusion, but the number was cited as evidence,
> so it is fixed here rather than left to be re-counted by the next reader.

The child cascade is **CLIENT-SIDE**. The Angular booking wizard POSTs to six further endpoints
after the create call (`angular/src/app/appointments/appointment-add.component.ts`):

| Child group        | Endpoint                                  | Anchor           |
| ------------------ | ----------------------------------------- | ---------------- |
| Employer details   | `/api/app/appointment-employer-details`   | `:1485`, `:2822` |
| Injury details     | `/api/app/appointment-injury-details`     | `:1514`, `:3721` |
| Accessors          | `/api/app/appointment-accessors`          | `:1531`, `:3246` |
| Claim examiners    | `/api/app/appointment-claim-examiners`    | `:3658`          |
| Primary insurances | `/api/app/appointment-primary-insurances` | `:3693`          |
| Body parts         | `/api/app/appointment-body-parts`         | `:3750`          |

Plus two attorney upserts on the app service --
`UpsertApplicantAttorneyForAppointmentAsync` (`AppointmentsAppService.cs:1266`) and
`UpsertDefenseAttorneyForAppointmentAsync` (`:1523`) -- and a separate document upload loop.

The wizard's own comment confirms it: "The post-create child cascade is identical for all three
[create / reval / reSubmit], so prefilled drafts persist as fresh rows on the new appointment."

**Consequence for 4d:** finalize is a STAFF SERVER-SIDE action with no wizard in the loop.
Reusing the create pipeline yields an appointment row plus custom field values and nothing else.
A server-side cascade has to be written regardless; only its shape was ever negotiable.

RESOLVED (Adrian, 2026-08-05): write a server-side cascade copier scoped to 4d, with an explicit
audit of all nine groups and ONE TEST PER GROUP, so the F18 failure mode cannot repeat.
Rejected: growing `AppointmentCreateDto` into a create-with-children DTO so the server owns the
cascade for booking too -- architecturally the better end-state, but it changes the highest-traffic
flow in the product and belongs in its own phase.

---

## 3. Context packet

### The code 4d rewrites

| What                                                              | Anchor                                                    |
| ----------------------------------------------------------------- | --------------------------------------------------------- |
| `ApproveRescheduleAsync` (finalize, as 4c left it)                | `AppointmentChangeRequestsAppService.Approval.cs:433`     |
| The in-place move block that becomes a split                      | `AppointmentChangeRequestsAppService.Approval.cs:474-483` |
| `RescheduleInPlacePolicy.ResolveFinalizedStatus` -- retired by 4d | `RescheduleInPlacePolicy.cs:30`                           |

> CORRECTED (design time): the research pass cited `:466` for the in-place move block. The block
> is `:474-483`.

### The child-copy SHAPES -- where F18 lives

The groups have TWO shapes, not one, which is why a flat "copy every table with an `AppointmentId`"
loop is insufficient:

| Shape                                                                | Groups                                                                                                                                                   | Copy rule                                                                       |
| -------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------- |
| **A. Direct child** (FK `AppointmentId`)                             | accessors, applicant attorneys, defense attorneys, claim examiners, employer details, injury details, primary insurances, custom field values, documents | new row, `AppointmentId` = new appointment                                      |
| **B. Grandchild** (FK points at a COPIED CHILD, not the appointment) | body parts -- `AppointmentBodyPart.AppointmentInjuryDetailId` (`AppointmentBodyPart.cs:18`)                                                              | must be copied AFTER injury details and re-pointed at the NEW injury-detail ids |

Shape B is what a naive copier misses entirely: body parts carry no `AppointmentId`, so a
"copy every table with an AppointmentId" loop drops the group silently -- exactly F18.

> CORRECTED (build time): the design pass added a third shape, "C. Many-to-many join", claiming
> accessors link through `AppointmentAccessorAppointment(AppointmentAccessorId, AppointmentId)` and
> that copying the accessor row would duplicate a person. **That is wrong.**
> `AppointmentAccessorAppointment` is DEAD CODE -- nothing under `src/` writes it, and it is
> neither mapped nor a `DbSet` in either context. `AppointmentAccessor` carries `AppointmentId`
> directly as a plain required FK (`CaseEvaluationDbContext.cs:924`, tenant `:816`). Accessors are
> Shape A, and one row per appointment is CORRECT: access is granted per appointment, so building
> to the three-shape table would have shipped a replacement appointment that NOBODY could open.

Entities that also carry an `AppointmentId` but must NOT be copied: `AppointmentChangeRequest`
(stays on the old row), `AppointmentPacket` (historical record), `AppointmentInfoRequest` (the old
row's review history), `ActiveSlotAppointment` (a capacity projection, not user data).

### Machinery that already exists -- reuse, do not reinvent

- **The two state-machine transitions 4d needs are already defined and currently UNREACHABLE:**
  `RescheduleRequested --ConfirmReschedule--> RescheduledNoBill` and
  `--ConfirmRescheduleLate--> RescheduledLate`
  (`AppointmentManager.cs:409-410`). They are dead today because B2/4c keeps the appointment's
  status via `RescheduleInPlacePolicy` and records the outcome on the change-request row instead.
  4d makes them live for the OLD appointment.

  > CORRECTED (build time): both triggers also had to be permitted FROM `Pending`. B1 (2026-07-01)
  > lets internal staff reschedule a still-Pending appointment, and `SubmitRescheduleAsync` skips
  > the Approved -> RescheduleRequested transition for such a source because none exists -- so it
  > arrives at finalize still `Pending` and the close threw an invalid-transition.

- **Confirmation numbers:** `ConfirmationNumberRetryPolicy.RunWithRetryAsync` wrapping
  `GenerateNextRequestConfirmationNumberAsync` (`AppointmentsAppService.cs:838-840`, generator at
  `:1047`). `(TenantId, RequestConfirmationNumber)` is a hard unique index, so the new appointment
  MUST get its own number; the retry policy exists precisely for that race.
- **Packet regeneration:** `AppointmentDocumentsAppService.RegeneratePacketAsync` enqueues all
  three kinds.

  > CORRECTED (build time): DO NOT call that method from finalize. Its first act is
  > `_readAccessGuard.EnsureCanReadAsync`, a guard written for a user-facing HTTP caller, and staff
  > finalizing are not a party to the appointment they have just created -- it threw "You do not
  > have permission to access this appointment". Finalize enqueues the same
  > `GenerateAppointmentPacketArgs` job that method enqueues. Same generator, no borrowed
  > authorization. General lesson: calling one app service from another inherits its authorization
  > silently.

- **Packet-set completion gate:** `PacketSetPolicy.IsComplete` via `PacketsCompleteHandler`.

### Chain-link trap (matters for decision 2)

`Appointment.OriginalAppointmentId` (`Appointment.cs:146`) already exists and already links a
re-evaluation to its source. But `EvaluationKind` (`:157`) was introduced SPECIFICALLY because
`OriginalAppointmentId` "also carried reschedule-chain links historically" and deriving the case
kind from it "would mislabel" the Case Tracker case folder
(`AppointmentsAppService.cs:891`). Reusing that column for the reschedule chain re-creates the
exact ambiguity `EvaluationKind` was added to remove.

### Documents

`AppointmentDocument.BlobName` (`AppointmentDocument.cs:43`) is a pointer, so copying rows shares
the underlying blob. The tracker's locked decision stands: copy rows that SHARE the object key,
with the consequence that delete becomes soft-delete-only for shared blobs -- which already
matches the retention guarantee given to Case Tracker. `AppointmentDocument.AppointmentId` is
indexed but NOT unique, so copying is cheap. Packets are the opposite: the unique index
`(TenantId, AppointmentId, Kind)` filtered on `IsDeleted = 0` is why packets are REGENERATED
rather than copied.

### Bug this phase closes

Packets go STALE after an in-place reschedule: packet content embeds the appointment date
(`PacketTokenResolver.cs:222-233`) and nothing calls regeneration from the reschedule path. The
tracker lists this as "affects production until then" -- 4d is the "then".

---

## 4. Decisions taken (Adrian, 2026-08-05, via questions modal)

| #   | Decision                                  | Chosen                                                                                  | Rationale                                                                                                                                                                                                       |
| --- | ----------------------------------------- | --------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Child copy mechanism                      | Server-side cascade copier scoped to 4d                                                 | Per-group audit + one test per group is what makes it not-a-patchwork. See section 2.                                                                                                                           |
| 2   | Chain link                                | NEW dedicated `RescheduledFromAppointmentId` column                                     | Avoids re-creating the ambiguity `EvaluationKind` was added to remove. Costs one column + a migration in BOTH contexts.                                                                                         |
| 3   | New appointment's starting status         | Inherits the source appointment's status, no re-approval                                | Matches R3 literally. Both sides already consented to this exact date in 4c; a second approval is redundant work on a settled decision.                                                                         |
| 4   | Old appointment's terminal status         | `RescheduledNoBill` / `RescheduledLate` from the billing outcome staff pick at finalize | Uses the already-defined but unreachable transitions; no new enum value or migration; gives Case Tracker the billing signal 4e needs.                                                                           |
| 5   | Change request + consent rounds ownership | Stay on the OLD appointment, but SURFACED on the new one via the chain link             | Nothing is rewritten, so 4c's audit trail stays byte-for-byte. Read-side join + a UI block so the new appointment explains itself instead of arriving as an orphan.                                             |
| 6   | Old appointment's packet                  | Left intact as a historical record                                                      | The old row is terminal; its packet correctly documents what WAS scheduled and what parties were sent. Regenerating would rewrite a document already in inboxes; deleting would destroy medical-legal evidence. |

> CORRECTED (build time), decision 3: research and the plan both recorded the chosen value as
> **Approved** rather than "whatever the source was". Those are the same thing only for an external
> reschedule, which requires an Approved source. B1 lets internal staff reschedule a still-Pending
> appointment, and hardcoding `Approved` would have turned an UNAPPROVED appointment into an
> approved one purely by rescheduling it -- past the approval gate and the claim-information check
> that guards it. The decision's own wording ("inherits the old status") was right; the value
> written next to it was not.

Note decision 5 is the one that went BEYOND the recommended minimum, deliberately.

Rejected alternatives worth recording:

- **Extend the create pipeline into create-with-children** -- right end-state, wrong phase.
- **Copy only a clinical subset** -- would silently lose authorized users and attorney links,
  which external parties would notice as lost access.
- **Reuse `OriginalAppointmentId`** -- no migration, but re-introduces the mislabel risk.
- **Repoint the change request to the new appointment** -- falsifies the record; the request was
  filed against the old appointment and consent was agreed against it.

---

## 5. Open questions deliberately left for design/build time

Both are now closed. Kept with their answers, because the SHAPE of each answer is reusable.

1. **Case Tracker.** 4d changes what CT sees (two rows where there was one). The contract break and
   the two-case semantics are phase 4e, so 4d must send NOTHING new.

   ANSWERED (design + build): silence is NOT the free default.
   `CaseTrackerPublishPolicy.IsPublished` is a DENY list of exactly three statuses
   (`Pending`, `Rejected`, `InfoRequested`), so both halves of the split publish on their own --
   the old row carrying a NoBill/Late billing status, the new row opening a SECOND case for one
   claim. `docs/integration/case-tracker-api-contract.md` section E2 (`:395`) still tells the
   receiver the portal "never creates a second one" and that a reschedule is signalled by a CHANGED
   DATE (`:403`); both become false the moment 4d ships, which is 4e's rewrite.

   4d therefore adds THREE suppression gates, not the two first identified -- one policy
   (`CaseTrackerRescheduleSuppressionPolicy`) called from `AppointmentChangedHandler.RePushAsync`,
   `CaseTrackerPacketPublishService.PublishSettledPacketsAsync`, and the
   `CaseTrackerCompletenessSweepJob` loop. The sweep is the one most easily missed: a replacement
   appointment is published, settled and has no intake row, which is precisely that job's
   definition of a lost enqueue -- so within the hour it would have re-created the second case the
   other two gates prevented, and logged it as a successful recovery. **All three are deleted in
   4e.**

2. **Slot release.** R3 says the old slot is freed. Does the old appointment's own slot need an
   explicit release?

   ANSWERED (build): **no code at all.** The capacity gate is
   `activeCount >= DoctorAvailability.Capacity` (`AppointmentsAppService.cs:1009`) fed by
   `IAppointmentRepository.GetActiveCountForSlotAsync`, whose predicate already excludes
   `RescheduledNoBill` and `RescheduledLate` (`EfCoreAppointmentRepository.cs:437-438`, plus three
   sibling queries). Closing the old row IS the release. `ActiveSlotAppointment` is a query
   PROJECTION for the staff-schedule chips, not a stored reservation, so there is nothing to
   delete. What WAS missing was coverage: the pre-existing freed-status test pins `Rejected` only,
   and these two arms had never been tested because nothing could produce them until 4d.
