# Appointments -- booking form and appointment view/edit

## What Lives Here

- `wizard/appointment-wizard.component.ts` -- the routed booking page (`/appointments/request`,
  every role). EXTENDS `AppointmentAddComponent` and adds the stepper, per-step validation,
  drafts and the leave prompt.
- `appointment-add.component.ts` -- the booking engine: owns the reactive FormGroup, the
  cascades, the booking modes and the submit call. No route loads it directly.
- `sections/` -- 9 template-only section components (Schedule, PatientDemographics,
  EmployerDetails, AttorneySection, ClaimPartiesSection, ClaimInformation, Documents,
  AuthorizedUsers, CustomFields).
- `availability-calendar/` -- the date and time picker, shared with the reschedule flows.
- `shared/submit-payload.mapper.ts` -- builds the single `AppointmentSubmitDto`;
  `shared/booking-mode.ts` -- resolves the booking mode from the query string.
- `appointment/components/appointment-view.component.ts` -- view/edit page for staff
  and external roles; reads via `getRawValue()` at save time.
- `appointment-change-logs/` -- read-only audit trail component.
- `shared/attorney-section-validators.ts` -- shared required-validator helpers for
  AA/DA section toggle wiring (used by both add and view).

## Conventions

### Section components are template-only -- add no state or HTTP there

All 9 sections under `sections/` receive `@Input() form: FormGroup` (or a slice of the
parent's state, such as the custom-fields FormArray or the injury list) and render template
controls. Every cascade subscription, lookup
call, and submit handler lives exclusively in `AppointmentAddComponent`. Violations
break the parent's concurrency counters and submit guards. The section docstrings
describe exactly which state stays in the parent.

### Race-safe request-version counters -- required for rapid type changes

`AppointmentAddComponent` maintains two counters:

- `fieldConfigsRequestVersion` -- guards `applyFieldConfigsForAppointmentType()`,
  which fetches `AppointmentTypeFieldConfigDto[]` from
  `GET /api/app/appointment-type-field-configs/by-appointment-type/:id`.
- `customFieldsRequestVersion` -- guards `loadCustomFieldsForAppointmentType()`,
  which calls `CustomFieldsService.getActiveForAppointmentType()`.

Before applying a response, compare the counter value to the captured snapshot. If
they differ, discard the response -- the user has already switched AppointmentType.
Add the same guard to any future per-type fetch in this component.

### AppointmentTypeFieldConfigDto -- an inline copy remains

The regenerated proxy now has the type (`proxy/appointment-type-field-configs/models.ts`), but
`appointment-add.component.ts` still declares its own inline copy near the top of the file. When
you touch that code, import the proxy type and delete the inline copy; never edit `proxy/` by
hand.

### AA/DA section toggle-off must open an ABP confirmation modal BEFORE clearing validators

`applicantAttorneyEnabled` / `defenseAttorneyEnabled` have THREE states: `null` means the booker
has not answered yet (the section is not required and no modal opens; the wizard blocks Continue
until they answer), `true` opens the section, `false` asks for confirmation.
`resolveAttorneyToggleAction` maps the value to the action -- never test `!enabled`, which treats
unanswered as No.

When one of them flips to false, the
`valueChanges` subscriber calls `confirmAaToggleOff()` / `confirmDaToggleOff()`,
which opens `confirmationService.warn(...)` before touching validators or values.

- On cancel/dismiss: revert the toggle with `setValue(true)` (with emitEvent so
  OnPush sections re-render) -- do NOT clear validators or email value.
- On confirm: call `applyConditionalEmailValidator(..., false)` then
  `applyAttorneySectionValidators(form, 'applicant|defenseAttorney', false)` then
  `setValue(null, { emitEvent: false })` on the email field.

IMPORTANT: always call `updateValueAndValidity({ emitEvent: false })` inside
`applyConditionalEmailValidator`. Omitting `emitEvent: false` re-fires the control's
`valueChanges`, which triggers the enabled-toggle subscriber and causes a recursive
loop. The DA modal polarity is inverted from AA: Yes = keep section (revert toggle);
No/dismiss = remove DA (clear validators).

### AppointmentViewComponent -- getRawValue() is required at save time

`save()` calls `this.form.getRawValue()`, not `this.form.value`. External roles hit
`form.disable()` in `ngOnInit` (the `isReadOnly` gate), so `form.value` would return
an empty object for them. `getRawValue()` includes disabled controls, keeping the
payload shape stable regardless of the role gate. The server's permission attributes
remain authoritative on every write.

### The Claim Examiner is one appointment-level record

The Claim Examiner (and the Primary Insurance) are entered once per appointment, on the wizard's
Examiner and Insurance steps (`AppointmentAddClaimPartiesSectionComponent`), in the
`appointmentClaimExaminer*` / `appointmentInsurance*` controls. CE Name and Email are required.
The injury modal no longer has insurance or CE fields. At submit, the appointment's
`claimExaminerEmail` comes from `appointmentClaimExaminerEmail`.

The older top-level `claimExaminerEnabled` / `claimExaminerName` / `claimExaminerEmail` controls
are still on the FormGroup but are not wired to any input -- leave `claimExaminerEnabled` false.
Setting it true puts a `Validators.required` on a control with no input, and the form can never
be submitted.

### The external-user-lookup endpoint is a scoped SEARCH, not a list

`GET /api/public/external-signup/external-user-lookup?filter=<term>` requires a search term
(blank returns nothing) and covers all four roles (Patient, Applicant Attorney, Defense
Attorney, Claim Examiner). Results are scoped by caller: internal staff search the whole
tenant; an external caller sees only co-parties on appointments they can already see
(`AppointmentVisibilityService` + `ExternalCoPartyRules`, HIPAA). The four roles are
capability-equal for this search, so do not re-narrow `allowedRoleNames`. (Elsewhere they
differ in one respect: only Patient holds `Patients.RevealSsn`.) The view-page AA/DA picker is an
`ngbTypeahead` search bar over this endpoint (not a load-all dropdown); the exact-email
"Load" box remains alongside it.

### AA/DA attorney section -- no identity-based pre-fill; the wizard pre-fills from the saved profile

The firm-based AA/DA model treats every AA/DA account as a firm: a firm/paralegal books
on behalf of a DISTINCT attorney, so the attorney section starts blank and editable
(`[isReadOnly]="false"` for both cards). The former own-role auto pre-fill was removed from the
booking form on 2026-06-12, in two parts, named here in plain text because neither exists in the
booking form any more: the construction-time self-seed applyOwnRoleAttorneyPrefill (deleted), and the
profile-load auto-load through loadApplicantAttorneyForCurrentUser and its defense twin (no longer
called by the booking form; the appointment view page keeps its own copy). The auto-load called the
`applicant-attorney-details-for-booking` / `defense-attorney-details-for-booking` endpoints with the
booker's identity. That endpoint
returns the firm's OWN email + registration firm name for a firm account, so auto-loading
would re-seed the booker's identity into the on-behalf section.

What replaced it (#9): the wizard's `prefillBookingAttorney()` fills a BLANK applicant or
defense step (per the profile's `kind`) from the booker's own saved attorney profile
(`MyAttorneyProfileService` -- the record they edit at `/user-management/attorneys/my-profile`),
not from their login identity. It never overwrites a section a resumed draft or a
re-evaluation / re-request prefill has already filled, and it runs only for Applicant or Defense
Attorney bookers.

The booking form has no attorney lookup UI. `loadApplicantAttorneyByEmail()`,
`onApplicantAttorneySelected()` and `loadDefenseAttorneyByEmail()` still exist in
`appointment-add.component.ts` but no template binds them; the working email search and picker
are on the appointment detail page. Submit persists what the booker typed to a master row keyed
by the form email (`AppointmentsAppService.UpsertApplicantAttorneyForAppointmentAsync`, called
inside `SubmitAsync`).

## Gotchas

- `form.reset()` nulls `applicantAttorneyEnabled` and `defenseAttorneyEnabled`, and since
  2026-08-18 that is the CORRECT post-reset state ("unanswered"). Do NOT patch them back to
  `true` -- that restores the silent "both attorneys exist" default the change removed. (See
  `reset()` in `appointment-add.component.ts`.)
- `appointmentDate` is stored as a combined ISO datetime (date + time merged at
  submit via `combineAppointmentDateAndTime`). Never pass the raw date-picker value
  directly to the API.
- Claim Information (`injuryDrafts`) is required before submit. The view page shows
  injuries read-only; the booking form is the canonical add/edit surface.

## Related

- docs/frontend/APPOINTMENT-BOOKING-FLOW.md
- docs/frontend/ROLE-BASED-UI.md
- docs/frontend/COMPONENT-PATTERNS.md
