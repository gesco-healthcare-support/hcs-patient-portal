---
id: OBS-41
title: Injury body parts stored as a single free-text BodyPartsSummary string; OLD used structured per-body-part rows; AppAppointmentBodyParts entity exists but is dormant
severity: medium
status: open
found: 2026-05-27 (userflow audit; code + DB + OLD parity confirmed)
flow: appointment-booking, appointment-injury-details
component: angular/src/app/appointments/sections/appointment-add-claim-information.component.* (Body Parts textarea); src/HealthcareSupport.CaseEvaluation.Application/AppointmentInjuryDetails/AppointmentInjuryDetailsAppService.cs (BodyPartsSummary only); src/.../HttpApi/Controllers/AppointmentBodyParts/AppointmentBodyPartController.cs (dormant)
parity: simplification -- OLD structured rows reduced to a summary string
---

# OBS-41 - Body parts captured as free text, not structured rows

## Observation

The Claim Information modal captures body parts as a single free-text
textarea labelled "Body Parts *" (placeholder "e.g. Lower back, right
knee, left wrist"). On submit this is persisted as
`AppAppointmentInjuryDetails.BodyPartsSummary` (a string).
`AppointmentInjuryDetailsAppService.cs` only ever reads/writes
`input.BodyPartsSummary` (lines 105, 126) -- it never creates structured
body-part rows.

A structured entity DOES exist in NEW: `AppAppointmentBodyParts` table +
`AppointmentBodyPartController` + `AppointmentBodyParts` domain entity.
But the booking/injury flow does **not** populate it -- it is dormant
scaffolding.

## OLD parity

OLD modelled body parts as structured rows:
`AppointmentInjuryBodyPartDetail` (per body part: `bodyPartId` from a
body-parts lookup, `bodyPartDescription`, plus the parent `claimNumber`/
`dateOfInjury`), added via a dedicated body-part add modal
(`P:\PatientPortalOld\...\appointment-add.component.ts:433-461`). So NEW
collapses OLD's structured, lookup-backed body-part list into one
free-text summary.

## Functional impact

- Lower than the BUG-042/043 data-loss issues: the body-part *text* is
  captured and displayed (the view renders `injury.bodyPartsSummary`).
- BUT: no structured/queryable body-part data. Anything that needs per-
  body-part records (reporting, the dormant `AppAppointmentBodyParts`
  surface, OLD-parity packets that itemise body parts, analytics) cannot
  rely on it. Free text also admits inconsistent entry.

## NEW vs OLD body-part model (source-verified 2026-05-27)

- **NEW dormant entity** `AppointmentBodyPart`
  (`src/.../Domain/AppointmentBodyParts/AppointmentBodyPart.cs`) is a
  child of `AppointmentInjuryDetail` with **only** `BodyPartDescription`
  (free-text string) -- **no `bodyPartId` lookup**. It is NOT referenced
  by `AppointmentInjuryDetail` (no nav collection) and is never written
  by the booking/injury flow.
- **OLD** `AppointmentInjuryBodyPartDetail` carried a `bodyPartId`
  (lookup-backed) **plus** `bodyPartDescription`.

So even the NEW structured entity is a partial-parity model
(descriptions only, no lookup). Restoring structured body parts has two
possible depths: (a) wire `AppointmentBodyPart` description-rows into
booking+persistence+view; (b) additionally add a body-part lookup for
full OLD parity.

## Decision (2026-05-27, Adrian): separate slice

Planned as its own slice, NOT bundled with the BUG-042/043/044 fixes.
See `docs/plans/2026-05-27-structured-body-parts.md`.

## Related

- [[BUG-043]] claim-information-not-required -- same injury-detail
  subsystem.
- [[BUG-040]] cumulative-trauma-not-persisting.
