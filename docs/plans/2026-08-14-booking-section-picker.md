---
feature: Section picker for prefilled bookings, and a clearer attorney question
date: 2026-08-14
revised: 2026-08-14
status: draft
base-branch: main
related-issues: []
---

# Item 2 -- Section picker for prefilled bookings

## Goal

After a source appointment is retrieved, ask the booker which sections have changed and prefill
only the sections they leave marked as unchanged; and replace the ambiguous attorney "Include"
checkbox with an explicit, role-aware yes/no question.

Two asks, one plan, because both turn on what `applicantAttorneyEnabled` means and both edit
`appointment-add-attorney-section.component.html`. Splitting them would put two PRs in the same
file reasoning independently about a flag with documented sharp edges.

## Context & decisions

Today prefill is all-or-nothing and every field stays editable, so stale data arrives looking
correct. A defense attorney who changed eight months ago passes through unless the booker
happens to notice. The picker turns a silent default into a deliberate answer.

Resolved decisions:

1. **Decision: a modal immediately after retrieve**, because prefill then applies once with the
   right data instead of being applied and partly undone. Matches the requirement wording
   ("once they enter the number and click retrieve, they should be asked").
2. **Decision: six sections** -- Patient, Applicant Attorney, Defense Attorney, Employer,
   Insurance, Examiner. Employer is included even though it is not its own wizard step, because
   the requirement names it. Schedule and Review are excluded (a new appointment needs its own
   slot; Review holds no data). Docs is out of scope.
3. **Decision: Claim/Injuries is NOT in the picker.** Injuries always copy and are edited or
   deleted individually in the existing injury table, because an appointment can carry several
   injuries where only one changed and section-level all-or-nothing would force re-entering all
   of them.
4. **Decision: default is "same"**, user ticks what changed -- the requirement's literal
   wording, and the fast path for the common case.
5. **Decision: re-openable mid-wizard**, because someone reaching the Defense step and not
   recognising the name should not have to restart the booking.
6. **Decision: warn before clearing** when a section is flipped to "changed" after its fields
   were edited, so two minutes of corrections are not lost to a mis-click.
7. **Decision: attorney "changed" leaves the section ENABLED but empty.** Blank and absent are
   different claims -- disabling it would record "this appointment has no applicant attorney".

Added 2026-08-14, folded in from a separate observation because it changes the same control:

8. **Decision: replace the "Include" checkbox with an explicit question and a yes/no switch.**
   "Include" conflates _"show me this section"_ with _"this appointment has an applicant
   attorney"_, which is the same ambiguity decision 7 has to work around. Making the question
   explicit removes it at the source.
9. **Decision: the wording is role-aware.** For the APPLICANT section, a patient sees "Do you
   have an attorney representing you?" and everyone else sees "Is the applicant represented?".
   For the DEFENSE section, everyone sees "Is there a defense attorney?". Patients get the
   jargon-free phrasing because they are the one audience who would not know what "applicant"
   means; "applicant" is used for the rest because it matches the section's own name.
10. **Decision: when the viewer IS that attorney type, the question is hidden and the answer is
    forced to Yes, with the section still required.** An applicant attorney does not need asking
    whether the applicant is represented. This holds even when a paralegal books under an
    attorney's role account, because an attorney is still on the claim -- the question is about
    representation, not about who is typing (Adrian, 2026-08-14). This is a DELIBERATE exception
    to decision 11 below; do not file it as a missing validator.
11. **Decision: the switch has no default -- the booker must answer before leaving the step.**
    Today `applicantAttorneyEnabled` defaults to `true`, so a section nobody confirmed quietly
    collects an attorney, and a defaulted-off one silently drops a party. Forcing the answer is
    the point of the change.

## All needed context

| Fact                                                                                  | Anchor                                                                    |
| ------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| Prefill application site (patch + drafts + ids + enabled flags)                       | `appointment-add.component.ts:1445-1470`                                  |
| `applySourcePatient` sets `currentPatientProfile = { patient, isExisting: true }`     | `appointment-add.component.ts:1683`                                       |
| `applyAttorneyEnabledFromSource` -- the enable/disable trap                           | `appointment-add.component.ts:1467-1468`                                  |
| Per-section mappers, each returns `{}` for null input                                 | `reval-prefill.mapper.ts:62, 80, 131, 147`                                |
| Injury draft mapper                                                                   | `reval-prefill.mapper.ts:107`                                             |
| Wizard steps (9)                                                                      | `appointment-wizard.component.ts:66`                                      |
| Step -> control-name map (schedule, patient, applicant, defense, insurance, examiner) | `step-errors.util.ts:22`                                                  |
| `ConfirmationService` already injected                                                | `appointment-add.component.ts:163`, `appointment-wizard.component.ts:130` |
| Draft persistence                                                                     | `appointment-wizard.component.ts:528` `draftService.upsert`               |

Gotchas:

- `WIZARD_STEP_CONTROLS` does NOT cover employer or claim, so it is not a complete
  section-to-control partition. Employer needs its own control list.
- Suppression is free on first prefill (omit the keys; `patchValue` only writes what it is
  given) but NOT on a later flip -- that requires explicitly clearing controls.
- Per-section dirty tracking does not exist. The warn-before-clear rule needs it.
- Type and location are applied LAST and WITH events (`:1470`) to drive the slot picker and
  field-config cascades. Do not reorder them into the suppressed set.

## Tasks

### T1 -- section model

approach: tdd

CREATE `angular/src/app/appointments/shared/prefill-sections.ts`: export
`PrefillSection = 'patient' | 'applicantAttorney' | 'defenseAttorney' | 'employer' | 'insurance' | 'examiner'`,
an ordered `PREFILL_SECTIONS` array with display labels, `PrefillSelection = Record<PrefillSection, boolean>`
(true = changed), a `defaultPrefillSelection()` returning all false, and
`SECTION_CONTROLS: Record<PrefillSection, readonly string[]>` naming the controls each section
owns -- employer's list built from the `employer*` keys in `mapEmployerToPatch`.

pattern: `step-errors.util.ts:22` for the shape of a section-to-controls constant.

acceptance (EARS): WHEN `defaultPrefillSelection()` is called, THE SYSTEM SHALL return every
section marked unchanged.

### T2 -- suppression in the mapper

approach: tdd

MODIFY `reval-prefill.mapper.ts`: accept a `PrefillSelection` and pass `null` into each
per-section mapper whose section is marked changed. Injuries always map regardless (decision 3).
Do NOT let a suppressed attorney reach `applyAttorneyEnabledFromSource` as `false`.

pattern: the mappers already return `{}` for null (`:62, 80, 131, 147`), so suppression needs no
new clearing logic on first prefill.

acceptance (EARS):

- WHEN a section is marked changed, THE SYSTEM SHALL omit that section's keys from `formPatch`.
- WHEN Applicant Attorney is marked changed AND the source had one, THE SYSTEM SHALL still
  report the section as enabled.
- WHEN Claim is not offered in the picker, THE SYSTEM SHALL always produce the source's injury
  drafts.

### T3 -- the picker modal

approach: test-after

CREATE `angular/src/app/appointments/shared/prefill-picker-modal.component.ts` (+ template): a
standalone component listing the six sections with checkboxes, defaulting to unchanged, with
confirm and cancel. Cancel means "prefill nothing" rather than "abandon the lookup".

pattern: existing standalone modal components under `appointments/appointment/components/`
(e.g. `reschedule-request-modal.component.ts`).

acceptance (EARS):

- WHEN the source appointment loads, THE SYSTEM SHALL open the picker before applying prefill.
- WHILE the picker is open, THE SYSTEM SHALL NOT have modified the form.

### T4 -- wire the picker into retrieve

approach: test-after

MODIFY `appointment-add.component.ts:1445-1470`: open the picker after the source loads, then
apply prefill with the returned selection. Skip `applySourcePatient` when Patient is marked
changed, but keep `patientId` and `isExisting` (see item 3's plan -- patient stays one row).

acceptance (EARS): WHEN the booker confirms the picker, THE SYSTEM SHALL prefill exactly the
sections left unchanged and leave the rest empty.

### T5 -- re-open and flip

approach: test-after

MODIFY `appointment-wizard.component.ts`: add a control to re-open the picker while the source is
loaded. On a flip from unchanged to changed, if any control in `SECTION_CONTROLS[section]` is
dirty, confirm via `ConfirmationService` before clearing; on a flip the other way, re-apply that
section's prefill from the retained source.

pattern: `ConfirmationService` usage already present at `appointment-wizard.component.ts:130`.

acceptance (EARS):

- IF a section is flipped to changed AND its controls are dirty, THEN THE SYSTEM SHALL ask for
  confirmation before clearing.
- WHEN the booker declines that confirmation, THE SYSTEM SHALL leave both the data and the
  selection untouched.
- WHEN a section is flipped back to unchanged, THE SYSTEM SHALL restore that section's values
  from the retained source.

### T6 -- persist the selection with the draft

approach: test-after

MODIFY the draft payload written at `appointment-wizard.component.ts:528` to carry the current
`PrefillSelection`, so resuming a draft does not silently lose which sections were declared
changed.

acceptance (EARS): WHEN a draft is resumed, THE SYSTEM SHALL restore the section selection as it
was saved.

### T7 -- replace the Include checkbox with a role-aware question

approach: test-after

MODIFY `angular/src/app/appointments/sections/appointment-add-attorney-section.component.html:18`
and its component: replace the `Include` checkbox label with an explicit question and a yes/no
switch bound to the same `applicantAttorneyEnabled` / `defenseAttorneyEnabled` control. Resolve
the question text per decision 9 from the viewer's role, which the wizard already knows --
`isInternalBooker` (`appointment-add.component.ts:1275`), `isApplicantAttorney` /
`isDefenseAttorney` (`:1313`), and the primary role at `:1212` (defaults to `'Patient'`). Role
names are `'Patient'`, `'Applicant Attorney'`, `'Defense Attorney'`, `'Claim Examiner'`.

Per decision 10, hide the switch entirely and force the value to `true` when the viewer's role
matches the section: applicant attorneys see no question on the applicant section, defense
attorneys none on the defense section.

Put the copy in a pure resolver (e.g. `attorney-question.util.ts`) so it unit-tests without a
TestBed, mirroring `wizard-copy.util.ts`.

acceptance (EARS):

- WHERE the viewer is a patient, THE SYSTEM SHALL ask "Do you have an attorney representing
  you?" on the applicant section.
- WHERE the viewer is internal staff, a defense attorney or a claim examiner, THE SYSTEM SHALL
  ask "Is the applicant represented?" on the applicant section.
- WHERE the viewer is an applicant attorney, THE SYSTEM SHALL NOT ask the applicant-section
  question AND SHALL treat the section as included.
- WHERE the viewer is a defense attorney, THE SYSTEM SHALL NOT ask the defense-section question
  AND SHALL treat the section as included.
- WHEN any other viewer reaches the defense section, THE SYSTEM SHALL ask "Is there a defense
  attorney?".

### T8 -- require an answer before leaving the step

approach: tdd

MODIFY the attorney form controls so `applicantAttorneyEnabled` and `defenseAttorneyEnabled`
start with NO value (`appointment-add.component.ts:596` and `:612` currently default to `[true]`)
and the Continue gate blocks until answered, except where decision 10 forces the value.

GOTCHA, documented in `angular/src/app/appointments/CLAUDE.md:115-117`: `form.reset()` nulls both
flags and must be followed by `patchValue({ applicantAttorneyEnabled: ..., defenseAttorneyEnabled: ... })`
or the required-validator state breaks. Removing the default makes that reset path more
sensitive, not less -- re-read those lines before touching it.

pattern: the per-step Continue gate driven by `WIZARD_STEP_CONTROLS` (`step-errors.util.ts:22`).

acceptance (EARS):

- WHILE the applicant-attorney question is unanswered, THE SYSTEM SHALL block Continue on that
  step and name the unanswered question in the step error summary.
- WHERE decision 10 hides the question, THE SYSTEM SHALL NOT block Continue.
- WHEN the booker answers No, THE SYSTEM SHALL clear that section's validators so an empty
  section submits.

## Validation loop

```bash
cd /c/src/patient-portal/main && npx prettier --check "angular/src/app/appointments/**/*.{ts,html}"
```

```bash
cd /c/src/patient-portal/main && npx eslint angular/src/app/appointments
```

```bash
cd /c/src/patient-portal/main && npx ng build
```

```bash
cd /c/src/patient-portal/main && export CHROME_BIN=<chrome path> && npx ng test --watch=false --browsers=ChromeHeadless --include='**/appointments/**/*.spec.ts'
```

Frontend-only, so no `dotnet test` -- but run `npx ng test` and not just the build: a spec that
pins the existing prefill behaviour will break the moment suppression lands, and the build alone
will not show it.

## Risk / rollback

Blast radius: the shared prefill path used by re-eval, re-submit and (once item 1 lands) re-book.
A defect here degrades every prefilled booking, though never a plain new booking.

Rollback: revert the PR. Frontend only, no schema or API change.
