---
feature: fax-optional
date: 2026-06-01
status: in-progress
base-branch: main
related-issues: []
branch: feat/fax-optional
---

## Goal

Make every Fax field on the appointment form optional -- so neither the form
nor the API rejects a blank Fax -- while keeping the existing max-length caps.

## Context

### Headline finding (corrects the premise)

The request assumed Fax is "required in both the frontend and backend." Research
shows that is only half true: **the backend already treats Fax as fully
optional** -- there is nothing to change server-side, and no DB migration is
needed. The required-ness lives **entirely in the Angular form**, applied
*dynamically* when a section is enabled. So this is a **front-end-only** change.

### Backend verified ALREADY optional (no change)

For every Fax-bearing entity (ApplicantAttorney `FaxNumber`, DefenseAttorney
`FaxNumber`, AppointmentClaimExaminer `Fax`, AppointmentPrimaryInsurance
`FaxNumber`):
- Domain entity property is `[CanBeNull] string?` -- no constructor guard.
- Manager `CreateAsync`/`UpdateAsync` call only `Check.Length(...)` (max-length),
  never `Check.NotNullOrWhiteSpace`.
- Create/Update DTOs carry only `[StringLength(...)]` -- no `[Required]`.
- No FluentValidation `RuleFor(x => x.Fax).NotEmpty()`.
- EF Core columns are NULLABLE (no `.IsRequired()`), in
  `CaseEvaluationDbContext` (ApplicantAttorney ~604, DefenseAttorney ~632,
  ClaimExaminer ~679, PrimaryInsurance ~695). **No migration required.**

So the API/DB already accept a blank Fax. (If you submit a booking with the
front-end validators bypassed, the server stores Fax = null happily.)

### Front-end: where "required" actually comes from

| Fax control | Component | Required today? | Mechanism |
|---|---|---|---|
| `applicantAttorneyFaxNumber` | attorney section (add + view, shared) | required WHEN the AA "Include" toggle is on | `ATTORNEY_SECTION_SUFFIXES` includes `{ name: 'FaxNumber', maxLength: 19 }` (`appointments/shared/attorney-section-validators.ts:30`); `applyAttorneySectionValidators` / `wireAttorneySectionToggle` add `Validators.required` to every suffix when the section is enabled |
| `defenseAttorneyFaxNumber` | attorney section (add + view, shared) | required WHEN the DA toggle is on | same shared array (one edit fixes both AA + DA, add + view) |
| `injuryClaimExaminerFax` | claim-information modal | required WHEN the per-injury claim-examiner toggle is on | `applyClaimExaminerRequiredValidators()` `fields` array includes `{ key: 'injuryClaimExaminerFax' }` (`appointments/sections/appointment-add-claim-information.component.ts:~295`) |
| `injuryInsuranceFax` | claim-information modal | ALREADY optional | not in any required list; label has no `*` |

Control declarations keep their own max-length: the attorney fax controls are
`[null, [Validators.maxLength(19)]]` (appointment-add ~445/461, appointment-view
~258/273); `injuryClaimExaminerFax` has no Angular validator (max-length is the
template `maxlength="20"` + backend `Check.Length`).

### Live UI / "why logs are thrown"

Investigated the booking form (Playwright, this session). The attorney sections
default to "Include" ON, so AA/DA Fax render with a `*` and are required; the
claim-examiner Fax shows `*` when its section is on; insurance Fax shows no `*`.
**No console errors/logs are thrown** -- the block is pure client-side reactive-
form validation: `onSubmit()` (`appointment-add.component.ts:~1004`) sees
`this.form.invalid`, sets "Please complete all required fields before saving.",
marks controls touched, and returns *before any HTTP call*. So nothing reaches
the server and no server log is emitted; the only "error" is the inline
`is-invalid` styling on the empty Fax field.

### Inconsistency to fix in passing

`appointment-view` renders the attorney Fax label WITHOUT a `*`, yet the shared
validator still makes it required when the section is enabled -- so an edit can
be blocked on an empty Fax with no visible reason. Removing the required wiring
fixes this mismatch.

## Approach

Front-end only. Stop adding `Validators.required` to the three Fax controls, and
remove the now-misleading `*` labels. Keep all max-length behavior.

1. Drop the `FaxNumber` entry from `ATTORNEY_SECTION_SUFFIXES`. Because the
   attorney fax controls are initialized with a static `Validators.maxLength(19)`
   and the shared toggle only ever *added* `required` to listed suffixes, removing
   the entry leaves max-length intact and simply never marks fax required (covers
   AA + DA across both the add and view forms).
2. Drop the `injuryClaimExaminerFax` entry from the `fields` array in
   `applyClaimExaminerRequiredValidators()` so it is never marked required (its
   max-length is the template attribute + backend, unchanged).
3. Remove the `*` from the two "Fax *" labels (attorney-section + claim-info
   templates). appointment-view already has no `*`. Leave the harmless
   `[class.is-invalid]` bindings (a blank fax can no longer be invalid).
4. No backend, DTO, migration, or proxy changes.

**Alternatives rejected:**
- Touching the backend/DTOs/migration: unnecessary -- already optional. Reject.
- Adding an `optional` flag to the shared suffix array instead of removing the
  entry: more code for the same result; removal is cleaner since the static
  max-length already lives on the control. Reject.

## Tasks

- T1: Make attorney Fax optional (AA + DA, add + view).
  - approach: test-after
  - files-touched: angular/src/app/appointments/shared/attorney-section-validators.ts (remove the `FaxNumber` suffix entry); update angular/src/app/appointments/shared/attorney-section-validators.spec.ts
  - acceptance: with a section enabled, the fax control is NOT `required` (valid
    when blank) and still rejects > 19 chars; with the section disabled it stays
    optional; spec updated to assert fax is never required; existing AA/DA
    required fields (name/firm/phone/address) still become required when enabled.

- T2: Make claim-examiner Fax optional.
  - approach: test-after
  - files-touched: angular/src/app/appointments/sections/appointment-add-claim-information.component.ts (remove `injuryClaimExaminerFax` from the required `fields` array)
  - acceptance: enabling the claim-examiner section no longer marks fax required;
    submitting an injury with a blank claim-examiner fax is allowed; other CE
    required fields unchanged.

- T3: Drop the misleading required markers.
  - approach: code
  - files-touched: appointment-add-attorney-section.component.html ("Fax *" -> "Fax"); appointment-add-claim-information.component.html ("Fax *" -> "Fax")
  - acceptance: no Fax label shows `*`; `npx ng build --configuration development` passes.

## Risk / Rollback

- Blast radius: booking-form + appointment-view validation for three Fax
  controls. The shared attorney array also governs AA + DA in both forms, so the
  one edit is verified to leave their OTHER required fields + the fax max-length
  intact (T1 spec). No server/data behavior changes (API already optional).
- Rollback: revert the PR; fax returns to conditionally-required in the form.

## Verification

Rebuild + serve. Booking form: with Applicant + Defense Attorney "Include" on and
their Fax blank -> submit succeeds (no "complete all required fields" block on
Fax); same for a claim with the claim-examiner section on and Fax blank. Entering
> max-length still trims/blocks per the existing maxlength. appointment-view:
editing an attorney with a blank Fax saves. Confirm no `*` on any Fax label and
no console errors. `ng test` (attorney-section-validators spec) + `ng build` green.
