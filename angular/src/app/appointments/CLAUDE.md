# Appointments -- booking form and appointment view/edit

## What Lives Here

- `appointment-add.component.ts` -- the booker; owns the reactive FormGroup.
- `sections/` -- 7 template-only section components (Schedule, PatientDemographics,
  AuthorizedUsers, EmployerDetails, AttorneySection, ClaimInformation, CustomFields).
- `appointment/components/appointment-view.component.ts` -- view/edit page for staff
  and external roles; reads via `getRawValue()` at save time.
- `appointment-change-logs/` -- read-only audit trail component.
- `shared/attorney-section-validators.ts` -- shared required-validator helpers for
  AA/DA section toggle wiring (used by both add and view).

## Conventions

### Section components are template-only -- add no state or HTTP there

All 7 sections under `sections/` receive `@Input() form: FormGroup` (or a FormArray
for CustomFields) and render template controls. Every cascade subscription, lookup
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

### AppointmentTypeFieldConfigDto is inlined, not in proxy/

The type is defined at the top of `appointment-add.component.ts` (search for
`AppointmentTypeFieldConfigDto`) because `abp generate-proxy` has not yet been run
after the W2-5 backend ship. Do NOT move it to `proxy/` by hand; regenerate the
proxy and delete the inline copy.

### AA/DA section toggle-off must open an ABP confirmation modal BEFORE clearing validators

When `applicantAttorneyEnabled` or `defenseAttorneyEnabled` flips to false, the
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

### claimExaminerEnabled is vestigial -- leave it false

The top-level `claimExaminerEnabled` / `claimExaminerName` / `claimExaminerEmail`
controls on the booker FormGroup are not wired to any DOM input. Setting
`claimExaminerEnabled` to true triggers a `Validators.required` on `claimExaminerEmail`
with no matching input, making the form permanently unsubmittable. The canonical
Claim Examiner data lives in each injury draft's child FormGroup (built in
`AppointmentAddClaimInformationComponent`). The CE email fan-out at submit time reads
from `injuryDrafts[0].claimExaminer.email`, not from this top-level control.

### DA is excluded from the external-user-lookup endpoint by design

`GET /api/public/external-signup/external-user-lookup` filters out the Defense
Attorney role per the D-2 decision. `defenseAttorneyOptions` will always be an empty
array even when DA users are registered. The DA pre-fill flow uses the email-search
box, not the dropdown. Do not add DA to the lookup endpoint's `allowedRoleNames`
without re-evaluating D-2.

### AA/DA own-role email pre-fill is readonly by design

When the booker is an Applicant Attorney or Defense Attorney (and not IT Admin),
`applyOwnRoleAttorneyPrefill()` seeds the corresponding email + firstName from their
identity. The HTML pairs this with `[readonly]` on those fields. IT Admin bookers
skip the pre-fill so they can book on behalf of any party.

## Gotchas

- `form.reset()` nulls `applicantAttorneyEnabled` and `defenseAttorneyEnabled`.
  `reset()` must be followed by `patchValue({ applicantAttorneyEnabled: true,
defenseAttorneyEnabled: true })` to restore the required-validator state.
  (See `reset()` in `appointment-add.component.ts`, BUG-044 comment.)
- `appointmentDate` is stored as a combined ISO datetime (date + time merged at
  submit via `combineAppointmentDateAndTime`). Never pass the raw date-picker value
  directly to the API.
- Claim Information (`injuryDrafts`) is required before submit. The view page shows
  injuries read-only; the booking form is the canonical add/edit surface.

## Related

- docs/frontend/APPOINTMENT-BOOKING-FLOW.md
- docs/frontend/ROLE-BASED-UI.md
- docs/frontend/COMPONENT-PATTERNS.md
