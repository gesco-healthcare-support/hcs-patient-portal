---
feature: appointment-add-component-decomposition
date: 2026-05-13
status: in-progress
base-branch: feat/replicate-old-app
related-issues: ["task #121"]
---

# Decompose appointment-add.component.ts into 7 section sub-components

## Goal

Reduce `angular/src/app/appointments/appointment-add.component.ts` from
**2,976 lines** (component class) + **1,612 lines** (template) to a
parent orchestrator under ~800 lines plus seven focused
section-component pairs, with no change in user-visible behavior, no
change in the form's data shape, no change in network calls.

## Context

### Why now

- The file is 12× over the project's 250-line-per-component cap
  (`~/.claude/rules/code-standards.md`).
- Main-worktree userflow testing is about to start filing bugs against
  the booking form. Each fix is more expensive on a 2,976-line wall.
- New booking-form features (custom fields per appointment type,
  attorney separation, injury workflow) will keep accumulating here
  unless we decompose now.
- The booking form is **already 100% reactive** -- the memory entry
  that framed #121 as "consolidate ngModel + reactive + modal" was
  wrong. There are zero `ngModel` usages in the HTML and three
  reactive FormGroups owned by the component. The real issue is
  monolithic structure, not form-pattern divergence.

### Why it's risky

- Booking is the most critical user flow. Any regression breaks the
  entire NEW app's main purpose.
- Cross-section cascade rules: when a user enables the Applicant
  Attorney section, an email field becomes required; when they pick a
  cumulative injury, two date fields become required; the
  `applyConditionalAttorneySectionValidators` orchestrator (TS lines
  622-680) reads controls from multiple sections.
- Custom Fields dynamically creates FormControls based on the selected
  AppointmentType. Extracting the section incorrectly would break
  AppointmentType-change propagation.
- Component tests are thin (the existing AppointmentsAppServiceTests
  cover the backend, not the form). Verification is manual + Playwright
  MCP after each phase.

### How the file got this big (git archaeology)

Between the memory snapshot (1594 lines) and current (2976 lines),
Wave 2 added:

| Commit | Feature | LOC delta |
| --- | --- | --- |
| `18ab944` | w2-8 injury workflow | +214 |
| `a413dad` | w2-7 attorney separation (AA + DA) | +192 |
| `a8eccee` + `a6620fe` | w2-5 custom fields per appointment type | +296 |
| `ad5797a` | wave 2 form parity + interpreter lock | +318 |
| `2f40a71` | B18 required-field + visibility | +94/-47 |
| Others | demos + post-demo fixes | ~100 |

Each delta corresponds to a self-contained UI section -- which is good
news for decomposition: the boundaries already exist.

## Approach

### Pattern: smart parent + dumb sections

Every extracted sub-component:

- Accepts the parent `FormGroup` (or a nested `AbstractControl`) as an
  Input. Form state stays with the parent -- single source of truth.
- Owns its own `templateUrl` (separate `.component.html`, per Adrian's
  decision in the planning chat). Matches the existing project
  convention.
- Owns helper methods that previously lived on the parent only when
  those helpers are section-local (e.g. cumulative-injury-date logic
  belongs in the injury sub-component).
- Emits events when the parent needs to react (e.g. "patient
  selected" -> parent reloads attorney lookup options).

The parent retains:

- The main `form` FormGroup definition.
- The submit / save flow.
- Role detection on init.
- Cross-section cascade orchestration (the `applyConditional*`
  validators that touch multiple sections at once).
- Lookup-loading helpers that feed multiple sections.

### Rejected alternatives

| Alternative | Rejected because |
| --- | --- |
| One big-bang PR | Too risky for the most critical user flow. |
| Sub-components own their own FormGroup, parent stitches together | Doubles the state surface (parent's form vs child's form); risks cascade rules going stale. |
| Inline templates (`template:` instead of `templateUrl:`) | Adrian explicitly chose external `.component.html`. |
| Add component-spec tests as part of this refactor | Out of scope. Main-worktree session will exercise the form via Playwright MCP. |

### Per-phase test gate

After each phase commit:

1. `npx ng build --configuration development` -- zero errors.
2. ESLint + Prettier pre-commit pass.
3. Manual + Playwright MCP smoke: log in as
   `applicant.attorney@falkinstein.test` (the role that exercises the
   most sections), open `/appointments/add`, walk through every
   section, save. Verify the submitted DTO is byte-identical to a
   baseline captured before Phase 1.
4. Console clean per `browser_console_messages`.
5. No new network requests, no removed ones.

## Tasks

Order chosen for **risk minimisation**: most-isolated section first,
most-cascade-coupled section last.

| id | task | approach | files-touched | acceptance |
| --- | --- | --- | --- | --- |
| **T0** | Baseline capture | code | (no source change) | Playwright MCP run captures pre-refactor request/response payloads + visual snapshots into `tests/baseline/appointment-add/`. |
| **T1** | Extract Custom Fields section | code | new `appointment-add-custom-fields.component.{ts,html}`; parent loses ~280 lines | Custom Fields tab still renders all 7 field types (text, numeric, picklist, tickbox, date, radio, time) per AppointmentType. Switching AppointmentType triggers re-render. Submitted payload identical to baseline. |
| **T2** | Extract Authorized Users (table + modal) | code | new `appointment-add-authorized-users.component.{ts,html}`; parent loses ~150 lines | Add / edit / remove rows work. Modal opens / closes cleanly. Submitted `authorizedUsers` array identical to baseline. |
| **T3** | Extract Employer Details | code | new `appointment-add-employer-details.component.{ts,html}`; parent loses ~80 lines | Every field reads / writes the same FormControl as before. |
| **T4** | Extract Injury list + modal | code | new `appointment-add-injuries.component.{ts,html}` + `appointment-add-injury-modal.component.{ts,html}`; parent loses ~600 lines | Injury list renders all entries. Modal Add / Edit Cumulative-injury date-range logic intact. Insurance + Claim Examiner nested sub-forms intact. Submitted `injuries` array identical to baseline. |
| **T5** | Extract Attorney sections (AA + DA via shared component) | code | new `appointment-add-attorney-section.component.{ts,html}` parameterised by `role: 'applicant' \| 'defense'`; parent loses ~500 lines | Email-search cascade still fires lookups. Conditional validators still apply when section enabled. Both AA + DA sections render and submit identically to baseline. |
| **T6** | Extract Patient Demographics + Address | code | new `appointment-add-patient-demographics.component.{ts,html}`; parent loses ~400 lines | SSN masking, DOB normalization, patient-profile pre-fill, patient-list lookup all intact. |
| **T7** | Extract Schedule (AppointmentType + Location + slot picker) | code | new `appointment-add-schedule.component.{ts,html}`; parent loses ~350 lines | AppointmentType -> Location -> DoctorAvailability cascade intact. Slot picker shows available dates + times. Past-date guard fires. |
| **T8** | Cleanup parent + docs | code | parent down to ~600-800 lines; update `Domain/Appointments/CLAUDE.md` and `Application/.../CLAUDE.md` to describe the new structure; remove any dead helpers | Parent component compiles, all sections render, full booking flow end-to-end passes Playwright MCP smoke per role. |

## Files touched

### New files (per phase)

```
angular/src/app/appointments/sections/
  appointment-add-custom-fields.component.ts          (T1)
  appointment-add-custom-fields.component.html        (T1)
  appointment-add-authorized-users.component.ts       (T2)
  appointment-add-authorized-users.component.html     (T2)
  appointment-add-employer-details.component.ts       (T3)
  appointment-add-employer-details.component.html     (T3)
  appointment-add-injuries.component.ts               (T4)
  appointment-add-injuries.component.html             (T4)
  appointment-add-injury-modal.component.ts           (T4)
  appointment-add-injury-modal.component.html         (T4)
  appointment-add-attorney-section.component.ts       (T5)
  appointment-add-attorney-section.component.html     (T5)
  appointment-add-patient-demographics.component.ts   (T6)
  appointment-add-patient-demographics.component.html (T6)
  appointment-add-schedule.component.ts               (T7)
  appointment-add-schedule.component.html             (T7)
```

### Modified files

```
angular/src/app/appointments/appointment-add.component.ts        (every phase -- shrinks)
angular/src/app/appointments/appointment-add.component.html       (every phase -- shrinks)
src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md (T8 -- docs sync)
```

### Untouched

- `appointment-add.component.scss` (if it exists; styles stay)
- Every proxy file under `angular/src/app/proxy/`
- Every backend `.cs` file
- The booking submit endpoint and its DTO

## Risk + Rollback

### Risks

| Risk | Mitigation |
| --- | --- |
| Cascade validator silently breaks because it references a control whose parent FormGroup just moved | Each phase: re-run the cascade-validator path manually (toggle AA section, toggle injury cumulative checkbox, etc.). |
| FormGroup reference is passed-by-value rather than passed-by-reference | TypeScript: `FormGroup` is a reference type. Verify each `@Input() form: FormGroup` is the same instance the parent owns by logging `===` once per phase. |
| Section visibility (`shouldShowApplicantAttorneySection()`) accidentally inverts | Phase 5: keep visibility method on parent, pass `[hidden]` or `*ngIf` from parent down. |
| Children try to call parent's lookup-loading helpers | Wire them as Outputs (`@Output() patientSelected = new EventEmitter<...>`) so the parent triggers its own loaders. |
| Custom Fields lose their dynamic FormControl registration | T1: keep the `customFields` array + the FormControl-creation logic on the parent. Child only renders. |
| ABP Suite regenerates `appointment-add.abstract.component.ts` and erases imports we added | The current file is `appointment-add.component.ts` (no abstract); not a Suite-generated pair. Safe. |

### Rollback

Each phase is a single commit. `git revert <sha>` if any phase
introduces a regression. Phases are independent; reverting T4
(injuries) does not require reverting T1-T3.

## Verification

### Baseline (T0)

Before Phase 1, capture pre-refactor reference:

1. Sign in as `applicant.attorney@falkinstein.test`.
2. Navigate to `/appointments/add`.
3. Fill every visible field with synthetic data:
   - Patient: new patient, all demographics populated
   - Employer: populated
   - Applicant Attorney: enabled + populated
   - Defense Attorney: enabled + populated
   - 1 injury with cumulative-date + insurance + claim examiner
   - 1 authorized user
   - Every custom field for the chosen AppointmentType
4. Submit.
5. Capture via Playwright MCP:
   - `browser_take_screenshot` of each section pre-submit
   - `browser_network_request` for the POST `/api/app/appointments`
     -- save request body + response
   - `browser_console_messages` -- expect no errors
6. Repeat as `staff@falkinstein.test` (internal role exercises
   different visibility rules).
7. Save artifacts under `tests/baseline/appointment-add/` (gitignored).

### End-to-end (after T8)

Same procedure, same screenshots, same payload. The diff against
baseline should be **empty**.

### Per-role visibility re-check (T8)

Run the form as each of these roles to verify section visibility
hasn't drifted:

- Patient (synthetic) -- expect simplified view (no internal sections)
- Applicant Attorney -- AA section enabled, DA optional
- Defense Attorney -- DA section enabled, AA optional
- Claim Examiner -- CE-restricted view
- Clinic Staff -- full view
- admin -- full view

## Out of scope (future work)

- `appointment-view.component.ts` ngModel -> reactive (separate task #122)
- Adding spec / component tests for the booking form (deferred)
- New booking features (any new section / field)
- Performance tuning (the form is reactive-heavy but performant)

## Dependencies on other in-flight work

- None. Phase B fixes (#107, #106a/b/c, #117, #105) are all merged in
  `feat/replicate-old-app` so this branch starts from a clean state.
- Main worktree starts Playwright MCP userflow testing in parallel.
  If main finds a bug in the booking form mid-refactor, they file a
  ticket here -- we triage whether to fix in this PR or defer.

## Hand-back

After T8 merges to `feat/replicate-old-app`:

- Update `~/.claude/projects/W--patient-portal-replicate-old-app/memory/`
  with corrected note (the 3-form-approaches framing was wrong;
  current structure is N sub-components owning parts of the parent's
  FormGroup).
- Surface the cleaned-up parent line count + per-section line counts
  in the PR description so review is calibrated.
