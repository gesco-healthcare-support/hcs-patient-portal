---
id: CI2
title: Keep Claim Information repeatable but strip per-injury Insurance/Claim Examiner
type: enhancement
components: [angular/src/app/appointments/sections/appointment-add-claim-information.component.ts, angular/src/app/appointments/sections/appointment-add-claim-information.component.html, angular/src/app/appointments/appointment-add.component.ts, angular/src/app/appointments/appointment/components/appointment-view.component.ts]
related_known_bugs: [BUG-040, OBS-41, BUG-043, BUG-045]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change
Claim Information must remain repeatable (multiple injuries -> multiple claim
numbers, each its own block) but the requester must NOT re-enter Insurance and
Claim Examiner for every block. After CI1 lifts Insurance + CE to a single
appointment-level entry, each repeatable per-injury block holds ONLY: Cumulative
Trauma flag, Date of Injury, Claim Number (required), WCAB Office (Venue), ADJ#
(required per CI3), and Body Parts (multiple). This item is the client-side
de-coupling: remove `primaryInsurance` / `claimExaminer` from the per-injury draft
and its form/serialize plumbing. Repeatability itself already exists and stays.

## Current behavior (from investigation)
- The per-injury draft model BUNDLES insurance + CE: `AppointmentInjuryDraft`
  declares `primaryInsurance` (appointment-add-claim-information.component.ts:40-51)
  and `claimExaminer` (`:52-63`) as nested objects on every injury row.
- The per-injury reactive FormGroup is flat and includes 10 insurance controls
  (`:188-197`) and 10 CE controls (`:198-207`), built in `buildInjuryForm`
  (`:164-208`).
- Conditional-required wiring exists per injury: `applyInsuranceRequiredValidators`
  (`:276-285`) and `applyClaimExaminerRequiredValidators` (`:291-311`), plus
  per-injury toggle subscriptions added in `buildInjuryForm` (`:221-231`).
- `makeEmptyInjuryDraft` seeds both nested objects with `isActive: true` defaults
  (`:246-269`).
- `serializeInjuryForm` re-emits insurance (`:339-350`) and CE (`:351-359`) back
  into the draft shape on every save.
- Repeatability is already implemented: `injuryDrafts: AppointmentInjuryDraft[]`
  (`:114`) with add/edit/remove modal management (`:393-465`); the HTML repeatable
  table is at appointment-add-claim-information.component.html:12-85, and the
  insurance/CE cards to remove are in the modal at `:233-486`.
- The parent submits per injury and currently has a SEPARATE per-injury POST
  cascade that attaches insurance + CE to each `AppointmentInjuryDetail`
  (appointment-add.component.ts:2752-2802), distinct from the injury-detail +
  body-parts POST loop (`:2703-2750`). The CE email fan-out reads
  `injuryDrafts[0].claimExaminer.email` (`:1149-1151`).
- Backend already models injuries one-to-many off the appointment
  (AppointmentInjuryDetail.cs has `AppointmentId`; repo
  EfCoreAppointmentInjuryDetailRepository.cs:41-76 returns a list per appointment),
  so repeatability needs NO backend change here.

## Relevant code locations
- angular/src/app/appointments/sections/appointment-add-claim-information.component.ts
  -- `AppointmentInjuryDraft` interface (`:40-63`), `buildInjuryForm` (`:164-208`),
  validator helpers (`:276-311`), toggle subs (`:221-231`),
  `makeEmptyInjuryDraft` (`:246-269`), `serializeInjuryForm` (`:339-359`).
- angular/src/app/appointments/sections/appointment-add-claim-information.component.html
  -- repeatable table (`:12-85`), insurance/CE modal cards to delete (`:233-486`).
- angular/src/app/appointments/appointment-add.component.ts -- per-injury
  insurance/CE POST cascade to lift out (`:2752-2802`), keep injury+body-parts
  loop (`:2703-2750`); CE email fan-out source (`:1149-1151`).
- angular/src/app/appointments/appointment/components/appointment-view.component.ts
  -- view currently reads insurance/CE per injury row (`:1196-1235`); must read
  the new appointment-level single entry instead (owned by CI1).
- appointment-add-claim-information.component.spec.ts -- tests the modal incl.
  insurance/CE validators; must drop those expectations.

## Phase 3 cross-reference
- BUG-040 -- cumulative-trauma flag + ToDateOfInjury not persisting from this same
  injury modal; fix while the same `serializeInjuryForm` path is being trimmed so
  the remaining per-injury fields all round-trip correctly.
- OBS-41 -- structured body-parts FormArray lives in the same modal; already
  partially addressed; confirm it stays intact after the insurance/CE cards are
  pulled (it sits at `:174-185` and is unaffected).
- BUG-043 -- at-least-one Claim Information row is now required (client-side at
  appointment-add.component.ts:1091 and in AppointmentManager); verify the
  required-row check still passes once each block is lighter (no behavioral change
  expected, but the spec touches the same submit guard).
- BUG-045 -- internal auto-approve booking drops insurance/CE via a 409 on the
  per-injury attach; removing the per-injury insurance/CE POST cascade (`:2752-2802`)
  directly retires the failing call path. Owned by CI1's persist restructure, but
  CI2 deletes the client cascade it depends on -- coordinate.

## Research findings
- Internal patterns / prior art:
  - Section components are template-only; ALL form-building, validators, and submit
    cascades live in `AppointmentAddComponent` (angular/src/app/appointments/CLAUDE.md,
    "Section components are template-only"). The injury draft array is passed in by
    reference and consumed at submit. So removing fields is a coordinated edit across
    the section component (model + form + serialize) and the parent (POST cascade),
    not a section-only change.
  - The top-level `claimExaminerEnabled` controls are already documented as vestigial
    and must stay false; the canonical CE today lives in the injury child FormGroup
    (CLAUDE.md "claimExaminerEnabled is vestigial"). CI1 moves CE to a real
    appointment-level entry; CI2 removes the now-dead per-injury copy.
- External docs: none required. This is a removal within Angular reactive forms;
  Angular signals/reactive-forms patterns already in use, no new API surface.

## Approaches considered (with tradeoffs)
1. CHOSEN -- Remove `primaryInsurance` + `claimExaminer` from `AppointmentInjuryDraft`
   and delete their controls/validators/subs from `buildInjuryForm`,
   `makeEmptyInjuryDraft`, and `serializeInjuryForm`; delete the insurance/CE modal
   cards; lift the per-injury insurance/CE POST cascade out of the parent (single
   appointment-level POST owned by CI1).
   - Pro: smallest correct model; eliminates duplicate data entry exactly as the
     feedback asks; retires the BUG-045 per-injury attach path; keeps repeatability
     untouched (no backend change for the one-to-many).
   - Con: must land in lockstep with CI1 (which adds the appointment-level Insurance/CE
     entry) and with the view-page read change, or the view loses CE/insurance display.
2. REJECTED -- Keep the per-injury insurance/CE fields in the model but hide the cards
   in the UI and copy block-1 values to all blocks.
   - Why rejected: leaves dead nested data on every injury row that downstream
     consumers (view, packet, email fan-out, BUG-045 path) still read, perpetuating
     the duplication the feedback wants gone; "hidden but present" is exactly the
     coupling CI2 is removing.
3. REJECTED -- Defer CI2 and only do the UI placement move in CI1.
   - Why rejected: CI1's appointment-level entry would coexist with live per-injury
     insurance/CE controls, giving two sources of truth and an unsubmittable / ambiguous
     form. The de-coupling must happen with the move.

## Decision (locked 2026-06-03)
After CI1, each repeatable per-injury block holds ONLY: Cumulative Trauma flag, Date
of Injury, Claim Number (req), WCAB Office (Venue), ADJ# (req), Body Parts (multiple).
Remove `primaryInsurance` and `claimExaminer` from `AppointmentInjuryDraft` and from
`makeEmptyInjuryDraft` / `serializeInjuryForm` / `buildInjuryForm` (controls + validator
helpers + toggle subscriptions). Repeatability is unchanged. This depends on CI1, which
owns the new single appointment-level Insurance + CE entry and the persist/read paths.

## Implementation outline (no code)
1. (Sequence after CI1) Confirm CI1 has added the appointment-level single Insurance +
   CE entry on the main form below the Attorney sections, with its own persist + view read.
2. Model: trim `AppointmentInjuryDraft` to the six retained fields; delete
   `primaryInsurance` (`:40-51`) and `claimExaminer` (`:52-63`).
3. Form build: delete the 20 insurance/CE controls from `buildInjuryForm` (`:188-207`),
   the `applyInsuranceRequiredValidators` / `applyClaimExaminerRequiredValidators`
   helpers (`:276-311`), and the two per-injury toggle subscriptions (`:221-231`).
4. Defaults/serialize: drop the nested objects from `makeEmptyInjuryDraft` (`:246-269`)
   and the insurance/CE blocks from `serializeInjuryForm` (`:339-359`).
5. Template: delete the insurance + CE cards from the modal
   (appointment-add-claim-information.component.html:233-486); keep the injury fields +
   body-parts FormArray.
6. Parent submit: remove the per-injury insurance/CE POST cascade
   (appointment-add.component.ts:2752-2802) and repoint the CE email fan-out
   (`:1149-1151`) to CI1's appointment-level CE source. Keep the injury-detail +
   body-parts loop (`:2703-2750`).
7. View page: change appointment-view.component.ts:1196-1235 to read the single
   appointment-level Insurance/CE (CI1) instead of per-injury rows.
8. Tests: update appointment-add-claim-information.component.spec.ts to drop
   insurance/CE validator expectations; verify the BUG-043 at-least-one-row guard still
   holds; add a check that two injury blocks share one Insurance/CE.
9. Migration: NONE for CI2 itself (repeatability already one-to-many; the FK move for
   Insurance/CE is CI1's migration). Proxy regen is owned by CI1 (insurance/CE model
   shape change); CI2 alone touches no proxy models.
10. Enforcement: required fields on the retained per-injury block (Claim Number, Date of
    Injury per buildInjuryForm `:168,:170`; ADJ# per CI3) are mirrored client-side AND
    server-side (DTO + AppointmentManager). CI2 changes no server validation; it only
    removes client controls.

## Dependencies
- DEPENDS ON CI1 (appointment-level single Insurance + CE entry, FK move, view read,
  proxy regen). CI2 cannot ship before CI1 or the form has two sources of truth.
- Coordinates with BUG-045 (CI2 deletes the failing per-injury attach client cascade)
  and CI3 (ADJ# required per retained block).
- Bundles BUG-040 and confirms OBS-41 survive the modal trim.

## Residual open questions
- none. The retained per-block field set and the "multiple blocks share one
  Insurance/CE" association are both fixed by the locked CI1/CI2 decision.
