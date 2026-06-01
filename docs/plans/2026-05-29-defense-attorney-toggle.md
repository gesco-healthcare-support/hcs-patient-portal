---
feature: defense-attorney-toggle
date: 2026-05-29
status: in-progress
base-branch: main
related-issues: []
sequence: 2 of 6 (booking-form cluster; do before ssn-redact-on-type and address-validation)
branch: feat/defense-attorney-toggle
reference-commit: ed12db0 (feat/replicate-old-app) -- the AA self-represented modal to mirror
---

## Goal

Re-add a Defense Attorney (DA) section "Include" toggle in the booking form that
behaves like the Applicant Attorney (AA) toggle, with a confirmation modal on
toggle-off. Available to every booker except a DA-role booker (who must keep
their own section). Insurance and Claim Examiner sections stay mandatory
(unchanged).

## Context

### What already exists (verified 2026-05-29, code + git + live UI)

- The **AA "self-represented" toggle-off modal is real and shipped**. Adrian
  built it as commit **`ed12db0`** ("feat(appointments): self-represented
  confirmation on AA toggle off", 2026-05-28 17:33) on `feat/replicate-old-app`,
  tested it, and demo'd it. Its content reached `main` via the #267 squash-merge
  (`8afb596`); `confirmAaToggleOff` is on `main` today
  (`appointment-add.component.ts:583`). This is the proven pattern F4 mirrors.
- The **DA toggle + modal was never built** -- `confirmDaToggleOff` exists in
  zero commits on any branch. DA is hard-mandatory per decision D2
  (`appointment-add.component.html:139` -> `[mandatory]="true"`).
- **Stale-image caveat for verification:** the running `main` Angular image was
  built 2026-05-28 16:37 -- about an hour before `ed12db0` and before #267
  merged to `main`. So the running `main` stack does NOT show the AA modal
  (toggling AA off just collapses the section, no modal, no console output --
  confirmed live). Any visual verification of F4 (or the existing AA modal)
  requires rebuilding the `main` Angular image first.

### Current code map (on `main`)

- **AA section** (`appointment-add.component.html:122-129`):
  `[mandatory]="isApplicantAttorney && !isItAdmin"`,
  `[isReadOnly]="isApplicantAttorney && !isItAdmin"`. Non-AA bookers get the
  Include toggle; AA bookers keep it mandatory.
- **DA section** (`appointment-add.component.html:132-142`): `[mandatory]="true"`
  (hard-coded, D2), `[isReadOnly]="isDefenseAttorney && !isItAdmin"`.
- **Shared section component**
  (`sections/appointment-add-attorney-section.component.ts`): role-agnostic. When
  `mandatory=false` it renders the `{prefix}Enabled` Include toggle and the `@if`
  shows the body when enabled; when `mandatory=true` the toggle is hidden and the
  body always renders. It already subscribes to `{prefix}Enabled.valueChanges` and
  calls `markForCheck()` (the OnPush re-render fix from `ed12db0`). No change
  needed here for DA -- it already works for `role="defense"`.
- **AA toggle wiring** (`appointment-add.component.ts:488-495`): on `!enabled` ->
  `confirmAaToggleOff()` and return; on `enabled` -> apply validators.
- **`confirmAaToggleOff()`** (`:583-605`): `confirmationService.warn(Message, Title,
  {yesText: AbpUi::Yes, cancelText: AbpUi::No})`; on `status !== confirm` (No/dismiss)
  -> `enabledControl.setValue(true)` (revert, re-fires valueChanges -> idempotent
  validator re-apply + OnPush re-render); on `confirm` (Yes) -> clear required
  validators + clear AA email.
- **DA toggle wiring TODAY** (`:496-502`): a plain subscriber that applies
  validators on enable and clears the email on disable, with NO modal. Because
  DA is mandatory, this only fires at init/reset today.
- **Localization** (`en.json:275-276`): the AA modal title + message keys.
- **Insurance + Claim Examiner**: no section-level toggle (per-injury modal flags
  default true). Out of scope -- they stay mandatory.

### Decision locked (Adrian, 2026-05-29)

The DA modal uses the wording "Is a Defense Attorney assigned to this case?".
Because that question's polarity is the OPPOSITE of the AA "self-represented?"
question, the Yes/No handling is INVERTED relative to AA:
- **Yes** = a Defense Attorney IS assigned -> cancel the toggle-off, keep the
  section required (revert).
- **No** = none assigned -> confirm removal -> hide the section + clear DA
  validators and email.

## Approach

Mirror the proven `ed12db0` AA pattern for DA, with the inverted Yes/No handling.

1. **Un-gate the DA toggle**: change `appointment-add.component.html:139` from
   `[mandatory]="true"` to `[mandatory]="isDefenseAttorney && !isItAdmin"`
   (exactly mirroring the AA binding at :125). Non-DA bookers get the Include
   toggle; a DA-role booker (not IT Admin) keeps the section mandatory with no
   toggle, so they must enter their own details.
2. **Rewire the DA subscriber** (`appointment-add.component.ts:496-502`) to mirror
   the AA subscriber: on `!enabled` -> `confirmDaToggleOff()` and return; on
   `enabled` -> `applyConditionalEmailValidator('defenseAttorneyEmail', true)` +
   `applyAttorneySectionValidators(this.form, 'defenseAttorney', true)`.
3. **Add `confirmDaToggleOff()`** mirroring `confirmAaToggleOff()` but with DA
   controls, DA localization keys, and INVERTED handling:
   - on `status === Confirmation.Status.confirm` (Yes = assigned) ->
     `enabledControl.setValue(true)` (revert; keep section required).
   - on `status !== confirm` (No / dismiss = not assigned) ->
     `applyConditionalEmailValidator('defenseAttorneyEmail', false)` +
     `applyAttorneySectionValidators(this.form, 'defenseAttorney', false)` +
     clear `defenseAttorneyEmail`.
4. **Add localization keys** in `en.json`. Proposed copy (Adrian may tweak):
   - `Appointment:DefenseAttorneyAssignedTitle` = "Defense Attorney"
   - `Appointment:DefenseAttorneyAssignedMessage` = "Is a Defense Attorney
     assigned to this case?"
5. No change to the shared section component (role-agnostic; already handles the
   toggle render + markForCheck for `role="defense"`).
6. The form reset already re-asserts `defenseAttorneyEnabled: true`
   (`:1098`), so the section defaults ON -- keep.

**Alternatives rejected:**
- AA-parallel polarity (Yes = remove) with a reworded message: rejected by Adrian
  in favor of the exact "Is a Defense Attorney assigned?" wording + inverted Yes/No.
- A brand-new shared modal component: unnecessary -- `confirmationService.warn`
  IS the shared modal; only the message/title keys and the Yes/No handling differ.

## Tasks

- T1: Un-gate the DA toggle.
  - approach: test-after
  - files-touched: angular/src/app/appointments/appointment-add.component.html (:139)
  - acceptance: a non-DA booker sees an "Include" toggle on the DA section; a
    DA-role booker (not IT Admin) sees it mandatory with no toggle; the body
    shows when enabled.

- T2: DA toggle-off confirmation modal with inverted Yes/No.
  - approach: test-after
  - files-touched: angular/src/app/appointments/appointment-add.component.ts
    (rewire the defenseAttorneyEnabled subscriber ~496-502; add confirmDaToggleOff
    mirroring confirmAaToggleOff ~583-605)
  - acceptance: toggling DA off opens the modal; **Yes** (assigned) reverts the
    toggle back ON and keeps the section required; **No** (not assigned) hides the
    section, drops DA required validators, and clears the DA email.

- T3: Localization keys for the DA modal.
  - approach: code
  - files-touched: src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json
  - acceptance: `Appointment:DefenseAttorneyAssignedTitle` +
    `Appointment:DefenseAttorneyAssignedMessage` exist and resolve in the modal.

- T4: Record the D2 reversal.
  - approach: code
  - files-touched: docs/parity/_parity-flags.md (create if absent)
  - acceptance: a row notes the DA-toggle restoration + modal as a deliberate
    reversal of decision D2, citing the AA pattern (`ed12db0`).

## Risk / Rollback

- Blast radius: booking-form DA section only. DA fields become conditionally
  required. A polarity bug (Yes/No swapped) would either keep an unwanted section
  or drop a needed one -- T2's acceptance checks both buttons explicitly. The
  shared component + AA path are untouched, so AA behavior cannot regress.
- Rollback: revert the PR; DA returns to hard-mandatory (D2 state).

## Verification

The running `main` Angular image is stale (pre-AA-modal), so first rebuild it:
`docker compose up -d --build angular` (restarts Angular only). Then on
`falkinstein.localhost:4200/appointments/add`:
1. As a Patient booker: DA section shows an "Include" toggle (on by default).
   Toggle off -> modal "Is a Defense Attorney assigned to this case?" ->
   **No** hides + clears DA; **Yes** keeps the section required.
2. Confirm the AA modal still works unchanged (regression check).
3. As a DA-role booker (`defatty1@`-type): DA section is mandatory, no toggle.
4. Insurance + Claim Examiner remain mandatory (unchanged).
5. `npx ng build --configuration development` passes.
