---
id: AF6
title: PQME-only "have the panel strike list?" checkbox that makes the strike-list upload mandatory to submit
type: enhancement
components: [angular/src/app/appointments/appointment-add.component.ts, angular/src/app/appointments/appointment-add.component.html, angular/src/app/appointment-documents/appointment-documents.component.ts]
related_known_bugs: [BUG-043, BUG-044, OBS-41, AF5, AF7]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change

When the booker selects Panel QME (PQME), show a checkbox between the Claim Information
section and the Additional Authorized User section asking whether they have the panel
strike list. When checked, uploading the strike-list document becomes a hard requirement
to submit the booking. The checkbox is PQME-only: it is hidden (and its value cleared) for
AME and IME. Enforcement is a client-side conditional reactive validator wired into the
existing pre-submit gate, so an invalid PQME booking never persists.

## Current behavior (from investigation)

- No such checkbox exists today. The booking form jumps straight from the Claim Information
  child component to the Authorized Users child component
  (`appointment-add.component.html:157-179`).
- Submit gates today are, in order: new-patient required-field check, `form.invalid`, and
  the at-least-one Claim Information guard (`appointment-add.component.ts:1059-1096`). The
  Claim Information guard (BUG-043) is the established inline submit-gate precedent --
  it sets a `claimInformationMissing` flag, sets `patientLoadMessage`, and `return`s before
  the appointment is created (`appointment-add.component.ts:1091-1096`).
- The `appointmentTypeId` control already drives per-type behavior via a `valueChanges`
  subscription that re-applies field configs and rebuilds custom fields
  (`appointment-add.component.ts:512-518`). This is the existing hook a PQME-conditional
  control would extend (OBS-41 type-conditional form behavior precedent).
- The Authorized Users block is wrapped in `@if (shouldShowAuthorizedUserSection())` and is
  hidden for Claim Examiner bookers (`appointment-add.component.html:174-179`), so the new
  checkbox must be a SIBLING block placed after the Claim Information component and BEFORE
  the `@if` Authorized Users block -- it cannot live inside that conditional.
- No panel-strike-list concept exists on the data model. `AppointmentDocument` carries only
  `IsAdHoc` and `IsJointDeclaration` booleans plus a free-text `DocumentName`; the typed
  document master library was deliberately not ported (AF5 / parity-v2/03 G-03-01). The
  `IsPanelStrikeList` flag that AF6 keys off is NET-NEW and lands in AF5.
- Document upload is structurally post-submit only today: the booker creates the appointment
  first, POSTs child entities, then navigates away -- there is no in-form upload step
  (`appointment-add.component.ts:1059-1096` flow; upload UI mounts only on the view page).
  Making the strike-list upload a submit precondition therefore REQUIRES the pre-submit
  upload mechanism delivered by AF7. This is a hard dependency.

## Relevant code locations

- `angular/src/app/appointments/appointment-add.component.html:157-179` -- the sibling slot
  for the new checkbox block (after Claim Information, before the Authorized Users `@if`).
- `angular/src/app/appointments/appointment-add.component.ts:512-518` -- the
  `appointmentTypeId.valueChanges` subscription that must toggle the checkbox control's
  visibility/validators and clear its value on non-PQME types.
- `angular/src/app/appointments/appointment-add.component.ts:1059-1096` -- the `onSubmit`
  gate where the new PQME strike-list guard slots in alongside the BUG-043 guard.
- `angular/src/app/appointment-documents/appointment-documents.component.ts` -- the upload
  surface; AF7 reshapes it for in-form pre-submit upload and AF6 reads its staged-file state
  to decide whether the mandatory-upload gate is satisfied.

## Phase 3 cross-reference

- BUG-043 (at-least-one Claim Information submit gate) -- reuse its exact inline-gate shape
  (flag + message + early `return`) so the new PQME gate reads consistently; no separate fix
  needed, just mirror the pattern.
- BUG-044 / F4 (AA/DA Include-toggle + confirmation-modal) -- the precedent for a
  type/role-conditional boolean control that re-applies validators on `valueChanges`; reuse
  the validator-toggle discipline (always `updateValueAndValidity({ emitEvent: false })` to
  avoid the recursive-loop gotcha documented in `appointments/CLAUDE.md`).
- OBS-41 (type-conditional form behavior) -- confirms the `appointmentTypeId.valueChanges`
  hook is the canonical place for PQME-conditional logic.

## Research findings

- Internal patterns / prior art:
  - The conditional-required-on-a-boolean pattern is already proven by the AA/DA Include
    toggles (BUG-044): a boolean control gates `Validators.required` on a dependent field,
    re-applied inside the `valueChanges` subscriber. AF6 is the same shape, except the
    "dependent field" is the presence of a staged strike-list document (AF7 state) rather
    than a text control.
  - The inline submit-gate (set flag, set message, `return` before persist) is the
    established BUG-043 idiom at `appointment-add.component.ts:1091-1096`. AF6 adds a
    parallel guard, not a new mechanism.
  - PQME identity must key off the seed GUID (per the locked appointment-types decision:
    "key off the PQME type identity (seed GUID), not a name substring"), consistent with the
    panel-number rule (AF-adjacent). Do NOT match on the label string.
- External docs (Angular):
  - Angular reactive forms support dynamic validators via `setValidators` /
    `clearValidators` followed by `updateValueAndValidity`; this is the documented way to
    make a control conditionally required (Angular docs, "Adding dynamic validators").
    Confidence HIGH. The repo already uses this idiom in the attorney-section validators
    (`shared/attorney-section-validators.ts`).

## Approaches considered (with tradeoffs)

1. Client-side conditional reactive validator wired into the existing pre-submit gate
   (CHOSEN). A `hasPanelStrikeList` boolean control, shown only for PQME, plus an `onSubmit`
   guard that blocks when checked-but-no-strike-list-file-staged. Because AF7 makes upload
   pre-submit, the guard runs BEFORE the appointment is created -- an invalid PQME booking
   never persists. Reuses BUG-043 / BUG-044 patterns; smallest blast radius; no migration.
   Tradeoff: the gate is client-only, so a crafted API call could still create a PQME
   appointment without a strike list (mitigated below).

2. Server-side enforcement in the AppointmentCreate DTO + AppointmentManager
   (REJECTED for AF6 scope). Would require the strike-list document to be part of the
   create transaction (it is not -- documents are separate children, and even after AF7 the
   upload is a distinct POST). Adding a server invariant "PQME with hasPanelStrikeList=true
   must have a strike-list document" creates a chicken-and-egg with the two-phase create
   (AF7 Option A): the appointment must exist before the document can be attached, so the
   server cannot atomically enforce it at create time without a larger transactional rework.
   The locked decision scopes AF6 enforcement to client-side; server-side document-type
   integrity is AF5's concern. Rejected as out-of-scope and architecturally premature here.

3. Free-text "I confirm I have the strike list" attestation with no upload requirement
   (REJECTED). Does not match the feedback ("uploading that document becomes mandatory").
   An attestation without an artifact gives staff nothing to verify against (AF5's whole
   purpose is the document staff verify office-correctness from). Rejected.

Why the chosen direction wins: it satisfies the feedback exactly (checkbox -> mandatory
upload), reuses two proven in-repo patterns, requires no migration of its own, and -- by
riding on AF7's pre-submit upload -- guarantees no invalid PQME booking ever reaches the
database. The client-only enforcement is acceptable because the booking form is the sole
HTTP surface bookers use, and the strike-list document is a workflow aid (staff manually
verify it per AF5), not a security boundary.

## Decision (locked 2026-06-03)

A PQME-only checkbox sits between Claim Information and Additional Authorized User asking
whether the booker has the panel strike list. When checked, the strike-list upload is
mandatory to submit, enforced as a client-side conditional reactive validator wired into the
existing pre-submit gate (pre-create), so an invalid PQME booking never persists. The
checkbox is hidden and its value cleared for AME and IME. PQME is identified by its seed
GUID, not a label substring. Hard dependency on AF7 (pre-submit upload) and on AF5 (the
`IsPanelStrikeList` flag that marks which staged document is the strike list).

## Implementation outline (no code)

1. (Layer: Angular form state) Add a `hasPanelStrikeList` boolean FormControl to the booker
   FormGroup in `appointment-add.component.ts`, defaulting to `false`. Do NOT add it to any
   create DTO -- it is a transient UI gate, not persisted (the persisted signal is AF5's
   `IsPanelStrikeList` flag on the uploaded document).
2. (Layer: Angular template) Add a sibling checkbox block in
   `appointment-add.component.html` immediately after the `<app-appointment-add-claim-information>`
   block (line 162) and before the `@if (shouldShowAuthorizedUserSection())` block (line 174).
   Wrap it in `@if (isPqmeSelected())` so it renders only for PQME. Use ng-bootstrap styling
   consistent with the rest of the form. Keep copy synthetic and plain.
3. (Layer: Angular cascade) Extend the existing `appointmentTypeId.valueChanges` subscriber
   (`appointment-add.component.ts:512-518`): when the type is not PQME, reset
   `hasPanelStrikeList` to `false` (clear value). Compute PQME via the seed GUID. Mirror the
   BUG-044 validator-toggle discipline -- if any dependent validator is added, call
   `updateValueAndValidity({ emitEvent: false })` to avoid the recursive-loop gotcha.
4. (Layer: Angular submit gate) In `onSubmit` (`appointment-add.component.ts:1059-1096`), add
   a guard alongside the BUG-043 block: if PQME is selected AND `hasPanelStrikeList === true`
   AND no staged document is flagged as the strike list (AF7 staged-file state, AF5 flag),
   set a `panelStrikeListMissing` flag + `patientLoadMessage` and `return` before create.
   Reuse the BUG-043 flag/message/return shape.
5. (Layer: Angular validation message) Add the inline error block in the template
   (mirroring `claimInformationMissing` at html:163-167) bound to the new flag.
6. No migration, no proxy regen, and no DTO change for AF6 itself (the persisted flag and any
   schema change belong to AF5/AF7). Server-vs-UI: AF6 enforcement is UI-only by decision;
   document-type integrity (AF5) and the pre-submit upload contract (AF7) carry the
   server-side concerns.

## Dependencies

- DEPENDS ON AF7 -- mandatory upload requires the pre-submit (create-then-upload, Option A)
  mechanism; without it there is no staged document for the AF6 gate to check.
- DEPENDS ON AF5 -- the `IsPanelStrikeList` boolean flag that marks which staged document is
  the strike list (AF6 reads this to satisfy the gate).
- DEPENDS ON the appointment-types decision -- PQME seed GUID must be resolvable client-side
  (same dependency as the panel-number rule).

## Residual open questions

- Exact checkbox label and inline-error copy (cosmetic; pick plain synthetic wording at build
  time, e.g. "Do you have the panel strike list for this PQME appointment?").
- Whether checking the box but staging no file should block on submit only, or also show a
  live inline hint as soon as the box is checked (UI affordance; default to submit-time block
  per the locked decision, optional live hint is a nicety).
