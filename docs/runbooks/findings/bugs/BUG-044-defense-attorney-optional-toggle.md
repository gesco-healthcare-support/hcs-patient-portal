---
id: BUG-044
title: Defense Attorney section is optional via an "Include" toggle; per product decision it must be mandatory in NEW
severity: medium
status: open
found: 2026-05-27 (userflow audit; product decision by Adrian)
flow: appointment-booking, appointment-view
component: angular/src/app/appointments/sections/appointment-add-attorney-section.component.html (Include switch); angular/src/app/appointments/shared/attorney-section-validators.ts (wireAttorneySectionToggle); angular/src/app/appointments/appointment-add.component.ts (defenseAttorneyEnabled); angular/src/app/appointments/appointment/components/appointment-view.component.ts (defenseAttorneyEnabled)
parity: intentional deviation from OLD (OLD had the toggle; NEW must not)
---

# BUG-044 - Defense Attorney section must not be optional

## Symptom / decision

Both the booking form and the appointment view render the Defense
Attorney section with an "Include" switch
(`appointment-add-attorney-section.component.html:10-18`,
`defenseAttorneyEnabled` control). Unchecking it makes the whole DA
section optional. Per Adrian (2026-05-27), the Defense Attorney section
**should not be optional in NEW** -- the toggle should not exist.

## Current behaviour (confirmed: code)

- `defenseAttorneyEnabled` defaults `[true]`
  (`appointment-add.component.ts:354`).
- `wireAttorneySectionToggle(form, 'defenseAttorney')`
  (`attorney-section-validators.ts:75-88`) subscribes to the toggle and,
  on each flip, runs `applyAttorneySectionValidators` -- adding/removing
  `Validators.required` on the 8 DA fields. When the toggle is off, the
  DA fields are non-required and the section is skipped on submit
  (`buildDefenseAttorneyInput`-style guard:
  `appointment-view.component.ts:1249` returns early when
  `!raw.defenseAttorneyEnabled`).
- The Applicant Attorney section has the **same** toggle
  (`applicantAttorneyEnabled`). Whether AA should also be mandatory is an
  open product question (see below).

## OLD parity note

OLD also had an `isActive` toggle on the Defense Attorney section
(`P:\PatientPortalOld\...\appointment-add.component.html:724-741`,
`addValidationForDefenceAttorney(...isActive)`), as well as on Applicant/
Patient Attorney, Claim Examiner, and Primary Insurance. So removing the
DA toggle is a **deliberate deviation from OLD parity**, made by product
decision -- not a parity restoration. Documenting so the deviation is
explicit (per the bug-and-deviation policy in the worktree CLAUDE.md).

## Decision (2026-05-27, Adrian)

**Both** Applicant Attorney AND Defense Attorney must be mandatory --
neither should have the "Include" toggle. The shared
`appointment-add-attorney-section.component` therefore always renders
without the switch and always applies required validators (for both
`prefix` instances). This applies to the booking form and the
appointment view.

## Recommended fix (high level -- see plan)

Applies to **both** AA and DA per the decision above.

1. Add an input to `appointment-add-attorney-section.component`
   (e.g. `[mandatory]="true"`) that hides the Include switch and forces
   `{prefix}Enabled = true` (disabled control) so the section always
   renders and its fields are always required.
2. Apply it to **both** the Applicant and Defense Attorney instances in
   the booking form and the appointment view; validators always-on.
3. In `wireAttorneySectionToggle` (or its callers), when mandatory, skip
   the toggle subscription and always apply required validators so a
   loaded appointment cannot present either section as toggled-off.
4. Server-side: ensure the create/upsert path treats both attorney
   sections as required (so non-SPA callers cannot omit them).

## Related

- [[BUG-042]] -- same attorney sections; fix should be coordinated (both
  touch the shared attorney-section component + the upsert path).
- [[OBS-17]] da-booking-no-ce-section.
