---
id: AF3
title: Panel Number disabled, cleared, and server-rejected for non-PQME appointment types
type: enhancement
components: [angular/src/app/appointments/appointment-add.component.ts, angular/src/app/appointments/sections/appointment-add-schedule.component.html, angular/src/app/appointments/appointment/components/appointment-view.component.ts, src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/AppointmentCreateDto.cs, src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/AppointmentUpdateDto.cs, src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs]
related_known_bugs: [BUG-043, BUG-012, OBS-24]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change
For AME and IME (every non-PQME type), Panel Number is not relevant. The control must be
disabled/blocked from entry and any value cleared when a non-PQME type is selected, on both
the add booking form and the view/edit form. Server-side defense-in-depth: reject a
PanelNumber submitted against a non-PQME type. This is the "off" half of one state machine;
AF4 is the "on" half (PQME makes Panel Number required).

## Current behavior (from investigation)
Panel Number is unconditionally optional and always editable on every surface (findings
wzbyujjsd.output:168).
- Add form control: `panelNumber: [null, [Validators.maxLength(50)]]` --
  `appointment-add.component.ts:386`. Only a length check; no type-dependent logic.
- Rendered as a plain text input with `maxlength=50` and no `[readonly]`/`[disabled]` binding
  -- `appointment-add-schedule.component.html:24-32` (verified: lines 24-32 are exactly the
  Panel Number `<div class="col-md-3">` with `formControlName="panelNumber"` and only an
  `[class.is-invalid]` binding).
- The `appointmentTypeId` valueChanges subscriber does field-configs, custom-fields, and slot
  loading only -- it does nothing to panelNumber -- `appointment-add.component.ts:512-518`
  (verified: subscriber calls `loadAvailableDatesBySelection`, `applyFieldConfigsForAppointmentType`,
  `loadCustomFieldsForAppointmentType`; no panelNumber branch).
- View/edit form mirrors this: `appointment-view.component.ts:205` (panelNumber with
  `Validators.maxLength(50)` only).
- Backend: `AppointmentCreateDto.cs:11-12` and `AppointmentUpdateDto.cs:12-13` --
  `PanelNumber string?` with `StringLength(50)`, not required, no type conditional.
  `AppointmentManager.cs:34,45,155,163,180` -- `panelNumber` is an optional param with only
  `Check.Length`, no required/conditional logic anywhere.

## Relevant code locations
- `angular/src/app/appointments/appointment-add.component.ts:386` -- panelNumber control decl.
- `angular/src/app/appointments/appointment-add.component.ts:512-518` -- appointmentTypeId
  valueChanges subscriber (the hook point shared with AF4).
- `angular/src/app/appointments/appointment-add.component.ts:2449-2459` -- existing
  setValidators/clearValidators + `patchValue(null)` + `updateValueAndValidity({ emitEvent:false })`
  precedent for `appointmentDate` (verified). This is the exact state-machine pattern to reuse.
- `angular/src/app/appointments/sections/appointment-add-schedule.component.html:24-32` --
  Panel Number input; needs a disabled/readonly affordance keyed off PQME state.
- `angular/src/app/appointments/appointment/components/appointment-view.component.ts:205` --
  view/edit form panelNumber control (apply same conditional on edit).
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/AppointmentCreateDto.cs:11-12`
  and `AppointmentUpdateDto.cs:12-13` -- DTO PanelNumber (DTO-level rejection lives here or in manager).
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs:34,45,155,163,180`
  -- domain manager Create/Update paths; add the PQME-identity conditional check here.

## Phase 3 cross-reference
- OBS-24 -- server-side defense-in-depth gap (UI enforces, server does not). Fix here by
  touching the DTO/manager, not just Angular. This item exists partly to NOT repeat that gap.
- BUG-043 -- Claim Information conditional-required-before-submit precedent on this same
  booking form; reuse its cross-field wiring shape for the clear/disable cascade.
- BUG-012 -- "firmname-required" conditional required-validator precedent referenced at
  `appointment-view.component.ts:343`; relevant to AF4 (the "on" half) but the same view-form
  wiring locus matters for the AF3 disable path on edit.

## Research findings
- Internal patterns / prior art:
  - The `appointmentDate` cascade (`appointment-add.component.ts:2449-2459`) already does
    exactly the four-step move this needs: `setValidators([...])` / `clearValidators()`,
    `patchValue(... : null)` to wipe stale state, then
    `updateValueAndValidity({ emitEvent: false })`. AF3 reuses this verbatim for panelNumber.
  - `claimExaminerEnabled` is documented as vestigial in `angular/src/app/appointments/CLAUDE.md`
    -- do NOT model the new conditional on that broken pattern (it makes the form
    permanently unsubmittable). Use the appointmentDate pattern instead.
  - The component already maintains race-safe request-version counters for per-type fetches
    (CLAUDE.md). The panelNumber toggle is synchronous (no HTTP), so it needs no counter, but
    it MUST live inside the same `appointmentTypeId` valueChanges subscriber to stay ordered.
  - Sections are template-only: all form logic stays in `AppointmentAddComponent`; the section
    HTML only renders a `[readonly]`/`[disabled]` binding driven by a parent flag.
- External docs (Angular):
  - Disabling a reactive control: `FormControl.disable()`/`enable()` is the canonical Angular
    20 mechanism. A disabled control is excluded from `form.value` but included in
    `getRawValue()` -- the view form already saves with `getRawValue()` (appointments/CLAUDE.md),
    so a disabled panelNumber on edit will still serialize correctly. Confidence HIGH
    (Angular ReactiveFormsModule docs).
  - Prefer `disable()` over template `[disabled]` on reactive controls: Angular logs a warning
    when `[disabled]` is set on a `formControlName` input. Use programmatic `disable()`/`enable()`
    in the TS subscriber; the HTML keeps only a visual affordance. Confidence HIGH.

## Approaches considered (with tradeoffs)
1. Programmatic `disable()` + clear value, keyed off PQME seed GUID (CHOSEN).
   - Pros: single source of truth in the parent subscriber; reuses the proven appointmentDate
     pattern; `disable()` keeps the control out of `form.value` so a stale value cannot leak;
     identity check is rename-proof (AF1 renames "Panel QME" to "PQME").
   - Cons: must remember to `enable()` on switch back to PQME; disabled controls need the
     `getRawValue()` save path (already in place on the view form).
2. Template `[disabled]`/`[readonly]` binding only (no programmatic disable).
   - Rejected: Angular warns on `[disabled]` over `formControlName`; `[readonly]` alone leaves
     the value in `form.value`, so a previously-typed Panel Number would still submit -- the
     "clear" requirement would be unmet and OBS-24 would recur.
3. Key the conditional off the type NAME substring (as the legacy booking-horizon router does).
   - Rejected by locked decision: brittle against the AME/IME/PQME label changes in AF1 and
     against localization. Must key off the seeded PQME GUID identity.
4. UI-only enforcement, no server change.
   - Rejected: OBS-24 explicitly flags UI-only as a recurring defense-in-depth gap. A crafted
     request could persist a Panel Number on an AME/IME appointment.

## Decision (locked 2026-06-03)
Net-new conditional state machine on Panel Number, paired with AF4:
- When the selected appointment type is NOT PQME (keyed off the PQME seed GUID), disable the
  `panelNumber` control and clear its value on BOTH the add form and the view/edit form.
- When the type IS PQME, enable it (AF4 adds the required validator).
- Server-side defense-in-depth: the DTO/AppointmentManager rejects a non-null PanelNumber when
  the appointment type is not PQME (and AF4 requires it for PQME). Enforced server-side (domain
  manager + DTO) AND mirrored in the UI.

## Implementation outline (no code)
1. Expose the PQME seed GUID identity to the Angular layer (shared constant aligned with
   `CaseEvaluationSeedIds.AppointmentTypes`; final mapping owned by AF1). Add an
   `isPanelType(appointmentTypeId)` helper in `AppointmentAddComponent`.
2. In the `appointmentTypeId` valueChanges subscriber (`appointment-add.component.ts:512-518`),
   add a panelNumber branch: if not PQME -> `panelNumber.patchValue(null, {emitEvent:false})`,
   `panelNumber.disable({emitEvent:false})`; if PQME -> `panelNumber.enable({emitEvent:false})`.
   AF4 layers `setValidators([Validators.required])` / `clearValidators()` on the same branch.
   Model on the appointmentDate pattern at lines 2449-2459.
3. Initialize the control state on form build / when editing an existing appointment so the
   disabled/cleared state reflects the loaded type (not just on user change).
4. Update `appointment-add-schedule.component.html:24-32` with a visual disabled affordance
   driven by a parent flag (no `[disabled]` on the formControl input; rely on programmatic
   `disable()` for the actual gate).
5. Mirror the same conditional in `appointment-view.component.ts:205` (edit path), using the
   `getRawValue()` save path already in place.
6. Server-side: in `AppointmentManager` Create/Update (`AppointmentManager.cs:34,45,155,163,180`),
   add a check that throws a domain/business exception when PanelNumber is non-null/non-empty and
   the resolved AppointmentType is not the PQME seed GUID. Optionally normalize (null out) defensively.
7. DTO annotations stay length-only; the cross-field PQME rule lives in the manager (DataAnnotations
   cannot see the related type). No new DB column -- PanelNumber already exists.
8. No migration required (no schema change). NO proxy regen required for AF3 alone unless the
   DTO surface changes; if AF4 changes DTO shape, regen once for the pair.
9. Add a localization key only if a new "Panel Number not applicable for this type" hint string
   is introduced (Domain.Shared en.json before any `L()` reference).

## Dependencies
- AF1 (final AME/IME/PQME type mapping + seed GUID identity) -- BLOCKS AF3: the conditional
  keys off the PQME seed GUID, which AF1 finalizes.
- AF4 (PQME requires Panel Number) -- SHARED state machine; build AF3 + AF4 together in the same
  subscriber branch and the same server-side check.

## Residual open questions
- None. The three findings open questions are resolved by the locked decision: clear (not just
  lock) the value; apply on both add AND view/edit forms; key off the PQME seed GUID; enforce
  server-side as defense-in-depth.
