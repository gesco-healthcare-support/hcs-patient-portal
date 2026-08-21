---
id: AF4
title: Panel Number required for PQME (block submit when empty), enforced UI + server
type: enhancement
components: [angular/src/app/appointments/appointment-add.component.ts, angular/src/app/appointments/sections/appointment-add-schedule.component.html, angular/src/app/appointments/appointment/components/appointment-view.component.ts, src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/AppointmentCreateDto.cs, src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs]
related_known_bugs: [BUG-012, OBS-24, BUG-043]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change
For a PQME appointment, the Panel Number field becomes REQUIRED and the booking
form must not submit when it is empty. This is the PQME-positive half of the same
state machine whose negative half is AF3 (AME/IME -> Panel Number disabled + cleared
+ optional). Enforce on BOTH the Angular form and the server (DTO + domain manager).

## Current behavior (from investigation)
- Panel Number is never required. The add-form control is
  `panelNumber: [null as string | null, [Validators.maxLength(50)]]`
  (appointment-add.component.ts:386) -- length cap only, no required validator.
- The `appointmentTypeId` valueChanges subscriber (appointment-add.component.ts:512-518)
  currently handles field-configs + custom-fields + slots only; it does NOT touch
  `panelNumber`, so type selection has no effect on Panel Number validation.
- Server side imposes no required check for any type:
  - `AppointmentCreateDto.PanelNumber` is `string?` with only
    `[StringLength(AppointmentConsts.PanelNumberMaxLength)]`
    (AppointmentCreateDto.cs:11-12).
  - `AppointmentManager.CreateAsync(..., string? panelNumber = null, ...)` takes
    panelNumber as an optional param (AppointmentManager.cs:34) and validates it only
    with `Check.Length(panelNumber, ..., AppointmentConsts.PanelNumberMaxLength)`
    (AppointmentManager.cs:45). No PQME-conditional required check exists.
- The dynamic-validator pattern already exists in this component: `appointmentDate`
  uses `setValidators([Validators.required])` / `clearValidators()` then
  `updateValueAndValidity({ emitEvent: false })` (appointment-add.component.ts:2449-2459).
- The view/edit form has BUG-012 conditional required-validator wiring as precedent
  (appointment-view.component.ts:343).
- Error styling is already wired: `isFieldInvalid('panelNumber')` drives the
  `is-invalid` class on the Panel Number input (appointment-add-schedule.component.html:24-32),
  so once a validator attaches, the inline error lights up with no template change.

## Relevant code locations
Frontend:
- angular/src/app/appointments/appointment-add.component.ts:386 -- `panelNumber`
  control; attach/clear conditional `Validators.required` here.
- angular/src/app/appointments/appointment-add.component.ts:512-518 -- the
  `appointmentTypeId` valueChanges hook point where the PQME conditional fires.
- angular/src/app/appointments/appointment-add.component.ts:2449-2459 -- the
  setValidators/clearValidators + `updateValueAndValidity` precedent to reuse.
- angular/src/app/appointments/sections/appointment-add-schedule.component.html:24-32 --
  Panel Number input; `is-invalid` styling already wired (no change needed unless a
  required asterisk/aria-required is added).
- angular/src/app/appointments/appointment/components/appointment-view.component.ts:205,343 --
  view/edit `panelNumber` (maxLength only at 205; BUG-012 conditional-required wiring
  at 343 is the pattern to mirror).

Backend:
- src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/AppointmentCreateDto.cs:11-12 --
  `PanelNumber` DTO field; DTO-level guard goes here (and the mirror in AppointmentUpdateDto).
- src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs:34,45 --
  CreateAsync signature + the panelNumber Check.Length line where the PQME-conditional
  required check is added (and the symmetric spot in UpdateAsync).

## Phase 3 cross-reference
- BUG-012 (appointment-view.component.ts:343): existing "firmname-required" conditional
  required-validator wiring. Reuse the exact pattern for consistency; do not invent a
  second style.
- OBS-24: recurring "UI-only enforcement, no server mirror" defense-in-depth gap. AF4
  closes one instance of it by adding the server-side required check, so cite OBS-24 as
  the rationale for the both-layers decision rather than as separate work.
- BUG-043: claim-information required-before-submit precedent on this same booking form;
  same submit-gate flow (validateOnSubmit) AF4 hooks into. Verify AF4 plays nicely with
  the existing gate rather than adding a parallel one.
- AF3 (AME/IME -> disabled + cleared + optional): SAME control, SAME valueChanges hook.
  Bundle AF3 + AF4 into one `applyPanelNumberStateForType(typeId)` helper so the two
  halves stay a single source of truth (PQME -> enabled + required; AME/IME -> disabled +
  cleared + optional).

## Research findings
- Internal patterns / prior art:
  - Dynamic validators via `setValidators` + `clearValidators` + `updateValueAndValidity`
    is already the house style (appointment-add.component.ts:2449-2459 for appointmentDate;
    appointment-view.component.ts:343 for BUG-012). No new pattern is introduced.
  - The FormGroup and ALL cascade subscriptions live exclusively in
    `AppointmentAddComponent`; the schedule section is template-only (angular/src/app/appointments/CLAUDE.md,
    "AppointmentAddComponent -- FormGroup lives here only"). The conditional logic MUST
    stay in the parent, not the section child.
  - Server-side: `Check.*` guards in the domain manager throw on violation; the project's
    domain-manager-owns-invariants rule (Domain CLAUDE.md) puts the authoritative PQME
    required check in `AppointmentManager`, with the DTO DataAnnotation as the cheap
    early reject at the boundary.
- External docs (Angular) if relevant:
  - Angular Reactive Forms `AbstractControl.setValidators` / `clearValidators` followed by
    `updateValueAndValidity` is the documented way to swap validators at runtime
    (Angular reactive-forms guide; CONFIDENCE HIGH -- matches existing in-repo usage).
  - C# equivalent for Adrian: think of the DTO `[Required]`-style attribute as an
    Express body-validator middleware (fast boundary reject) and the AppointmentManager
    check as the service-layer invariant (authoritative, always runs).

## Approaches considered (with tradeoffs)
1. UI-only conditional required (rejected as sole fix). Cheapest -- attach
   `Validators.required` on PQME, clear otherwise. But it repeats the OBS-24 gap: a
   crafted request or the ngModel view-page path could persist a PQME with empty Panel
   Number. PQME panel number is an integrity field (it ties the booking to a specific
   QME panel), so UI-only is insufficient.
2. Server-only (rejected). Correct data integrity but bad UX -- the user only learns the
   field is required after a failed submit round-trip; no inline affordance.
3. Both layers, keyed off PQME GUID identity (CHOSEN). UI conditional required for
   immediate feedback + reuse of the existing submit gate; server DTO + AppointmentManager
   check as the authoritative guard. Keying off the PQME seed GUID (not a name substring)
   survives the AF1 label rename and is the locked project-wide convention for type-specific
   behavior.

Why the decision wins: it matches the locked validation principle (integrity/security rules
enforced server-side AND mirrored in UI), reuses the established validator pattern with zero
new abstractions, and closes the OBS-24 defense-in-depth gap for this field.

## Decision (locked 2026-06-03)
- Angular: conditional `Validators.required` on `panelNumber` when the selected type is
  PQME; clear it (and, per AF3, disable + null the control) for AME/IME. Drive it from the
  `appointmentTypeId` valueChanges subscriber using the existing
  setValidators/clearValidators + `updateValueAndValidity({ emitEvent: false })` pattern.
  Form must not submit while PQME + empty -- satisfied by `Validators.required` gating the
  existing validateOnSubmit flow.
- Server: PQME-conditional required check in BOTH `AppointmentCreateDto` (boundary reject)
  and `AppointmentManager` Create/Update (authoritative invariant), throwing a localized
  `UserFriendlyException` when PQME and panelNumber is null/whitespace.
- Identity: key the PQME branch off the PQME seed GUID, not a name substring.

## Implementation outline (no code)
1. Define a single helper in `AppointmentAddComponent`, e.g.
   `applyPanelNumberStateForType(typeId)`, that owns BOTH AF3 and AF4 outcomes:
   PQME -> enable + `setValidators([Validators.required, Validators.maxLength(50)])`;
   AME/IME -> `clearValidators()` + clear value + disable. Call it from the
   `appointmentTypeId` valueChanges subscriber (appointment-add.component.ts:512-518) and
   once during form init for the preselected type. Finish with
   `updateValueAndValidity({ emitEvent: false })`.
2. Mirror the same conditional on the view/edit form
   (appointment-view.component.ts:205) using the BUG-012 wiring at line 343 as the
   template; handle the edge case where an existing non-PQME appointment is switched to
   PQME during edit (validator must re-evaluate on that change).
3. Optional UI affordance: add a required marker / `aria-required` to the Panel Number
   label in appointment-add-schedule.component.html when PQME is active (pure affordance,
   UI-only is acceptable per the locked validation rule).
4. Server DTO: add the conditional-required guard for PanelNumber to
   `AppointmentCreateDto` (and the symmetric `AppointmentUpdateDto`). A plain `[Required]`
   cannot express "only when PQME"; use a custom validation attribute or validate the
   pair in the AppService before calling the manager. Keep the existing `[StringLength]`.
5. Server domain manager (AUTHORITATIVE): in `AppointmentManager.CreateAsync` and
   `UpdateAsync`, after resolving the appointment type, if the type is the PQME GUID and
   `panelNumber` is null/whitespace, throw a localized `UserFriendlyException`
   (add a new localization key in Domain.Shared en.json before referencing it). Place it
   alongside the existing `Check.Length(panelNumber, ...)` (AppointmentManager.cs:45).
   NOTE: CreateAsync/UpdateAsync need the PQME GUID available -- pass the type identity or
   resolve it via the seed contributor's known GUID; coordinate with AF1/AF3 on the shared
   PQME-identity source so all three items read one constant.
6. PROXY REGEN: if a new DTO field or validation surface changes the generated contract,
   run `abp generate-proxy` (do not hand-edit `angular/src/app/proxy/`). A pure
   DataAnnotation/manager-side check that does not alter the DTO shape needs no regen.
7. MIGRATION: none. PanelNumber column + max-length already exist; this is validation
   only, no schema change.
8. Server-vs-UI split: required-when-PQME is an integrity rule -> enforced server-side
   (DTO + manager) AND mirrored in UI. The required-asterisk/aria marker is a pure
   affordance -> UI-only is fine.

## Dependencies
- Depends on AF1 (final PQME identity / seed GUID) -- the conditional must key off the
  decided PQME GUID, not the legacy "Panel QME" name.
- Tightly coupled to AF3 (AME/IME disable + clear) -- same control, same hook; implement
  the two halves as one helper. Sequence AF3 and AF4 together.
- Shares the submit-gate flow with BUG-043; confirm no conflict with the
  claim-information required gate.

## Residual open questions
- Edit-form switch behavior: when an existing non-PQME appointment is changed to PQME on
  the view/edit page, confirm the expected UX (block save until Panel Number entered) --
  treated as the same rule as the add form unless product says otherwise. Minor.
