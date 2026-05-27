---
feature: structured-body-parts
date: 2026-05-27
status: draft
base-branch: main
related-issues: [OBS-41]
---

## Goal

Restore structured per-body-part capture for injury details, replacing
(or backing) the single free-text `BodyPartsSummary` string, by wiring
the dormant `AppointmentBodyPart` entity into the booking claim modal,
persistence, and the appointment view. Scoped as a standalone slice
because it is materially larger than the BUG-042/043/044 booking fixes
and carries open product questions.

## Context (source-verified 2026-05-27)

- **NEW captures body parts as one free-text textarea** ("Body Parts *")
  persisted to `AppAppointmentInjuryDetails.BodyPartsSummary` (a required
  string). `AppointmentInjuryDetailsAppService` only reads/writes
  `BodyPartsSummary` (lines 105, 126).
- A **dormant** structured entity exists:
  `src/.../Domain/AppointmentBodyParts/AppointmentBodyPart.cs` --
  `FullAuditedEntity<Guid>, IMultiTenant`, FK
  `AppointmentInjuryDetailId`, single field `BodyPartDescription`
  (free-text, **no `bodyPartId` lookup**). There is a manual controller
  `AppointmentBodyPartController`. `AppointmentInjuryDetail` imports the
  namespace but has **no nav collection** of body parts; nothing in the
  booking/injury flow writes `AppAppointmentBodyParts`.
- **OLD** modelled body parts as `AppointmentInjuryBodyPartDetail` rows
  carrying a `bodyPartId` (lookup-backed) **and** `bodyPartDescription`,
  added via a dedicated body-part add modal (OLD
  `appointment-add.component.ts:433-461`).

So NEW has two partial models (a summary string + a dormant
description-only child) and OLD has a lookup-backed multi-row model.

## Open product questions (resolve before build)

1. **Depth of parity:** description-only rows (use the existing
   `AppointmentBodyPart` as-is) vs full OLD parity with a body-part
   **lookup** (`bodyPartId` + a seeded body-parts master). OLD's lookup
   gives consistent, queryable body parts; description-only is simpler.
2. **Keep `BodyPartsSummary`?** Options: (a) drop it once structured rows
   exist; (b) keep it as a denormalized/display convenience derived from
   the rows; (c) keep both independently. Affects the view, packets, and
   the `NotNull` constraint on the injury entity.
3. **Per-body-part attributes:** does NEW need OLD's per-part
   date/claim linkage, or just a flat list of parts per injury?

## Approach (provisional -- pending Q1-Q3)

Assuming description-only rows + keep `BodyPartsSummary` as a derived
field for now (least disruptive, defers the lookup decision):

- Add a `BodyParts` nav collection to `AppointmentInjuryDetail` (or
  manage `AppointmentBodyPart` via its own repo keyed by injury id).
- Claim modal: replace the single textarea with a repeatable body-part
  list (add/remove rows of `BodyPartDescription`); compute
  `BodyPartsSummary` (comma-join) on submit so existing readers keep
  working.
- Persist `AppointmentBodyPart` rows alongside the injury detail in the
  injury-create path.
- Appointment view Claim Information card: render the structured body
  parts (list) instead of / in addition to the summary cell.

Full-lookup parity (Q1 = yes) would additionally require a body-parts
master entity + seed + lookup-select in the modal + `bodyPartId` on
`AppointmentBodyPart` (migration) -- a larger sub-slice.

## Tasks (provisional)

- T1: Decide Q1-Q3 with Adrian; lock scope. (gate)
  - approach: code (decision record)
- T2: Wire `AppointmentBodyPart` into the injury aggregate/persistence
  (repo + create path); migration if schema changes (e.g. nav
  collection, or `bodyPartId` if Q1=lookup).
  - approach: tdd
  - files-touched: [src/.../Domain/AppointmentInjuryDetails/AppointmentInjuryDetail.cs, src/.../Application/AppointmentInjuryDetails/AppointmentInjuryDetailsAppService.cs, src/.../Domain/AppointmentBodyParts/*, EF migration]
- T3: Claim modal -- repeatable body-part rows; derive `BodyPartsSummary`
  on submit.
  - approach: test-after
  - files-touched: [angular/src/app/appointments/sections/appointment-add-claim-information.component.* , angular/src/app/appointments/appointment-add.component.ts (persistInjuryDraftsIfProvided)]
- T4: Appointment view -- render structured body parts in the Claim
  Information card.
  - approach: test-after
  - files-touched: [angular/src/app/appointments/appointment/components/appointment-view.component.html + .ts]
- T5 (only if Q1=lookup): body-parts master entity + data seed +
  lookup-select in the modal.
  - approach: tdd + test-after

## Risk / Rollback

- Larger blast radius than the booking-bug slice (injury aggregate +
  modal redesign + view). Sequence AFTER the BUG-042/043/044 slice
  merges to avoid claim-modal merge conflicts (T7 of that plan touches
  the same submit path).
- If `BodyPartsSummary` stays derived, existing packet/email readers are
  unaffected (rollback-safe). Dropping it (Q2=a) would touch those
  readers -- higher risk.
- Migration additive if description-only; `bodyPartId` path adds a FK +
  seed (larger).

## Verification (after build)

1. Book an appointment, add a claim with 3 body parts (e.g. "Lower back",
   "Right knee", "Left wrist").
2. SQL: 3 `AppAppointmentBodyParts` rows for the injury; `BodyPartsSummary`
   reflects them (if kept).
3. View the appointment: Claim Information shows the 3 structured parts.
4. Packet/report output still renders body parts (no regression).
5. `dotnet test` green.
