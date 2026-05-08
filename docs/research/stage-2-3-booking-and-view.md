---
phase: stage-2-3
tasks: [B1, B2, V1]
date: 2026-05-04
status: research-only
authors: research-agent
---

# Stage 2 + 3 Research: Booking custom fields, Approve permission, View page

Covers B1 (render all 7 CustomField types on the booking form),
B2 (fix the permission attribute on `ApproveAsync`), and V1
(external-user view-detail + change-log frontend).

ASCII only; path:line citations are exact. OLD code is read-only.

---

## B1 -- Render all 7 CustomField types on the booking form

### B1.1 OLD source

OLD enum (`CustomFieldTypeEnum`):

| Value | Name |
|---|---|
| 12 | Alphanumeric |
| 13 | Numeric |
| 14 | Picklist |
| 15 | Tickbox |
| 16 | Date |
| 17 | Radio |
| 18 | Time |

Citations:
- `P:\PatientPortalOld\PatientAppointment.DbEntities\Enums\CustomFieldType.cs:1-13`
  (back-end enum identical to front-end).
- `P:\PatientPortalOld\patientappointment-portal\src\app\enums\custom-field-type.ts:1-9`
  (front-end enum, used in templates as `customeFieldsEnums.<Name>`).

OLD entity (`spm.CustomFields`):
- `P:\PatientPortalOld\PatientAppointment.DbEntities\Models\CustomField.cs:14-104`.
  Columns relevant to rendering: `FieldLabel`, `FieldTypeId`, `FieldLength`,
  `MultipleValues` (comma-separated options for Picklist / Radio / Tickbox),
  `DefaultValue`, `IsMandatory`, `DisplayOrder`, `AppointmentTypeId`.
- Value rows: `P:\PatientPortalOld\PatientAppointment.DbEntities\Models\CustomFieldsValue.cs:14-69`
  -- `CustomFieldId`, `ReferenceId` (= AppointmentId), `CustomFieldValue` (string).

OLD lookup endpoint feeding the booking form:
- Constant `customfieldlookups`:
  `P:\PatientPortalOld\PatientAppointment.Api\Constants\AppointmentRequestLookups.cs:15`.
- It is NOT served by `CustomFieldLookupsController` (that one only returns
  `vCustomFieldTypeLookUp` rows: `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Lookups\CustomFieldLookupsController.cs:22-26`).
  The booking form pulls all named lookups via the generic
  `AppointmentsService.lookup([...])` call -- the array literal
  `[..., AppointmentRequestLookups.customFieldLookUps, ...]` at
  `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\add\appointment-add.component.ts:105`
  and similarly `:350`. The lookup method maps each name to its REST GET
  and returns an `AppointmentLookupGroup` whose `customFieldLookUps`
  collection feeds the form.

OLD form-array binding (line citations are the OLD HTML):
`P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\add\appointment-add.component.html:806-822`
and the edit-mode mirror at
`P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\edit\appointment-edit.component.html:948-963`.

### B1.2 OLD UI structure (the parity-relevant truth)

CRITICAL FINDING: OLD's HTML only renders THREE of the seven enum values.

```text
*ngIf="filedTypeId == customeFieldsEnums.Alphanumeric"  -> <input type="text">
*ngIf="filedTypeId == customeFieldsEnums.Numeric"       -> <input type="text"> (numeric label only)
*ngIf="filedTypeId == customeFieldsEnums.Date"          -> <rx-date>
```

Picklist (14), Tickbox (15), Radio (17), Time (18) exist in the OLD enum
and the OLD database, but no `*ngIf` branch was ever wired in the OLD
booking form HTML. Confirmed by `Grep` of the entire OLD
`appointment-request/appointments/` folder: only the three branches
above appear.

OLD layout:
- One `*ngFor` over `appointmentFormGroup.controls.customFieldsValues.controls`.
- Each entry binds a child `[formGroup]` and renders one of the three
  `<input>` controls plus a `<label>{{fieldLabel}}</label>`.
- Section header "Additional Details" only shows when
  `isCustomeFileds = true`.
- No client-side `Validators.required` is attached to custom-field
  controls (the original form-array `bindFormGroup` pulls the model
  shape, but `IsMandatory` does not flow into Angular validation in
  OLD -- this is an OLD gap).

### B1.3 Decision: parity-plus

Strict parity for the 3 OLD-rendered types is mandatory. The remaining 4
(Picklist, Tickbox, Radio, Time) are LATENT in OLD: schema + enum exist
but the HTML never rendered them. Per CLAUDE.md "OLD code is the
target", a strict reading would skip them, but per the task brief the
work is "post-G3 enum extension" and B1 is explicitly "render all 7
types". Treat the 4 missing branches as a NEW-only completion (flag in
`docs/parity/_parity-flags.md`).

### B1.4 Renderer matrix (NEW implementation)

NEW uses Angular 20 standalone + reactive forms + LeptonX styling. NO
Angular Material is currently imported in `appointment-add.component.ts`
-- the booking form already uses `@ng-bootstrap/ng-bootstrap`
(`NgbDatepickerModule`, `NgbTimepickerModule` via `NgbTimeAdapter`) and
plain `<input>` / `<select>`. Stick with that family for consistency.

| FieldType | Control | Validator(s) | Options source |
|---|---|---|---|
| Alphanumeric (12) | `<input type="text" formControlName="customFieldValue" maxlength="{FieldLength}">` | `Validators.required` if `IsMandatory`; `Validators.maxLength(FieldLength)` if `FieldLength != null`; no regex (OLD has none) | n/a |
| Numeric (13) | `<input type="number" formControlName="customFieldValue" inputmode="numeric">` | `Validators.required` if `IsMandatory`; `Validators.pattern(/^-?\\d+(\\.\\d+)?$/)` (decimal allowed -- OLD column is plain string, no integer-only constraint) | n/a |
| Picklist (14) | `<select formControlName="customFieldValue"><option *ngFor="let o of options">{{o}}</option></select>` | `Validators.required` if `IsMandatory` | `MultipleValues.split(",").map(s => s.trim())` |
| Tickbox (15) | Single `<input type="checkbox" formControlName="customFieldValue">` -- if the OLD `MultipleValues` field has a comma list, render one checkbox PER option and store the selected list as a comma-joined string (OLD's pattern for value persistence) | `Validators.requiredTrue` (single) or custom "at least one selected" if list, only when `IsMandatory` | Same comma split as Picklist |
| Date (16) | `<input ngbDatepicker formControlName="customFieldValue" placeholder="MM/DD/YYYY">` (matches the existing booking form Date pattern) | `Validators.required` if `IsMandatory`; `MM/DD/YYYY` parser already configured | n/a |
| Radio (17) | `<input type="radio" name="customField{i}" formControlName="customFieldValue" [value]="o" *ngFor="let o of options">` | `Validators.required` if `IsMandatory` | Same comma split as Picklist |
| Time (18) | `<ngb-timepicker [meridian]="true" formControlName="customFieldValue">` | `Validators.required` if `IsMandatory` | n/a |

`customFieldValue` is stored on the wire as `string` to match OLD's
`CustomFieldsValue.CustomFieldValue` schema. Date and Time serialize
to `MM/DD/YYYY` and `HH:mm` respectively before submit.

### B1.5 NEW current state

- Enum currently has 3 values (per CLAUDE.md / G3 not yet shipped).
  G3 must extend NEW's `CustomFieldType` enum to OLD's 7 values BEFORE
  B1 lands; otherwise the renderer cannot reference Picklist / Tickbox
  / Radio / Time.
- Domain entity already includes `MultipleValues`, `DefaultValue`,
  `IsMandatory`, `FieldLength`:
  `W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Domain\CustomFields\CustomField.cs:30-95`.
  No separate `CustomFieldOption` entity is needed -- options live in
  `MultipleValues` as a comma-separated string (OLD parity verified
  against `CustomField.cs:78-80` OLD).
- DTO surface (CustomFields feature folder under
  `Application.Contracts/CustomFields/`) already exposes the same
  fields per the W2-5 audit row in
  `docs/parity/external-user-view-appointment.md`.
- Frontend: NO existing render code for custom fields lives in
  `angular/src/app/appointments/appointment-add.component.ts` today.
  Search of that file for "customField" returns only the W2-5
  `AppointmentTypeFieldConfigDto` typedef (lines 42-60), which is the
  DIFFERENT W2-5 feature (override existing booking-form fields, not
  add new ones). B1's renderer is greenfield in NEW.

### B1.6 Implementation plan (B1)

Files to add / edit:

| Path | Change |
|---|---|
| `angular/src/app/appointments/appointment-add.component.ts` | Add `customFieldsArray: FormArray` initialisation; on `appointmentTypeId` change, fetch `CustomField` rows for that type via the proxy `CustomFieldService.getList({ appointmentTypeId })` (already exists per W2-5 audit) and rebuild the array. Build child FormGroups with the validator selection from the matrix above |
| `angular/src/app/appointments/appointment-add.component.html` | New "Additional Details" `<section>` -- one `*ngFor` over the array, with `@switch` on `fieldType` rendering the seven branches above. ARIA labels = `fieldLabel` |
| `angular/src/app/appointments/components/custom-field-control.component.ts` | OPTIONAL: extract the seven branches into a single dumb component to keep `appointment-add.component.html` under the 250-line Angular component cap |
| `src/HealthcareSupport.CaseEvaluation.Application.Contracts/CustomFields/CustomFieldDto.cs` | Verify `FieldType`, `FieldLabel`, `FieldLength`, `MultipleValues`, `DefaultValue`, `IsMandatory`, `DisplayOrder` are exposed. Patch the proxy with `abp generate-proxy` after any backend change |
| `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs` `CreateAsync`/`UpdateAsync` | Persist `CustomFieldsValue` rows from the new DTO field `appointment.customFieldValues` -- map `(CustomFieldId, AppointmentId, Value)` 1:1 with OLD's `CustomFieldsValue` table |

Tests:
- Unit: `AppointmentAddComponent` builds the right validator set for
  each `FieldType` (table-driven test: 7 cases).
- Unit (xUnit): `AppointmentsAppService.CreateAsync` persists submitted
  `CustomFieldValue` rows -- one fact per type (alphanumeric / numeric
  / date / picklist / tickbox / radio / time). approach=tdd for the
  C# side, test-after for the Angular component (UI).

### B1.7 Acceptance criteria (B1)

- Selecting an `AppointmentType` whose `CustomField` rows exist
  populates the "Additional Details" section.
- Each of the 7 types renders its specified control.
- Mandatory custom fields block submit when empty; the field control
  shows an error.
- Picklist / Radio / Tickbox option lists come from
  `CustomField.MultipleValues` parsed as comma-separated.
- Date submits in `MM/DD/YYYY`; Time in `HH:mm`.
- A `Save` round-trip persists `CustomFieldsValue` rows that are read
  back on view (V1 wires this in section V1.6).

---

## B2 -- Permission attribute on `AppointmentsAppService.ApproveAsync`

### B2.1 Current state

```csharp
[Authorize(CaseEvaluationPermissions.Appointments.Edit)]
public virtual async Task<AppointmentDto> ApproveAsync(Guid id)
{
    var appointment = await _appointmentManager.ApproveAsync(id, CurrentUser.Id);
    return ObjectMapper.Map<Appointment, AppointmentDto>(appointment);
}
```

Source:
`W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Application\Appointments\AppointmentsAppService.cs:1194-1199`.

The next method (`RejectAsync`) is also annotated with
`[Authorize(...Appointments.Edit)]` at line 1201 -- it has the same
bug. Both should switch to the per-action gates that already exist.

### B2.2 Permission constant

`Approve` constant exists and is exact:

```csharp
public const string Approve = Default + ".Approve";  // CaseEvaluation.Appointments.Approve
public const string Reject  = Default + ".Reject";   // CaseEvaluation.Appointments.Reject
```

Source:
`W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Application.Contracts\Permissions\CaseEvaluationPermissions.cs:103-104`.

### B2.3 Provider registration

Both children are registered:

```csharp
appointmentPermission.AddChild(CaseEvaluationPermissions.Appointments.Approve, L("Permission:Approve"));
appointmentPermission.AddChild(CaseEvaluationPermissions.Appointments.Reject,  L("Permission:Reject"));
```

Source:
`W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Application.Contracts\Permissions\CaseEvaluationPermissionDefinitionProvider.cs:61-62`.

ROLE GRANTS NOT VERIFIED HERE -- the
`CaseEvaluationPermissionDefinitionProvider` only DEFINES permissions;
role-to-permission grants live in the data seeder
(`CaseEvaluationDataSeederContributor` / role-seeding migration). Out
of scope for B2 (the 1-line attribute fix). If Staff Supervisor / IT
Admin role grants for `Approve` are missing, that surfaces in A1
(approval UI smoke test) and is fixed there.

### B2.4 The richer entry point uses `Approve` correctly

```csharp
[Authorize(CaseEvaluationPermissions.Appointments.Approve)]
public virtual async Task<AppointmentDto> ApproveAppointmentAsync(Guid id, ApproveAppointmentInput input)
```

Source:
`W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Application\Appointments\AppointmentsAppService.Approval.cs:78-79`.

Reject pair at the same file line 129 also uses `Reject`. So Phase 12
got it right; the thin endpoints in `AppointmentsAppService.cs` are
the stale ones.

### B2.5 Other concerns on the method body

- `_appointmentManager.ApproveAsync(id, CurrentUser.Id)` is the same
  domain entrypoint the Phase 12 service uses (line 106 of
  `AppointmentApprovalAppService`), so behavior is consistent.
- No idempotency guard here (Phase 12 added one in the richer
  service); the manager throws on invalid transitions. Acceptable for
  the thin endpoint.
- `CurrentUser.Id` is `Guid?`; the manager signature accepts `Guid?`,
  so no null guard is needed.
- No event publish here (the thin service relies on the manager's
  internal `AppointmentStatusChangedEto` publish). Acceptable.

### B2.6 Tests

Searched
`W:\patient-portal\replicate-old-app\test\HealthcareSupport.CaseEvaluation.Application.Tests\Appointments\AppointmentsAppServiceTests.cs`
for `ApproveAsync` / `Approve` -- no match. No test pins
Edit-vs-Approve on the thin endpoint, so the fix needs no test update.

A new `[Fact]` should land alongside the fix:
`ApproveAsync_RequiresApprovePermission` -- arrange a current user
without `Appointments.Approve`, expect
`AbpAuthorizationException`. Symmetric test for `RejectAsync`.

### B2.7 Implementation plan (B2)

Single file edit:

| Path | Change |
|---|---|
| `src/HealthcareSupport.CaseEvaluation.Application\Appointments\AppointmentsAppService.cs:1194` | `Edit` -> `Approve` |
| `src/HealthcareSupport.CaseEvaluation.Application\Appointments\AppointmentsAppService.cs:1201` | `Edit` -> `Reject` (same defect on the sibling method -- in scope as a one-line fix while the file is open) |

Tests (approach=tdd):
- `test/.../Appointments/AppointmentsAppServiceTests.cs`: 2 new
  `[Fact]`s as above.

### B2.8 Acceptance criteria (B2)

- A user holding `Appointments.Edit` but not `Appointments.Approve`
  receives 403 from the thin `ApproveAsync` endpoint.
- A user holding `Appointments.Approve` succeeds.
- Same matrix for `RejectAsync` / `Appointments.Reject`.
- The two new xUnit facts pass.

---

## V1 -- External-user view-detail + change-log frontend

### V1.1 OLD source (binding)

| Surface | Path |
|---|---|
| External-user appointment list | `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\my-appointments\my-appointment-list.component.html:1-206` and `.ts:1-222` |
| Shared view/edit page | `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\edit\appointment-edit.component.html:1-1009` |
| List route | `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\my-appointments\my-appointment-list.routing.ts` (`applicationModuleId: 6`) |
| Edit route | `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\edit\appointment-edit.routing.ts` (NO guard) |
| Confirmation# search | `my-appointment-list.component.ts:188-209` (`viewAppointment`) -- calls `AppointmentsService.search({ confirmationNumber, accessorEmail, isSearch: 1 })` and routes to `/appointments/<id>?mode=<accessTypeId>` |
| Change log | OLD has a STANDALONE search page at `/appointment-change-logs` (`appointment-change-log/appointment-change-logs/list/appointment-change-log-list.component.html:1-175`); NO per-appointment change-log embedded in `edit/`. |
| Access-rule SQL | `P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs:62, 66, 80-95` (the stored proc adds `CreatedById = @UserId OR AppointmentAccessor row exists`; per-appointment `Get(int id)` returns the full eager graph) |

### V1.2 OLD UI structure

List page sections:

1. Header `<h2>` + "Advanced Search" accordion: 8 filters
   (AppointmentType, Confirmation Number, Location, Status, Claim #,
   Date Of Injury, Date Of Birth `*ngIf="!isUserIsPatient"`,
   SSN). Buttons: Search / Reset / Sync.
2. `<rx-table>` with 11 columns: Type / Patient Name / Gender /
   Confirmation # (link) / Appt Date / SSN / Claim # / DOI / Location /
   Status (color badge) / Action (Document Manager).
3. Pagination footer.

Detail page sections (`appointment-edit.component.html`):

1. Header `<h2>` + status badge + action buttons (top + repeat at
   bottom L:969-1007).
2. **Appointment Details** (L:82-276): status, banners
   (Reschedule reason, Document rejection note, Rejection note,
   Admin Reschedule reason), readonly form fields.
3. **Patient Demographics** (L:280-418): 18+ fields incl. radio
   (Gender), date (DOB), masked phone/SSN, language + "Other" free
   text, interpreter Y/N + vendor.
4. **Employer Details** (L:420-460): FormArray, repeating cards.
5. **Claim Information** (L:463-738): table + Bootstrap modal for
   add/edit.
6. **Additional Authorized Users** (L:740-796): name / email / role /
   rights (View=23 / Edit=24).
7. **Attorney Details** (L:799-937): two cards (Applicant + Defense)
   with include-attorney toggles.
8. **Additional Details** (L:939-965): the 3 OLD-rendered custom
   fields (Alphanumeric / Numeric / Date).

### V1.3 Access-policy / role-matrix table

Source for matrix:
`docs/design/external-user-view-appointment-design.md:435-444`,
`docs/parity/external-user-view-appointment.md:23-69, 99-110`,
and the OLD stored-proc filter cited in V1.1.

External roles (4): Patient, Adjuster (a.k.a. Claim Examiner),
Applicant Attorney, Defense Attorney.
Internal roles (3): Clinic Staff, Staff Supervisor, IT Admin
(Doctor has no list access).

Field-level visibility / editability (external users only -- internal
users see all sections, all fields):

| Section / Field | Patient (owner) | Adjuster (owner) | App.Atty (owner) | Def.Atty (owner) | Accessor View (23) | Accessor Edit (24) | Non-owner non-accessor |
|---|---|---|---|---|---|---|---|
| Appointment Details (status, dates, type) | RO | RO | RO | RO | RO | RO | 404 |
| Patient Demographics: Email | RO (identity-linked) | RW | RW | RW | RO | RW | 404 |
| Patient Demographics: all other fields | RW (in re-apply only) | RW | RW | RW | RO | RW | 404 |
| Employer Details | RW (re-apply) | RW | RW | RW | RO | RW | 404 |
| Claim Information (Add / Edit) | RW (re-apply) | RW | RW | RW | RO | RW | 404 |
| Authorized Users (Add / Edit) | RW | RW | RW | RW | RO | RW | 404 |
| Applicant Attorney section | RW | RW | RW (own email RO) | RW | RO | RW (own email RO) | 404 |
| Defense Attorney section | RW | RW | RW | RW (own email RO) | RO | RW (own email RO) | 404 |
| Additional Details (custom fields) | RW (re-apply) | RW | RW | RW | RO | RW | 404 |
| List filter "Date Of Birth" | hidden | shown | shown | shown | n/a | n/a | n/a |

Action buttons (external):

| Button | Visibility |
|---|---|
| Save | edit mode AND `isView=false` AND user has CanEdit |
| Re-schedule Appointment | status=Approved AND no pending change request |
| Cancel Appointment | status=Approved AND no pending change request |
| Upload Documents | always (gated by accessor edit OR owner) |
| Re-Request | status=Rejected AND `createdById == loginUserId` (re-apply path) |
| Help | always (opens query modal) |
| Back | always (returns to list) |

Hard rule from OLD: a non-owner, non-accessor caller must NOT see the
appointment exists -- the back-end response is 404 (parity with
OLD's "stored proc returns no row"). NEW already enforces this via
`AppointmentAccessRules.CanRead` + `EnsureCanReadAsync` in
`AppointmentsAppService.GetAsync` and
`GetWithNavigationPropertiesAsync` (Phase 13a).

### V1.4 NEW current state

Routing:
- `angular/src/app/appointments/appointment/appointment-routes.ts:4-21`
  -- two routes: `''` (list, both guards) and `'view/:id'` (only
  `authGuard`). NO `permissionGuard` on the view route. Server gate
  closes the same hole via `EnsureCanReadAsync` (per parity doc line
  104).

Components:
- `angular/src/app/appointments/appointment/components/appointment-view.component.ts`
  -- standalone, ngModel-based, ~969 lines. Per CLAUDE.md the design
  doc says ~1604 lines; the working tree count is 969. Either way,
  this is the surface to wire V1 against.
- `angular/src/app/appointments/appointment/components/appointment.component.ts`
  -- list page (ngx-datatable). Already pulls the proxy
  `AppointmentService.getList`.
- `angular/src/app/appointments/appointment-change-logs/appointment-change-logs.component.ts:1-40`
  -- per-appointment change-log viewer (W2-4, ABP audit-log proxy).
  No standalone cross-appointment search page exists.

Backend (Phase 13a/b, already implemented):
- `IAppointmentsAppService.GetByConfirmationNumberAsync(string)` --
  per `docs/parity/external-user-view-appointment.md:102` -- route
  `GET api/app/appointments/by-confirmation-number/{confNum}`. Returns
  `null` when no row, throws `AppointmentAccessDenied` when row
  exists but caller cannot read.
- `GetWithNavigationPropertiesAsync` extended to include
  AppointmentDefenseAttorney, AppointmentEmployerDetail,
  AppointmentInjuryDetails (with sub-entities), AppointmentAccessors
  (per parity doc line 103, Phase 13b).
- `AppointmentAccessRules.CanRead` / `CanEdit` --
  `src/HealthcareSupport.CaseEvaluation.Domain\Appointments\AppointmentAccessRules.cs:60-115`.
  External callers must be creator OR have an `AppointmentAccessor`
  row. `Edit` requires `AccessType.Edit` (24).
- `ExternalUserDtoFilter.MaskInternalFields` masks
  `InternalUserComments` for non-internal callers (Phase 13b, parity
  doc line 107).
- W2-4: `[Audited]` attribute on `Appointment` entity --
  `appointmentPermission` does NOT register children for change-log
  but a separate `AppointmentChangeLogs.Default` permission exists
  (`CaseEvaluationPermissionDefinitionProvider.cs:107`).

Gaps that V1 must close (per the design doc and the parity doc):

1. List page filters do not match OLD's 8-filter Advanced Search
   accordion (Confirmation #, Status, Claim #, DOI, DOB,
   SSN). The current `GetAppointmentsInput` exposes 7 filters but
   the proxy `getList` already sends 18; the missing ones for V1 are
   `confirmationNumber`, `claimNumber`, `dateOfInjury`, `dateOfBirth`,
   `socialSecurityNumber`, `appointmentStatus`. Backend may need
   filter expansion (verify post-Phase 13).
2. List page Document Manager action button (per OLD) -- routes to
   document upload (D1-D4 territory; V1 stubs it as a navigate to
   `/appointments/view/:id` documents tab).
3. Confirmation # search input on the list page header. Wire to
   `AppointmentService.getByConfirmationNumber(confNum)` proxy
   (regenerate proxy after backend ship).
4. View page sections: align with the 7 OLD sections + the 4
   NEW-only sections (AwaitingMoreInfo banner, embedded documents,
   embedded packet, View Change Log button) per
   `docs/design/external-user-view-appointment-design.md:13-486`.
5. Per-field locking: the `canEdit('fieldName')` keyed-by-status
   helper (per design doc section 17) is the chosen approach.
   `AppointmentAccessRules.CanEdit` controls the section-level grant;
   the per-field map handles the inner cases (e.g. Applicant Attorney
   email RO for that role).
6. View Change Log button: route to
   `/appointments/view/:id/change-log` (already in design doc line 41
   and in `appointment-change-logs.component.ts`). Add the route entry
   to `appointment-routes.ts` -- it currently only has `''` and
   `'view/:id'`.

### V1.5 Change-log fetch

OLD: cross-appointment search via stored proc on
`spm.AppointmentChangeLogs` (NEW does NOT need to replicate this;
it's a separate, deferred OLD-only standalone page per the design
doc Exception 1).

NEW per-appointment fetch: `AuditLogsService.getEntityChanges` from
`@volo/abp.ng.audit-logging/proxy` keyed on `entityTypeFullName =
"HealthcareSupport.CaseEvaluation.Appointments.Appointment"` plus the
appointment id -- already implemented at
`appointment-change-logs.component.ts:30-39`. Returns
`EntityChangeWithUsernameDto[]` -- a flat list of property diffs. No
custom DTO/endpoint needed.

### V1.6 Implementation plan (V1)

Files to add / edit:

| Path | Change |
|---|---|
| `angular/src/app/appointments/appointment/components/appointment.component.ts` (+ `.html`) | Replace placeholder filters with the 8-filter Advanced Search accordion (use `LookupSelectComponent` for AppointmentType / Location / Status; `NgbDatepickerModule` for DOI/DOB; mask directive for SSN). Hide DOB row for Patient role via a `RoleService.is('patient')` check |
| Same | Add Confirmation # search input next to the page heading; on submit call the new `AppointmentService.getByConfirmationNumber(...)` (proxy regenerated post-Phase 13a). Toast "Invalid confirmation number" when the call returns null; navigate to `/appointments/view/<id>` on success (with `?mode=<accessType>` query param mirroring OLD) |
| Same | Status badge column -- map `AppointmentStatusType` enum (13 values) to LeptonX colour classes per design doc 3d |
| `angular/src/app/appointments/appointment/components/appointment-view.component.ts` (+ `.html`) | Restructure into the 7 OLD sections + 4 NEW-only sections per design doc 7-14. Per-field lock: introduce `canEdit(fieldName)` helper that returns false unless `accessRule.CanEdit AND statusAllowsField(fieldName, currentStatus)`. Wire `internalUserComments` as readonly text in section 1 (when present). Add "View Change Log" button gated by `*abpPermission="'CaseEvaluation.AppointmentChangeLogs'"` routed to sub-route |
| `angular/src/app/appointments/appointment/appointment-routes.ts` | Add child route `'view/:id/change-log'` -> `AppointmentChangeLogsComponent` with `[authGuard, permissionGuard]` |
| `angular/src/app/proxy/...` | After backend changes, run `abp generate-proxy` (do NOT hand-edit) |
| `src/HealthcareSupport.CaseEvaluation.Application.Contracts\Appointments\GetAppointmentsInput.cs` | Add the missing filter fields the OLD list relies on: `RequestConfirmationNumber`, `ClaimNumber`, `DateOfInjury`, `DateOfBirth`, `SocialSecurityNumber`, `AppointmentStatus` |
| `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore\Appointments\EfCoreAppointmentRepository.cs` | Add `WHERE` clauses for the 6 new filters; respect existing accessor-scope subquery |
| `test/.../Appointments/AppointmentsAppServiceTests.cs` | New facts: filter by ConfirmationNumber, by ClaimNumber, by AppointmentStatus, by DateOfInjury range; also a fact for `GetByConfirmationNumberAsync` returning null vs throwing AccessDenied |
| Component tests (Karma/Jest) | `appointment-view` shows correct sections per role + status; `canEdit('email')` returns false for AppliantAttorney role; "View Change Log" button visibility gated by permission |

approach: `code` for proxy regeneration; `tdd` for the new repo
filters; `test-after` for the Angular components.

### V1.7 Acceptance criteria (V1)

List page:
- Patient role does NOT see DOB filter; the other 3 external roles
  do.
- Confirmation # search routes to `/appointments/view/<id>` on hit;
  toasts "Invalid confirmation number" on miss; never leaks the
  existence of an inaccessible appointment (server returns 403 ->
  toast same generic message).
- Status badge colour matches the 13-value table at design doc 3d.

View page:
- 7 OLD sections render in order; 4 NEW-only sections render when
  applicable.
- `canEdit(field)` denies edits to non-accessor users in tenant
  (404 from server, friendly UI fallback page).
- Applicant Attorney's own email is RO when current role = Applicant
  Attorney; same for Defense Attorney.
- `internalUserComments` shown as RO text only when value is present
  (server already masks for external users).
- "View Change Log" button visible iff
  `CaseEvaluation.AppointmentChangeLogs` permission held; routes to
  `/appointments/view/:id/change-log`.

Change-log sub-page:
- Renders ABP entity changes for the appointment, sorted by
  `changeTime DESC`.
- Permission guard blocks unauthenticated / unauthorised access.

Cross-cutting:
- All visible status badges, action buttons, and field labels match
  the design doc (`docs/design/external-user-view-appointment-design.md`)
  and are Localized via `| abpLocalization`.

---

## Open items / surface to Adrian

1. B1: confirm with Adrian that the 4 latent OLD types
   (Picklist, Tickbox, Radio, Time) should be RENDERED in NEW. They
   exist in OLD's enum and DB but the OLD HTML never rendered them.
   Treating as "completion of an OLD intention" rather than strict
   parity. Will add a `PARITY-FLAG` row on landing.
2. V1: the OLD standalone cross-appointment change-log search at
   `/appointment-change-logs` is intentionally deferred (per
   `docs/design/appointment-change-log-design.md` Exception 1). Confirm
   this remains out of scope for V1.
3. V1: the `view/:id` server gate is already in place
   (`EnsureCanReadAsync`); a client-side `permissionGuard` adds no
   parity gain. Suggest leaving the route guard as `[authGuard]`
   only and relying on the server 403 -> toast pattern for the UX.
4. B2: no role-grant verification was performed; the constants are
   defined and registered. If A1 surfaces "Staff Supervisor cannot
   approve" the data seeder is the place to look, not B2.
