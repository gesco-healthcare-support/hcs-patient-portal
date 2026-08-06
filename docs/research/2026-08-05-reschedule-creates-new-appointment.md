# Reschedule creates a new appointment -- Research (epic phase 4d)

Research-only output. No code edits in this pass. Produced 2026-08-05 against `main` at
`2ce2ef3f` (immediately after phase 4c merged as PR #428).

Epic tracker: `docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md`.
Companion plan: NOT YET WRITTEN -- `/feature-design` is the next step.

Every claim below was verified by reading the current code at the cited `file:line`, not from
memory or a prior summary. Confidence is HIGH unless stated otherwise. Line numbers drift --
re-verify before editing.

---

## 1. The ask (source item R3)

"Old appointment goes to a Rescheduled status and a NEW appointment is created in the old one's
status, slot freed, history linked."

Today, finalize moves the SAME appointment in place. 4d splits that into two rows.

---

## 2. THE HEADLINE FINDING -- a locked epic decision does not hold

The tracker records this locked decision:

> the new appointment is created by REUSING the existing create pipeline (`CreateRevalAsync`-style
> `AppointmentCreateDto` path) rather than resurrecting the deleted cascade cloner -- with an
> explicit per-child-group audit + tests, because the old cloner caused bug F18 (silently dropped
> 2 of 8 child groups).

**It cannot work as written.** `AppointmentCreateDto`
(`src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/AppointmentCreateDto.cs`,
71 lines total) carries **17 scalar fields plus `CustomFieldValues` -- and nothing else**. No
injuries, body parts, employer details, accessors, attorney details, insurances or claim
examiners.

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

| What                                                              | Anchor                                                |
| ----------------------------------------------------------------- | ----------------------------------------------------- |
| `ApproveRescheduleAsync` (finalize, as 4c left it)                | `AppointmentChangeRequestsAppService.Approval.cs:433` |
| The in-place move block that becomes a split                      | `AppointmentChangeRequestsAppService.Approval.cs:466` |
| `RescheduleInPlacePolicy.ResolveFinalizedStatus` -- retired by 4d | `RescheduleInPlacePolicy.cs:30`                       |

### Machinery that already exists -- reuse, do not reinvent

- **The two state-machine transitions 4d needs are already defined and currently UNREACHABLE:**
  `RescheduleRequested --ConfirmReschedule--> RescheduledNoBill` and
  `--ConfirmRescheduleLate--> RescheduledLate`
  (`AppointmentManager.cs:409-410`). They are dead today because B2/4c keeps the appointment's
  status via `RescheduleInPlacePolicy` and records the outcome on the change-request row instead.
  4d makes them live for the OLD appointment.
- **Confirmation numbers:** `ConfirmationNumberRetryPolicy.RunWithRetryAsync` wrapping
  `GenerateNextRequestConfirmationNumberAsync` (`AppointmentsAppService.cs:838-840`, generator at
  `:1047`). `(TenantId, RequestConfirmationNumber)` is a hard unique index, so the new appointment
  MUST get its own number; the retry policy exists precisely for that race.
- **Packet regeneration:** `AppointmentDocumentsAppService.RegeneratePacketAsync` enqueues all
  three kinds. Reuse it; do not write a new generator.
- **Packet-set completion gate:** `PacketSetPolicy.IsComplete` via `PacketsCompleteHandler`.

### The nine child groups

Accessors, applicant attorneys, defense attorneys, body parts, claim examiners, employer details,
injury details, primary insurances, custom field values -- plus documents (separate concern).
All hang off `AppointmentId` with `OnDelete: NoAction`.

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

| #   | Decision                                  | Chosen                                                                                  | Rationale                                                                                                                                                                                                                       |
| --- | ----------------------------------------- | --------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Child copy mechanism                      | Server-side cascade copier scoped to 4d                                                 | Per-group audit + one test per group is what makes it not-a-patchwork. See section 2.                                                                                                                                           |
| 2   | Chain link                                | NEW dedicated `RescheduledFromAppointmentId` column                                     | Avoids re-creating the ambiguity `EvaluationKind` was added to remove. Costs one column + a migration in BOTH contexts.                                                                                                         |
| 3   | New appointment's starting status         | **Approved** -- inherits the old status, no re-approval                                 | Matches R3 literally. Both sides already consented to this exact date in 4c; a second approval is redundant work on a settled decision.                                                                                         |
| 4   | Old appointment's terminal status         | `RescheduledNoBill` / `RescheduledLate` from the billing outcome staff pick at finalize | Uses the already-defined but unreachable transitions; no new enum value or migration; gives Case Tracker the billing signal 4e needs.                                                                                           |
| 5   | Change request + consent rounds ownership | Stay on the OLD appointment, but SURFACED on the new one via the chain link             | Nothing is rewritten, so 4c's audit trail stays byte-for-byte. Read-side join + a UI block so the new appointment explains itself ("rescheduled from A00036, agreed by both sides on <date>") instead of arriving as an orphan. |
| 6   | Old appointment's packet                  | Left intact as a historical record                                                      | The old row is terminal; its packet correctly documents what WAS scheduled and what parties were sent. Regenerating would rewrite a document already in inboxes; deleting would destroy medical-legal evidence.                 |

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

None blocking. Two to keep in view:

1. **Case Tracker.** 4d changes what CT sees (two rows where there was one). The contract break
   and the two-case semantics are phase 4e, so the plan must state explicitly what 4d pushes and
   what it defers -- `docs/integration/case-tracker-api-contract.md` section E2 still documents
   the OPPOSITE ("moves the SAME appointment in place ... never creates a second one").
2. **Slot release.** R3 says the old slot is freed. The existing finalize already releases the
   transient Reserved hold; confirm whether the OLD appointment's own slot needs an explicit
   release now that the appointment no longer occupies it, since the capacity model counts active
   appointments per slot and the old row lands in a terminal status (which is excluded from the
   active count -- so this may need no code at all).
