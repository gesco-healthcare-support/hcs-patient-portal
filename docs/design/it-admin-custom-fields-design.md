---
feature: it-admin-custom-fields
date: 2026-05-04
phase: 6-frontend (backend CustomField entity + CustomFieldValue implemented 2026-05-03; Angular UI pending)
status: draft
old-source: patientappointment-portal/src/app/components/custom-field/custom-fields/
old-components:
  - list/ (custom fields list page)
  - add/ (add custom field modal)
  - edit/ (edit custom field modal)
old-ts: list/custom-field-list.component.ts
new-feature-path: angular/src/app/custom-fields/
shell: internal-user-authenticated (top-bar + side-nav)
screenshots: pending
---

# Design: IT Admin -- Custom Fields

## 1. Routes

One list page. Add and Edit are modals launched from the list.

| | OLD | NEW |
|---|---|---|
| URL | `/custom-fields` | `/custom-fields` |
| Guard | `PageAccess` `applicationModuleId: 10` | `[Authorize(CaseEvaluationPermissions.CustomFields.Default)]` |

Backend: `GET api/app/custom-fields` + `POST api/app/custom-fields` +
`PUT api/app/custom-fields/{id}` + `DELETE api/app/custom-fields/{id}`.
All endpoints implemented in Phase 6 (2026-05-03). Angular proxy in
`angular/src/app/proxy/custom-fields/` regenerates via `abp generate-proxy`.

## 2. Shell

Internal-user authenticated shell. Side-nav item "Custom Fields" under an
IT Admin section (or "Custom Field Configuration" as the heading -- see layout).

## 3. Page Layout (List)

```
+-------------------------------------------------------+
| [H2] Custom Field Configuration    [Search input]  [refresh icon] |
+-------------------------------------------------------+
| [Card]                                               |
|   [Card header -- only visible when !hideAddButton]  |
|     (empty left col)         [Add + button -- right] |
|   [Table]                                            |
|   Field Label | Field Name | Is Mandatory | Field Length | Action |
+-------------------------------------------------------+
```

The "Add +" button is inside the card header, which is entirely hidden
(`*ngIf="!hideAddButton"`) when the active custom field count >= 10.
When there are 10 or more active fields, no card header renders -- the
table starts immediately after the outer card opens.

OLD source: `list/custom-field-list.component.html:23-35`

## 4. Table Columns

| Column | OLD field | Notes |
|---|---|---|
| Field Label | `fieldLabel` | Plain text |
| Field Name | `fieldName` | System-generated identifier (snake_case) |
| Is Mandatory | `isMandatory` | Custom template: "Yes" with class `apporved` (green) or "No" with class `rejected` (red) |
| Field Length | `fieldLength` | Numeric; null when Field Type is Date |
| Action | -- | Pencil (edit) + trash (delete) icons |

Commented out in OLD (not shown):
- `displayOrder`
- `defaultValue`
- Status column (Active/Inactive) -- the `statusId` field exists in the entity
  but is never shown in the list; active-count check is handled implicitly by
  the `hideAddButton` logic

OLD source: `list/custom-field-list.component.html:40-80`

### Is Mandatory color classes

- `class="apporved"` (sic -- OLD typo in class name) -> green label for "Yes"
- `class="rejected"` -> red label for "No"

NEW: use Angular Material `mat-chip` or `<span>` styled via design tokens:
`--status-approved` / `--status-rejected` from `_design-tokens.md`.
Do NOT propagate the `apporved` class name typo.

## 5. Add Modal (`CustomFieldAddComponent`)

Header: "Add Custom Field Detail"
Subtitle: "Please configure custom appointment form details to be filled by a user."

```
+-----------------------------------+
| Add Custom Field Detail     [X]  |
| Please configure custom...        |
+-----------------------------------+
| Field Label    [text]  Field Type [select] |
| Field Length   [text]  Is Mandatory [checkbox] |
|   (hidden if Date type)           |
+-----------------------------------+
| [Add Custom Field ]  [Cancel]    |
+-----------------------------------+
```

### 5a. Form Fields (Add)

**Field Label (required):**
- Label: "Field Label"
- `type="text"`, `formControlName="fieldLabel"`
- Placeholder: "Field Label"

**Field Type (required):**
- Label: "Field Type"
- `<select>` bound to `customFieldLookupGroup.customFieldTypeLookUps` lookup
- Option value: `field.customFieldId`
- Option display: `field.customeFiledName` (note: `customeFiledName` is a typo
  in the OLD API response property -- the displayed names are correct:
  "Date", "Text", "Number")
- Default: "Select" (disabled placeholder, value "null")
- Enum values: `Date=1`, `Text=2`, `Number=3` (`CustomFieldTypeEnum`)
- On change: triggers re-evaluation of `hideDateValidation()` which
  hides Field Length and patches it to null when Date is selected

**Field Length (conditional):**
- Label: "Field Length"
- `type="text"`, `formControlName="fieldLength"`
- Placeholder: "Field Length"
- Visible: `*ngIf="!hideDateValidation()"` -- hidden when Field Type = Date
- When Date is selected, `hideDateValidation()` also calls
  `patchValue({ fieldLength: null })` to clear the stored value
- In NEW: use `type="number"` with `min="1"` for type-safe input

**Is Mandatory checkbox:**
- Label: "Is Mandatory"
- `formControlName="isMandatory"`
- Bootstrap 4 custom checkbox (`custom-control custom-checkbox`)
- NEW: use `mat-checkbox`

**Appointment Type select (COMMENTED OUT in both Add and Edit):**
- OLD HTML has the select commented out in both modals (lines 15-24 of add, 15-24 of edit)
- OLD TS hardcodes `appointmentTypeId = AppointmentTypeEnum.AME` on the model before
  POST -- so ALL custom fields are silently created for AME only
- NEW: surface an Appointment Type select (Exception 1). The backend entity already has
  `AppointmentTypeId` as a required FK; the NEW max-10 check is per AppointmentTypeId.
  Hiding this select was an incomplete implementation, not a deliberate design choice.

OLD source: `add/custom-field-add.component.html:15-52`

### 5b. Default values set by TS before POST

| Field | Value | Source |
|---|---|---|
| `appointmentTypeId` | `AppointmentTypeEnum.AME` (3) | Hardcoded in `ngOnInit` |
| `statusId` | `StatusEnum.Active` (1) | Hardcoded in `ngOnInit` |
| `availableTypeId` | `AvailableTypeEnum.Appointment` | Hardcoded in `ngOnInit` |
| `createdById` | `user.data["userId"]` | Current user |
| `createdDate` | `new Date()` | Current timestamp |
| `defaultValue` | `null` | No UI for it |
| `multipleValues` | `null` | No UI for it |
| `displayOrder` | `1` | Hardcoded -- overridden server-side to `max + 1` |
| `fieldLength` | `null` | Populated from form only when not Date type |

OLD source: `add/custom-field-add.component.ts:44-60`

## 6. Edit Modal (`CustomFieldEditComponent`)

Header: "Edit Custom Field "
(Note: trailing space in OLD -- "Edit Custom Field " not "Edit Custom Field")
Subtitle: "Please edit custom field configuration from here."

```
+-----------------------------------+
| Edit Custom Field            [X] |
| Please edit custom field...       |
+-----------------------------------+
| Field Label    [text]  Field Type [select] |
| Field Length   [text]  Is Mandatory [checkbox] |
|   (hidden if Date type)           |
+-----------------------------------+
| [Edit Custom Field ]  [Cancel]   |
+-----------------------------------+
```

### 6a. Form Fields (Edit -- diff from Add)

- Identical visible fields to Add: Field Label, Field Type, Field Length, Is Mandatory
- Same `hideDateValidation()` conditional on Field Length
- **Field Type IS editable in Edit** (not disabled -- can change Date<->Text<->Number)
- **Appointment Type IS NOT shown** (same commented-out state as Add)

Commented out in Edit but NOT in Add:
- **Default Value** (`formControlName="defaultValue"`, placeholder "Default Value")
- **Multiple Value** (`formControlName="multipleValues"`, placeholder "Multipal Value" -- typo)
- **Is Active checkbox** (`formControlName="statusId"`, `(change)="activeInactive($event)"`,
  `[checked]="isChecked"` -- isChecked pre-filled from `statusId == StatusEnum.Active`)

NEW should surface Is Active in the Edit modal (Exception 2).

Buttons: "Edit Custom Field " (trailing space in OLD) / "Cancel"

OLD source: `edit/custom-field-edit.component.html:1-77`

## 7. Delete

Delete uses `dialog.confirmation([fieldLabel], "delete")` inline from the list
action column. No separate delete component exists.

Confirmation text: shows the field's `fieldLabel` value.
On confirm: `customFieldsService.delete(customFieldId)` then refreshes the list.

OLD source: `list/custom-field-list.component.ts:85-97`

## 8. Add Button Visibility (Max-10 Guard)

OLD logic in `ngOnInit`:

```
let maxLength = 0;
for (let customField of customFields) {
  if (customField.statusId == StatusEnum.Active) {
    maxLength++;
  }
}
if (maxLength >= 10) {
  this.hideAddButton = true;
}
```

This counts ALL active custom fields globally (not per appointment type).
When >= 10, the card header containing the "Add +" button is hidden entirely.

**NEW fix (Exception 3):** the max-10 guard must be per `AppointmentTypeId` because
each appointment type's booking form has its own set of fields. The backend Phase 6
implementation already enforces `>= 10` per AppointmentTypeId via `ValidateMaxCount`.
The Angular list should receive the per-type count from the API response and hide "Add +"
only when the selected type's count >= 10. Since the Appointment Type field is being
surfaced in NEW (Exception 1), the UI knows which type is being managed.

OLD source: `list/custom-field-list.component.ts:43-73`

## 9. Buttons

| Button | Context | OLD class | Disabled condition |
|---|---|---|---|
| Add + | List page header | `btn btn-primary btn-sm` | whole header hidden when max reached |
| Edit (pencil) | Action column | `.oi.oi-pencil` icon | never |
| Delete (trash) | Action column | `fas fa-trash-alt` icon | never |
| Add Custom Field | Add modal footer | `btn btn-primary` | `!customFieldFormGroup.valid` |
| Cancel | Add modal footer | `btn btn-secondary` | never |
| Edit Custom Field | Edit modal footer | `btn btn-primary` | `!customFieldFormGroup.valid` |
| Cancel | Edit modal footer | `btn btn-secondary` | never |

OLD source: `list/custom-field-list.component.html:29`, `add/custom-field-add.component.html:56`, `edit/custom-field-edit.component.html:72`

## 10. Role Visibility Matrix

| Role | Access |
|---|---|
| IT Admin | Full CRUD (list, add, edit, delete) |
| Staff Supervisor | No access |
| Clinic Staff | No access |
| External users | No access (fields rendered at booking time only) |

OLD uses `PageAccess` guard (`applicationModuleId: 10`). Custom fields are not
referenced elsewhere in OLD's admin UI -- they surface only on the booking form
for the corresponding appointment type.

## 11. Branding Tokens

| Element | Token |
|---|---|
| "Yes" label | `--status-approved` (green) |
| "No" label | `--status-rejected` (red) |
| Add button | `--brand-primary` via `btn-primary` |
| Modal header | `--brand-primary` background (Lighthouse Theme standard) |

The `apporved` (sic) and `rejected` CSS class names from OLD encode the green/red
semantics via Bootstrap overrides in the Lighthouse Theme. NEW should replicate the
color behavior using design tokens, not the typo'd class name.

## 12. NEW Stack Delta

1. **Appointment Type select surfaced:** Uncomment the Appointment Type select in both
   Add and Edit modals. The backend entity requires `AppointmentTypeId`; the OLD TS
   hardcoded AME silently. NEW should let IT Admin choose which type's form gets the
   field. Use `ng-select` bound to the `AppointmentType` lookup.

2. **Is Active in Edit:** Surface the Is Active checkbox in the Edit modal. It exists in
   the backend entity and the OLD TS (`isChecked` pre-filled from statusId). The OLD HTML
   just commented it out -- deactivating a custom field without deleting it is a valid
   use case (removes from booking form without losing configuration).

3. **Max-10 per AppointmentType:** The "Add +" button visibility guard must use the
   per-AppointmentTypeId count from the server response, not a client-side global count.
   Backend already enforces this; Angular should surface the constraint with a clear
   message: "Maximum 10 custom fields allowed per appointment type."

4. **Field Length as number:** Replace OLD `type="text"` with `type="number"` + `min="1"`
   for Field Length so the keyboard is numeric on mobile and HTML5 validation prevents
   negative or zero values.

5. **No success toast:** OLD adds/edits close the popup on success with no toast. NEW
   should show a success notification via ABP notification service (Exception 4).

6. **ABP proxy:** After finalizing `CustomFieldDto` surface, run `abp generate-proxy` to
   regenerate `angular/src/app/proxy/custom-fields/`.

## 13. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Appointment Type select commented out | Both modals comment out the select; TS hardcodes `appointmentTypeId = AME` | Surface Appointment Type select in both Add and Edit modals | Incomplete implementation in OLD: the entity has `AppointmentTypeId` FK, the max-10 logic is per-type, yet the only user-visible field was silently locked to AME. NEW must let IT Admin configure per-type fields. Flag with `// PARITY-FLAG` until Adrian confirms |
| 2 | Is Active checkbox commented out in Edit | `edit/template-edit.component.html` lines 62-68 commented out | Surface Is Active checkbox in Edit modal | Soft-delete is a valid use case; the `statusId` field and `isChecked` pre-fill logic exist in the TS and backend; the checkbox was just never uncommented in the HTML |
| 3 | Max-10 guard is global active count | `maxLength++` for every `StatusEnum.Active` record regardless of appointment type | Guard is per `AppointmentTypeId` | Bug fix: Phase 6 backend enforces per-type limit; Angular must match. OLD logic would block all adds after 10 total across all types, preventing additional PQME fields even if AME has 0 |
| 4 | No success toast on Add or Edit | `addCustomField()` and `editCustomField()` close popup on success with no notification | Show success toast via ABP notification service | UX omission; consistent with the system parameters design where the same gap was flagged |
| 5 | `customeFiledName` typo in API property | Lookup option display bound to `field.customeFiledName` (misspelled) | Bind to corrected property name in NEW proxy -- `customFieldName` or whatever `abp generate-proxy` produces | OLD typo is in the in-house `rx-control-design` lookup model; NEW proxy will generate the correct camelCase from the C# DTO |
| 6 | `apporved` CSS class name typo | `class="apporved"` (missing 'r') in table cell template | Use design-token classes, not the typo'd class name | Cosmetic; do not propagate the typo into NEW component CSS |
| 7 | "Add Custom Field " / "Edit Custom Field " trailing space | Button labels have a trailing space in OLD HTML | Trim trailing space in NEW button labels | Cosmetic artifact of OLD HTML indentation |

## 14. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `list/custom-field-list.component.html` | 1-90 | Full list page (search, add button, table) |
| `list/custom-field-list.component.html` | 23-35 | Card header with conditional Add button |
| `list/custom-field-list.component.html` | 40-80 | Table columns (fieldLabel, fieldName, isMandatory, fieldLength, Action) |
| `list/custom-field-list.component.ts` | 43-73 | `ngOnInit()` -- GET list + max-10 active-count guard |
| `list/custom-field-list.component.ts` | 85-97 | `deleteCustomeField()` -- inline confirmation dialog |
| `add/custom-field-add.component.html` | 1-58 | Full Add modal (Appointment Type commented out, 4 visible fields) |
| `add/custom-field-add.component.html` | 15-24 | Appointment Type select (commented out) |
| `add/custom-field-add.component.ts` | 44-60 | `ngOnInit()` -- hardcoded defaults before form bind |
| `add/custom-field-add.component.ts` | 96-103 | `hideDateValidation()` -- Date type hides Field Length |
| `edit/custom-field-edit.component.html` | 1-77 | Full Edit modal (same visible fields as Add) |
| `edit/custom-field-edit.component.html` | 52-68 | Commented-out DefaultValue, MultipleValues, Is Active |
| `edit/custom-field-edit.component.ts` | 43-60 | `ngOnInit()` -- load record + pre-fill `isChecked` |
| `docs/parity/it-admin-custom-fields.md` | all | Full parity audit (Phase 6 entity, max-10 fix, CustomFieldType enum, migration) |

## 15. Verification Checklist

- [ ] IT Admin navigates to `/custom-fields` and sees the list
      (Field Label, Field Name, Is Mandatory, Field Length, Action columns)
- [ ] "Is Mandatory = true" rows show green "Yes" label; "false" rows show red "No" label
- [ ] "Add +" button is visible when active field count for the selected appointment type
      is < 10; hidden (or shows informational message) when >= 10
- [ ] Clicking "Add +" opens the Add modal with Appointment Type select (NEW addition)
- [ ] Field Type select shows "Date", "Text", "Number" options
- [ ] Selecting "Date" hides the Field Length field and clears its value
- [ ] Selecting "Text" or "Number" shows the Field Length field
- [ ] Save with required fields blank: button disabled (form invalid)
- [ ] Saving an Add creates the field; list refreshes; new row visible
- [ ] Delete icon shows confirmation dialog with the field's Field Label
- [ ] Confirming delete removes the row; list refreshes
- [ ] Edit pencil opens Edit modal with current values pre-filled
- [ ] Field Type change in Edit toggles Field Length visibility correctly
- [ ] Is Active checkbox is visible in Edit modal (NEW addition); unchecking
      deactivates the field without deleting it
- [ ] Saving an Edit updates the record; list refreshes; row reflects changes
- [ ] Custom fields for the correct appointment type appear on the booking form
      when `isCustomField = true` in System Parameters
- [ ] Adding a Date-type custom field results in a date picker on the booking form
- [ ] Adding a mandatory custom field blocks booking form submission when left blank
