---
feature: it-admin-custom-fields
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\CustomFieldModule\CustomFieldDomain.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\custom-field\
old-docs:
  - socal-project-overview.md (lines 541-549)
  - data-dictionary-table.md (CustomFields, CustomFieldsValues)
audited: 2026-05-01
re-verified: 2026-05-03
status: in-progress
priority: 2
strict-parity: true
internal-user-role: ITAdmin
depends-on:
  - it-admin-system-parameters    # IsCustomField flag gates the feature
required-by:
  - external-user-appointment-request   # form renders custom fields if IsCustomField is on
---

> **CORRECTION 2026-05-03 [VERIFIED against OLD source + NEW source].**
>
> The earlier draft of this audit (line 78 below) claimed NEW had renamed
> `CustomField` to `AppointmentTypeFieldConfig`. **That assumption was
> wrong.** NEW's existing `AppointmentTypeFieldConfig` (entity created
> 2026-04-29 as W2-5) is a **completely different feature** -- it stores
> per-AppointmentType form-field overrides (`Hidden`, `ReadOnly`,
> `DefaultValue`) keyed by an existing form-control name. OLD's
> `CustomField` is the user-defined additional intake field
> (`FieldLabel`, `DisplayOrder`, `FieldType` enum, `IsMandatory`, etc.).
> Neither entity matches the other.
>
> **Phase 6 implementation decision:** Add OLD's `CustomField` as a NEW
> entity at `Domain/CustomFields/CustomField.cs` (verbatim OLD field set).
> Leave the existing W2-5 `AppointmentTypeFieldConfig` in place -- it's a
> NEW improvement that an admin may keep or descope post-parity. Both
> entities share the existing `CaseEvaluationPermissions.CustomFields.*`
> permission group because both are IT-Admin CRUD; future work may rename
> the W2-5 group to `AppointmentTypeFieldOverrides` for clarity (deferred).
>
> **Source-of-truth for OLD behavior:** `CustomFieldDomain.cs:38-42` does
> a GLOBAL count check (`Repository<CustomField>().All().Where(...).Count()`)
> that ignores `AppointmentTypeId`. Spec says "10 per type"; OLD code
> enforces "10 globally". OLD also uses `== 10` instead of `>= 10`,
> which means an admin who hits 11 rows by other paths can keep adding.
> Both are OLD-bug-fix exceptions: NEW enforces `>= 10 active per
> AppointmentTypeId` to honor spec intent.

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

| Aspect | OLD | NEW | Action | Sev | Status |
|--------|-----|-----|--------|-----|--------|
| Entity exists | `CustomField` | NEW had no equivalent; W2-5 `AppointmentTypeFieldConfig` is a different feature | **Add `CustomField` entity** (Domain/CustomFields/) verbatim with OLD's full field set | B | [IMPLEMENTED 2026-05-03 - pending testing] -- Domain/CustomFields/CustomField.cs as `FullAuditedAggregateRoot<Guid>, IMultiTenant, [Audited]`. Composite index on (TenantId, AppointmentTypeId, IsActive) supports the per-tenant cap query. |
| Max 10 per type | OLD spec line 543; OLD code GLOBAL count + `== 10` (bug) | -- | **Add per-AppointmentTypeId active-count check** with `>= 10` (OLD-bug-fix) | I | [IMPLEMENTED 2026-05-03 - pending testing OLD-BUG-FIX] -- `CustomFieldsAppService.EnsureUnderActiveCapAsync` filters by AppointmentTypeId; helper `IsAtOrOverCap` uses `>= MaxActiveCountPerAppointmentType` (10). Unit tests verify boundaries 9/10/11. |
| `FieldType` enum: Date / Text / Number | OLD `CustomFieldTypeEnum` | -- | **Match enum values** | I | [IMPLEMENTED 2026-05-03 - pending testing] -- Domain.Shared/Enums/CustomFieldType.cs (Date=1, Text=2, Number=3). Matches OLD verbatim. |
| `IsMandatory` flag | OLD bool | -- | **Add bool field** | I | [IMPLEMENTED 2026-05-03 - pending testing] |
| `AppointmentTypeId` scoping (null = all types) | OLD | -- | **Add nullable Guid FK** | I | [IMPLEMENTED 2026-05-03 - pending testing] -- nullable on entity, but the AppService Create/Update DTOs require it (matches OLD UI contract). |
| `MultipleValues` (dropdown options) | OLD | -- | **Add string column** | I | [IMPLEMENTED 2026-05-03 - pending testing] |
| `DefaultValue` | OLD | -- | **Add string column** | I | [IMPLEMENTED 2026-05-03 - pending testing] |
| `DisplayOrder` | OLD auto-assigned `max + 1` | -- | **Auto-assign on create; editable on update** | I | [IMPLEMENTED 2026-05-03 - pending testing] -- `ComputeNextDisplayOrder` helper extracted internal-static; unit-tested with empty / non-empty / boundary inputs. |
| `IsCustomField` system flag gates rendering | OLD `SystemParameter.IsCustomField` | -- | **Booking form reads the flag (Phase 11)** | I | [DEFERRED 2026-05-03 - Phase 11] -- Phase 6 ships the catalog. Phase 11 (Booking) will gate the booking-form render behind `SystemParameter.IsCustomField`. |
| Per-appointment values entity | `CustomFieldsValues` polymorphic via `ReferenceId` | -- | **Add `CustomFieldValue` with explicit AppointmentId FK** (replaces OLD's polymorphic `ReferenceId`) | I | [IMPLEMENTED 2026-05-03 - pending testing] -- Domain/CustomFields/CustomFieldValue.cs. Explicit FK, not polymorphic; OLD's `ReferenceId` only ever pointed at appointments. |
| Permissions | -- | -- | **Reuse existing `CaseEvaluation.CustomFields.{Default, Create, Edit, Delete}`** -- shared with W2-5 since both are IT-Admin CRUD | I | [IMPLEMENTED 2026-05-03 - pending testing] -- existing perm group reused; granted to IT Admin in InternalUserRoleDataSeedContributor (Session B's Phase 1.4 pre-existing setup). |
| Migration | -- | -- | **Generate EF migration for the two new tables** | -- | [IMPLEMENTED 2026-05-03 - pending testing] -- migration `20260503230345_Phase6_Add_CustomFields` creates `AppCustomFields` + `AppCustomFieldValues` tables with three indexes and two FKs. |

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
