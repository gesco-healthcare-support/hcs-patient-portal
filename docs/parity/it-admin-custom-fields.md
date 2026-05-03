---
feature: it-admin-custom-fields
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\CustomFieldModule\CustomFieldDomain.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\custom-field\
old-docs:
  - socal-project-overview.md (lines 541-549)
  - data-dictionary-table.md (CustomFields, CustomFieldsValues)
audited: 2026-05-01
status: audit-only
priority: 2
strict-parity: true
internal-user-role: ITAdmin
depends-on:
  - it-admin-system-parameters    # IsCustomField flag gates the feature
required-by:
  - external-user-appointment-request   # form renders custom fields if IsCustomField is on
---

# IT Admin -- Custom intake fields

## Purpose

IT Admin can add up to 10 additional custom fields to the patient intake form. Each custom field has: display name, data type (Date/Text/Number), mandatory flag, multiple-values config, default value, display order. The booking form renders these dynamically when `SystemParameters.IsCustomField = true`.

**Strict parity with OLD.** NEW has renamed `CustomFields` -> `AppointmentTypeFieldConfigs`.

## OLD behavior (binding)

### Schema (`CustomFields` table)

- `CustomFieldId` (PK)
- `FieldLabel` (nvarchar 200)
- `DisplayOrder` (int)
- `FieldTypeId` (int -- enum: Date / Text / Number)
- `AvailableTypeId` (int -- TO VERIFY meaning; possibly which roles see this field)
- `FieldLength` (int? -- max length for text)
- `MultipleValues` (nvarchar 200 -- comma-separated options for dropdown-like)
- `DefaultValue` (nvarchar 200)
- `IsMandatory` (bit)
- `StatusId` (Active/Delete)
- `AppointmentTypeId` (int -- which appointment type this field applies to; nullable for "all types")
- Audit fields

`CustomFieldsValues` (the per-appointment values):

- `CustomFieldValueId` (PK)
- `CustomFieldId` (FK)
- `CustomFieldValue` (varchar Max -- the entered value)
- `ReferenceId` (the appointment ID -- used as polymorphic ref since custom fields might apply to other entities)
- Audit fields

### IT Admin UI (per spec lines 541-549)

For each new field, configure:

- Display Name
- Data Type (Date / Text / Number)
- Mandatory (Yes / No)
- (Plus likely: AppointmentTypeId, FieldLength, DefaultValue, MultipleValues -- richer than spec lists)

### Booking form rendering

Per booking audit + `appointment-add.component.ts` line 105 + 127-131:

- Form loads `customFieldLookUps` (active CustomFields)
- If `SystemParameters.IsCustomField = true` AND custom fields exist -> form renders extra fields per `FieldTypeId`:
  - Date -> date picker
  - Text -> text input (max length per `FieldLength`)
  - Number -> numeric input
- On submit, fields are saved as `CustomFieldsValues` rows with `ReferenceId = AppointmentId`

### Critical OLD behaviors

- **Up to 10 fields per appointment type** (spec line 543). Strict parity: enforce on add (max count check).
- **`IsMandatory` -> required form validation.**
- **Per-AppointmentType scoping** (or null for all types).
- **Renamed in NEW to `AppointmentTypeFieldConfigs`** (per earlier glob output). Same semantics.

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/CustomFieldModule/CustomFieldDomain.cs` | Custom field CRUD |
| `PatientAppointment.Models.Enums.CustomFieldTypeEnum` | Field type enum |
| `patientappointment-portal/.../custom-field/...` | UI |

## NEW current state

- `Application.Contracts/AppointmentTypeFieldConfigs/AppointmentTypeFieldConfigDto.cs` exists -- confirms the rename.
- `Application.Contracts/AppointmentTypeFieldConfigs/IAppointmentTypeFieldConfigsAppService.cs` exists.
- TO VERIFY: max-10 enforcement, per-appointment-type scoping, mandatory enforcement on form, `IsCustomField` global gate.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| Entity exists | `CustomField` | `AppointmentTypeFieldConfig` (renamed) | None -- naming differs but semantics match | -- |
| Max 10 per type | OLD spec | NEW: TO VERIFY | **Add count check** in `CreateAsync` -- reject if >= 10 active configs for that AppointmentTypeId | I |
| `FieldType` enum: Date / Text / Number | OLD `CustomFieldTypeEnum` | NEW: TO VERIFY | **Match enum values** | I |
| `IsMandatory` flag | OLD bool | TO VERIFY | **Verify** | I |
| `AppointmentTypeId` scoping (null = all types) | OLD | TO VERIFY | **Verify nullable FK** | I |
| `MultipleValues` (dropdown options) | OLD | TO VERIFY | **Verify** | I |
| `DefaultValue` | OLD | TO VERIFY | **Verify** | I |
| `DisplayOrder` | OLD | TO VERIFY | **Verify** | I |
| `IsCustomField` system flag gates rendering | OLD | TO VERIFY | **Verify booking form respects flag** | I |
| Per-appointment values entity | `CustomFieldsValues` polymorphic via `ReferenceId` | NEW: TO VERIFY (likely renamed `AppointmentTypeFieldConfigValue` or similar) | **Verify** | I |
| Permissions | -- | -- | **`CaseEvaluation.AppointmentTypeFieldConfigs.{Default, Create, Edit, Delete}`** -- IT Admin | I |

## Internal dependencies surfaced

- `SystemParameters.IsCustomField` -- already covered.
- `AppointmentTypes` master -- already covered.

## Branding/theming touchpoints

- Field rendering on booking form (logo, primary color, label styling).

## Replication notes

### ABP wiring

- Existing `AppointmentTypeFieldConfigsAppService` -- verify and extend if gaps.
- **Form renderer** in Angular: dynamic FormArray based on lookup; respect `FieldType` for input type, `IsMandatory` for required validator, `MultipleValues` for select options.
- **Polymorphic value table** -- avoid; use `AppointmentTypeFieldConfigValue` with explicit `AppointmentId` FK rather than `ReferenceId`. Cleaner than OLD's polymorphic pattern. Single-purpose since OLD only used it for appointments anyway.

### Things NOT to port

- Polymorphic `ReferenceId` -- use explicit FK.
- `StatusId` -> `IsActive` + `ISoftDelete`.

### Verification (manual test plan)

1. IT Admin creates 10 custom fields for AppointmentType=PQME -> success
2. IT Admin tries to create 11th -> rejected
3. External user books PQME -> form shows 10 extra fields
4. Mark `IsMandatory = true` on field -> form validates
5. Mark `IsCustomField = false` in SystemParameters -> form hides custom fields
6. Submit booking with custom field values -> values saved as AppointmentTypeFieldConfigValue rows
