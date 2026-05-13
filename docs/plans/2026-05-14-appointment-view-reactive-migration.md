---
status: in-progress
issue: 122
owner: AdrianG
created: 2026-05-14
approach: code (no tests; mechanical conversion, Playwright MCP per-phase regression check)
---

# #122 -- appointment-view.component reactive-forms migration

## Goal

Migrate `angular/src/app/appointments/appointment/components/appointment-view.component.{ts,html}` from the current mixed template-driven + reactive pattern to a single flat reactive `FormGroup` mirroring the booker (#121) form shape. After this PR, both pages expose the same `form` surface so future shared section components can drop in.

## Why

1. **Consistency with #121.** The booker `appointment-add` is now 100% reactive with a flat 55-control FormGroup. The view file edits the same entities (Patient + Employer + AA + DA + AppointmentInjuryDetails) and should share the shape.
2. **Patch vs assign.** Today the view does `this.patientForm = { ... }` (and 4 sibling assignments). Reactive `patchValue()` updates in place.
3. **`{ standalone: true }` hack disappears.** With a `<form [formGroup]="form">` wrapper, every `formControlName` resolves natively.
4. **OnPush unlocks.** ngModel ↔ plain object two-way binding effectively forbids OnPush. Reactive controls emit Observables.
5. **Server contracts unchanged.** All 5 save endpoints (patient, employer, AA, DA, accessors) keep their DTO shapes; only the front-end state binding changes.

## Locked-in decisions (2026-05-14)

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | **Flat + prefixed FormGroup** (e.g. `applicantAttorneyFirstName`, not nested `applicantAttorney.firstName`) | Matches booker; future shared sections (AA/DA/Patient Demographics) accept a flat parent form via `[formGroup]="form"` |
| 2 | **`[readonly]="isReadOnly"`** replaces every `[disabled]="isReadOnly"` | Matches booker OLD-parity convention (see booker dateOfBirth comment); preserves validators + submit values |
| 3 | **Preserve current no-validator behavior** (no Validators.required / maxLength / email added in this PR) | Adding validators could surface currently-passing-but-actually-invalid existing records. Tackle parity in a follow-up. |
| 4 | **Migrate the authorized-user modal too** (5 fields) | Drops the last ngModel sites; lets us drop `FormsModule` entirely. |
| 5 | **Single PR, 2 commits** (revised 2026-05-14 from initial 7-commit plan) | The 7-section split forced ~50 lines of `[ngModelOptions]={ standalone: true }` churn per intermediate commit because mid-conversion `<form [formGroup]>` still wraps unconverted ngModel inputs. Mechanical regex-replace on identical fields doesn't bisect meaningfully. Cleaner: one conversion commit + one cleanup commit. |

## Phase plan

Each phase ends with `npx ng build --configuration development`, `docker compose restart angular`, Playwright MCP DOM-shape check, and one commit.

| Phase | Subject | Scope | Expected delta |
|-------|---------|-------|----------------|
| V0 | baseline | Full-page Playwright screenshot + DOM snapshot. No code change. | tests/baseline/ only |
| V1 | Patient subsection (19 fields) | Replace `patientForm` + `stateIdControl`; ngModel -> formControlName; wrap with `<form [formGroup]="form">` element; `loadPatientIntoForm()` patch | -100 ts, -10 html |
| V2 | Employer subsection (7 fields) | Replace `employerForm` + `employerStateIdControl`; loadEmployerDetails patches | -40 ts, -5 html |
| V3 | Applicant Attorney subsection (12 fields + emailSearch + identityUserId picker + enabled toggle) | Replace `applicantAttorneyForm` + `applicantAttorneyStateIdControl`; 5 load-AA-* methods patchValue; email-search keeps working | -80 ts, -8 html |
| V4 | Defense Attorney subsection (12 fields, mirrors AA) | Same as V3 | -80 ts, -8 html |
| V5 | Top-level singletons (`panelNumber`, `applicantAttorneyEnabled`, `defenseAttorneyEnabled`) | Trivial flat controls | -10 ts |
| V6 | Authorized-user modal (5 fields, sub-FormGroup) | New `authorizedUserFormGroup` builder; modal binds via `[formGroup]` | -20 ts |
| V7 | Cleanup | Drop `FormsModule` import + `[ngModelOptions]`; drop the 4 orphan FormControl props; drop dead reference re-assignments; Playwright MCP final regression | -20 ts, -5 html |

Final expected: `appointment-view.component.ts` ~1,462 -> ~1,200 lines (-260, -18%), html ~952 -> ~916 (-36).

## File-name mapping (flat + prefixed)

| Source object | Control name (flat) | Notes |
|---------------|---------------------|-------|
| `panelNumber` | `panelNumber` | top-level |
| `patientForm.firstName` | `patientFirstName` | |
| `patientForm.lastName` | `patientLastName` | |
| `patientForm.middleName` | `patientMiddleName` | |
| `patientForm.email` | `patientEmail` | |
| `patientForm.genderId` | `patientGenderId` | |
| `patientForm.dateOfBirth` | `patientDateOfBirth` | NgbDateStruct or string -- match booker |
| `patientForm.cellPhoneNumber` | `patientCellPhoneNumber` | |
| `patientForm.phoneNumber` | `patientPhoneNumber` | |
| `patientForm.phoneNumberTypeId` | `patientPhoneNumberTypeId` | |
| `patientForm.socialSecurityNumber` | `patientSocialSecurityNumber` | |
| `patientForm.street` | `patientStreet` | |
| `patientForm.address` | `patientAddress` | |
| `patientForm.apptNumber` | `patientApptNumber` | "Unit #" -- view page only |
| `patientForm.city` | `patientCity` | |
| `patientForm.stateId` | `patientStateId` | replaces `stateIdControl` |
| `patientForm.zipCode` | `patientZipCode` | |
| `patientForm.appointmentLanguageId` | `patientAppointmentLanguageId` | |
| `patientForm.needsInterpreter` | `patientNeedsInterpreter` | |
| `patientForm.interpreterVendorName` | `patientInterpreterVendorName` | |
| `patientForm.refferedBy` | `patientRefferedBy` | OLD spelling preserved per parity |
| `employerForm.employerName` | `employerName` | unchanged (already prefixed) |
| `employerForm.occupation` | `employerOccupation` | |
| `employerForm.phoneNumber` | `employerPhoneNumber` | |
| `employerForm.street` | `employerStreet` | |
| `employerForm.city` | `employerCity` | |
| `employerStateIdControl` | `employerStateId` | |
| `employerForm.zipCode` | `employerZipCode` | |
| `applicantAttorneyEnabled` | `applicantAttorneyEnabled` | |
| `applicantAttorneyEmailSearch` | `applicantAttorneyEmailSearch` | |
| `applicantAttorneyForm.identityUserId` | `applicantAttorneyIdentityUserId` | |
| `applicantAttorneyForm.firstName` | `applicantAttorneyFirstName` | |
| `applicantAttorneyForm.lastName` | `applicantAttorneyLastName` | |
| `applicantAttorneyForm.email` | `applicantAttorneyEmail` | |
| `applicantAttorneyForm.firmName` | `applicantAttorneyFirmName` | |
| `applicantAttorneyForm.webAddress` | `applicantAttorneyWebAddress` | |
| `applicantAttorneyForm.phoneNumber` | `applicantAttorneyPhoneNumber` | |
| `applicantAttorneyForm.faxNumber` | `applicantAttorneyFaxNumber` | |
| `applicantAttorneyForm.street` | `applicantAttorneyStreet` | |
| `applicantAttorneyForm.city` | `applicantAttorneyCity` | |
| `applicantAttorneyStateIdControl` | `applicantAttorneyStateId` | |
| `applicantAttorneyForm.zipCode` | `applicantAttorneyZipCode` | |
| `applicantAttorneyForm.applicantAttorneyId` | (kept on instance, not in form) | only used at save() to detect existing-vs-new |
| `defenseAttorney*` | `defenseAttorney*` | mirror AA exactly |
| `authorizedUserDraft.*` | sub-FormGroup `authorizedUser` | modal-only; `[formGroup]="authorizedUserForm"` |

## Risks

- **Lookup-select rebind glitch.** `<abp-lookup-select>` historically prefers `[formControl]`. With `formControlName` inside a `<form [formGroup]>`, the component still receives a FormControl via the directive -- but the timing of the `setValue` calls in load-by-email handlers matters. Playwright MCP per-phase check catches mis-rebind.
- **`patientForm.dateOfBirth` shape.** Currently `{ year, month, day } | string | null`. ngbDatepicker's CVA writes `NgbDateStruct`; API returns ISO string. Existing `parseDateOfBirthFromApi` / `formatDateOfBirthForApi` helpers handle the conversion at load + save -- keep them, just feed the form control instead of the object property.
- **`appointmentInjuryDetails` table.** Read-only on the view page (lines 1215-1260 of TS). Untouched by this migration.
- **`isReadOnly` cascade.** Switching all `[disabled]` to `[readonly]` is a behavioral change for external users: they now see the field as visually-readable-but-locked rather than greyed-out. The server permission gate is authoritative; this is purely a frontend convention swap matching the booker.

## Out of scope (follow-ups)

- Validators parity with booker (Validators.required / maxLength / email) -- separate ticket.
- Section-component extraction (`<app-appointment-add-patient-demographics>` etc.) reused on the view page -- separate ticket; needs an OnPush + Input(form) pass after this migration.
- `appointmentInjuryDetails` add/edit modal on the view page -- separate ticket; today the view page is read-only for injuries.

## Acceptance

- 0 `[(ngModel)]` sites remain in `appointment-view.component.html` after V7.
- 0 `[ngModelOptions]` sites remain.
- `FormsModule` import dropped from the component.
- All 5 save endpoints still receive identical payload shapes (verified by reading form.getRawValue() vs the previous `this.patientForm` reads side-by-side).
- Playwright MCP after V7: same 6 cards render in the same order, `isReadOnly` external user flow shows locked-but-readable inputs, internal admin flow allows edit-and-save.
- Zero console errors per phase.

## Out of scope risks

None identified beyond the above.
