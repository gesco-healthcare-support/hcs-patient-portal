---
feature: it-admin-system-parameters
date: 2026-05-04
phase: 3-frontend (backend AppService implemented 2026-05-03; Angular UI pending)
status: draft
old-source: patientappointment-portal/src/app/components/system-parameter/system-parameters/edit/
old-html: system-parameter-edit.component.html
old-ts: system-parameter-edit.component.ts
new-feature-path: angular/src/app/system-parameters/
shell: internal-user-authenticated (top-bar + side-nav)
screenshots: pending
---

# Design: IT Admin -- System Parameters

## 1. Routes

Single edit page. No list page -- the table has exactly one row per tenant.

| | OLD | NEW |
|---|---|---|
| URL | `/system-parameters/1` (ID=1 hardcoded in nav link) | `/system-parameters` (no ID -- singleton) |
| Component | `SystemParameterEditComponent` | `SystemParametersComponent` (standalone) |
| Guard | `PageAccess` `rootModuleId:33, applicationModuleId:11, accessItem:'edit'` | `[Authorize(CaseEvaluationPermissions.SystemParameters.Edit)]` (IT Admin) |

Backend: `GET api/app/system-parameters` (load) + `PUT api/app/system-parameters` (save).
Both endpoints implemented in Phase 3 (2026-05-03). Angular proxy in
`angular/src/app/proxy/system-parameters/` regenerates on `abp generate-proxy`.

## 2. Shell

Internal-user authenticated shell: top-bar + left side-nav. The nav item
"System Parameters" appears under an IT Admin section in the side-nav.
No tabs, no sub-navigation.

## 3. Screen Layout

```
+-------------------------------------------+
| [H2] System Parameters                    |
+-------------------------------------------+
| [Card]                                    |
|   [Card header] Appointment Days/Time     |
|   [Card body]                             |
|   Row 1: LeadTime | MaxTimePQME           |
|           MaxTimeAME | MaxTimeOTHER       |
|   Row 2: DurationTime | AutoCancelCutoff  |
|           ReminderCutoff | DueDays        |
|   Row 3: CancelTime | JDFCutoffDays       |
|           OverdueDays | isCustomField     |
|                                           |
|   [Card sub-header] CC Email Config       |
|   Row 4: ccEmailIds (full width)          |
|   [Note text -- separator hint]           |
|                                           |
|              [Update button -- right]     |
+-------------------------------------------+
```

OLD layout: responsive grid `col-12 col-sm-6 col-md-4 col-xl-3` per field =
4 fields per row on XL screens, 3 on MD, 2 on SM, 1 on XS.

## 4. Form Fields

All fields use `pattern="[0-9.]+"` on text inputs in OLD (effectively digit-only).
In NEW, use Angular Material `mat-form-field` with `type="number"` and
`min="1"` to enforce positive integer in a type-safe way.

Each int field has a `?` tooltip icon (ABP LeptonX `mat-icon-button` with `matTooltip`).

| # | Form control | Label | Tooltip (verbatim from OLD) | Placeholder | Type | Visible |
|---|---|---|---|---|---|---|
| 1 | `appointmentLeadTime` | Appointment Lead Time (in days) | "Minimum days required to book an appointment. Difference between Today's Date & Appointment Date." | "Appointment Lead Time " (trailing space in OLD) | int | Yes |
| 2 | `appointmentMaxTimePQME` | Appointment Max Time PQME  (in days) (double space) | "Maximum duration allowed to book an appointment for PQME type of appointment." | "Appointment Max Time PQME" | int | Yes |
| 3 | `appointmentMaxTimeAME` | Appointment Max Time AME (in days) | "Maximum duration allowed to book an appointment for AME type of appointment." | "Appointment Max Time AME" | int | Yes |
| 4 | `appointmentMaxTimeOTHER` | Appointment Max Time OTHER  (in days) (double space) | "Maximum duration allowed to book an appointment for OTHER type of appointment." | "Appointment Max Time PQME" (wrong -- see Exception 1) | int | Yes |
| 5 | `appointmentDurationTime` | Appointment Duration Time (in minutes) | "Time duration required for each appointment to take place." | "Appointment Duration Time" | int | Yes |
| 6 | `autoCancelCutoffTime` | Auto Cancel Cut-off Time (in days) | "Specified number of days to upload joint declaration and if it is pending, then the appointment will be auto-cancelled." | "Auto Cancel Cut-off Time" | int | Yes |
| 7 | `reminderCutoffTime` | Reminder Cut-off Time (in days) | "Stop sending reminders if time difference between appointment due date and today's date is matching with this parameter." | "Reminder Cut-off Time" | int | Yes |
| 8 | `appointmentDueDays` | Appointment Due Days (in days) | "Specified number of days to upload documents and if it is pending, the user will be notified to reach before 30 minutes of appointment time." | "Appointment Due Days" | int | Yes |
| 9 | `pendingAppointmentOverDueNotificationDays` | Pending Appointment Over Due Notification (in days) | none | "Pending Appointment Over Due Notification" | int | Yes |
| 10 | `appointmentCancelTime` | Appointment Cancel Time (in days) | none (OLD used "value" as placeholder) | "Appointment Cancel Time" | int | **NEW: Yes (see Exception 2)** |
| 11 | `jointDeclarationUploadCutoffDays` | Days Limit for Joint Declaration Form (in days) | none (OLD used "value") | "Days Limit for Joint Declaration Form" | int | **NEW: Yes (see Exception 2)** |
| 12 | `isCustomField` | Is Show Custom Field | "Display additional fields on appointment booking page." | -- | checkbox | Yes |
| 13 | `ccEmailIds` | CC Email IDs | "Enter the email ids for CC Email Configuration" | (empty) | text | Yes |

OLD source for all fields: `system-parameter-edit.component.html:10-70`

### Validation rules (all int fields 1-11)

- Required (from OLD entity `[Range(1, int.MaxValue)]`)
- Minimum value: 1 (zero and negative are invalid)
- No maximum constraint in OLD
- Validated server-side by `ValidatePositiveIntegers` in `SystemParametersAppService`

### `ccEmailIds` field

- Not required (nullable in DB)
- No email format validation (OLD has none -- strict parity)
- Semicolon-separated multiple addresses
- Help text below field (from OLD line 68): "If you need to add multiple email IDs
  then please seperate using ';'" (OLD typo: "seperate" -- see Exception 3)

### `isCustomField` checkbox

- Renders as a standard checkbox (not a toggle switch in OLD)
- Controls whether custom fields appear on the appointment booking form
- Old class: `custom-control custom-checkbox`, `custom-control-input` (Bootstrap 4)
- NEW: use Angular Material `mat-checkbox`

OLD source: `system-parameter-edit.component.html:55-61`

## 5. Tables / Grids

None. Single-record form.

## 6. Modals

None. No popup/dialog in this component.

## 7. Buttons

| Button | OLD class | Disabled condition | Action |
|---|---|---|---|
| Update | `btn btn-primary float-right` | `!systemParameterFormGroup.valid` | `editSystemParameter()` -> PUT `api/app/system-parameters` |

No Cancel / Reset buttons in OLD.

OLD source: `system-parameter-edit.component.html:73`

## 8. Role Visibility Matrix

| Role | Access |
|---|---|
| IT Admin | Read + Write (full form visible, Update button enabled) |
| Staff Supervisor | Read-only (NEW: render as disabled fields; no Update button or button disabled) |
| Clinic Staff | Read-only (same) |
| External users | No access (API returns 403) |

OLD uses module-level `PageAccess` guard (`applicationModuleId: 11, accessItem: 'edit'`).
In NEW, IT Admin has `SystemParameters.Default` + `SystemParameters.Edit`; other internal
roles have `SystemParameters.Default` (read-only). External users have neither.

For read-only display: the OLD app relies on the `PageAccess` guard to prevent non-admins
from reaching the route at all -- they never see the form. In NEW, decide at UI layer:
either gate the route to IT Admin only, OR show read-only form to Staff Supervisor (useful
for them to see the values that gate their workflows). This is a design decision to surface
to Adrian.

## 9. Branding Tokens

| Element | Token |
|---|---|
| Page title H2 | `--text-color-primary` (default text color) |
| Card header `h6` | `--brand-primary` background or neutral card-header style |
| Update button | `--brand-primary` via `.btn-primary` override |

OLD page has no explicit brand-color on the card header -- it uses Bootstrap default
`card-header` styling (light grey). Match this in NEW unless Adrian specifies otherwise.

## 10. NEW Stack Delta

1. **Singleton route:** Replace OLD `/system-parameters/:systemParameterId` with
   `/system-parameters` (no ID). The AppService `GetAsync()` fetches the per-tenant
   singleton internally.

2. **Form type:** Replace OLD `type="text"` + `pattern="[0-9.]+"` on int fields
   with `type="number"` + `min="1"` Angular Material inputs. This gives proper
   numeric keyboard on mobile and native HTML5 validation.

3. **Tooltip:** Replace OLD `<strong rxTooltip="...">?</strong>` with
   Angular Material `<mat-icon matTooltip="...">help_outline</mat-icon>` (or
   LeptonX equivalent).

4. **Concurrency:** The NEW AppService uses `ConcurrencyStamp`. The Angular form
   must load + include `concurrencyStamp` as a hidden field and submit it with the
   PUT payload. ABP's EF Core integration will reject a stale update with a
   `DbUpdateConcurrencyException` -> surface as "Someone else saved first, please reload."

5. **No ID in URL:** Remove `activatedRoute.params` subscription. Load via
   `systemParametersService.get()` directly.

6. **`checkDaysValidation()` removed:** The OLD commented-out `(keyup)` handler
   on `jointDeclarationUploadCutoffDays` (line 49, commented out) used to do
   cross-field validation against `autoCancelCutoffTime`. In NEW, implement as
   a reactive form validator if the business requires it. Otherwise omit.

7. **ABP proxy regeneration:** After adding `SystemParameterDto` to the AppService,
   run `abp generate-proxy` to regenerate `angular/src/app/proxy/system-parameters/`.
   Never edit proxy files manually.

## 11. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Placeholder for `appointmentMaxTimeOTHER` | "Appointment Max Time PQME" (copy-paste bug) | "Appointment Max Time OTHER" | Clear copy-paste typo; no backend/business impact |
| 2 | `appointmentCancelTime` + `jointDeclarationUploadCutoffDays` hidden | Both commented out in OLD HTML; exist in DB and are read at runtime by cancel + JDF flows | Surface in NEW UI as editable fields | OLD-bug-fix: hiding configurable runtime gates from the only role allowed to manage them is a usability error. The parity audit (GAP table row) explicitly calls this out as a bug-fix exemption |
| 3 | Help text typo "seperate" | "If you need to add multiple email IDs then please seperate using ';'" | "If you need to add multiple email IDs then please separate using ';'" | Spelling fix; no functional impact |
| 4 | No success toast on Update | `editSystemParameter()` calls `put()` but shows no success feedback -- only error dialog on failure | Show success notification via ABP's notification service or `mat-snackbar` on PUT success | Clear UX omission bug; no business rule implication |
| 5 | Label double-spaces | "Appointment Max Time PQME  (in days)" and "Appointment Max Time OTHER  (in days)" have two spaces before the parenthesis | Normalize to single space in labels | Cosmetic; copy-paste artifact |
| 6 | Tooltip placeholder "value" on commented-out fields | Fields 10+11 had `tipMessage="value"` (unfilled) | Write meaningful tooltips in NEW: field 10 "Number of days before appointment when cancellation is allowed"; field 11 "Number of days before appointment after which JDF upload triggers auto-cancel" | OLD fields were never completed; NEW exposes them so they need real tooltips |

## 12. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `edit/system-parameter-edit.component.html` | 1-80 | Full form (all fields, layout, button) |
| `edit/system-parameter-edit.component.html` | 10-62 | All visible form group rows |
| `edit/system-parameter-edit.component.html` | 43-50 | Commented-out `appointmentCancelTime` + `jointDeclarationUploadCutoffDays` |
| `edit/system-parameter-edit.component.html` | 63-70 | CC Email section |
| `edit/system-parameter-edit.component.html` | 72-74 | Update button |
| `edit/system-parameter-edit.component.ts` | 46-58 | `ngOnInit()` -- GET by ID + form init |
| `edit/system-parameter-edit.component.ts` | 62-71 | `editSystemParameter()` -- PUT (no success toast) |
| `edit/system-parameter-edit.component.ts` | 83-88 | `isNumberKey()` -- digit-only key filter |
| `system-parameters.routing.ts` | 5-11 | Route: `:systemParameterId` path |
| `start/app.lazy.routing.ts` | 64-65 | Full URL: `/system-parameters` |
| `docs/parity/it-admin-system-parameters.md` | all | Full parity audit (gap table, backend status as of 2026-05-03) |

## 13. Verification Checklist

- [ ] IT Admin navigates to `/system-parameters` and sees the form pre-populated
      with the current DB values
- [ ] All 13 fields are visible (including `appointmentCancelTime` and
      `jointDeclarationUploadCutoffDays` which were hidden in OLD)
- [ ] All integer fields reject 0 and negative values (Save button disabled)
- [ ] `isCustomField` checkbox state is pre-populated from DB
- [ ] `ccEmailIds` accepts semicolon-separated addresses; no format validation enforced
- [ ] Update button is `float-right` / right-aligned
- [ ] Successful PUT shows a success notification
- [ ] PUT with a stale `concurrencyStamp` returns an error prompting reload
- [ ] Non-IT-Admin internal user access is restricted (route guard or read-only render)
- [ ] After saving, booking validation reflects the new values (e.g., change
      LeadTime from 3 to 5, attempt a booking within 4 days -- should be rejected)
- [ ] `isCustomField=true` causes custom fields to appear on the booking form
